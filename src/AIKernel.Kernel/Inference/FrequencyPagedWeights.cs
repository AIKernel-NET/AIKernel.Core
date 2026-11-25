using Microsoft.Win32.SafeHandles;

namespace AIKernel.Kernel.Inference;

/// <summary>Physical page ranges of each expert's three projections in one layer.</summary>
internal sealed class ExpertPageRanges
{
    private readonly (long First, int Count)[][] experts;
    private readonly Action<(long First, int Count), bool> update;

    internal ExpertPageRanges((long First, int Count)[][] experts, Action<(long First, int Count), bool> update)
        => (this.experts, this.update) = (experts, update);

    internal int PageCount(int expert) => experts[expert].Sum(range => range.Count);

    internal int SetRetention(int expert, bool retained)
    {
        foreach (var range in experts[expert]) update(range, retained);
        return PageCount(expert);
    }
}

/// <summary>Prefers pages of hot routed experts in the existing RAM tier of the tensor MMU.</summary>
internal sealed class FrequencyPagedWeights : GgufPagedWeights
{
    private readonly Dictionary<int, ExpertPageRanges> banks = new();
    private readonly HashSet<long> preferred = new();
    internal ExpertRetention Placement { get; }

    internal FrequencyPagedWeights(string path, IEnumerable<GgufWeights.Tensor> tensors, GgufMemoryOptions options, SafeFileHandle handle)
        : base(path, tensors, options, 1 << 20, handle)
    {
        Placement = new(options.ExpertFrequency, pageSize);
        ConnectRetention();
    }

    private void ConnectRetention()
    {
        space.PreferRamPages(preferred.Contains);
        CacheShrinking = Placement.Limit;
    }

    internal void Selected(int index, MixtureModelSession.Layer layer, ReadOnlySpan<int> selected)
    {
        if (!banks.TryGetValue(index, out var ranges)) banks.Add(index, ranges = Describe(layer));
        Placement.Observe(index, selected, ranges, CacheCapacityBytes);
    }

    private ExpertPageRanges Describe(MixtureModelSession.Layer layer)
    {
        var matrices = new[] { layer.Gate.Weight, layer.Up.Weight, layer.Down.Weight };
        var experts = new (long First, int Count)[matrices[0].Depth][];
        for (int i = 0; i < experts.Length; i++)
            experts[i] = matrices.Select(matrix => Range(matrix.MatrixSlice(i).Offset)).ToArray();
        return new(experts, Update);
    }

    private void Update((long First, int Count) range, bool retained)
    {
        for (long page = range.First; page < range.First + range.Count; page++)
        {
            if (retained) preferred.Add(page);
            else preferred.Remove(page);
        }
    }
}

internal static class ExpertPaging
{
    internal static void Selected(GgufPagedWeights? pages, int index, MixtureModelSession.Layer layer, ReadOnlySpan<int> active)
    {
        pages?.Hints?.Selected(index, active);
        if (pages is FrequencyPagedWeights mapped) mapped.Selected(index, layer, active);
    }

    internal static void Predict(GgufPagedWeights? pages, int index, MixtureModelSession.Layer layer, CancellationToken token)
    {
        var bank = layer.Gate.Weight;
        pages?.Hints?.Experts(index, bank.Offset, bank.RowByteLength * bank.Rows, token);
    }
}
