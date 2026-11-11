#ifndef AIKERNEL_CUDA_H
#define AIKERNEL_CUDA_H

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
#define AK_EXPORT __declspec(dllexport)
#else
#define AK_EXPORT __attribute__((visibility("default")))
#endif
#ifdef __cplusplus
extern "C" {
#endif

enum { AK_ABI = 1, AK_OK = 0, AK_INVALID = 1, AK_DRIVER_MISSING = 2, AK_NO_DEVICE = 3,
       AK_OOM = 4, AK_DRIVER_SYMBOL = 5, AK_VERSION = 6, AK_CUDA_ERROR_BASE = 10000 };

typedef struct ak_context ak_context;

typedef struct ak_stats {
    uint32_t size;
    uint32_t version;
    uint64_t device_blocks;
    uint64_t device_bytes;
    uint64_t pinned_blocks;
    uint64_t pinned_bytes;
    uint64_t scratch_bytes_per_tier;
} ak_stats;

/* Calls on one context must be serialized. A NULL context selects host execution for math. */
AK_EXPORT uint32_t ak_abi_version(void);
AK_EXPORT int32_t ak_create(uint32_t version, int32_t ordinal, ak_context **result);
AK_EXPORT int32_t ak_destroy(ak_context *ctx);
AK_EXPORT int32_t ak_get_stats(ak_context *ctx, ak_stats *result);
AK_EXPORT int32_t ak_device_alloc(ak_context *ctx, size_t bytes, uint64_t *address);
AK_EXPORT int32_t ak_device_free(ak_context *ctx, uint64_t address);
AK_EXPORT int32_t ak_pinned_alloc(ak_context *ctx, size_t bytes, void **address);
AK_EXPORT int32_t ak_pinned_free(ak_context *ctx, void *address);
AK_EXPORT int32_t ak_upload(ak_context *ctx, uint64_t address, const void *source, size_t bytes);
AK_EXPORT int32_t ak_upload_shared(ak_context *ctx, uint64_t address, void *base, size_t capacity,
                                       size_t offset, size_t bytes);
AK_EXPORT int32_t ak_wipe_device(ak_context *ctx, uint64_t address, size_t bytes);
AK_EXPORT int32_t ak_wipe_scratch(ak_context *ctx);
AK_EXPORT int32_t ak_get_pci_bus_id(ak_context *ctx, char *target, int capacity);
AK_EXPORT int32_t ak_download(ak_context *ctx, uint64_t address, void *destination, size_t bytes);
AK_EXPORT int32_t ak_gemv(ak_context *ctx, uint64_t weights, const float *x, float *y, uint32_t rows, uint32_t cols);
AK_EXPORT int32_t ak_normalize(ak_context *ctx, const float *x, const float *scale, float *y,
                               uint32_t count, float epsilon, int32_t center);
AK_EXPORT int32_t ak_rope(ak_context *ctx, float *x, const float *sin_cos, uint32_t count);
AK_EXPORT int32_t ak_swiglu(ak_context *ctx, float *gate, const float *up, uint32_t count);
AK_EXPORT int32_t ak_softmax(ak_context *ctx, float *x, uint32_t count);
/* Reads a tile of interleaved key/value rows. Mode 0 emits scores, mode 1 emits a weighted value sum. */
AK_EXPORT int32_t ak_attention_tile(ak_context *ctx, uint64_t page, const float *x, float *y,
                                    uint32_t rows, uint32_t kv_width, uint32_t head_offset,
                                    uint32_t head_width, uint32_t mode);

#ifdef __cplusplus
}
#endif
#endif
