#include "cpu.h"
#include "half.h"
#include <immintrin.h>
#include <string.h>

// Q1_0 stores bit 1 as +1, bit 0 as -1. Keep bits as unsigned 0/1 for
// VPDPBUSD, then apply 2*dot-sum(x). Input sums are computed once per block.
AK_TARGET("avx512f,avx512bw,avx512vnni")
void ak_vnni512_rows(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,float* y)
{
    uint32_t blocks=columns/128;
    const __m512i ones=_mm512_set1_epi8(1),zero=_mm512_setzero_si512();
    for(uint32_t first=0;first<rows;first+=4) {
        uint32_t count=rows-first<4?rows-first:4;
        float totals[4]={0,0,0,0};
        for(uint32_t block=0;block<blocks;block++) {
            __m512i x0=_mm512_loadu_si512(x+block*128),x1=_mm512_loadu_si512(x+block*128+64);
            for(uint32_t r=0;r<count;r++) {
                const uint8_t* p=w+((size_t)(first+r)*blocks+block)*18;
                uint64_t a,b;memcpy(&a,p+2,8);memcpy(&b,p+10,8);
                __m512i w0=_mm512_and_si512(_mm512_movm_epi8((__mmask64)a),ones);
                __m512i w1=_mm512_and_si512(_mm512_movm_epi8((__mmask64)b),ones);
                // Independent half-block accumulators, no cross-block scale mixing.
                __m512i d0=_mm512_dpbusd_epi32(zero,w0,x0),d1=_mm512_dpbusd_epi32(zero,w1,x1);
                int dot=_mm512_reduce_add_epi32(_mm512_add_epi32(d0,d1));
                totals[r]+=(float)(2*dot-sums[block])*(scales[block]*ak_half(p));
            }
        }
        for(uint32_t r=0;r<count;r++)y[first+r]=totals[r];
    }
}
