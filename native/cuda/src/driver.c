#include "driver.h"
#include "aikernel_cuda.h"
#include <string.h>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#define AK_SYMBOL(lib, name) GetProcAddress((HMODULE)(lib), (name))
#else
#include <dlfcn.h>
#define AK_SYMBOL(lib, name) dlsym((lib), (name))
#endif

int ak_driver_open(ak_driver *d) {
#ifdef _WIN32
    d->library = LoadLibraryExW(L"nvcuda.dll", NULL, LOAD_LIBRARY_SEARCH_SYSTEM32);
#else
    d->library = dlopen("libcuda.so.1", RTLD_NOW | RTLD_LOCAL);
    if (!d->library) d->library = dlopen("libcuda.so", RTLD_NOW | RTLD_LOCAL);
#endif
    if (!d->library) return AK_DRIVER_MISSING;

    /* CUDA symbols have the Driver API's calling convention on this platform. */
#define LOAD(field, name) do { \
    void *symbol = (void *)AK_SYMBOL(d->library, name); \
    if (!symbol) { ak_driver_close(d); return AK_DRIVER_SYMBOL; } \
    memcpy(&d->field, &symbol, sizeof(symbol)); \
} while (0)
    LOAD(init, "cuInit");
    LOAD(device_get, "cuDeviceGet");
    LOAD(device_count, "cuDeviceGetCount");
    LOAD(ctx_create, "cuCtxCreate_v2");
    LOAD(ctx_destroy, "cuCtxDestroy_v2");
    LOAD(ctx_push, "cuCtxPushCurrent_v2");
    LOAD(ctx_pop, "cuCtxPopCurrent_v2");
    LOAD(mem_alloc, "cuMemAlloc_v2");
    LOAD(mem_free, "cuMemFree_v2");
    LOAD(host_alloc, "cuMemAllocHost_v2");
    LOAD(host_free, "cuMemFreeHost");
    LOAD(host_register, "cuMemHostRegister_v2");
    LOAD(host_unregister, "cuMemHostUnregister");
    LOAD(memset8, "cuMemsetD8Async");
    LOAD(pci_bus_id, "cuDeviceGetPCIBusId");
    LOAD(htod, "cuMemcpyHtoDAsync_v2");
    LOAD(dtoh, "cuMemcpyDtoHAsync_v2");
    LOAD(stream_create, "cuStreamCreate");
    LOAD(stream_sync, "cuStreamSynchronize");
    LOAD(module_load, "cuModuleLoadData");
    LOAD(module_func, "cuModuleGetFunction");
    LOAD(launch, "cuLaunchKernel");
#undef LOAD
    return AK_OK;
}

void ak_driver_close(ak_driver *d) {
    if (!d->library) return;
#ifdef _WIN32
    FreeLibrary((HMODULE)d->library);
#else
    dlclose(d->library);
#endif
    d->library = NULL;
}

int ak_status(int value) {
    switch (value) {
        case 0: return AK_OK;
        case 2: return AK_OOM;
        case 100:
        case 101: return AK_NO_DEVICE;
        default: return AK_CUDA_ERROR_BASE + value;
    }
}
