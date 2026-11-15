#include "cpu.h"
#include <string.h>
#include <math.h>
#ifdef _WIN32
#include <windows.h>
#include <intrin.h>
static void cpuid(uint32_t leaf,uint32_t sub,uint32_t r[4]) { __cpuidex((int*)r,(int)leaf,(int)sub); }
static uint64_t xstate(void) { return _xgetbv(0); }
#else
#include <cpuid.h>
#include <unistd.h>
#include <sys/syscall.h>
static void cpuid(uint32_t leaf,uint32_t sub,uint32_t r[4]) { __cpuid_count(leaf,sub,r[0],r[1],r[2],r[3]); }
static uint64_t xstate(void) { uint32_t a,d;__asm__ volatile("xgetbv":"=a"(a),"=d"(d):"c"(0));return ((uint64_t)d<<32)|a; }
#endif
#define TILE_MASK ((UINT64_C(1)<<17)|(UINT64_C(1)<<18))
#ifdef _WIN32
#define AK_TLS __declspec(thread)
#else
#define AK_TLS _Thread_local
#endif
uint32_t ak_cpu_abi(void) { return 1; }
static int amx_os(int enable)
{
#ifdef _WIN32
    typedef DWORD64 (WINAPI *get_fn)(void);
    typedef BOOL (WINAPI *enable_fn)(DWORD64);
    HMODULE module=GetModuleHandleW(L"kernel32.dll");
    get_fn supported=NULL,thread=NULL;enable_fn request=NULL;
    FARPROC p=GetProcAddress(module,"GetEnabledXStateFeatures");memcpy(&supported,&p,sizeof(p));
    p=GetProcAddress(module,"GetThreadEnabledXStateFeatures");memcpy(&thread,&p,sizeof(p));
    p=GetProcAddress(module,"EnableProcessOptionalXStateFeatures");memcpy(&request,&p,sizeof(p));
    if(!supported||!thread||!request||(supported()&TILE_MASK)!=TILE_MASK)return 0;
    if(!enable)return 1;
    return request(TILE_MASK)&&(thread()&TILE_MASK)==TILE_MASK;
#else
    uint64_t supported=0,permitted=0;
    if(syscall(SYS_arch_prctl,0x1021,&supported)!=0||(supported&TILE_MASK)!=TILE_MASK)return 0;
    if(!enable)return 1;
    if(syscall(SYS_arch_prctl,0x1023,18)!=0)return 0;
    return syscall(SYS_arch_prctl,0x1022,&permitted)==0&&(permitted&TILE_MASK)==TILE_MASK;
#endif
}
uint32_t ak_cpu_supports(void)
{
    static AK_TLS uint32_t cached=UINT32_MAX;
    if(cached!=UINT32_MAX)return cached;
    uint32_t r[4],flags=0;cpuid(0,0,r);if(r[0]<7)return cached=0;
    cpuid(1,0,r);if((r[2]&(UINT32_C(1)<<27))==0||(xstate()&6)!=6)return cached=0;
    cpuid(7,0,r);uint32_t sub=r[0],b=r[1],c=r[2],d=r[3];
    if(b&(UINT32_C(1)<<5))flags|=8;
    if(sub>=1){cpuid(7,1,r);if((r[0]&(UINT32_C(1)<<4))&&(b&(UINT32_C(1)<<5)))flags|=1;}
    const uint32_t avx512_mask=(UINT32_C(1)<<16)|(UINT32_C(1)<<17)|(UINT32_C(1)<<30)|(UINT32_C(1)<<31);
    if((b&avx512_mask)==avx512_mask&&(c&(UINT32_C(1)<<11))&&(xstate()&0xe6)==0xe6)flags|=2;
    if((d&(UINT32_C(1)<<24))&&(d&(UINT32_C(1)<<25))&&(xstate()&TILE_MASK)==TILE_MASK&&amx_os(0))flags|=4;
    return cached=flags;
}
int ak_amx_enable(void)
{
    static AK_TLS int ready=0;
    if(!ready)ready=(ak_cpu_supports()&4)!=0&&amx_os(1);
    return ready;
}
int ak_cpu_q1_q8(const uint8_t* w,size_t bytes,const int8_t* x,const float* scales,const int32_t* sums,
    uint32_t rows,uint32_t columns,uint32_t backend,float* y)
{
    if(!w||!x||!scales||!sums||!y||!rows||!columns||columns%128||columns>200000||rows>200000||
       (uint64_t)rows*(columns/128)*18>bytes||backend<1||backend>4)return -1;
    uint32_t flag=1u<<(backend-1);
    if((ak_cpu_supports()&flag)==0)return -2;
    if(backend==3)
    {
        if(!ak_amx_enable())return -3;
        ak_amx_rows(w,x,scales,rows,columns,y);
    }
    else if(backend==4)ak_avx2int8_rows(w,x,scales,sums,rows,columns,y);
    else ak_vnni_rows(w,x,scales,sums,rows,columns,backend,y);
    return 0;
}
