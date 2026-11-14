import ctypes as c,random,struct,json,hashlib,sys
from pathlib import Path
libpath=Path(sys.argv[1]);lib=c.CDLL(str(libpath));lib.ak_cpu_abi.restype=c.c_uint32;lib.ak_cpu_supports.restype=c.c_uint32
f=lib.ak_cpu_q1_q8;f.argtypes=[c.c_void_p,c.c_size_t,c.c_void_p,c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32,c.c_void_p];f.restype=c.c_int
assert lib.ak_cpu_abi()==1;flags=lib.ak_cpu_supports();rng=random.Random(141114);cases=0
for rows in [1,4,5,17,64,65]:
 for cols in [128,256,384,1024]:
  blocks=cols//128;x=[rng.randint(-127,127) for _ in range(cols)];scales=[2.0**rng.randint(-10,-3) for _ in range(blocks)];sums=[sum(x[b*128:(b+1)*128]) for b in range(blocks)]
  blob=bytearray();expected=[]
  for row in range(rows):
   total=0.0
   for b in range(blocks):
    scale=rng.choice([0,.5,-.5,1,2]);bits=bytes([0 if row%5==0 else 255 if row%5==1 else rng.randrange(256) for _ in range(16)])
    blob.extend(struct.pack('<e',scale)+bits)
    dot=sum((1 if bits[k//8]>>(k%8)&1 else -1)*x[b*128+k] for k in range(128));total+=dot*scales[b]*scale
   expected.append(total)
  # Deliberately misalign the first block. Packed Q1 rows are not cache-line padded.
  w=c.create_string_buffer(b'X'+blob);xp=(c.c_int8*cols)(*x);sp=(c.c_float*blocks)(*scales);ip=(c.c_int32*blocks)(*sums);yp=(c.c_float*rows)()
  for backend in [1,2,3]:
   if backend==3 and flags&4:continue # No unrequested AMX execution.
   code=f(c.byref(w,1),len(blob),xp,sp,ip,rows,cols,backend,yp)
   if flags&(1<<(backend-1)):
    if backend==3:continue # Explicitly unvalidated AMX hardware.
    assert code==0 and list(yp)==expected,(rows,cols,backend,code,list(yp),expected);cases+=1
   else:assert code==-2,(backend,code)
  assert f(c.byref(w,1),len(blob)-1,xp,sp,ip,rows,cols,1,yp)==-1
  assert f(c.byref(w,1),len(blob),xp,sp,ip,rows,cols+1,1,yp)==-1
  assert f(c.byref(w,1),len(blob),xp,sp,ip,rows,cols,4,yp)==-1
print(json.dumps({'passed':True,'nativeSha256':hashlib.sha256(libpath.read_bytes()).hexdigest(),'flags':flags,'exactNativeCases':cases,'unsupportedIsaAndBoundsChecked':True}))
