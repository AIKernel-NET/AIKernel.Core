"""Exercise CUDA shared DMA and VRAM zeroing with a separate producer process."""
import argparse
import ctypes as c
import json
import multiprocessing as mp
from multiprocessing.shared_memory import SharedMemory


SIZE = 4096
PAYLOAD = bytes((index * 43 + 7) % 256 for index in range(SIZE))


def produce(pipe):
    with SharedMemoryOwner() as region:
        region.buf[:] = PAYLOAD
        pipe.send(region.name)
        pipe.recv()


class SharedMemoryOwner:
    def __enter__(self):
        self.region = SharedMemory(create=True, size=SIZE)
        return self.region

    def __exit__(self, *_):
        self.region.close()
        self.region.unlink()


def verify(library, ordinal):
    dll = c.CDLL(library)
    signatures = {
        "ak_create": (c.c_uint32, c.c_int32, c.POINTER(c.c_void_p)),
        "ak_destroy": (c.c_void_p,),
        "ak_device_alloc": (c.c_void_p, c.c_size_t, c.POINTER(c.c_uint64)),
        "ak_device_free": (c.c_void_p, c.c_uint64),
        "ak_upload_shared": (c.c_void_p, c.c_uint64, c.c_void_p, c.c_size_t, c.c_size_t, c.c_size_t),
        "ak_download": (c.c_void_p, c.c_uint64, c.c_void_p, c.c_size_t),
        "ak_wipe_device": (c.c_void_p, c.c_uint64, c.c_size_t),
        "ak_wipe_scratch": (c.c_void_p,),
        "ak_get_pci_bus_id": (c.c_void_p, c.c_void_p, c.c_int),
    }
    for name, arguments in signatures.items():
        function = getattr(dll, name)
        function.argtypes = arguments
        function.restype = c.c_int32

    context = c.c_void_p()
    status = dll.ak_create(1, ordinal, c.byref(context))
    assert status == 0, ("ak_create", status)
    try:
        pci = c.create_string_buffer(32)
        assert dll.ak_get_pci_bus_id(context, pci, len(pci)) == 0
        address = c.c_uint64()
        assert dll.ak_device_alloc(context, SIZE, c.byref(address)) == 0
        try:
            parent, child = mp.Pipe()
            process = mp.Process(target=produce, args=(child,))
            process.start()
            child.close()
            try:
                assert parent.poll(30), "shared producer did not start"
                region = SharedMemory(parent.recv())
                try:
                    pointer = c.addressof(c.c_char.from_buffer(region.buf))
                    status = dll.ak_upload_shared(context, address, pointer, SIZE, 0, SIZE)
                    assert status == 0, ("ak_upload_shared", status)
                    observed = c.create_string_buffer(SIZE)
                    assert dll.ak_download(context, address, observed, SIZE) == 0
                    assert observed.raw == PAYLOAD
                    assert dll.ak_wipe_device(context, address, SIZE) == 0
                    assert dll.ak_download(context, address, observed, SIZE) == 0
                    assert observed.raw == bytes(SIZE)
                    assert dll.ak_wipe_scratch(context) == 0
                finally:
                    region.close()
            finally:
                parent.send(None)
                parent.close()
                process.join(15)
                if process.is_alive():
                    process.terminate()
                    process.join()
            assert process.exitcode == 0
        finally:
            assert dll.ak_device_free(context, address) == 0
        print(json.dumps({"pci": pci.value.decode(), "cross_process_dma_bytes": SIZE, "vram_zeroed": True}))
    finally:
        assert dll.ak_destroy(context) == 0


if __name__ == "__main__":
    mp.set_start_method("spawn")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("library")
    parser.add_argument("--device", type=int, default=0)
    args = parser.parse_args()
    verify(args.library, args.device)
