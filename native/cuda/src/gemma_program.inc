/* Included after the private context/allocation helpers. No changes to Bonsai kernels. */
static int gemma_leave(ak_context* ctx,int status) {
    int result=leave(ctx,status);
    if(result)ctx->fault=result; /* Partial graphs must never be reused after a driver failure. */
    return result;
}
static size_t gemma_row_bytes(uint32_t n,uint32_t type) {
    if(!n||n>12288)return 0;
    if(type==1&&n%32==0)return (size_t)n*2;
    if(type==2&&n%32==0)return (size_t)n/32*18;
    if(type==14&&n%256==0)return (size_t)n/256*210;
    return 0;
}
static int gemma_range(ak_context* ctx,uint64_t address,size_t floats) {
    return address%4==0&&floats>0&&floats<=SIZE_MAX/4&&within(&ctx->device_blocks,address,floats*4);
}
static int gemma_command_valid(ak_context* ctx,const ak_gemma_command* p,uint32_t context,uint32_t trace) {
    if(p->reserved||p->padding||p->flags>3||!isfinite(p->scale))return 0;
    size_t row=0;
    switch(p->op) {
    case 1:
        row=gemma_row_bytes(p->n,p->type);
        return row&&p->m==262144&&p->a%2==0&&within(&ctx->device_blocks,p->a,row*p->m)&&gemma_range(ctx,p->b,p->n);
    case 2:
        row=gemma_row_bytes(p->m,p->type);
        return row&&p->n>0&&p->n<=262144&&p->a%2==0&&within(&ctx->device_blocks,p->a,row*p->n)&&gemma_range(ctx,p->b,p->m)&&gemma_range(ctx,p->c,p->n);
    case 3:
        return p->n>0&&p->n<=12288&&p->m>0&&p->m<=35&&gemma_range(ctx,p->a,(size_t)p->n*p->m)&&(!p->b||gemma_range(ctx,p->b,p->n))&&gemma_range(ctx,p->c,(size_t)p->n*p->m);
    case 4:
        return (p->n==256||p->n==512)&&p->m>0&&p->m<=8&&gemma_range(ctx,p->a,(size_t)p->n*p->m)&&gemma_range(ctx,p->b,(size_t)context*p->n);
    case 5:
        return (p->n==256||p->n==512)&&p->m>0&&p->m<=context&&gemma_range(ctx,p->a,p->n)&&gemma_range(ctx,p->b,p->n)&&gemma_range(ctx,p->c,(size_t)p->n*p->m)&&gemma_range(ctx,p->d,(size_t)p->n*p->m);
    case 6:
        return (p->n==256||p->n==512)&&p->m>0&&p->m<=context&&p->k==8&&gemma_range(ctx,p->a,(size_t)p->n*p->k)&&gemma_range(ctx,p->b,(size_t)p->n*p->m)&&gemma_range(ctx,p->c,(size_t)p->n*p->m)&&gemma_range(ctx,p->d,(size_t)p->n*p->k);
    case 7:
        return p->n>0&&p->n<=262144&&p->type>=1&&p->type<=5&&gemma_range(ctx,p->a,p->n)&&gemma_range(ctx,p->c,p->n)&&((p->type==3||p->type==4)||gemma_range(ctx,p->b,p->n));
    case 8:
        return p->n==262144&&gemma_range(ctx,p->a,p->n)&&gemma_range(ctx,p->b,2);
    case 9:
        return (p->flags&2)&&p->offset<=trace&&p->n<=trace-p->offset&&gemma_range(ctx,p->a,p->n);
    default:return 0;
    }
}
int32_t ak_gemma_configure(ak_context* ctx,const ak_gemma_command* commands,uint32_t count,uint32_t context,
    uint64_t work,size_t work_bytes,uint64_t logits,uint64_t result,uint32_t trace_floats) {
    if(!ctx||ctx->gemma_commands||!commands||!count||count>4096||context<64||context>2048||trace_floats>4000000||
       !work_bytes||!within(&ctx->device_blocks,work,work_bytes)||!gemma_range(ctx,logits,262144)||!gemma_range(ctx,result,2))return AK_INVALID;
    for(uint32_t i=0;i<count;i++)if(!gemma_command_valid(ctx,commands+i,context,trace_floats))return AK_INVALID;
    ak_gemma_command* copy=malloc((size_t)count*sizeof(*copy));if(!copy)return AK_OOM;
    memcpy(copy,commands,(size_t)count*sizeof(*copy));
    int status=enter(ctx);if(status){free(copy);return status;}
    void* module=NULL;
    const char* names[]={"gemma_embedding","gemma_gemv","gemma_norm","gemma_rope","gemma_kv","gemma_attention","gemma_point","gemma_argmax"};
    int code=ctx->driver.module_load(&module,ak_gemma_forward_ptx);
    for(int i=0;i<8&&!code;i++)code=ctx->driver.module_func(&ctx->gemma_functions[i],module,names[i]);
    status=ak_status(code);
    if(!status) {
        ctx->gemma_commands=copy;copy=NULL;ctx->gemma_command_count=count;ctx->gemma_context=context;ctx->gemma_trace_floats=trace_floats;
        ctx->gemma_work=work;ctx->gemma_work_bytes=work_bytes;ctx->gemma_logits=logits;ctx->gemma_result=result;
        status=finish_stream(ctx,ctx->driver.memset8(work,0,work_bytes,ctx->stream));
    }
    free(copy);return gemma_leave(ctx,status);
}
int32_t ak_gemma_reset(ak_context* ctx) {
    if(!ctx||!ctx->gemma_commands)return AK_INVALID;
    int status=enter(ctx);if(status)return status;
    status=finish_stream(ctx,ctx->driver.memset8(ctx->gemma_work,0,ctx->gemma_work_bytes,ctx->stream));
    int scratch_status=wipe_scratch_current(ctx);if(!status)status=scratch_status;
    if(!status)ctx->gemma_position=0;
    return gemma_leave(ctx,status);
}
int32_t ak_gemma_read_stats(ak_context* ctx,ak_gemma_forward_stats* out) {
    if(!ctx||!out||!ctx->gemma_commands)return AK_INVALID;
    *out=ctx->gemma_stats;return AK_OK;
}
int32_t ak_gemma_forward(ak_context* ctx,uint32_t token,uint32_t position,uint32_t logits_enabled,float* logits,float* trace,uint32_t* next) {
    if(!ctx||!ctx->gemma_commands||!next||token>=262144||position!=ctx->gemma_position||position>=ctx->gemma_context||logits_enabled>1||(!logits_enabled&&logits))return AK_INVALID;
    *next=0;
    int status=enter(ctx);if(status)return status;
    size_t logit_bytes=logits?262144u*4u:0,trace_bytes=trace?(size_t)ctx->gemma_trace_floats*4:0;
    status=scratch(ctx,8+logit_bytes+trace_bytes);
    /* Conditional traces (e.g. skipped logits during prefill) must not expose old staging bytes. */
    if(!status&&trace_bytes)memset((char*)ctx->scratch_host+8+logit_bytes,0,trace_bytes);
    int code=0;uint64_t downloaded=0;
    for(uint32_t i=0;i<ctx->gemma_command_count&&!status&&!code;i++) {
        ak_gemma_command* p=ctx->gemma_commands+i;
        if((p->flags&1)&&!logits_enabled)continue;
        if((p->flags&2)&&!trace)continue;
        void* args[8]={0};unsigned grid=1,block=256;
        args[0]=&p->a;args[1]=&p->b;
        switch(p->op) {
        case 1:args[2]=&p->n;args[3]=&p->type;args[4]=&token;args[5]=&p->scale;grid=(p->n+255)/256;break;
        case 2:args[2]=&p->c;args[3]=&p->m;args[4]=&p->n;args[5]=&p->type;grid=(p->n+3)/4;block=128;break;
        case 3:args[2]=&p->c;args[3]=&p->n;grid=p->m;break;
        case 4:args[2]=&p->n;args[3]=&p->m;args[4]=&position;grid=(p->n/2*p->m+255)/256;break;
        case 5:args[2]=&p->c;args[3]=&p->d;args[4]=&p->n;args[5]=&p->m;args[6]=&position;grid=(p->n+255)/256;break;
        case 6:args[2]=&p->c;args[3]=&p->d;args[4]=&p->n;args[5]=&p->m;args[6]=&position;grid=p->k;break;
        case 7:args[2]=&p->c;args[3]=&p->n;args[4]=&p->type;args[5]=&p->scale;grid=(p->n+255)/256;break;
        case 8:args[2]=&p->n;break;
        case 9:
            code=ctx->driver.dtoh((char*)ctx->scratch_host+8+logit_bytes+(size_t)p->offset*4,p->a,(size_t)p->n*4,ctx->stream);
            downloaded+=(uint64_t)p->n*4;continue;
        default:status=AK_INVALID;continue;
        }
        code=ctx->driver.launch(ctx->gemma_functions[p->op-1],grid,1,1,block,1,1,0,ctx->stream,args,NULL);
    }
    if(!status) {
        if(!code&&logits_enabled){code=ctx->driver.dtoh(ctx->scratch_host,ctx->gemma_result,8,ctx->stream);downloaded+=8;}
        if(!code&&logits){code=ctx->driver.dtoh((char*)ctx->scratch_host+8,ctx->gemma_logits,logit_bytes,ctx->stream);downloaded+=logit_bytes;}
        status=finish_stream(ctx,code);ctx->gemma_stats.calls++;ctx->gemma_stats.synchronizations++;ctx->gemma_stats.download_bytes+=downloaded;
        if(!status&&logits_enabled) {
            uint32_t pair[2];memcpy(pair,ctx->scratch_host,8);
            if(pair[1]||pair[0]>=262144)status=AK_INVALID;else *next=pair[0];
        }
        if(!status) {
            if(logits)memcpy(logits,(char*)ctx->scratch_host+8,logit_bytes);
            if(trace)memcpy(trace,(char*)ctx->scratch_host+8+logit_bytes,trace_bytes);
            ctx->gemma_position++;
        }
    }
    return gemma_leave(ctx,status);
}
