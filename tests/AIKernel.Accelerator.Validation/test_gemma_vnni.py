"""Independent double oracle for Q4_0 / three residual Q8 planes; includes odd rows and signed scales."""
import ctypes as c,json,math,random,struct,sys
lib=c.CDLL(sys.argv[1]);lib.ak_cpu_gemma_supports.restype=c.c_uint32
flags=lib.ak_cpu_gemma_supports()
f=lib.ak_cpu_gemma_q4_residual
f.argtypes=[c.c_void_p,c.c_size_t,c.c_void_p,c.c_void_p,c.c_void_p,c.c_uint32,c.c_uint32,c.c_uint32,c.c_void_p]
f.restype=c.c_int
rng=random.Random(1611);results=[]
f32=lambda x:struct.unpack('f',struct.pack('f',x))[0]
for cols,rows in [(32,1),(32,3),(1536,5),(12288,3)]:
 blocks=cols//32;raw=bytearray(rows*blocks*18);weights=[]
 for b in range(rows*blocks):
  scale=struct.unpack('e',struct.pack('e',rng.uniform(-.3,.3)))[0]
  raw[b*18:b*18+2]=struct.pack('e',scale)
  qs=[rng.randrange(16) for _ in range(32)]
  for i in range(16):raw[b*18+2+i]=qs[i]|(qs[16+i]<<4)
  weights.extend([scale*(q-8) for q in qs])
 original=[f32(rng.uniform(-3,3)) for _ in range(cols)]
 q=[0]*(cols*3);scales=[0.]*(blocks*3);sums=[0]*(blocks*3)
 for b in range(blocks):
  values=original[b*32:b*32+32]
  for p in range(3):
   scale=f32(max(map(abs,values))/127);scales[p*blocks+b]=scale
   if not scale:continue
   for i in range(32):
    value=max(-127,min(127,round(f32(values[i]/scale))))
    q[p*cols+b*32+i]=value;sums[p*blocks+b]+=value
    values[i]=f32(values[i]-f32(value*scale))
 decoded=[sum(q[p*cols+i]*scales[p*blocks+i//32] for p in range(3)) for i in range(cols)]
 assert max(abs(x-y) for x,y in zip(original,decoded))<2e-6
 oracle=[math.fsum(weights[r*cols+i]*decoded[i] for i in range(cols)) for r in range(rows)]
 w=(c.c_ubyte*len(raw)).from_buffer_copy(raw);x=(c.c_int8*len(q))(*q);s=(c.c_float*len(scales))(*scales);sums_c=(c.c_int32*len(sums))(*sums);out=(c.c_float*rows)()
 for backend in (1,2):
  status=f(w,len(raw),x,s,sums_c,rows,cols,backend,out)
  if not flags&(1<<(backend-1)):assert status==-2;continue
  assert status==0
  error=max(abs(a-b) for a,b in zip(oracle,out))
  assert all(abs(a-b)<=max(2e-5,abs(a)*2e-6) for a,b in zip(oracle,out)),(backend,error)
  assert f(w,len(raw)-1,x,s,sums_c,rows,cols,backend,out)==-1
  results.append({'backend':backend,'columns':cols,'rows':rows,'maxError':error})
print(json.dumps({'pass':True,'capabilities':flags,'cases':results}))
