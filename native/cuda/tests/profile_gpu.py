"""Check opt-in GEMV event accounting without changing the arithmetic result."""
import ctypes as c
import json
import math
import struct
import sys

lib = c.CDLL(sys.argv[1])
device = int(sys.argv[2]) if len(sys.argv) > 2 else 0

class Profile(c.Structure):
    _fields_ = [("size", c.c_uint32), ("version", c.c_uint32),
                ("calls", c.c_uint64), ("upload_bytes", c.c_uint64), ("download_bytes", c.c_uint64),
                ("upload_ms", c.c_double), ("kernel_ms", c.c_double),
                ("download_ms", c.c_double), ("host_sync_ms", c.c_double)]

def fn(name, *args):
    f = getattr(lib, name)
    f.argtypes, f.restype = args, c.c_int
    return f

create = fn("ak_create", c.c_uint32, c.c_int32, c.POINTER(c.c_void_p))
destroy = fn("ak_destroy", c.c_void_p)
configure = fn("ak_profile_configure", c.c_void_p, c.c_uint32)
read = fn("ak_profile_read", c.c_void_p, c.POINTER(Profile))
alloc = fn("ak_device_alloc", c.c_void_p, c.c_size_t, c.POINTER(c.c_uint64))
upload = fn("ak_upload", c.c_void_p, c.c_uint64, c.c_void_p, c.c_size_t)
gemv = fn("ak_gemv_q1_0", c.c_void_p, c.c_uint64, c.c_void_p, c.c_void_p, c.c_uint32, c.c_uint32)
ctx = c.c_void_p()
assert create(1, device, c.byref(ctx)) == 0
try:
    p = Profile(size=c.sizeof(Profile), version=1)
    assert read(ctx, c.byref(p)) == 1  # Disabled by default.
    assert configure(ctx, 2) == 1
    data = c.create_string_buffer((struct.pack("<e", .5) + b"\xff" * 16) * 5)
    address = c.c_uint64()
    assert alloc(ctx, 90, c.byref(address)) == 0
    assert upload(ctx, address, data, 90) == 0
    x = (c.c_float * 128)(*range(128))
    y = (c.c_float * 5)()
    assert gemv(ctx, address, x, y, 5, 128) == 0
    expected = list(y)
    assert expected == [4064.0] * 5
    assert configure(ctx, 1) == 0
    for _ in range(3):
        assert gemv(ctx, address, x, y, 5, 128) == 0
        assert list(y) == expected
    assert read(ctx, c.byref(p)) == 0
    assert (p.calls, p.upload_bytes, p.download_bytes) == (3, 3 * 512, 3 * 20)
    for name in ["upload_ms", "kernel_ms", "download_ms", "host_sync_ms"]:
        assert math.isfinite(getattr(p, name)) and getattr(p, name) >= 0
    assert configure(ctx, 1) == 0
    assert read(ctx, c.byref(p)) == 0 and p.calls == 0
    assert configure(ctx, 0) == 0
    assert read(ctx, c.byref(p)) == 1
    assert gemv(ctx, address, x, y, 5, 128) == 0 and list(y) == expected
finally:
    assert destroy(ctx) == 0
print(json.dumps({"passed": True, "device": device, "profileResetAndDisable": True, "exactTokenIndependentMatrix": True}))
