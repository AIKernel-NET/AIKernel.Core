using System.Runtime.InteropServices;
using AIKernel.Core.Memory;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Request-private KV, allocated in 64-token blocks. Dirty pages use encrypted, zero-wiped swap.</summary>
internal sealed class CosmosPagedKv : IDisposable
{
    private const int TokensPerPage=64;
    private readonly VirtualMemorySpace space;
    private readonly CosmosMemoryBudget budget;
    private readonly int width,pagesPerLayer,context;
    private readonly int[] counts;
    private bool disposed;
    internal CosmosPagedKv(int layers,int width,int context,CosmosMemoryBudget budget)
    {
        this.width=width;this.context=context;this.budget=budget;counts=new int[layers];
        pagesPerLayer=(context+TokensPerPage-1)/TokensPerPage;
        int size=checked(TokensPerPage*width*sizeof(float));
        long blocks=checked((long)layers*2*pagesPerLayer);
        space=new(blocks,size,1,budget.Options.SwapDirectory,swapPolicy:new SwapEncryptionPolicy(true,true));
        try{budget.RegisterKv(space,checked((int)Math.Min(blocks,Math.Max(1,budget.Options.KvCacheBytes/size))));}
        catch{budget.ReleaseKv(space);space.Dispose();throw;}
    }
    internal void Append(int layer,int position,float[] key,float[] value)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if((uint)layer>=(uint)counts.Length||position!=counts[layer]||position>=context||key.Length!=width||value.Length!=width)
            throw new ArgumentException("Invalid paged KV append.");
        budget.PageBoundary();int page=position/TokensPerPage,offset=position%TokensPerPage*width*sizeof(float);
        space.WriteBlockSlice((long)layer*2*pagesPerLayer+page,offset,MemoryMarshal.AsBytes(key.AsSpan()));
        space.WriteBlockSlice(((long)layer*2+1)*pagesPerLayer+page,offset,MemoryMarshal.AsBytes(value.AsSpan()));counts[layer]++;
    }
    internal void ReadLayer(int layer,float[] keys,float[] values)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        int count=checked(counts[layer]*width);
        if(keys.Length<count||values.Length<count)throw new ArgumentException("KV destination too small.");
        for(int kind=0;kind<2;kind++)
        {
            float[] destination=kind==0?keys:values;
            for(int first=0,page=0;first<count;first+=TokensPerPage*width,page++)
            {
                budget.PageBoundary();int start=first,length=Math.Min(TokensPerPage*width,count-first);
                space.UseComputeBlock(((long)layer*2+kind)*pagesPerLayer+page,(bytes,_)=>
                    bytes[..(length*sizeof(float))].CopyTo(MemoryMarshal.AsBytes(destination.AsSpan(start,length))));
            }
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;try{budget.ReleaseKv(space);}finally{space.Dispose();Array.Clear(counts);}}
}
