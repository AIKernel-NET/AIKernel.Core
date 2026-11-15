// Build-time CUDA compiler only; the shipped library loads this PTX through the Driver API.
// nvcc -ptx -arch=compute_75 --fmad=false -O3 gemma_gemv.cu -o gemma_gemv.ptx
__device__ __forceinline__ float half_value(unsigned short h) {
    float value; asm("cvt.f32.f16 %0, %1;" : "=f"(value) : "h"(h)); return value;
}
extern "C" __global__ void gemma_gemv(const unsigned char* weights,const float* x,float* y,
                                    unsigned cols,unsigned rows,unsigned type) {
    unsigned row=blockIdx.x*4+threadIdx.x/32,lane=threadIdx.x%32;
    if(row>=rows)return;
    unsigned stride=type==2?cols/32*18:type==14?cols/256*210:cols*2;
    const unsigned char* w=weights+(size_t)row*stride;
    float sum=0;
    for(unsigned k=lane;k<cols;k+=32) {
        float v;
        if(type==2) {
            const unsigned char* b=w+(k/32)*18;unsigned j=k%32;
            int q=((b[2+j%16]>>(j/16*4))&15)-8;
            v=half_value(*(const unsigned short*)b)*q;
        } else if(type==14) {
            const unsigned char* b=w+(k/256)*210;unsigned i=k%256,h=i/128,q=(i%128)/32,l=i%32;
            int low=(b[h*64+(q%2)*32+l]>>(q/2*4))&15;
            int high=(b[128+h*32+l]>>(q*2))&3;
            v=(half_value(*(const unsigned short*)(b+208))*(int)((const signed char*)b)[192+i/16])*((low|(high<<4))-32);
        } else v=half_value(((const unsigned short*)w)[k]);
        sum+=v*x[k];
    }
    for(int offset=16;offset>0;offset/=2)sum+=__shfl_down_sync(0xffffffff,sum,offset);
    if(lane==0)y[row]=sum;
}
