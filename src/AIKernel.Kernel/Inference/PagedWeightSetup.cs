using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace AIKernel.Kernel.Inference;

internal static class PagedWeightSetup
{
    internal static void Validate(TensorPageBudget options, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        if(options.MaximumResidentBytes<256L*1024*1024||options.RuntimeReserveBytes<64L*1024*1024||
            options.WeightCacheBytes<pageSize||options.WeightCacheBytes>options.MaximumResidentBytes-options.RuntimeReserveBytes-3L*pageSize)
            throw new ArgumentOutOfRangeException(nameof(options),"The RAM cache must leave runtime and transient-buffer headroom.");
        ArgumentOutOfRangeException.ThrowIfNegative(options.InitialWeightCacheBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.InitialWeightCacheBytes, options.WeightCacheBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SwapDirectory);
    }

    internal static TensorPageStore Configure(this TensorPageStore store, GgufMemoryOptions memory)
    {
        try
        {
            store.Hints = Start(store.backing, memory, store.pageSize, store.FirstColdPage);
            store.CheckBudget();
            return store;
        }
        catch { store.Dispose(); throw; }
    }

    internal static TensorPageStore Open(string path, IEnumerable<ModelTensor> tensors, GgufMemoryOptions memory, SafeFileHandle handle)
        => new TensorPageStore(path, tensors, GgufDenseWeights.PageBudget(memory), backingHandle: handle).Configure(memory);

    internal static GgufPagedWeights OpenExperts(string path, IEnumerable<GgufWeights.Tensor> tensors, GgufMemoryOptions memory, SafeFileHandle handle)
    {
        var store = new GgufPagedWeights(path, tensors, memory, pageSize: 1 << 20, backingHandle: handle);
        store.Configure(memory);
        return store;
    }

    internal static WeightPageHints? Start(WeightPageFile file, GgufMemoryOptions memory, int pageSize, Func<long, long> coldPage)
    {
        var policy = memory.Prefetch ?? throw new ArgumentNullException(nameof(memory.Prefetch));
        long spare = memory.MaximumResidentBytes - memory.RuntimeReserveBytes - memory.WeightCacheBytes - 3L * pageSize;
        int slots = (int)Math.Clamp(Math.Min(spare, policy.MaximumBytes) / pageSize, 0, 64);
        if (!policy.Enabled || slots == 0) return null;
        file.Ahead = new PageReadAhead(slots, pageSize, file.Read);
        return new WeightPageHints(file.Ahead, policy.Depth, coldPage);
    }

    internal static void Restrict(WeightPageHints? hints, Process process, long rssLimit, long? addressLimit, long reserve)
    {
        if (hints == null) return;
        const long margin = 64L << 20;
        bool addressPressure = addressLimit.HasValue && addressLimit.Value - process.VirtualMemorySize64 < reserve + margin;
        hints.SetPressure(addressPressure || rssLimit - process.WorkingSet64 < margin);
    }
}
