using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Row-aligned immutable pages; the model is never mapped in this execution mode.</summary>
internal class GgufPagedWeights : IVirtualMemorySource,IDisposable
{
    internal const int PageBytes=4*1024*1024;
    private readonly int pageSize;
    private readonly bool[] loaded;
    internal long BackingBytesRead { get; private set; }
    internal long DistinctPagesRead { get; private set; }
    private sealed record Layout(int RowsPerPage,long FirstPage,int Pages);
    private readonly Dictionary<long,Layout> layouts=new();
    private readonly List<(long Offset,int Bytes)> pages=new();
    private readonly SafeFileHandle handle;
    private readonly bool ownsHandle;
    private readonly VirtualMemorySpace space;
    private readonly GgufMemoryOptions options;
    private readonly Process process=Process.GetCurrentProcess();
    private int accesses;
    private readonly long? addressSpaceLimit;
    private int cacheSlots;
    internal long CacheCapacityBytes => (long)cacheSlots*pageSize;
    internal GgufPagedWeights(string path,IEnumerable<GgufWeights.Tensor> tensors,GgufMemoryOptions options,
        int pageSize=PageBytes, SafeFileHandle? backingHandle=null)
    {
        this.pageSize=pageSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        if(options.MaximumResidentBytes<256L*1024*1024||options.RuntimeReserveBytes<64L*1024*1024||
            options.WeightCacheBytes<pageSize||options.WeightCacheBytes>options.MaximumResidentBytes-options.RuntimeReserveBytes-3L*pageSize)
            throw new ArgumentOutOfRangeException(nameof(options),"The RAM cache must leave runtime and transient-buffer headroom.");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SwapDirectory);this.options=options;
        foreach(var tensor in tensors.DistinctBy(t=>t.Offset))
        {
            if(tensor.RowBytes>pageSize||tensor.Rank>2)throw new NotSupportedException("GGUF tensor exceeds virtual page shape.");
            int count=pageSize/tensor.RowBytes,n=(tensor.Rows+count-1)/count;
            layouts.Add(tensor.Offset,new(count,pages.Count,n));
            for(int row=0;row<tensor.Rows;row+=count)pages.Add((tensor.Offset+row*tensor.RowByteLength,Math.Min(count,tensor.Rows-row)*tensor.RowBytes));
        }
        loaded=new bool[pages.Count];
        cacheSlots=checked((int)Math.Min(pages.Count,options.WeightCacheBytes/pageSize));
        addressSpaceLimit=ReadAddressSpaceLimit();
        ownsHandle=backingHandle is null;
        handle=backingHandle??File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileOptions.RandomAccess);
        try{space=new(pages.Count,pageSize,cacheSlots,options.SwapDirectory,source:this);CheckBudget();}
        catch
        {
            try{space?.Dispose();}
            finally{if(ownsHandle)handle.Dispose();process.Dispose();}
            throw;
        }
    }
    public void ReadBlock(long index,Span<byte> destination)
    {
        var page=pages[checked((int)index)];var target=destination[..page.Bytes];long offset=page.Offset;
        while(!target.IsEmpty)
        {int count=RandomAccess.Read(handle,target,offset);if(count==0)throw new EndOfStreamException("Truncated GGUF backing file.");target=target[count..];offset+=count;}
        destination[page.Bytes..].Clear();
        BackingBytesRead+=page.Bytes;
        if(!loaded[(int)index]){loaded[(int)index]=true;DistinctPagesRead++;}
    }
    internal VirtualMemoryStatistics Statistics=>space.GetStatistics();
    private static long? ReadAddressSpaceLimit()
    {
        const string path="/proc/self/limits", label="Max address space";
        if(!OperatingSystem.IsLinux() || !File.Exists(path))return null;
        foreach(string line in File.ReadLines(path))
        {
            if(!line.StartsWith(label,StringComparison.Ordinal))continue;
            var value=line.AsSpan(label.Length).TrimStart();
            int end=value.IndexOf(' ');
            if(end>0 && long.TryParse(value[..end],out long limit))return limit;
        }
        return null;
    }
    internal void CheckBudget()
    {
        process.Refresh();
        long reserve=options.RuntimeReserveBytes+3L*pageSize;
        // RLIMIT_AS also counts the CLR's non-resident reservations. Keep room for them
        // before a native page allocation fails even though measured RSS is still low.
        if(addressSpaceLimit is long limit)
        {
            int resident=cacheSlots-space.GetAvailableBlocks().RamBlocks;
            long headroom=limit-process.VirtualMemorySize64-reserve;
            int affordable=(int)Math.Clamp(resident+headroom/pageSize,1,cacheSlots);
            if(affordable<cacheSlots)
            {
                space.SetRamCapacity(affordable);
                cacheSlots=affordable;
                process.Refresh();
            }
        }
        if(process.WorkingSet64<=options.MaximumResidentBytes-reserve)return;
        space.TrimRam(0);process.Refresh();
        if(process.WorkingSet64>options.MaximumResidentBytes-reserve)
            throw new OutOfMemoryException("Non-pageable process memory leaves insufficient headroom within the configured GGUF RSS ceiling.");
    }
    private void CheckPage(){if((accesses++&15)==0)CheckBudget();}
    internal void ReadRow(GgufWeights.Tensor tensor,int row,float[] destination)
    {
        if((uint)row>=(uint)tensor.Rows||destination.Length<tensor.Columns)throw new ArgumentOutOfRangeException(nameof(row));
        var layout=layouts[tensor.Offset];CheckPage();
        space.UseComputeBlock(layout.FirstPage+row/layout.RowsPerPage,(bytes,_)=>
            tensor.Encoding.Decode(bytes.Slice((row%layout.RowsPerPage)*tensor.RowBytes,tensor.RowBytes),destination.AsSpan(0,tensor.Columns)));
    }
    internal unsafe void VisitRows(GgufWeights.Tensor tensor, CancellationToken cancellation, Action<nint,int,int> visit)
    {
        var layout=layouts[tensor.Offset];
        for (int page=0; page<layout.Pages; page++)
        {
            cancellation.ThrowIfCancellationRequested();
            CheckPage();
            int first=page*layout.RowsPerPage, count=Math.Min(layout.RowsPerPage,tensor.Rows-first);
            space.UseComputeBlock(layout.FirstPage+page,(bytes,_) =>
            {
                fixed (byte* pointer=bytes) visit((nint)pointer,first,count);
            });
        }
    }
    public void Dispose(){try{space.Dispose();}finally{if(ownsHandle)handle.Dispose();process.Dispose();}}
}
