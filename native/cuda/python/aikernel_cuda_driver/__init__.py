"""ctypes access to AIKernel's Native CUDA Driver and host math ABI."""
from contextlib import contextmanager
import ctypes as C
from pathlib import Path
import sys
from threading import RLock


_ABI = 1
_NAME = "aikernel_cuda_driver.dll" if sys.platform == "win32" else "libaikernel_cuda_driver.so"
_lib = C.CDLL(str(Path(__file__).with_name(_NAME)))
_FLOAT = C.POINTER(C.c_float)


def _bind(name, *args):
    fn = getattr(_lib, "ak_" + name)
    fn.argtypes = args
    fn.restype = C.c_int32
    return fn


_version = _lib.ak_abi_version
_version.argtypes = []
_version.restype = C.c_uint32
if _version() != _ABI:
    raise ImportError("AIKernel native ABI version mismatch")

_create = _bind("create", C.c_uint32, C.c_int32, C.POINTER(C.c_void_p))
_destroy = _bind("destroy", C.c_void_p)
_alloc = _bind("device_alloc", C.c_void_p, C.c_size_t, C.POINTER(C.c_uint64))
_free = _bind("device_free", C.c_void_p, C.c_uint64)
_upload = _bind("upload", C.c_void_p, C.c_uint64, C.c_void_p, C.c_size_t)
_gemv = _bind("gemv", C.c_void_p, C.c_uint64, _FLOAT, _FLOAT, C.c_uint32, C.c_uint32)
_normalize = _bind("normalize", C.c_void_p, _FLOAT, _FLOAT, _FLOAT, C.c_uint32, C.c_float, C.c_int32)
_rope = _bind("rope", C.c_void_p, _FLOAT, _FLOAT, C.c_uint32)
_swiglu = _bind("swiglu", C.c_void_p, _FLOAT, _FLOAT, C.c_uint32)
_softmax = _bind("softmax", C.c_void_p, _FLOAT, C.c_uint32)


class _Stats(C.Structure):
    _fields_ = [("size", C.c_uint32), ("version", C.c_uint32),
                ("device_blocks", C.c_uint64), ("device_bytes", C.c_uint64),
                ("pinned_blocks", C.c_uint64), ("pinned_bytes", C.c_uint64),
                ("scratch_bytes_per_tier", C.c_uint64)]


_stats = _bind("get_stats", C.c_void_p, C.POINTER(_Stats))


class NativeError(RuntimeError):
    """A native status code returned by the ABI."""

    def __init__(self, status):
        self.status = status
        super().__init__(f"AIKernel native status {status}")


def _check(status):
    if status:
        raise NativeError(status)


def _floats(values):
    return (C.c_float * len(values))(*values)


class Context:
    """Serializes operations over a private CUDA context, or the host backend."""

    def __init__(self, device=0, *, fallback=True):
        self._handle = C.c_void_p()
        self._lock = RLock()
        self._closed = False
        self.cuda_status = 0
        if device is not None:
            if not isinstance(device, int) or not 0 <= device <= 0x7fffffff:
                raise ValueError("device must be a non-negative 32-bit ordinal")
            self.cuda_status = _create(_ABI, device, C.byref(self._handle))
            if not fallback or self.cuda_status in (1, 6):
                _check(self.cuda_status)

    @property
    def uses_cuda(self):
        return bool(self._handle)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    def close(self):
        with self._lock:
            if not self._closed:
                self._closed = True
                if self._handle:
                    handle, self._handle = self._handle, C.c_void_p()
                    _check(_destroy(handle))

    @contextmanager
    def _access(self):
        with self._lock:
            if self._closed:
                raise RuntimeError("Context is closed")
            yield self._handle

    def stats(self):
        with self._access() as handle:
            value = _Stats(C.sizeof(_Stats), _ABI)
            if handle:
                _check(_stats(handle, C.byref(value)))
            return {name: getattr(value, name) for name, _ in value._fields_[2:]}

    def gemv(self, matrix, vector):
        cols = len(vector)
        if not cols or not len(matrix) or len(matrix) % cols:
            raise ValueError("matrix must contain complete rows")
        rows = len(matrix) // cols
        weight, x, y = _floats(matrix), _floats(vector), (C.c_float * rows)()
        with self._access() as handle:
            if not handle:
                _check(_gemv(handle, C.addressof(weight), x, y, rows, cols))
            else:
                address = C.c_uint64()
                _check(_alloc(handle, C.sizeof(weight), C.byref(address)))
                try:
                    _check(_upload(handle, address, weight, C.sizeof(weight)))
                    _check(_gemv(handle, address, x, y, rows, cols))
                finally:
                    _check(_free(handle, address))
        return list(y)

    def normalize(self, values, scale, *, epsilon=1e-5, center=False):
        if len(values) != len(scale):
            raise ValueError("input and scale lengths must match")
        x, w, y = _floats(values), _floats(scale), (C.c_float * len(values))()
        with self._access() as handle:
            _check(_normalize(handle, x, w, y, len(x), epsilon, int(center)))
        return list(y)

    def rope(self, values, sin_cos):
        if len(values) != len(sin_cos):
            raise ValueError("input and coefficient lengths must match")
        x, coefficients = _floats(values), _floats(sin_cos)
        with self._access() as handle:
            _check(_rope(handle, x, coefficients, len(x)))
        return list(x)

    def swiglu(self, gate, up):
        if len(gate) != len(up):
            raise ValueError("gate and up lengths must match")
        g, u = _floats(gate), _floats(up)
        with self._access() as handle:
            _check(_swiglu(handle, g, u, len(g)))
        return list(g)

    def softmax(self, values):
        x = _floats(values)
        with self._access() as handle:
            _check(_softmax(handle, x, len(x)))
        return list(x)
