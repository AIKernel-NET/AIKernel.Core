"""Independent exact integer oracle, deliberately unaligned Q1 rows and tail tiles."""
import ctypes as c,sys,struct,random,json,hashlib
from pathlib import Path
lib=c.CDLL(sys.argv[1]);device=int(sys.argv[2]) if len(sys.argv)>2 else 0
def fn(name,args):
 f=getattr(lib,name);f.argtypes=args;f.restype=c.c_int;return f
create=fn('ak_create',[c.c_uint32,c.c_int32,c.POINTER(c.c_void_p)])
destroy=fn('ak_destroy',[c.c_void_p]);caps=fn('ak_get_int8_capabilities',[c.c_void_p,c.POINTER(c.c_uint32)])
alloc=fn('ak_device_alloc',[c.c_void_p,c.c_size_t,c.POINTER(c.c_uint64)])
free=fn('ak_device_free',[c.c_void_p,c.c_uint64]);upload=fn('ak_upload',[c.c_void_p,c.c_uint64,c.c_void_p,c.c_size_t])
gemv=fn('ak_gemv_q1_q8',[c.c_void_p,c.c_uint64,c.c_void_p,c.c_void_p,c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32])
ctx=c.c_void_p();assert create(1,device,c.byref(ctx))==0
flags=c.c_uint32();assert caps(ctx,c.byref(flags))==0
rng=random.Random(141115);cases=0
try:
 for rows in [1,4,15,16,17,65,129]:
  for columns in [128,256,384,1024]:
   blocks=columns//128
   x=[rng.randint(-127,127) for _ in range(columns)];x[:4]=[-127,127,0,-127]
   scales=[2.0**rng.randint(-10,-3) for _ in range(blocks)];sums=[sum(x[b*128:(b+1)*128]) for b in range(blocks)]
   data=bytearray(b'XX');expected=[]
   for row in range(rows):
    total=0
    for block in range(blocks):
     scale=rng.choice([0,.5,-.5,1,2]);bits=bytes([0 if row%5==0 else 255 if row%5==1 else rng.randrange(256) for _ in range(16)])
     data.extend(struct.pack('<e',scale)+bits)
     dot=sum((1 if bits[k//8]>>(k%8)&1 else -1)*x[block*128+k] for k in range(128))
     total+=dot*scale*scales[block]
    expected.append(total)
   w=c.create_string_buffer(bytes(data));address=c.c_uint64();assert alloc(ctx,len(data),c.byref(address))==0
   try:
    assert upload(ctx,address.value,w,len(data))==0
    q=(c.c_int8*columns)(*x);s=(c.c_float*blocks)(*scales);sm=(c.c_int32*blocks)(*sums);y=(c.c_float*rows)()
    for backend in [1,2]:
     code=gemv(ctx,address.value+2,q,s,sm,y,rows,columns,backend)
     if flags.value&(1<<(backend-1)):
      assert code==0,(backend,code)
      assert list(y)==expected,(rows,columns,backend,list(y)[:20],expected[:20]);cases+=1
     else:assert code==6,(backend,code)
    assert gemv(ctx,address.value+2,q,s,sm,y,rows,columns,0)==1
    assert gemv(ctx,address.value+2,q,s,sm,y,rows,columns+1,1)==1
    assert gemv(ctx,address.value+2,q,s,sm,y,rows+1,columns,1)==1
   finally:assert free(ctx,address.value)==0
finally:assert destroy(ctx)==0
print(json.dumps({'passed':True,'device':device,'capabilityFlags':flags.value,'exactCases':cases,'nativeSha256':hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest()}))
