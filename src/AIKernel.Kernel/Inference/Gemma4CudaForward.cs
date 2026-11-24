using System.Runtime.InteropServices;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>One private CUDA graph executor. Encoded weights, shared KV and intermediates remain on device.
/// Each forward submits the complete graph then synchronizes once. Diagnostic traces are opt-in.</summary>
internal sealed unsafe class Gemma4CudaForward : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Command
    {
        internal uint Op,N,M,K,Type,Flags,Offset,Reserved;
        internal ulong A,B,C,D;
        internal float Scale;
        internal uint Padding;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Statistics { public ulong Calls,Synchronizations,DownloadBytes; }
    private readonly CudaVirtualMemoryDevice device;
    private readonly Dictionary<long,ulong> addresses=new();
    private readonly List<(int Layer,string Stage,int Count,int Offset,bool Logits)> traces=[];
    private int traceFloats;
    private readonly long budget;
    internal long WeightBytes{get;}
    internal long ResidentBytes{get;private set;}
    internal Gemma4CudaForward(GgufWeights file,GemmaTextSession.Layer[] layers,GgufWeights.Tensor embedding,
        GgufWeights.Tensor pleEmbedding,GgufWeights.Tensor pleProjection,float[] finalNorm,float[] pleNorm,
        float[] localFrequencies,float[] fullFrequencies,int context,int ordinal,long budget)
    {
        this.budget=budget;
        var tensors=layers.SelectMany(l=>new[]{l.Q,l.K,l.V,l.O,l.Gate,l.Up,l.Down,l.PleGate,l.PleOutput})
            .OfType<GgufWeights.Tensor>().Append(embedding).Append(pleProjection).Append(pleEmbedding).DistinctBy(t=>t.Offset).ToArray();
        WeightBytes=tensors.Sum(t=>t.ByteLength);
        if(budget<WeightBytes)throw new ArgumentOutOfRangeException(nameof(budget),"Resident Gemma weights exceed the VRAM budget.");
        if(tensors.Any(t=>t.Type is not (1 or 2 or 14)))throw new NotSupportedException("Resident Gemma CUDA supports F16/Q4_0/Q6_K.");
        device=new(ordinal);
        try
        {
            foreach(var tensor in tensors)
            {
                ulong address=Allocate(tensor.ByteLength);addresses.Add(tensor.Offset,address);
                for(long offset=0;offset<tensor.ByteLength;offset+=1024*1024)
                {
                    int count=(int)Math.Min(1024*1024,tensor.ByteLength-offset);
                    NativeCompute.Check(NativeCompute.Upload(device.Context,address+(ulong)offset,file.BytesAt(tensor.Offset+offset,count),(nuint)count),"Gemma resident weight upload");
                }
            }
            ulong local=UploadRope(localFrequencies,context),full=UploadRope(fullFrequencies,context);
            // Offsets in one zeroable allocation, including logits and every shared KV owner.
            ulong cursor=0;
            ulong Reserve(int floats){ulong at=cursor;cursor=checked((cursor+(ulong)floats*4+63)&~63UL);return at;}
            ulong x=Reserve(1536),normal=Reserve(1536),temp=Reserve(1536),query=Reserve(4096),key=Reserve(512),value=Reserve(512),
                attention=Reserve(4096),gate=Reserve(12288),up=Reserve(12288),ple=Reserve(8960),projected=Reserve(8960),pleGate=Reserve(256),
                logits=Reserve(262144),result=Reserve(2);
            var keys=new ulong[15];var values=new ulong[15];var capacities=new int[15];
            for(int i=0;i<15;i++){capacities[i]=(i+1)%5==0?context:Math.Min(context,512);keys[i]=Reserve(capacities[i]*layers[i].HeadSize);values[i]=Reserve(capacities[i]*layers[i].HeadSize);}
            ulong work=Allocate(checked((long)cursor));
            x+=work;normal+=work;temp+=work;query+=work;key+=work;value+=work;attention+=work;gate+=work;up+=work;ple+=work;projected+=work;pleGate+=work;logits+=work;result+=work;
            for(int i=0;i<15;i++){keys[i]+=work;values[i]+=work;}
            var commands=new List<Command>();
            void Add(Command command)=>commands.Add(command);
            void Emb(GgufWeights.Tensor t,ulong output,float scale)=>Add(new(){Op=1,A=addresses[t.Offset],B=output,N=(uint)t.Columns,M=(uint)t.Rows,Type=(uint)t.Type,Scale=scale});
            void Mm(GgufWeights.Tensor t,ulong input,ulong output,uint flags=0)=>Add(new(){Op=2,A=addresses[t.Offset],B=input,C=output,N=(uint)t.Rows,M=(uint)t.Columns,Type=(uint)t.Type,Flags=flags});
            void Norm(ulong input,ulong output,float[]? weights,int n,int groups=1)=>Add(new(){Op=3,A=input,B=weights is null?0:Upload(weights),C=output,N=(uint)n,M=(uint)groups});
            void Rope(ulong input,ulong table,int head,int groups)=>Add(new(){Op=4,A=input,B=table,N=(uint)head,M=(uint)groups});
            void Point(ulong a,ulong b,ulong output,int n,uint mode,float scale=0,uint flags=0)=>Add(new(){Op=7,A=a,B=b,C=output,N=(uint)n,Type=mode,Scale=scale,Flags=flags});
            void Trace(int layer,string stage,ulong input,int count,bool final=false)
            {
                traces.Add((layer,stage,count,traceFloats,final));
                Add(new(){Op=9,A=input,N=(uint)count,Offset=(uint)traceFloats,Flags=final?3u:2u});traceFloats+=count;
            }
            Emb(embedding,x,MathF.Sqrt(1536));Trace(-1,"embedding",x,1536);
            Emb(pleEmbedding,ple,MathF.Sqrt(256));Mm(pleProjection,x,projected);Point(projected,0,projected,8960,3,1f/MathF.Sqrt(1536));
            Norm(projected,projected,pleNorm,256,35);Point(projected,ple,ple,8960,5,(float)Math.Pow(2,-.5));Trace(-1,"ple",ple,8960);
            for(int i=0;i<layers.Length;i++)
            {
                var l=layers[i];int head=l.HeadSize,owner=l.Owner;ulong table=head==256?local:full;
                Norm(x,normal,l.Norm,1536);Mm(l.Q,normal,query);Norm(query,query,l.QNorm,head,8);Rope(query,table,head,8);
                if(i<15)
                {
                    Mm(l.K!,normal,key);Mm(l.V!,normal,value);Norm(key,key,l.KNorm,head);Rope(key,table,head,1);Norm(value,value,null,head);
                    Add(new(){Op=5,A=key,B=value,C=keys[i],D=values[i],N=(uint)head,M=(uint)capacities[i]});
                }
                Add(new(){Op=6,A=query,B=keys[owner],C=values[owner],D=attention,N=(uint)head,M=(uint)capacities[owner],K=8});
                Mm(l.O,attention,temp);Norm(temp,temp,l.PostAttention,1536);Point(x,temp,x,1536,1);Trace(i,"attention",x,1536);
                Norm(x,normal,l.FfnNorm,1536);Mm(l.Gate,normal,gate);Mm(l.Up,normal,up);Point(gate,up,gate,l.Hidden,2);
                Mm(l.Down,gate,temp);Norm(temp,temp,l.PostFfn,1536);Point(x,temp,x,1536,1);Trace(i,"ffn",x,1536);
                Mm(l.PleGate,x,pleGate);Point(pleGate,ple+(ulong)i*256*4,pleGate,256,2);Mm(l.PleOutput,pleGate,temp);
                Norm(temp,temp,l.PostPle,1536);Point(x,temp,x,1536,1);Point(x,0,x,1536,3,l.Scale);Trace(i,"layer",x,1536);
            }
            Norm(x,normal,finalNorm,1536);Trace(-1,"final_norm",normal,1536);
            Mm(embedding,normal,logits,1);Point(logits,0,logits,262144,4,flags:1);Trace(-1,"logits",logits,262144,true);
            Add(new(){Op=8,A=logits,B=result,N=262144,Flags=1});
            var program=commands.ToArray();
            fixed(Command* p=program)NativeCompute.Check(NativeCompute.GemmaConfigure(device.Context,p,(uint)program.Length,(uint)context,work,(nuint)cursor,logits,result,(uint)traceFloats),"Gemma resident graph configuration");
        }
        catch{device.Dispose();throw;}
    }
    private ulong Allocate(long bytes)
    {
        if(bytes<1||bytes>budget-ResidentBytes)throw new OutOfMemoryException("Gemma resident VRAM budget exceeded (weights, tables and private state).");
        NativeCompute.Check(NativeCompute.DeviceAllocate(device.Context,(nuint)bytes,out ulong address),"Gemma resident allocation");ResidentBytes+=bytes;return address;
    }
    private ulong Upload(float[] data)
    {
        ulong address=Allocate((long)data.Length*4);
        fixed(float* p=data)NativeCompute.Check(NativeCompute.Upload(device.Context,address,(byte*)p,(nuint)data.Length*4),"Gemma constant upload");
        return address;
    }
    private ulong UploadRope(float[] frequencies,int context)
    {
        int n=frequencies.Length;var table=new float[context*n*2];
        for(int p=0;p<context;p++)for(int i=0;i<n;i++){float angle=p*frequencies[i];table[p*n*2+i]=MathF.Cos(angle);table[p*n*2+n+i]=MathF.Sin(angle);}
        return Upload(table);
    }
    internal int Forward(int token,int position,bool computeLogits,float[]? logits,Action<int,string,float[]>? trace)
    {
        float[]? data=trace is null?null:new float[traceFloats];
        try
        {
            uint next;
            fixed(float* l=logits,t=data)
                NativeCompute.Check(NativeCompute.GemmaForward(device.Context,(uint)token,(uint)position,computeLogits?1u:0u,l,t,&next),"Gemma resident forward");
            if(trace is not null)foreach(var entry in traces)
            {
                if(entry.Logits&&!computeLogits)continue;
                var values=data!.AsSpan(entry.Offset,entry.Count).ToArray();
                try{trace(entry.Layer,entry.Stage,values);}finally{Array.Clear(values);}
            }
            return (int)next;
        }
        finally{if(data is not null)Array.Clear(data);}
    }
    internal Statistics ReadStatistics(){NativeCompute.Check(NativeCompute.GemmaStatistics(device.Context,out var stats),"Gemma forward counters");return stats;}
    internal void Reset()=>NativeCompute.Check(NativeCompute.GemmaReset(device.Context),"Gemma resident state and staging wipe");
    public void Dispose()=>device.Dispose();
}
