"""Exercise an explicit native library without querying or opening any GPU."""
import ctypes as C
import math
import sys

library = C.CDLL(sys.argv[1])
library.ak_abi_version.argtypes = []
library.ak_abi_version.restype = C.c_uint32
assert library.ak_abi_version() == 1
library.ak_softmax.argtypes = [C.c_void_p, C.POINTER(C.c_float), C.c_uint32]
library.ak_softmax.restype = C.c_int32
values = (C.c_float * 2)(3, 1)
assert library.ak_softmax(None, values, 2) == 0
assert math.isclose(values[0], 1 / (1 + math.exp(-2)), abs_tol=1e-6)
assert math.isclose(values[1], 1 / (1 + math.exp(2)), abs_tol=1e-6)
assert library.ak_softmax(None, None, 0) == 1
print('cuda-driver-host-abi: ok; abi=1; gpu-test=not-run')
