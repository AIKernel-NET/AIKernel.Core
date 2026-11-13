"""Independent Q1_0 g128 arithmetic and range checks. Requires a real CUDA device."""
import argparse,ctypes as C,json,math,random,struct
p=argparse.ArgumentParser();p.add_argument('library');p.add_argument('--device',type=int,default=0);args=p.parse_args()
lib=C.CDLL(args.library)
def bind(name,*types):
 f=getattr(lib,'ak_'+name);f.argtypes=types;f.restype=C.c_int32;return f
def check(status):assert status==0, status
create=bind('create',C.c_uint32,C.c_int32,C.POINTER(C.c_void_p))
destroy=bind('destroy',C.c_void_p)
alloc=bind('device_alloc',C.c_void_p,C.c_size_t,C.POINTER(C.c_uint64))
free=bind('device_free',C.c_void_p,C.c_uint64)
upload=bind('upload',C.c_void_p,C.c_uint64,C.c_void_p,C.c_size_t)
download=bind('download',C.c_void_p,C.c_uint64,C.c_void_p,C.c_size_t)
gemv=bind('gemv_q1_0',C.c_void_p,C.c_uint64,C.POINTER(C.c_float),C.POINTER(C.c_float),C.c_uint32,C.c_uint32)
f32=bind('gemv',C.c_void_p,C.c_uint64,C.POINTER(C.c_float),C.POINTER(C.c_float),C.c_uint32,C.c_uint32)
ctx=C.c_void_p();check(create(1,args.device,C.byref(ctx)))
rng=random.Random(20261114);max_abs=0;cases=0
try:
 for rows in [1,3,65,129]:
  for cols in [128,256,640,1024,2048]:
   x=(C.c_float*cols)(*[rng.uniform(-1,1) for _ in range(cols)])
   payload=bytearray();expected=[];bounds=[]
   for row in range(rows):
    products=[]
    for block in range(cols//128):
     # Positive, negative, zero and subnormal half scales; independent scalar double reference.
     scale=struct.unpack('<e',struct.pack('<e',rng.choice([0,2**-24,-.03125,.2,-.9,2])))[0]
     bits=bytes(rng.getrandbits(8) for _ in range(16));payload+=struct.pack('<e',scale)+bits
     products.extend((scale if bits[j//8]&(1<<(j%8)) else -scale)*x[block*128+j] for j in range(128))
    expected.append(math.fsum(products));bounds.append(math.fsum(abs(v) for v in products))
   raw=b'prefix!!'+payload+b'trailer!';host=C.create_string_buffer(raw,len(raw));address=C.c_uint64()
   check(alloc(ctx,len(raw),C.byref(address)))
   try:
    # Exercise interior uploads/downloads and exact end-of-allocation ranges.
    split=len(raw)//2
    check(upload(ctx,address.value,host,split))
    check(upload(ctx,address.value+split,C.byref(host,split),len(raw)-split))
    back=C.create_string_buffer(len(payload));check(download(ctx,address.value+8,back,len(payload)));assert back.raw==payload
    y=(C.c_float*(rows+2))();y[rows]=1234;y[rows+1]=-4321
    check(gemv(ctx,address.value+8,x,y,rows,cols))
    for i,value in enumerate(expected):
     err=abs(y[i]-value);max_abs=max(max_abs,err)
     assert math.isfinite(y[i]) and err<=1e-5+2e-6*bounds[i], (rows,cols,i,y[i],value,err)
    assert (y[rows],y[rows+1])==(1234,-4321)
    assert gemv(None,address.value+8,x,y,rows,cols)==1
    assert gemv(ctx,address.value+9,x,y,rows,cols)==1
    assert gemv(ctx,address.value+8,x,y,rows,cols+1)==1
    assert gemv(ctx,address.value+8,x,y,rows+1,cols)==1
    assert gemv(ctx,address.value+8,x,y,0,cols)==1
    assert gemv(ctx,address.value+8,x,y,4294967295,4294967168)==1
    assert upload(ctx,address.value+len(raw)-1,host,2)==1
    assert upload(ctx,address.value+len(raw),host,1)==1
    assert download(ctx,2**64-2,back,4)==1
    assert free(ctx,address.value+8)==1
    check(gemv(ctx,address.value+8,x,y,rows,cols)) # invalid arguments must not poison context
   finally:check(free(ctx,address))
   assert gemv(ctx,address.value+8,x,y,rows,cols)==1
   cases+=1
 # Existing FP32 GEMV also supports aligned tensor offsets.
 a=C.c_uint64();host=(C.c_float*8)(99,99,1,2,3,4,5,6);check(alloc(ctx,C.sizeof(host),C.byref(a)))
 try:
  check(upload(ctx,a.value,host,C.sizeof(host)));x=(C.c_float*3)(1,2,3);y=(C.c_float*2)()
  check(f32(ctx,a.value+8,x,y,2,3));assert list(y)==[14,32]
 finally:check(free(ctx,a))
 print(json.dumps({'passed':True,'cases':cases,'maxAbsoluteError':max_abs,'reference':'independent scalar double dot of FP16 scales and FP32 inputs','rangeChecks':True,'cpuFallback':False}))
finally:check(destroy(ctx))
