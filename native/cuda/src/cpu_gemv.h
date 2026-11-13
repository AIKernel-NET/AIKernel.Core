#ifndef AIKERNEL_CPU_GEMV_H
#define AIKERNEL_CPU_GEMV_H
#include <stdint.h>
int ak_cpu_gemv(const float *matrix, const float *x, float *y, uint32_t rows, uint32_t cols);
float ak_dot_avx(const float *a, const float *b, uint32_t n);
float ak_dot_avx2(const float *a, const float *b, uint32_t n);
float ak_dot_avx512(const float *a, const float *b, uint32_t n);
#endif
