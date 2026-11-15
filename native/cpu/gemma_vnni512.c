#include "cpu.h"
#include "half.h"
#include <immintrin.h>
static inline AK_TARGET("avx2,avx512f,avx512bw,avx512dq,avx512vnni") int sum_half(__m256i value) {
    __m128i pair=_mm_add_epi32(_mm256_castsi256_si128(value),_mm256_extracti128_si256(value,1));
    pair=_mm_hadd_epi32(pair,pair);pair=_mm_hadd_epi32(pair,pair);return _mm_cvtsi128_si32(pair);
}
static inline AK_TARGET("avx2,avx512f,avx512bw,avx512dq,avx512vnni") __m256i unpack(const uint8_t* p) {
    __m128i packed=_mm_loadu_si128((const __m128i*)(p+2)),mask=_mm_set1_epi8(15);
    return _mm256_set_m128i(_mm_and_si128(_mm_srli_epi16(packed,4),mask),_mm_and_si128(packed,mask));
}
AK_TARGET("avx2,avx512f,avx512bw,avx512dq,avx512vnni")
void ak_gemma_vnni512(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,float* y) {
    uint32_t blocks=columns/32;
    for(uint32_t row=0;row<rows;row+=2) {
        double totals[2]={0,0};
        for(uint32_t block=0;block<blocks;block++) {
            const uint8_t* p0=w+((size_t)row*blocks+block)*18;
            const uint8_t* p1=row+1<rows?p0+(size_t)blocks*18:p0;
            __m512i weights=_mm512_inserti64x4(_mm512_castsi256_si512(unpack(p0)),unpack(p1),1);
            double values[2]={0,0};
            for(uint32_t pass=0;pass<3;pass++) {
                __m256i input=_mm256_loadu_si256((const __m256i*)(x+(size_t)pass*columns+block*32));
                __m512i dot=_mm512_dpbusd_epi32(_mm512_setzero_si512(),weights,_mm512_broadcast_i64x4(input));
                int correction=8*sums[pass*blocks+block];float scale=scales[pass*blocks+block];
                values[0]+=(double)(sum_half(_mm512_castsi512_si256(dot))-correction)*scale;
                values[1]+=(double)(sum_half(_mm512_extracti64x4_epi64(dot,1))-correction)*scale;
            }
            totals[0]+=values[0]*ak_half(p0);totals[1]+=values[1]*ak_half(p1);
        }
        y[row]=(float)totals[0];if(row+1<rows)y[row+1]=(float)totals[1];
    }
}
