#include "aikernel_cuda.h"
#include "cpu_gemv.h"
#include "driver.h"
#include "embedded_ptx.h"
#include "q1_dp4a.h"
#include "q1_mma.h"
#include "gemma_gemv.h"

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <time.h>
#endif
#include <math.h>
#include <stdlib.h>
#include <string.h>

typedef struct allocation {
    uint64_t address;
    size_t bytes;
    struct allocation *next;
} allocation;

struct ak_context {
    ak_driver driver;
    void *cuda;
    int device;
    void *stream;
    void *gemv;
    void *gemv_q1;
    void *gemma_gemv;
    void *gemv_q8[2];
    uint32_t int8_capabilities;
    void *normalize;
    void *rope;
    void *swiglu;
    void *softmax;
    void *attention;
    allocation *device_blocks;
    allocation *pinned_blocks;
    void *scratch_host;
    uint64_t scratch_device;
    size_t scratch_bytes;
    int fault;
    int profile_enabled;
    void *profile_events[4]; /* Context-owned, reclaimed with the private context. */
    ak_matrix_profile profile;
};

uint32_t ak_abi_version(void) { return AK_ABI; }

int32_t ak_get_device_count(int32_t *count) {
    if (!count) return AK_INVALID;
    *count = 0;
    ak_driver driver = { 0 };
    int status = ak_driver_open(&driver);
    if (status) return status;
    int code = driver.init(0);
    int devices = 0;
    if (!code) code = driver.device_count(&devices);
    if (!code) *count = devices;
    ak_driver_close(&driver);
    return ak_status(code);
}

int32_t ak_get_device_pci_bus_id(int32_t ordinal, char *target, int32_t capacity) {
    if (!target || capacity < 16) return AK_INVALID;
    target[0] = '\0';
    if (ordinal < 0) return AK_INVALID;

    ak_driver driver = { 0 };
    int status = ak_driver_open(&driver);
    if (status) return status;
    int device = 0;
    int code = driver.init(0);
    if (!code) code = driver.device_get(&device, ordinal);
    if (!code) code = driver.pci_bus_id(target, capacity, device);
    if (code) target[0] = '\0';
    ak_driver_close(&driver);
    return ak_status(code);
}

int32_t ak_create(uint32_t version, int32_t ordinal, ak_context **result) {
    if (!result) return AK_INVALID;
    *result = NULL;
    if (version != AK_ABI) return AK_VERSION;
    if (ordinal < 0) return AK_INVALID;

    ak_context *ctx = calloc(1, sizeof(*ctx));
    if (!ctx) return AK_OOM;
    int status = ak_driver_open(&ctx->driver);
    if (status == AK_OK) {
        ak_driver *d = &ctx->driver;
        void *module = NULL, *popped = NULL;
        int code = d->init(0);
        if (!code) code = d->device_get(&ctx->device, ordinal);
        int major = 0, minor = 0;
        if (!code) code = d->device_attribute(&major, 75, ctx->device);
        if (!code) code = d->device_attribute(&minor, 76, ctx->device);
        if (!code && (major > 6 || (major == 6 && minor >= 1))) ctx->int8_capabilities |= 1;
        /* This MMA kernel targets sm_80, not all sm_75 devices have Tensor Cores. */
        if (!code && major >= 8) ctx->int8_capabilities |= 2;
        if (!code) code = d->ctx_create(&ctx->cuda, 0, ctx->device);
        if (!code) code = d->stream_create(&ctx->stream, 1);
        if (!code) code = d->module_load(&module, ak_embedded_ptx);
        if (!code) code = d->module_func(&ctx->gemv, module, "matvec");
        if (!code) code = d->module_func(&ctx->gemv_q1, module, "matvec_q1_0");
        if (!code) code = d->module_func(&ctx->normalize, module, "normalize");
        if (!code) code = d->module_func(&ctx->rope, module, "rotate");
        if (!code) code = d->module_func(&ctx->swiglu, module, "swiglu");
        if (!code) code = d->module_func(&ctx->softmax, module, "softmax");
        if (!code) code = d->module_func(&ctx->attention, module, "attention");
        if (!code) code = d->ctx_pop(&popped);
        status = ak_status(code);
    }

    if (status != AK_OK) {
        if (ctx->cuda) ctx->driver.ctx_destroy(ctx->cuda);
        ak_driver_close(&ctx->driver);
        free(ctx);
        return status;
    }

    *result = ctx;
    return AK_OK;
}

static int enter(ak_context *ctx) {
    if (!ctx) return AK_INVALID;
    if (ctx->fault) return ctx->fault;
    return ak_status(ctx->driver.ctx_push(ctx->cuda));
}

static int leave(ak_context *ctx, int status) {
    void *popped;
    ctx->fault = ak_status(ctx->driver.ctx_pop(&popped));
    return status ? status : ctx->fault;
}

static int finish_stream(ak_context *ctx, int code) {
    int sync = ctx->driver.stream_sync(ctx->stream);
    return ak_status(code ? code : sync);
}

static double profile_clock_ms(void) {
#ifdef _WIN32
    LARGE_INTEGER counter, frequency;
    QueryPerformanceCounter(&counter); QueryPerformanceFrequency(&frequency);
    return (double)counter.QuadPart * 1000.0 / (double)frequency.QuadPart;
#else
    struct timespec now;
    clock_gettime(CLOCK_MONOTONIC, &now);
    return (double)now.tv_sec * 1000.0 + (double)now.tv_nsec / 1000000.0;
#endif
}

int32_t ak_profile_configure(ak_context *ctx, uint32_t enabled) {
    if (enabled > 1) return AK_INVALID;
    int status = enter(ctx);
    if (status) return status;
    ctx->profile_enabled = 0;
    memset(&ctx->profile, 0, sizeof(ctx->profile));
    ctx->profile.size = sizeof(ctx->profile); ctx->profile.version = AK_ABI;
    for (int i = 0; enabled && i < 4 && !status; i++) {
        if (!ctx->profile_events[i]) status = ak_status(ctx->driver.event_create(&ctx->profile_events[i], 0));
    }
    if (!status) ctx->profile_enabled = (int)enabled;
    return leave(ctx, status);
}

int32_t ak_profile_read(ak_context *ctx, ak_matrix_profile *result) {
    if (!ctx || !result || result->size < sizeof(*result)) return AK_INVALID;
    if (result->version != AK_ABI) return AK_VERSION;
    if (!ctx->profile_enabled) return AK_INVALID;
    if (ctx->fault) return ctx->fault;
    *result = ctx->profile;
    return AK_OK;
}

/* Record on the existing private stream; no additional synchronization. */
static int profile_mark(ak_context *ctx, int index, int code) {
    return !code && ctx->profile_enabled
        ? ctx->driver.event_record(ctx->profile_events[index], ctx->stream) : code;
}

static int profile_finish(ak_context *ctx, int code, size_t upload, size_t download) {
    double start = ctx->profile_enabled ? profile_clock_ms() : 0;
    int status = finish_stream(ctx, code);
    if (ctx->profile_enabled && !status) {
        ctx->profile.host_sync_ms += profile_clock_ms() - start;
        float elapsed[3] = {0};
        for (int i = 0; i < 3 && !status; i++)
            status = ak_status(ctx->driver.event_elapsed(&elapsed[i], ctx->profile_events[i], ctx->profile_events[i+1]));
        if (!status) {
            ctx->profile.calls++; ctx->profile.upload_bytes += upload; ctx->profile.download_bytes += download;
            ctx->profile.upload_ms += elapsed[0]; ctx->profile.kernel_ms += elapsed[1]; ctx->profile.download_ms += elapsed[2];
        }
    }
    return status;
}

static void wipe_host(void *address, size_t bytes) {
    volatile unsigned char *cursor = address;
    while (bytes--) *cursor++ = 0;
}

static int wipe_scratch_current(ak_context *ctx) {
    if (!ctx->scratch_bytes) return AK_OK;
    wipe_host(ctx->scratch_host, ctx->scratch_bytes);
    return finish_stream(ctx, ctx->driver.memset8(ctx->scratch_device, 0, ctx->scratch_bytes, ctx->stream));
}

int32_t ak_wipe_scratch(ak_context *ctx) {
    int status = enter(ctx);
    if (status != AK_OK) return status;
    return leave(ctx, wipe_scratch_current(ctx));
}

int32_t ak_get_pci_bus_id(ak_context *ctx, char *target, int capacity) {
    if (!ctx || !target || capacity < 16) return AK_INVALID;
    return ak_status(ctx->driver.pci_bus_id(target, capacity, ctx->device));
}

static allocation **find_allocation(allocation **head, uint64_t address) {
    while (*head && (*head)->address != address) head = &(*head)->next;
    return head;
}

static int within(allocation **head, uint64_t address, size_t bytes) {
    /* Interior ranges are needed for bounded, chunked uploads and mapped tensors.
       Subtraction avoids overflow at the end of an allocation. Free still requires its base. */
    for (allocation *item = *head; item; item = item->next) {
        if (address >= item->address && address - item->address < item->bytes &&
            bytes <= item->bytes - (size_t)(address - item->address)) return 1;
    }
    return 0;
}

int32_t ak_destroy(ak_context *ctx) {
    if (!ctx) return AK_INVALID;
    ak_driver *d = &ctx->driver;
    int status = ak_status(d->ctx_push(ctx->cuda));
    int current = status == AK_OK;
    if (current) {
        status = wipe_scratch_current(ctx);
        if (ctx->scratch_host) {
            int next = ak_status(d->host_free(ctx->scratch_host));
            if (!status) status = next;
        }
    }
    while (ctx->pinned_blocks) {
        allocation *item = ctx->pinned_blocks;
        ctx->pinned_blocks = item->next;
        if (current) {
            int next = ak_status(d->host_free((void *)(uintptr_t)item->address));
            if (!status) status = next;
        }
        free(item);
    }
    while (ctx->device_blocks) {
        allocation *item = ctx->device_blocks;
        ctx->device_blocks = item->next;
        free(item);
    }
    /* Destroying the private context reclaims its device allocations, stream and module. */
    int destroyed = ak_status(d->ctx_destroy(ctx->cuda));
    if (!status) status = destroyed;
    ak_driver_close(d);
    free(ctx);
    return status;
}

int32_t ak_get_stats(ak_context *ctx, ak_stats *result) {
    if (!ctx || !result || result->size < sizeof(*result)) return AK_INVALID;
    if (result->version != AK_ABI) return AK_VERSION;
    ak_stats stats = { sizeof(stats), AK_ABI, 0, 0, 0, 0, ctx->scratch_bytes };
    for (allocation *item = ctx->device_blocks; item; item = item->next) {
        stats.device_blocks++;
        stats.device_bytes += item->bytes;
    }
    for (allocation *item = ctx->pinned_blocks; item; item = item->next) {
        stats.pinned_blocks++;
        stats.pinned_bytes += item->bytes;
    }
    *result = stats;
    return AK_OK;
}

static int allocate(ak_context *ctx, size_t bytes, uint64_t *address, int pinned) {
    if (!address || !bytes) return AK_INVALID;
    *address = 0;
    allocation *item = malloc(sizeof(*item));
    if (!item) return AK_OOM;

    int status = enter(ctx);
    if (status == AK_OK) {
        void *host = NULL;
        int code = pinned ? ctx->driver.host_alloc(&host, bytes) : ctx->driver.mem_alloc(address, bytes);
        if (pinned) *address = (uint64_t)(uintptr_t)host;
        status = ak_status(code);
        if (status == AK_OK) {
            allocation **list = pinned ? &ctx->pinned_blocks : &ctx->device_blocks;
            item->address = *address;
            item->bytes = bytes;
            item->next = *list;
            *list = item;
            item = NULL;
        }
        status = leave(ctx, status);
    }

    free(item);
    if (status != AK_OK) *address = 0;
    return status;
}

int32_t ak_device_alloc(ak_context *ctx, size_t bytes, uint64_t *address) {
    return allocate(ctx, bytes, address, 0);
}

int32_t ak_pinned_alloc(ak_context *ctx, size_t bytes, void **address) {
    if (!address) return AK_INVALID;
    uint64_t result = 0;
    int status = allocate(ctx, bytes, &result, 1);
    *address = (void *)(uintptr_t)result;
    return status;
}

static int release(ak_context *ctx, uint64_t address, int pinned) {
    if (!ctx || !address) return AK_INVALID;
    allocation **slot = find_allocation(pinned ? &ctx->pinned_blocks : &ctx->device_blocks, address);
    if (!*slot) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;

    status = ak_status(pinned ? ctx->driver.host_free((void *)(uintptr_t)address) : ctx->driver.mem_free(address));
    if (status == AK_OK) {
        allocation *item = *slot;
        *slot = item->next;
        free(item);
    }
    /* Once freed, report success and retain any context-pop fault for the next call. */
    leave(ctx, status);
    return status;
}

int32_t ak_device_free(ak_context *ctx, uint64_t address) { return release(ctx, address, 0); }
int32_t ak_pinned_free(ak_context *ctx, void *address) { return release(ctx, (uint64_t)(uintptr_t)address, 1); }

static int scratch(ak_context *ctx, size_t bytes) {
    if (bytes <= ctx->scratch_bytes) return AK_OK;
    ak_driver *d = &ctx->driver;
    int cleared = wipe_scratch_current(ctx);
    if (cleared) return cleared;
    ctx->scratch_bytes = 0;
    if (ctx->scratch_host) {
        int code = d->host_free(ctx->scratch_host);
        if (code) return ak_status(code);
        ctx->scratch_host = NULL;
    }
    if (ctx->scratch_device) {
        int code = d->mem_free(ctx->scratch_device);
        if (code) return ak_status(code);
        ctx->scratch_device = 0;
    }

    int code = d->host_alloc(&ctx->scratch_host, bytes);
    if (!code) code = d->mem_alloc(&ctx->scratch_device, bytes);
    if (!code) ctx->scratch_bytes = bytes;
    return ak_status(code);
}

int32_t ak_upload(ak_context *ctx, uint64_t address, const void *source, size_t bytes) {
    if (!ctx || !source || !bytes || !within(&ctx->device_blocks, address, bytes)) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;

    if (!within(&ctx->pinned_blocks, (uint64_t)(uintptr_t)source, bytes)) {
        status = scratch(ctx, bytes);
        if (!status) source = memcpy(ctx->scratch_host, source, bytes);
    }
    if (!status) status = finish_stream(ctx, ctx->driver.htod(address, source, bytes, ctx->stream));
    return leave(ctx, status);
}

int32_t ak_wipe_device(ak_context *ctx, uint64_t address, size_t bytes) {
    if (!ctx || !bytes || !within(&ctx->device_blocks, address, bytes)) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;
    return leave(ctx, finish_stream(ctx, ctx->driver.memset8(address, 0, bytes, ctx->stream)));
}

int32_t ak_upload_shared(ak_context *ctx, uint64_t address, void *base, size_t capacity,
                         size_t offset, size_t bytes) {
    if (!ctx || !base || !bytes || offset > capacity || bytes > capacity - offset ||
        !within(&ctx->device_blocks, address, bytes)) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;

    int code = ctx->driver.host_register(base, capacity, 0);
    if (code == 0) {
        status = finish_stream(ctx, ctx->driver.htod(address, (char *)base + offset, bytes, ctx->stream));
        int cleanup = ak_status(ctx->driver.host_unregister(base));
        if (!status) status = cleanup;
    } else {
        status = ak_status(code);
    }
    return leave(ctx, status);
}

int32_t ak_download(ak_context *ctx, uint64_t address, void *destination, size_t bytes) {
    if (!ctx || !destination || !bytes || !within(&ctx->device_blocks, address, bytes)) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;

    void *target = destination;
    if (!within(&ctx->pinned_blocks, (uint64_t)(uintptr_t)destination, bytes)) {
        status = scratch(ctx, bytes);
        target = ctx->scratch_host;
    }
    if (!status) status = finish_stream(ctx, ctx->driver.dtoh(target, address, bytes, ctx->stream));
    if (!status && target != destination) memcpy(destination, target, bytes);
    return leave(ctx, status);
}

static int device_gemv(ak_context *ctx, void *kernel, uint64_t weights, const float *x, float *y,
                       uint32_t rows, uint32_t cols) {
    int status = enter(ctx);
    if (status != AK_OK) return status;

    const size_t x_bytes = (size_t)cols * sizeof(float);
    const size_t y_bytes = (size_t)rows * sizeof(float);
    status = scratch(ctx, x_bytes + y_bytes);
    if (!status) {
        uint64_t dx = ctx->scratch_device, dy = dx + x_bytes;
        void *args[] = { &weights, &dx, &dy, &cols };
        char *host_y = (char *)ctx->scratch_host + x_bytes;
        memcpy(ctx->scratch_host, x, x_bytes);
        int code = profile_mark(ctx, 0, 0);
        if (!code) code = ctx->driver.htod(dx, ctx->scratch_host, x_bytes, ctx->stream);
        code = profile_mark(ctx, 1, code);
        if (!code) code = ctx->driver.launch(kernel, rows, 1, 1, 128, 1, 1, 0, ctx->stream, args, NULL);
        code = profile_mark(ctx, 2, code);
        if (!code) code = ctx->driver.dtoh(host_y, dy, y_bytes, ctx->stream);
        code = profile_mark(ctx, 3, code);
        status = profile_finish(ctx, code, x_bytes, y_bytes);
        if (!status) memcpy(y, host_y, y_bytes);
    }
    return leave(ctx, status);
}

int32_t ak_gemv(ak_context *ctx, uint64_t weights, const float *x, float *y, uint32_t rows, uint32_t cols) {
    if (!weights || !x || !y || !rows || !cols ||
        (size_t)rows > SIZE_MAX / sizeof(float) / cols) return AK_INVALID;
    if (!ctx) return ak_cpu_gemv((const float *)(uintptr_t)weights, x, y, rows, cols);
    if (weights % sizeof(float) || !within(&ctx->device_blocks, weights, (size_t)rows * cols * sizeof(float))) return AK_INVALID;
    return device_gemv(ctx, ctx->gemv, weights, x, y, rows, cols);
}

int32_t ak_gemv_q1_0(ak_context *ctx, uint64_t weights, const float *x, float *y, uint32_t rows, uint32_t cols) {
    /* GGUF type 41: 128 signs + one FP16 scale = 18 bytes per block. No CPU fallback. */
    if (!ctx || !weights || weights % 2 || !x || !y || !rows || !cols || cols % 128 ||
        (size_t)rows > SIZE_MAX / 18 / (cols / 128)) return AK_INVALID;
    if (!within(&ctx->device_blocks, weights, (size_t)rows * (cols / 128) * 18)) return AK_INVALID;
    return device_gemv(ctx, ctx->gemv_q1, weights, x, y, rows, cols);
}

int32_t ak_gemv_gemma(ak_context *ctx, uint64_t weights, const float *x, float *y,
                       uint32_t rows, uint32_t cols, uint32_t type) {
    if (!ctx || !weights || weights % 2 || !x || !y || !rows || !cols) return AK_INVALID;
    size_t stride;
    if (type == 2 && cols % 32 == 0) stride = (size_t)cols / 32 * 18;
    else if (type == 14 && cols % 256 == 0) stride = (size_t)cols / 256 * 210;
    else if (type == 1 && cols % 32 == 0) stride = (size_t)cols * 2;
    else return AK_INVALID;
    if (rows > SIZE_MAX / stride || !within(&ctx->device_blocks, weights, (size_t)rows * stride)) return AK_INVALID;
    int status = enter(ctx);
    if (status) return status;
    if (!ctx->gemma_gemv) {
        void *module = NULL;
        int code = ctx->driver.module_load(&module, ak_gemma_gemv_ptx);
        if (!code) code = ctx->driver.module_func(&ctx->gemma_gemv, module, "gemma_gemv");
        status = ak_status(code);
    }
    size_t x_bytes = (size_t)cols * sizeof(float), y_bytes = (size_t)rows * sizeof(float);
    if (!status) status = scratch(ctx, x_bytes + y_bytes);
    if (!status) {
        uint64_t dx = ctx->scratch_device, dy = dx + x_bytes;
        void *args[] = { &weights, &dx, &dy, &cols, &rows, &type };
        char *host_y = (char *)ctx->scratch_host + x_bytes;
        memcpy(ctx->scratch_host, x, x_bytes);
        int code = profile_mark(ctx, 0, 0);
        if (!code) code = ctx->driver.htod(dx, ctx->scratch_host, x_bytes, ctx->stream);
        code = profile_mark(ctx, 1, code);
        if (!code) code = ctx->driver.launch(ctx->gemma_gemv, rows / 4 + (rows % 4 != 0), 1, 1, 128, 1, 1, 0, ctx->stream, args, NULL);
        code = profile_mark(ctx, 2, code);
        if (!code) code = ctx->driver.dtoh(host_y, dy, y_bytes, ctx->stream);
        code = profile_mark(ctx, 3, code);
        status = profile_finish(ctx, code, x_bytes, y_bytes);
        if (!status) memcpy(y, host_y, y_bytes);
    }
    return leave(ctx, status);
}

int32_t ak_get_int8_capabilities(ak_context *ctx, uint32_t *flags) {
    if (!ctx || !flags) return AK_INVALID;
    *flags = ctx->int8_capabilities;
    return AK_OK;
}

int32_t ak_gemv_q1_q8(ak_context *ctx, uint64_t weights, const int8_t *x,
                     const float *scales, const int32_t *sums, float *y,
                     uint32_t rows, uint32_t cols, uint32_t backend) {
    /* backend 1: DP4A (sm_61+); 2: integer MMA (sm_80+). Never silently fallback. */
    if (!ctx || !weights || weights % 2 || !x || !scales || !sums || !y ||
        !rows || rows > 200000 || !cols || cols > 200000 || cols % 128 || backend < 1 || backend > 2 ||
        !within(&ctx->device_blocks, weights, (size_t)rows * (cols / 128) * 18)) return AK_INVALID;
    if (!(ctx->int8_capabilities & (1u << (backend - 1)))) return AK_VERSION;
    int status = enter(ctx);
    if (status) return status;
    void **kernel = &ctx->gemv_q8[backend - 1];
    if (!*kernel) {
        void *module = NULL;
        int code = ctx->driver.module_load(&module, backend == 1 ? ak_q1_dp4a_ptx : ak_q1_mma_ptx);
        if (!code) code = ctx->driver.module_func(kernel, module, backend == 1 ? "matvec_q1_q8_dp4a" : "matvec_q1_q8_mma");
        if (code) return leave(ctx, ak_status(code));
    }
    const size_t block_bytes = (size_t)(cols / 128) * sizeof(float);
    const size_t input_bytes = cols + block_bytes * 2, output_bytes = (size_t)rows * sizeof(float);
    status = scratch(ctx, input_bytes + output_bytes);
    if (!status) {
        char *host = ctx->scratch_host;
        memcpy(host, x, cols); memcpy(host + cols, scales, block_bytes); memcpy(host + cols + block_bytes, sums, block_bytes);
        uint64_t dx = ctx->scratch_device, ds = dx + cols, di = ds + block_bytes, dy = dx + input_bytes;
        void *args[] = { &weights, &dx, &ds, &di, &dy, &rows, &cols };
        int code = profile_mark(ctx, 0, 0);
        if (!code) code = ctx->driver.htod(dx, host, input_bytes, ctx->stream);
        uint32_t grid = backend == 1 ? (rows + 3) / 4 : (rows + 15) / 16;
        code = profile_mark(ctx, 1, code);
        if (!code) code = ctx->driver.launch(*kernel, grid, 1, 1, backend == 1 ? 128 : 32, 1, 1, 0, ctx->stream, args, NULL);
        code = profile_mark(ctx, 2, code);
        if (!code) code = ctx->driver.dtoh(host + input_bytes, dy, output_bytes, ctx->stream);
        code = profile_mark(ctx, 3, code);
        status = profile_finish(ctx, code, input_bytes, output_bytes);
        if (!status) memcpy(y, host + input_bytes, output_bytes);
    }
    return leave(ctx, status);
}

static int vector_kernel(ak_context *ctx, void *kernel, const float *a, const float *b, float *out,
                         uint32_t count, float epsilon, uint32_t mode, int serial) {
    int status = enter(ctx);
    if (status != AK_OK) return status;
    size_t bytes = (size_t)count * sizeof(float);
    size_t inputs = b ? bytes * 2 : bytes;
    status = scratch(ctx, inputs + bytes);
    if (!status) {
        uint64_t da = ctx->scratch_device, db = b ? da + bytes : 0, dout = da + inputs;
        char *host = ctx->scratch_host;
        memcpy(host, a, bytes);
        if (b) memcpy(host + bytes, b, bytes);
        void *args[] = { &da, &db, &dout, &count, &epsilon, &mode };
        int code = ctx->driver.htod(da, host, inputs, ctx->stream);
        if (!code) code = ctx->driver.launch(kernel, serial ? 1 : (count - 1) / 128 + 1,
            1, 1, serial ? 1 : 128, 1, 1, 0, ctx->stream, args, NULL);
        if (!code) code = ctx->driver.dtoh(host + inputs, dout, bytes, ctx->stream);
        status = finish_stream(ctx, code);
        if (!status) memcpy(out, host + inputs, bytes);
    }
    return leave(ctx, status);
}

int32_t ak_normalize(ak_context *ctx, const float *x, const float *scale, float *y,
                     uint32_t count, float epsilon, int32_t center) {
    if (!x || !scale || !y || !count || !isfinite(epsilon) || epsilon <= 0 || (center != 0 && center != 1))
        return AK_INVALID;
    if (ctx) return vector_kernel(ctx, ctx->normalize, x, scale, y, count, epsilon, (uint32_t)center, 1);

    float mean = 0, variance = 0;
    if (center) {
        for (uint32_t i = 0; i < count; ++i) mean += x[i];
        mean /= (float)count;
    }
    for (uint32_t i = 0; i < count; ++i) {
        float v = x[i] - mean;
        variance += v * v;
    }
    float inverse = 1.0f / sqrtf(variance / (float)count + epsilon);
    for (uint32_t i = 0; i < count; ++i) y[i] = (x[i] - mean) * inverse * scale[i];
    return AK_OK;
}

int32_t ak_rope(ak_context *ctx, float *x, const float *sin_cos, uint32_t count) {
    if (!x || !sin_cos || !count || count % 2) return AK_INVALID;
    if (ctx) return vector_kernel(ctx, ctx->rope, x, sin_cos, x, count, 0, 0, 0);
    for (uint32_t i = 0; i < count; i += 2) {
        float a = x[i], b = x[i + 1], s = sin_cos[i], c = sin_cos[i + 1];
        x[i] = a * c - b * s;
        x[i + 1] = a * s + b * c;
    }
    return AK_OK;
}

int32_t ak_swiglu(ak_context *ctx, float *gate, const float *up, uint32_t count) {
    if (!gate || !up || !count) return AK_INVALID;
    if (ctx) return vector_kernel(ctx, ctx->swiglu, gate, up, gate, count, 0, 0, 0);
    for (uint32_t i = 0; i < count; ++i) gate[i] = gate[i] / (1.0f + expf(-gate[i])) * up[i];
    return AK_OK;
}

int32_t ak_softmax(ak_context *ctx, float *x, uint32_t count) {
    if (!x || !count) return AK_INVALID;
    if (ctx) return vector_kernel(ctx, ctx->softmax, x, NULL, x, count, 0, 0, 1);

    float max = x[0], sum = 0;
    for (uint32_t i = 1; i < count; ++i) max = fmaxf(max, x[i]);
    for (uint32_t i = 0; i < count; ++i) {
        x[i] = expf(x[i] - max);
        sum += x[i];
    }
    for (uint32_t i = 0; i < count; ++i) x[i] /= sum;
    return AK_OK;
}

int32_t ak_attention_tile(ak_context *ctx, uint64_t page, const float *x, float *y,
                          uint32_t rows, uint32_t kv_width, uint32_t head_offset,
                          uint32_t head_width, uint32_t mode) {
    if (!page || !x || !y || !rows || !kv_width || !head_width || mode > 1 || kv_width > UINT32_MAX / 8 ||
        head_offset > kv_width || head_width > kv_width - head_offset ||
        (size_t)rows > SIZE_MAX / (2 * sizeof(float)) / kv_width) return AK_INVALID;

    uint32_t outputs = mode ? head_width : rows;
    uint32_t inputs = mode ? rows : head_width;
    if (!ctx) {
        const float *block = (const float *)(uintptr_t)page + head_offset + (mode ? kv_width : 0);
        for (uint32_t out = 0; out < outputs; ++out) {
            float sum = 0;
            for (uint32_t in = 0; in < inputs; ++in) {
                size_t row = mode ? in : out;
                size_t col = mode ? out : in;
                sum += block[row * 2 * kv_width + col] * x[in];
            }
            y[out] = sum;
        }
        return AK_OK;
    }

    if (!within(&ctx->device_blocks, page, (size_t)rows * 2 * kv_width * sizeof(float))) return AK_INVALID;
    int status = enter(ctx);
    if (status != AK_OK) return status;
    size_t in_bytes = (size_t)inputs * sizeof(float), out_bytes = (size_t)outputs * sizeof(float);
    status = scratch(ctx, in_bytes + out_bytes);
    if (!status) {
        uint64_t dx = ctx->scratch_device, dy = dx + in_bytes;
        char *host_y = (char *)ctx->scratch_host + in_bytes;
        void *args[] = { &page, &dx, &dy, &rows, &kv_width, &head_offset, &head_width, &mode };
        memcpy(ctx->scratch_host, x, in_bytes);
        int code = ctx->driver.htod(dx, ctx->scratch_host, in_bytes, ctx->stream);
        if (!code) code = ctx->driver.launch(ctx->attention, (outputs - 1) / 128 + 1, 1, 1,
            128, 1, 1, 0, ctx->stream, args, NULL);
        if (!code) code = ctx->driver.dtoh(host_y, dy, out_bytes, ctx->stream);
        status = finish_stream(ctx, code);
        if (!status) memcpy(y, host_y, out_bytes);
    }
    return leave(ctx, status);
}
