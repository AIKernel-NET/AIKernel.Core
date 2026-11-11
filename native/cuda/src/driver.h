#ifndef AK_DRIVER_H
#define AK_DRIVER_H

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
#define CUDA_CALL __stdcall
#else
#define CUDA_CALL
#endif

typedef int (CUDA_CALL *ak_launch_fn)(void *, unsigned, unsigned, unsigned, unsigned, unsigned, unsigned,
                                     unsigned, void *, void **, void **);
typedef struct ak_driver {
    void *library;
    int (CUDA_CALL *init)(unsigned);
    int (CUDA_CALL *device_get)(int *, int);
    int (CUDA_CALL *ctx_create)(void **, unsigned, int);
    int (CUDA_CALL *ctx_destroy)(void *);
    int (CUDA_CALL *ctx_push)(void *);
    int (CUDA_CALL *ctx_pop)(void **);
    int (CUDA_CALL *mem_alloc)(uint64_t *, size_t);
    int (CUDA_CALL *mem_free)(uint64_t);
    int (CUDA_CALL *host_alloc)(void **, size_t);
    int (CUDA_CALL *host_free)(void *);
    int (CUDA_CALL *host_register)(void *, size_t, unsigned);
    int (CUDA_CALL *host_unregister)(void *);
    int (CUDA_CALL *memset8)(uint64_t, unsigned char, size_t, void *);
    int (CUDA_CALL *pci_bus_id)(char *, int, int);
    int (CUDA_CALL *htod)(uint64_t, const void *, size_t, void *);
    int (CUDA_CALL *dtoh)(void *, uint64_t, size_t, void *);
    int (CUDA_CALL *stream_create)(void **, unsigned);
    int (CUDA_CALL *stream_sync)(void *);
    int (CUDA_CALL *module_load)(void **, const void *);
    int (CUDA_CALL *module_func)(void **, void *, const char *);
    ak_launch_fn launch;
} ak_driver;

int ak_driver_open(ak_driver *driver);
void ak_driver_close(ak_driver *driver);
int ak_status(int cuda_result);

#endif
