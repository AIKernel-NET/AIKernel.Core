#include "cpu.h"
#include "half.h"
#include <immintrin.h>
#include <string.h>
// C: 16x1 int32, A: 16x64 int8, B: 64x1 int8 packed as 16 rows of four bytes.
// Two K=64 tiles produce each Q1_0 block's exact signed integer dot product.
struct tile_config { uint8_t palette,start,reserved[14];uint16_t columns[16];uint8_t rows[16]; };
_Static_assert(sizeof(struct tile_config)==64,"AMX tile configuration must be 64 bytes");
AK_TARGET("amx-tile,amx-int8")
void ak_amx_rows(const uint8_t* w,const int8_t* x,const float* scales,uint32_t rows,uint32_t columns,float* y)
{
    AK_ALIGN(64) struct tile_config config={0};
    config.palette=1;config.columns[0]=4;config.rows[0]=16;
    config.columns[1]=64;config.rows[1]=16;config.columns[2]=4;config.rows[2]=16;
    AK_ALIGN(64) int8_t a[16][64];AK_ALIGN(64) int32_t result[16];
    uint32_t blocks=columns/128;
    _tile_loadconfig(&config);
    for(uint32_t first=0;first<rows;first+=16)
    {
        uint32_t count=rows-first<16?rows-first:16;
        for(uint32_t r=0;r<count;r++)y[first+r]=0;
        for(uint32_t block=0;block<blocks;block++)
        {
            _tile_zero(0);
            for(uint32_t half=0;half<2;half++)
            {
                memset(a,0,sizeof(a));
                for(uint32_t r=0;r<count;r++)
                {
                    const uint8_t* bits=w+((size_t)(first+r)*blocks+block)*18+2;
                    for(uint32_t k=0;k<64;k++)a[r][k]=(int8_t)(((bits[half*8+k/8]>>(k%8))&1)?1:-1);
                }
                _tile_loadd(1,a,64);_tile_loadd(2,x+block*128+half*64,4);_tile_dpbssd(0,1,2);
            }
            _tile_stored(0,result,4);
            for(uint32_t r=0;r<count;r++)y[first+r]+=(float)result[r]*(scales[block]*ak_half(w+((size_t)(first+r)*blocks+block)*18));
        }
    }
    _tile_release();
    // Input-derived scratch never survives the call on the native stack.
    volatile int8_t* clear=(volatile int8_t*)a;for(size_t i=0;i<sizeof(a);i++)clear[i]=0;
    volatile int32_t* clear_result=result;for(int i=0;i<16;i++)clear_result[i]=0;
}
