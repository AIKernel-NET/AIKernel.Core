#include "cpu_gemv.h"
#include <immintrin.h>
#if defined(__GNUC__) || defined(__clang__)
__attribute__((target("avx,no-avx2,no-fma"), noinline))
#else
__declspec(noinline)
#endif
float ak_dot_avx(const float *a, const float *b, uint32_t n) {
    __m256 sum = _mm256_setzero_ps();
    uint32_t i = 0;
    for (; n - i >= 8; i += 8)
        sum = _mm256_add_ps(sum, _mm256_mul_ps(_mm256_loadu_ps(a + i), _mm256_loadu_ps(b + i)));
    float lanes[8];
    _mm256_storeu_ps(lanes, sum);
    float result = 0;
    for (int j = 0; j < 8; ++j) result += lanes[j];
    for (; i < n; ++i) result += a[i] * b[i];
    return result;
}
