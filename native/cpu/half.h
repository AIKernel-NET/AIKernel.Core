#pragma once
#include <stdint.h>
#include <string.h>
#include <math.h>
static inline float ak_half(const uint8_t* p)
{
    uint16_t h=(uint16_t)(p[0]|((uint16_t)p[1]<<8));
    uint32_t sign=(uint32_t)(h&0x8000)<<16,exponent=(h>>10)&31,mantissa=h&1023,bits;
    if(exponent==0)return (h&0x8000)?-ldexpf((float)mantissa,-24):ldexpf((float)mantissa,-24);
    bits=sign|(exponent==31?0x7f800000u:(exponent+112)<<23)|(mantissa<<13);
    float value;memcpy(&value,&bits,4);return value;
}
