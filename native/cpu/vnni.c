#include "cpu.h"
void ak_vnni_rows(const uint8_t* w,const int8_t* x,const float* scales,const int32_t* sums,uint32_t rows,uint32_t columns,uint32_t backend,float* y)
{
    if(backend==1)ak_vnni256_rows(w,x,scales,sums,rows,columns,y);
    else ak_vnni512_rows(w,x,scales,sums,rows,columns,y);
}
