// Same encoded GEMV as the previous matrix-only path; no extra activation quantization.
#include "gemma_gemv.cu"

extern "C" __global__ void gemma_embedding(const unsigned char* w,float* y,unsigned n,unsigned type,unsigned token,float scale) {
    unsigned k=blockIdx.x*blockDim.x+threadIdx.x;if(k>=n)return;
    size_t stride=type==2?(size_t)n/32*18:type==14?(size_t)n/256*210:(size_t)n*2;
    w+=(size_t)token*stride;float value;
    if(type==2) {
        const unsigned char* b=w+(k/32)*18;unsigned j=k%32;
        value=half_value(*(const unsigned short*)b)*(((b[2+j%16]>>(j/16*4))&15)-8);
    } else if(type==14) {
        const unsigned char* b=w+(k/256)*210;unsigned i=k%256,h=i/128,q=(i%128)/32,l=i%32;
        int low=(b[h*64+(q%2)*32+l]>>(q/2*4))&15,high=(b[128+h*32+l]>>(q*2))&3;
        value=(half_value(*(const unsigned short*)(b+208))*(int)((const signed char*)b)[192+i/16])*((low|(high<<4))-32);
    } else value=half_value(((const unsigned short*)w)[k]);
    y[k]=value*scale;
}

extern "C" __global__ void gemma_norm(const float* x,const float* weights,float* y,unsigned n) {
    unsigned group=blockIdx.x;x+=(size_t)group*n;y+=(size_t)group*n;
    __shared__ float factor;
    // Preserve the reference's accumulation order before moving pointwise work to the GPU.
    if(threadIdx.x==0){float sum=0;for(unsigned i=0;i<n;i++)sum+=x[i]*x[i];factor=powf(sum/n+1e-6f,-.5f);}
    __syncthreads();
    for(unsigned i=threadIdx.x;i<n;i+=blockDim.x)y[i]=(x[i]*factor)*(weights?weights[i]:1.f);
}

extern "C" __global__ void gemma_rope(float* x,const float* table,unsigned n,unsigned groups,unsigned position) {
    unsigned i=blockIdx.x*blockDim.x+threadIdx.x,half=n/2;if(i>=groups*half)return;
    unsigned group=i/half,j=i%half;float* v=x+(size_t)group*n;
    const float* t=table+(size_t)position*n;
    float a=v[j],b=v[j+half];v[j]=a*t[j]-b*t[j+half];v[j+half]=a*t[j+half]+b*t[j];
}

extern "C" __global__ void gemma_kv(const float* key,const float* value,float* keys,float* values,unsigned n,unsigned capacity,unsigned position) {
    unsigned i=blockIdx.x*blockDim.x+threadIdx.x;if(i>=n)return;
    size_t offset=(size_t)(position%capacity)*n;keys[offset+i]=key[i];values[offset+i]=value[i];
}

extern "C" __global__ void gemma_attention(const float* query,const float* keys,const float* values,float* output,unsigned n,unsigned capacity,unsigned position) {
    unsigned h=blockIdx.x,t=threadIdx.x,count=position+1<capacity?position+1:capacity,start=position+1-count;
    query+=(size_t)h*n;output+=(size_t)h*n;
    __shared__ float scores[2048];
    __shared__ float total;
    for(unsigned i=t;i<count;i+=blockDim.x){float sum=0;const float* k=keys+(size_t)((start+i)%capacity)*n;for(unsigned j=0;j<n;j++)sum+=query[j]*k[j];scores[i]=sum;}
    __syncthreads();
    if(t==0){float maximum=-__int_as_float(0x7f800000);for(unsigned i=0;i<count;i++)maximum=fmaxf(maximum,scores[i]);float sum=0;for(unsigned i=0;i<count;i++){scores[i]=expf(scores[i]-maximum);sum+=scores[i];}total=sum;}
    __syncthreads();
    for(unsigned j=t;j<n;j+=blockDim.x){float sum=0;for(unsigned i=0;i<count;i++)sum+=(scores[i]/total)*values[(size_t)((start+i)%capacity)*n+j];output[j]=sum;}
}

extern "C" __global__ void gemma_point(const float* a,const float* b,float* y,unsigned n,unsigned mode,float scale) {
    unsigned i=blockIdx.x*blockDim.x+threadIdx.x;if(i>=n)return;
    float x=a[i];
    if(mode==1)y[i]=x+b[i];
    else if(mode==2)y[i]=(.5f*x*(1.f+tanhf(.7978845608028654f*(x+.044715f*x*x*x))))*b[i];
    else if(mode==3)y[i]=x*scale;
    else if(mode==4)y[i]=30.f*tanhf(x/30.f);
    else if(mode==5)y[i]=(x+b[i])*scale;
}

extern "C" __global__ void gemma_argmax(const float* logits,unsigned* result,unsigned n) {
    __shared__ float maxima[256];__shared__ unsigned indices[256],invalid[256];
    unsigned t=threadIdx.x,best=t,bad=0;float maximum=-__int_as_float(0x7f800000);
    for(unsigned i=t;i<n;i+=256){float v=logits[i];if(!isfinite(v))bad=1;if(v>maximum){maximum=v;best=i;}}
    maxima[t]=maximum;indices[t]=best;invalid[t]=bad;__syncthreads();
    for(unsigned stride=128;stride;stride/=2){if(t<stride){float v=maxima[t+stride];unsigned index=indices[t+stride];if(v>maxima[t]||(v==maxima[t]&&index<indices[t])){maxima[t]=v;indices[t]=index;}invalid[t]|=invalid[t+stride];}__syncthreads();}
    if(t==0){result[0]=indices[0];result[1]=invalid[0];}
}
