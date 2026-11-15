#include "cpu.h"
#include "half.h"
#include "bit_lut.h"
#include <immintrin.h>

/* Unsigned weights are 0/1, not 0..255: each signed int16 pair sum is
   bounded by +/-254, so VPMADDUBSW cannot saturate. Bias is shared by rows. */
AK_TARGET("avx2")
void ak_avx2int8_rows(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,float* y)
{
    uint32_t blocks=columns/128;
    const __m256i ones=_mm256_set1_epi16(1);
    for(uint32_t row=0;row<rows;row++) {
        float total=0;
        for(uint32_t block=0;block<blocks;block++) {
            const uint8_t* p=w+((size_t)row*blocks+block)*18;
            __m256i dot=_mm256_setzero_si256();
            for(int i=0;i<16;i+=4) {
                __m256i weights=_mm256_set_epi64x((long long)ak_bit_bytes[p[5+i]],(long long)ak_bit_bytes[p[4+i]],
                    (long long)ak_bit_bytes[p[3+i]],(long long)ak_bit_bytes[p[2+i]]);
                __m256i pairs=_mm256_maddubs_epi16(weights,_mm256_loadu_si256((const __m256i*)(x+block*128+i*8)));
                dot=_mm256_add_epi32(dot,_mm256_madd_epi16(pairs,ones));
            }
            __m128i pair=_mm_add_epi32(_mm256_castsi256_si128(dot),_mm256_extracti128_si256(dot,1));
            pair=_mm_hadd_epi32(pair,pair);pair=_mm_hadd_epi32(pair,pair);
            total+=(float)(2*_mm_cvtsi128_si32(pair)-sums[block])*(scales[block]*ak_half(p));
        }
        y[row]=total;
    }
}
