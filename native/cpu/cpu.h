#pragma once
#include <stdint.h>
#include <stddef.h>
#ifdef _WIN32
#define AK_API __declspec(dllexport)
#define AK_ALIGN(n) __declspec(align(n))
#define AK_TARGET(x)
#else
#define AK_API __attribute__((visibility("default")))
#define AK_ALIGN(n) __attribute__((aligned(n)))
#define AK_TARGET(x) __attribute__((target(x)))
#endif
/* All functions return before an unsupported instruction is reached.
   bits 1/2/4: AVX-VNNI / AVX512-VNNI / AMX-INT8 with OS support.
   Q8 inputs are signed [-127,127], with one scale and sum per 128 elements. */
AK_API uint32_t ak_cpu_abi(void);
AK_API uint32_t ak_cpu_supports(void);
AK_API int ak_cpu_q1_q8(const uint8_t* w, size_t bytes, const int8_t* x,
    const float* scales, const int32_t* sums, uint32_t rows, uint32_t columns, uint32_t backend, float* y);
void ak_vnni256_rows(const uint8_t*,const int8_t*,const float*,const int32_t*,uint32_t,uint32_t,float*);
void ak_vnni512_rows(const uint8_t*,const int8_t*,const float*,const int32_t*,uint32_t,uint32_t,float*);
int ak_amx_enable(void);
void ak_vnni_rows(const uint8_t*,const int8_t*,const float*,const int32_t*,uint32_t,uint32_t,uint32_t,float*);
void ak_amx_rows(const uint8_t*,const int8_t*,const float*,uint32_t,uint32_t,float*);
