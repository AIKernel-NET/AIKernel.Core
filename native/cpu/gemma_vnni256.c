#include "cpu.h"
#include "half.h"
#include <immintrin.h>
static inline AK_TARGET("avx2,avxvnni") int sum256(__m256i value) {
    __m128i pair=_mm_add_epi32(_mm256_castsi256_si128(value),_mm256_extracti128_si256(value,1));
    pair=_mm_hadd_epi32(pair,pair);pair=_mm_hadd_epi32(pair,pair);return _mm_cvtsi128_si32(pair);
}
AK_TARGET("avx2,avxvnni")
void ak_gemma_vnni256(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,float* y) {
    uint32_t blocks=columns/32;const __m128i mask=_mm_set1_epi8(15);
    for(uint32_t row=0;row<rows;row++) {
        double total=0;
        for(uint32_t block=0;block<blocks;block++) {
            const uint8_t* p=w+((size_t)row*blocks+block)*18;
            __m128i packed=_mm_loadu_si128((const __m128i*)(p+2));
            __m256i weights=_mm256_set_m128i(_mm_and_si128(_mm_srli_epi16(packed,4),mask),_mm_and_si128(packed,mask));
            double value=0;
            for(uint32_t pass=0;pass<3;pass++) {
                __m256i input=_mm256_loadu_si256((const __m256i*)(x+(size_t)pass*columns+block*32));
#ifdef _MSC_VER
                __m256i dot=_mm256_dpbusd_avx_epi32(_mm256_setzero_si256(),weights,input);
#else
                __m256i dot=_mm256_dpbusd_epi32(_mm256_setzero_si256(),weights,input);
#endif
                value+=(double)(sum256(dot)-8*sums[pass*blocks+block])*scales[pass*blocks+block];
            }
            total+=value*ak_half(p);
        }
        y[row]=(float)total;
    }
}
