using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Row-aligned immutable pages; the model is never mapped in this execution mode.</summary>
internal class TensorPageStore : IVirtualMemorySource,IDisposable
{
    internal const int PageBytes=4*1024*1024;
    internal readonly int pageSize;
    internal readonly WeightPageFile backing;
    internal WeightPageHints? Hints;
    internal long BackingBytesRead => backing.Counts.Bytes;
    internal long DistinctPagesRead => backing.Counts.Distinct;
    internal WeightPrefetchStatistics PrefetchStatistics => backing.Statistics;
    private sealed record Layout(int RowsPerPage,long FirstPage,int Pages);
    private readonly Dictionary<long,Layout> layouts=new();
    private readonly List<(long Offset,int Bytes)> pages=new();
    private readonly SafeFileHandle handle;
    private readonly bool ownsHandle;
    private readonly VirtualMemorySpace space;
    private readonly TensorPageBudget options;
    private readonly Process process=Process.GetCurrentProcess();
    private int accesses;
    private readonly long? addressSpaceLimit;
    private int cacheSlots;
    internal long CacheCapacityBytes => (long)cacheSlots*pageSize;
    internal TensorPageStore(string path,IEnumerable<ModelTensor> tensors,TensorPageBudget options,
        int pageSize=PageBytes, SafeFileHandle? backingHandle=null)
    {
        this.pageSize=pageSize;
        PagedWeightSetup.Validate(options, pageSize);
        this.options=options;
        foreach(var tensor in tensors.DistinctBy(t=>t.Backing))
        {
            if(tensor.RowBytes>pageSize)throw new NotSupportedException("encoded tensor exceeds virtual page shape.");
            int count=pageSize/tensor.RowBytes,n=(tensor.Rows+count-1)/count;
            layouts.Add(tensor.Backing,new(count,pages.Count,n));
            for(int row=0;row<tensor.Rows;row+=count)pages.Add((tensor.Backing+row*(long)tensor.RowBytes,Math.Min(count,tensor.Rows-row)*tensor.RowBytes));
        }
        cacheSlots=checked((int)Math.Min(pages.Count,options.WeightCacheBytes/pageSize));
        addressSpaceLimit=ProcessAddressSpace.ReadAddressSpaceLimit();
        ownsHandle=backingHandle is null;
        handle=backingHandle??OpenBacking(path);
        try
        {
            backing = new(handle, pages);
            space = new(pages.Count, pageSize, cacheSlots, options.SwapDirectory, source: this);
            CheckBudget();
            if (options.InitialWeightCacheBytes > 0)
            {
                long headroom = options.MaximumResidentBytes - options.RuntimeReserveBytes - 3L * pageSize - process.WorkingSet64;
                long initialBytes = Math.Min(options.InitialWeightCacheBytes, Math.Max(0, headroom));
                space.ReserveRam((int)Math.Min(cacheSlots, initialBytes / pageSize));
                CheckBudget();
            }
        }
        catch
        {
            try{space?.Dispose();}
            finally{if(ownsHandle)handle.Dispose();process.Dispose();}
            throw;
        }
    }
    public void ReadBlock(long index, Span<byte> destination) => backing.Demand(index, destination);

    internal long FirstColdPage(long offset)
    {
        long first = layouts[offset].FirstPage;
        return space.GetBlockTier(first) == VirtualMemoryTier.Ram ? -1 : first;
    }

    internal VirtualMemoryStatistics Statistics=>space.GetStatistics();
    private static SafeFileHandle OpenBacking(string path) =>
        File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileOptions.RandomAccess);

    internal ResourceResidency Observe(long backing)
    {
        var layout = layouts[backing];
        int resident = 0;
        for (int i = 0; i < layout.Pages; i++)
            if (space.GetBlockTier(layout.FirstPage + i) == VirtualMemoryTier.Ram) resident++;
        return new(layout.Pages, resident);
    }
    internal VirtualMemoryRamUsage RamUsage => space.GetRamUsage();
    internal void CheckBudget()
    {
        process.Refresh();
        long reserve=options.RuntimeReserveBytes+3L*pageSize;
        // RLIMIT_AS also counts the CLR's non-resident reservations. Keep room for them
        // before a native page allocation fails even though measured RSS is still low.
        AdjustCapacity(reserve);
        PagedWeightSetup.Restrict(Hints, process, options.MaximumResidentBytes - reserve, addressSpaceLimit, reserve);
        if(process.WorkingSet64<=options.MaximumResidentBytes-reserve)return;
        space.TrimRam(0);process.Refresh();
        if(process.WorkingSet64>options.MaximumResidentBytes-reserve)
            throw new OutOfMemoryException("Non-pageable process memory leaves insufficient headroom within the configured encoded RSS ceiling.");
    }
    private void AdjustCapacity(long reserve)
    {
        if(addressSpaceLimit is long limit)
        {
            long allocated = space.GetRamUsage().AllocatedBytes / pageSize;
            long headroom=limit-process.VirtualMemorySize64-reserve;
            int affordable=(int)Math.Clamp(allocated+headroom/pageSize,1,cacheSlots);
            if(affordable<cacheSlots)
            {
                space.SetRamCapacity(affordable);
                cacheSlots=affordable;
                process.Refresh();
            }
        }
    }
    private void CheckPage(){if((accesses++&15)==0)CheckBudget();}
    internal delegate void RowReader(ReadOnlySpan<byte> bytes);

    private readonly record struct Segment(long Page, int ByteOffset, int RowCount);

    private Segment Locate(ModelTensor tensor, int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)tensor.Rows);
        var layout = layouts[tensor.Backing];
        int row = checked(tensor.FirstRow + index);
        int within = row % layout.RowsPerPage;
        return new(layout.FirstPage + row / layout.RowsPerPage, within * tensor.RowBytes,
            Math.Min(layout.RowsPerPage - within, tensor.Rows - index));
    }

    internal void VisitRow(ModelTensor tensor, int row, RowReader read)
    {
        var segment = Locate(tensor, row);
        CheckPage();
        space.UseComputeBlock(segment.Page, (bytes, _) => read(bytes.Slice(segment.ByteOffset, tensor.RowBytes)));
    }

    internal void VisitRows(ModelTensor tensor, CancellationToken cancellation, Action<nint,int,int> visit)
    {
        for (int first = 0; first < tensor.Rows;)
        {
            cancellation.ThrowIfCancellationRequested();
            var segment = Locate(tensor, first);
            VisitSegment(segment, first, tensor.RowBytes, visit);
            first += segment.RowCount;
        }
    }

    private unsafe void VisitSegment(Segment segment, int first, int stride, Action<nint,int,int> visit)
    {
        CheckPage();
        space.UseComputeBlock(segment.Page, (bytes, _) =>
        {
            fixed (byte* pointer = bytes.Slice(segment.ByteOffset, segment.RowCount * stride))
                visit((nint)pointer, first, segment.RowCount);
        });
    }
    private void ReleaseStorage()
    {
        backing.Ahead?.Dispose();
        space.Dispose();
    }

    public void Dispose(){try{ReleaseStorage();}finally{if(ownsHandle)handle.Dispose();process.Dispose();}}
}
