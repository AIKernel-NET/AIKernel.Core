"""Generate the committed PTX with NVRTC (build-time only, no host compiler/STL dependency)."""
import ctypes as c
import hashlib
import json
import sys
from pathlib import Path

lib = c.CDLL(sys.argv[1])
source = Path(__file__).parent / 'src/gemma_gemv.cu'
target = source.with_suffix('.ptx')
program = c.c_void_p()
lib.nvrtcCreateProgram.argtypes = [c.POINTER(c.c_void_p), c.c_char_p, c.c_char_p, c.c_int, c.c_void_p, c.c_void_p]
assert lib.nvrtcCreateProgram(c.byref(program), source.read_bytes(), b'gemma_gemv.cu', 0, None, None) == 0
options = [b'--gpu-architecture=compute_75', b'--fmad=false', b'--std=c++17']
lib.nvrtcCompileProgram.argtypes = [c.c_void_p, c.c_int, c.POINTER(c.c_char_p)]
status = lib.nvrtcCompileProgram(program, len(options), (c.c_char_p * len(options))(*options))
size = c.c_size_t()
lib.nvrtcGetProgramLogSize(program, c.byref(size))
log = c.create_string_buffer(size.value)
lib.nvrtcGetProgramLog(program, log)
if log.value: print(log.value.decode(), file=sys.stderr)
if status: raise RuntimeError(f'NVRTC compilation failed: {status}')
lib.nvrtcGetPTXSize(program, c.byref(size))
ptx = c.create_string_buffer(size.value)
assert lib.nvrtcGetPTX(program, ptx) == 0
target.write_bytes(ptx.value)
lib.nvrtcDestroyProgram(c.byref(program))
print(json.dumps({'source': hashlib.sha256(source.read_bytes()).hexdigest(), 'ptx': hashlib.sha256(target.read_bytes()).hexdigest(), 'options': [x.decode() for x in options]}))
