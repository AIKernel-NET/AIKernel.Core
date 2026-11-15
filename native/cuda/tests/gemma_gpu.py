"""Encoded F16/Q4_0/Q6_K GEMV: independent double oracle, unaligned rows and tail warps."""
import ctypes as c
import struct, random, sys, json, math
lib=c.CDLL(sys.argv[1]);device=int(sys.argv[2]) if len(sys.argv)>2 else 0
def fn(name,args):
 f=getattr(lib,name);f.argtypes=args;f.restype=c.c_int;return f
create=fn('ak_create',[c.c_uint32,c.c_int32,c.POINTER(c.c_void_p)])
destroy=fn('ak_destroy',[c.c_void_p]);alloc=fn('ak_device_alloc',[c.c_void_p,c.c_size_t,c.POINTER(c.c_uint64)])
free=fn('ak_device_free',[c.c_void_p,c.c_uint64]);upload=fn('ak_upload',[c.c_void_p,c.c_uint64,c.c_void_p,c.c_size_t])
gemv=fn('ak_gemv_gemma',[c.c_void_p,c.c_uint64,c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32])
wipe=fn('ak_wipe_scratch',[c.c_void_p]);ctx=c.c_void_p();assert create(1,device,c.byref(ctx))==0
rng=random.Random(4142);cases=0;maximum=0
try:
 for kind in [1,2,14]:
  for rows in [1,3,4,5,17]:
   for cols in [256,1536]:
    x=[rng.uniform(-1,1) for _ in range(cols)];x=list((c.c_float*cols)(*x))
    data=bytearray(b'XX');expected=[];magnitudes=[]
    for row in range(rows):
     decoded=[]
     if kind==1:
      for k in range(cols):
       h=struct.pack('<e',rng.uniform(-.1,.1));data.extend(h);decoded.append(struct.unpack('<e',h)[0])
     else:
      block_size=32 if kind==2 else 256
      for b in range(cols//block_size):
       scale=struct.pack('<e',rng.choice([0,.03125,-.0625,.125]));d=struct.unpack('<e',scale)[0]
       if kind==2:
        bits=bytes(rng.randrange(256) for _ in range(16));data.extend(scale+bits)
        decoded.extend([d*((v&15)-8) for v in bits]);decoded.extend([d*((v>>4)-8) for v in bits])
       else:
        low=bytes(rng.randrange(256) for _ in range(128));high=bytes(rng.randrange(256) for _ in range(64));scales=[rng.randrange(-128,128) for _ in range(16)]
        data.extend(low+high+struct.pack('16b',*scales)+scale)
        for h in range(2):
         for q in range(4):
          for lane in range(32):
           i=h*128+q*32+lane
           quant=((low[h*64+(q%2)*32+lane]>>(q//2*4))&15)|(((high[h*32+lane]>>(q*2))&3)<<4)
           decoded.append(d*scales[i//16]*(quant-32))
     terms=[a*b for a,b in zip(decoded,x)];expected.append(sum(terms));magnitudes.append(sum(map(abs,terms)))
    address=c.c_uint64();assert alloc(ctx,len(data),c.byref(address))==0
    try:
     encoded=(c.c_ubyte*len(data)).from_buffer_copy(data);assert upload(ctx,address.value,encoded,len(data))==0
     xv=(c.c_float*cols)(*x);yv=(c.c_float*(rows+2))(*([12345.]*(rows+2)))
     assert gemv(ctx,address.value+2,xv,yv,rows,cols,kind)==0
     for actual,ref,mag in zip(yv,expected,magnitudes):
      maximum=max(maximum,abs(actual-ref));assert abs(actual-ref)<=max(1e-5,mag*2e-6),(kind,rows,cols,actual,ref)
     assert yv[rows]==12345. and yv[rows+1]==12345.
     assert gemv(ctx,address.value+2,xv,yv,rows+1,cols,kind)==1
     assert gemv(ctx,address.value+1,xv,yv,rows,cols,kind)==1
     assert gemv(ctx,address.value+2,xv,yv,rows,cols,41)==1
     assert wipe(ctx)==0;cases+=1
    finally:assert free(ctx,address.value)==0
finally:assert destroy(ctx)==0
print(json.dumps({'pass':True,'device':device,'cases':cases,'maximumAbsoluteError':maximum}))
