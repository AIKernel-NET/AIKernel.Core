"""Smoke the installed py3-none wheel on CPU or CUDA, including native allocation counters."""
import argparse
import ctypes as C
import json
import math

from aikernel_cuda_driver import Context, NativeError, _ABI, _bind, _check


def close(actual, expected):
    assert len(actual) == len(expected)
    assert all(math.isclose(a, e, rel_tol=1e-5, abs_tol=1e-6) for a, e in zip(actual, expected)), actual


def exercise(context):
    close(context.gemv([1, 2, 3, 4, 5, 6], [1, 2, 3]), [14, 32])
    close(context.normalize([3, 4], [1, 2]), [3 / math.sqrt(12.5 + 1e-5), 8 / math.sqrt(12.5 + 1e-5)])
    close(context.normalize([3, 5], [1, 1], center=True), [-1 / math.sqrt(1 + 1e-5), 1 / math.sqrt(1 + 1e-5)])
    close(context.rope([3, 4, 2, 1], [1, 0, 0, 1]), [-4, 3, 2, 1])
    close(context.swiglu([1, -1], [2, 3]), [2 / (1 + math.exp(-1)), -3 / (1 + math.exp(1))])
    close(context.softmax([3, 1]), [1 / (1 + math.exp(-2)), 1 / (1 + math.exp(2))])


def allocation_cycles(context):
    alloc = _bind("device_alloc", C.c_void_p, C.c_size_t, C.POINTER(C.c_uint64))
    free = _bind("device_free", C.c_void_p, C.c_uint64)
    host_alloc = _bind("pinned_alloc", C.c_void_p, C.c_size_t, C.POINTER(C.c_void_p))
    host_free = _bind("pinned_free", C.c_void_p, C.c_void_p)
    upload = _bind("upload", C.c_void_p, C.c_uint64, C.c_void_p, C.c_size_t)
    download = _bind("download", C.c_void_p, C.c_uint64, C.c_void_p, C.c_size_t)
    baseline = context.stats()
    with context._access() as handle:
        for i in range(100):
            device, ram = C.c_uint64(), C.c_void_p()
            _check(host_alloc(handle, 4096, C.byref(ram)))
            try:
                _check(alloc(handle, 4096, C.byref(device)))
                try:
                    resident = context.stats()
                    assert resident["pinned_bytes"] == resident["device_bytes"] == 4096
                    C.memset(ram, i, 4096)
                    _check(upload(handle, device, ram, 4096))
                    C.memset(ram, 0, 4096)
                    assert download(handle, device, ram, 4097) == 1
                    _check(download(handle, device, ram, 4096))
                    assert C.string_at(ram, 4096) == bytes([i]) * 4096
                finally:
                    _check(free(handle, device))
                assert free(handle, device) == 1
            finally:
                _check(host_free(handle, ram))
        huge = C.c_uint64()
        status = alloc(handle, (1 << 63), C.byref(huge))
        if status == 0:
            _check(free(handle, huge))
        assert status != 0 and huge.value == 0
    assert context.stats() == baseline
    assert baseline["device_blocks"] == baseline["pinned_blocks"] == 0
    return baseline


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--gpu", action="store_true")
    parser.add_argument("--no-driver", action="store_true")
    args = parser.parse_args()
    probe_pci = _bind("get_device_pci_bus_id", C.c_int32, C.POINTER(C.c_char), C.c_int32)
    pci = C.create_string_buffer(b"unresolved", 32)
    assert probe_pci(-1, pci, len(pci)) == 1 and not pci.value
    assert probe_pci(0, pci, 15) == 1
    assert probe_pci(2147483647, pci, len(pci)) != 0 and not pci.value
    if args.no_driver:
        assert probe_pci(0, pci, len(pci)) == 2 and not pci.value
    if args.gpu:
        assert probe_pci(0, pci, len(pci)) == 0 and b":" in pci.value
    probed_pci = pci.value

    create = _bind("create", C.c_uint32, C.c_int32, C.POINTER(C.c_void_p))
    handle = C.c_void_p()
    assert create(_ABI + 1, 0, C.byref(handle)) == 6 and not handle
    with Context(2147483647) as unavailable:
        assert unavailable.cuda_status != 0 and not unavailable.uses_cuda
        exercise(unavailable)
        if args.no_driver:
            assert unavailable.cuda_status == 2
    with Context(0, fallback=not args.gpu) as context:
        if args.gpu:
            get_pci = _bind("get_pci_bus_id", C.c_void_p, C.POINTER(C.c_char), C.c_int32)
            with context._access() as handle:
                _check(get_pci(handle, pci, len(pci)))
            assert pci.value == probed_pci
        assert not args.no_driver or not context.uses_cuda
        exercise(context)
        stats = allocation_cycles(context) if args.gpu else context.stats()
        print(json.dumps({"cuda": context.uses_cuda, "cuda_status": context.cuda_status,
                          "allocation_cycles": 100 if args.gpu else 0, "stats": stats}))
    try:
        Context(2147483647, fallback=False)
        raise AssertionError("Unavailable device unexpectedly opened")
    except NativeError as ex:
        assert ex.status != 0
