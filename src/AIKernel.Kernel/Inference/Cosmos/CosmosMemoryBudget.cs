using System.Diagnostics;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

// All calls originate on the serialized inference coordinator, outside page callbacks.
internal sealed class CosmosMemoryBudget : IDisposable
{
    internal CosmosMemoryOptions Options { get; }
    private readonly Process process=Process.GetCurrentProcess();
    private readonly Func<long> resident;
    private readonly long baseline;
    private ICosmosWeightPages? weights;
    private VirtualMemorySpace? kv;
    private int weightSize,kvSize,weightMaximum,kvMaximum;
    private long managed,peakManaged,observed,peak,adjustments,closedKvReads;
    private int checks;
    internal CosmosMemoryBudget(CosmosMemoryOptions options,Func<long>? resident=null)
    {
        options.Validate();
        Directory.CreateDirectory(options.SwapDirectory);
        Options=options;
        this.resident=resident??(()=>{process.Refresh();return process.WorkingSet64;});
        try{baseline=this.resident();Check();}catch{process.Dispose();throw;}
    }
    internal void RegisterWeights(ICosmosWeightPages value,int maximum)
    {if(weights is not null)throw new InvalidOperationException("A weight store is already registered.");weights=value;weightSize=CosmosPagedWeights.PageBytes;weightMaximum=maximum;Check();}
    internal void RegisterKv(VirtualMemorySpace value,int maximum)
    {if(kv is not null)throw new InvalidOperationException("A KV request is already registered.");kv=value;kvSize=value.BlockSize;kvMaximum=maximum;Check();}
    internal void ReleaseKv(VirtualMemorySpace value)
    {if(!ReferenceEquals(kv,value))return;closedKvReads+=value.GetStatistics().SwapReads;kv=null;kvSize=kvMaximum=0;}
    internal void PageBoundary(){if((checks++&15)==0)Check();}
    internal void Reserve(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        managed=checked(managed+bytes);peakManaged=Math.Max(peakManaged,managed);
        try{Check();}catch{managed-=bytes;throw;}
    }
    internal void Release(long bytes)=>managed-=bytes;
    internal void Check()
    {
        observed=resident();peak=Math.Max(peak,observed);
        long residentPages=(weights?.ResidentBytes??0)+(long)(kv?.GetStatistics().RamBlocks??0)*kvSize;
        // Count untouched managed allocations too. Native cache spill buffers and allocator lag stay in the reserve.
        long nonPageable=Math.Max(Math.Max(0,observed-residentPages),baseline+managed);
        long available=Options.MaximumResidentBytes-Options.RuntimeReserveBytes-Options.ProtectedResidentBytes-nonPageable;
        long minimum=(weights is null?0:weightSize)+(kv is null?0:kvSize);
        if(available<minimum)
        {
            weights?.Trim();kv?.TrimRam(0);
            throw new OutOfMemoryException("Cosmos non-pageable allocations exceed the resident budget; reduce input/context or raise the budget.");
        }
        int k=kv is null?0:(int)Math.Min(kvMaximum,Math.Max(1,(available-(weights is null?0:weightSize))/kvSize));
        long remainder=available-(long)k*kvSize;
        int w=weights is null?0:(int)Math.Min(weightMaximum,Math.Max(1,remainder/weightSize));
        // Shrink both before either can grow; no model data is changed.
        if (weights is not null && w < weights.Capacity) { weights.Resize(w); adjustments++; }
        Resize(kv,k,true);Resize(kv,k,false);
        if (weights is not null && w > weights.Capacity) { weights.Resize(w); adjustments++; }
    }
    private void Resize(VirtualMemorySpace? space,int count,bool shrink)
    {
        if(space is null)return;
        var stat=space.GetStatistics();int capacity=space.GetAvailableBlocks().RamBlocks+stat.RamBlocks;
        if(count==capacity || (count<capacity)!=shrink)return;
        space.SetRamCapacity(count);adjustments++;
    }
    internal CosmosMemoryStatistics Statistics
    {
        get
        {
            var k=kv?.GetStatistics()??default;
            return new(Options.MaximumResidentBytes,observed,peak,
                (long)(weights?.Capacity??0)*weightSize,weights?.ResidentBytes??0,
                (long)((kv?.GetAvailableBlocks().RamBlocks??0)+k.RamBlocks)*kvSize,(long)k.RamBlocks*kvSize,
                managed,peakManaged,adjustments,weights?.BackingReads??0,k.SwapReads+closedKvReads);
        }
    }
    public void Dispose()=>process.Dispose();
}
