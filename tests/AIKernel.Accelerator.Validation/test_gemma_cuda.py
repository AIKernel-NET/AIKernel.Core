"""Resident graph ABI bounds, deterministic selection, counters and wipe on a real GPU."""
import ctypes as c
import json
import sys

lib=c.CDLL(sys.argv[1])
class Command(c.Structure):
    _fields_=[(name,c.c_uint32) for name in ['op','n','m','k','type','flags','offset','reserved']]+[(name,c.c_uint64) for name in ['a','b','dout','d']]+[('scale',c.c_float),('padding',c.c_uint32)]
class Stats(c.Structure):
    _fields_=[(name,c.c_uint64) for name in ['calls','synchronizations','download_bytes']]
assert c.sizeof(Command)==72
def function(name,args):
    f=getattr(lib,name);f.argtypes=args;f.restype=c.c_int;return f
ptr=c.c_void_p;u=c.c_uint32;q=c.c_uint64;size=c.c_size_t
create=function('ak_create',[u,c.c_int,c.POINTER(ptr)])
destroy=function('ak_destroy',[ptr])
alloc=function('ak_device_alloc',[ptr,size,c.POINTER(q)])
free=function('ak_device_free',[ptr,q])
upload=function('ak_upload',[ptr,q,ptr,size]);download=function('ak_download',[ptr,q,ptr,size])
configure=function('ak_gemma_configure',[ptr,c.POINTER(Command),u,u,q,size,q,q,u])
forward=function('ak_gemma_forward',[ptr,u,u,u,ptr,ptr,c.POINTER(u)])
reset=function('ak_gemma_reset',[ptr]);stats=function('ak_gemma_read_stats',[ptr,c.POINTER(Stats)])
ctx=ptr();assert create(1,int(sys.argv[2]) if len(sys.argv)>2 else 0,c.byref(ctx))==0
try:
    n=262144;bytes_count=(n+2)*4;address=q();assert alloc(ctx,bytes_count,c.byref(address))==0
    result=address.value+n*4
    program=(Command*2)(Command(op=8,n=n,a=address.value,b=result,flags=1),Command(op=9,n=5,a=address.value,flags=2))
    # No unchecked traces, out-of-range pointers, empty work or oversized traces.
    program[1].flags=0;assert configure(ctx,program,2,64,address,bytes_count,address,result,5)==1
    program[1].flags=2;program[1].offset=1;assert configure(ctx,program,2,64,address,bytes_count,address,result,5)==1
    program[1].offset=0;program[1].flags=3
    assert configure(ctx,program,2,64,address,0,address,result,5)==1
    assert configure(ctx,program,2,64,address,bytes_count,address,result,5)==0
    assert free(ctx,address)==1 # Program addresses cannot become dangling.
    data=(c.c_float*n)();data[1]=data[2]=3;data[0]=-1
    assert upload(ctx,address,data,n*4)==0
    next_token=u();assert forward(ctx,1,1,1,None,None,c.byref(next_token))==1
    assert forward(ctx,n,0,1,None,None,c.byref(next_token))==1
    assert forward(ctx,1,0,1,None,None,c.byref(next_token))==0 and next_token.value==1
    info=Stats();assert stats(ctx,c.byref(info))==0
    assert (info.calls,info.synchronizations,info.download_bytes)==(1,1,8)
    trace=(c.c_float*5)();logits=(c.c_float*n)()
    assert forward(ctx,1,1,1,logits,trace,c.byref(next_token))==0
    assert list(trace)==[-1,3,3,0,0] and list(logits[:5])==list(trace)
    assert forward(ctx,1,2,0,None,trace,c.byref(next_token))==0 and list(trace)==[0,0,0,0,0]
    assert reset(ctx)==0
    cleared=(c.c_ubyte*bytes_count)();assert download(ctx,address,cleared,bytes_count)==0
    assert not any(cleared)
    assert forward(ctx,1,0,1,None,None,c.byref(next_token))==0 and next_token.value==0
    # Non-finite logits are rejected; the failed graph cannot be reused.
    data[1]=float('nan');assert upload(ctx,address,data,n*4)==0
    assert forward(ctx,1,1,1,None,None,c.byref(next_token))==1
    assert forward(ctx,1,1,1,None,None,c.byref(next_token))==1
    assert reset(ctx)==1
finally:
    assert destroy(ctx)==0 # Destruction still attempts a wipe after the logical fault.
print(json.dumps({'pass':True,'gpu':int(sys.argv[2]) if len(sys.argv)>2 else 0,'bounds':True,'argmaxTies':True,'singleSync':True,'eightByteResult':True,'privateStateWipe':True,'nonFiniteFailClosed':True}))
