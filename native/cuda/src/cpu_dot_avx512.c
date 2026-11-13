#include "cpu_gemv.h"
#include <immintrin.h>
#if defined(__GNUC__) || defined(__clang__)
__attribute__((target("avx512f,fma"), noinline))
#else
__declspec(noinline)
#endif
float ak_dot_avx512(const float *a, const float *b, uint32_t n) {
    __m512 sum = _mm512_setzero_ps();
    uint32_t i = 0;
    for (; n - i >= 16; i += 16)
        sum = _mm512_fmadd_ps(_mm512_loadu_ps(a + i), _mm512_loadu_ps(b + i), sum);
    float lanes[16];
    _mm512_storeu_ps(lanes, sum);
    float result = 0;
    for (int j = 0; j < 16; ++j) result += lanes[j];
    for (; i < n; ++i) result += a[i] * b[i];
    return result;
}
