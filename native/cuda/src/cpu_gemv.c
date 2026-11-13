#include "aikernel_cuda.h"
#include "cpu_gemv.h"
#include <stdlib.h>
#include <string.h>

/* This translation unit is always compiled for baseline x86-64. */
#if defined(_MSC_VER)
#include <intrin.h>
#endif
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#endif

static float dot_scalar(const float *a, const float *b, uint32_t n) {
    float sum = 0;
    for (uint32_t i = 0; i < n; ++i) sum += a[i] * b[i];
    return sum;
}

int32_t ak_cpu_gemv_backend(void) {
    int avx = 0, avx2 = 0, avx512 = 0;
#if defined(_MSC_VER)
    int cpu[4];
    __cpuid(cpu, 0);
    int max_leaf = cpu[0];
    if (max_leaf >= 1) {
        __cpuidex(cpu, 1, 0);
        int fma = (cpu[2] & (1 << 12)) != 0;
        /* XGETBV itself is illegal without CPU XSAVE and OSXSAVE. */
        if ((cpu[2] & (7 << 26)) == (7 << 26)) {
            uint64_t xcr0 = _xgetbv(0);
            avx = (xcr0 & 6) == 6;
            if (avx && max_leaf >= 7) {
                __cpuidex(cpu, 7, 0);
                avx2 = fma && (cpu[1] & (1 << 5));
                /* MSVC /arch:AVX512 may emit F, DQ, CD, BW and VL. */
                avx512 = fma && ((uint32_t)cpu[1] & UINT32_C(0xd0030000)) == UINT32_C(0xd0030000)
                    && (xcr0 & 0xe6) == 0xe6;
            }
        }
    }
#elif (defined(__GNUC__) || defined(__clang__)) && defined(__x86_64__)
    /* GCC/Clang probes include OS XSAVE support. */
    avx = __builtin_cpu_supports("avx");
    avx2 = __builtin_cpu_supports("avx2") && __builtin_cpu_supports("fma");
    avx512 = __builtin_cpu_supports("avx512f") && __builtin_cpu_supports("fma");
#endif
    /* The override is process configuration for validation, never request input.
     * Forced unsupported paths fail without executing a single wide instruction. */
#ifdef _WIN32
    /* A statically linked CRT's getenv snapshot is separate from the host's. */
    char value[16];
    DWORD size = GetEnvironmentVariableA("AIKERNEL_CPU_GEMV", value, sizeof(value));
    if (size >= sizeof(value)) return AK_CPU_UNAVAILABLE;
    const char *requested = size ? value : NULL;
#else
    const char *requested = getenv("AIKERNEL_CPU_GEMV");
#endif
    if (requested && requested[0] && strcmp(requested, "auto")) {
        if (!strcmp(requested, "scalar")) return AK_CPU_SCALAR;
        if (!strcmp(requested, "avx")) return avx ? AK_CPU_AVX : AK_CPU_UNAVAILABLE;
        if (!strcmp(requested, "avx2")) return avx2 ? AK_CPU_AVX2 : AK_CPU_UNAVAILABLE;
        if (!strcmp(requested, "avx512")) return avx512 ? AK_CPU_AVX512 : AK_CPU_UNAVAILABLE;
        return AK_CPU_UNAVAILABLE;
    }
    return avx512 ? AK_CPU_AVX512 : avx2 ? AK_CPU_AVX2 : avx ? AK_CPU_AVX : AK_CPU_SCALAR;
}

int ak_cpu_gemv(const float *matrix, const float *x, float *y, uint32_t rows, uint32_t cols) {
    int backend = ak_cpu_gemv_backend();
    if (backend == AK_CPU_UNAVAILABLE) return AK_INVALID;
    float (*dot)(const float *, const float *, uint32_t) = dot_scalar;
    if (backend == AK_CPU_AVX) dot = ak_dot_avx;
    if (backend == AK_CPU_AVX2) dot = ak_dot_avx2;
    if (backend == AK_CPU_AVX512) dot = ak_dot_avx512;
    for (uint32_t row = 0; row < rows; ++row)
        y[row] = dot(matrix + (size_t)row * cols, x, cols);
    return AK_OK;
}
