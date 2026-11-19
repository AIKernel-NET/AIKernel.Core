#include "cpu.h"
#include "half.h"
#include "bit_lut.h"
#include <immintrin.h>
static inline AK_TARGET("avx2,avxvnni") int dot256(const uint8_t* bits,const int8_t* x)
{
    __m256i total=_mm256_setzero_si256();
    for(int i=0;i<16;i+=4) {
        __m256i weights=_mm256_set_epi64x((long long)ak_bit_bytes[bits[i+3]],(long long)ak_bit_bytes[bits[i+2]],
                                         (long long)ak_bit_bytes[bits[i+1]],(long long)ak_bit_bytes[bits[i]]);
#ifdef _MSC_VER
        total=_mm256_dpbusd_avx_epi32(total,weights,_mm256_loadu_si256((const __m256i*)(x+i*8)));
#else
        total=_mm256_dpbusd_epi32(total,weights,_mm256_loadu_si256((const __m256i*)(x+i*8)));
#endif
    }
    __m128i pair=_mm_add_epi32(_mm256_castsi256_si128(total),_mm256_extracti128_si256(total,1));
    pair=_mm_hadd_epi32(pair,pair);pair=_mm_hadd_epi32(pair,pair);return _mm_cvtsi128_si32(pair);
}

AK_TARGET("avx2,avxvnni")
void ak_vnni256_rows(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,float* y)
{
    uint32_t blocks=columns/128;
    for(uint32_t row=0;row<rows;row++) {
        float total=0;
        for(uint32_t block=0;block<blocks;block++) {
            const uint8_t* p=w+((size_t)row*blocks+block)*18;
            int dot=dot256(p+2,x+block*128);
            total+=(float)(2*dot-sums[block])*(scales[block]*ak_half(p));
        }
        y[row]=total;
    }
}
