namespace AIKernel.Kernel.Inference;

/// <summary>Read-only page observations for host-paged CPU sessions.</summary>
public static class WeightPaging
{
    /// <summary>Returns a snapshot of the paged dense session's counters. Mapped sessions return null.</summary>
    public static WeightPrefetchStatistics? GetWeightPrefetchStatistics(this DenseModelSession session) =>
        Pages(session.file)?.PrefetchStatistics;

    /// <summary>Returns a snapshot of the paged expert session's counters. Mapped sessions return null.</summary>
    public static WeightPrefetchStatistics? GetWeightPrefetchStatistics(this MixtureModelSession session) =>
        session.pages?.PrefetchStatistics;

    internal static TensorPageStore? Pages(IDenseModelWeights weights) => weights switch
    {
        GgufDenseWeights gguf => gguf._pages,
        SnapshotWeights snapshot => snapshot.store.pages,
        _ => null
    };

    internal static void EndExperts(GgufPagedWeights? pages) => pages?.Hints?.EndRequest();

    internal static DenseLayer Layer(IDenseModelWeights weights, int index, CancellationToken token)
    {
        Pages(weights)?.Hints?.Dense(weights.Layers, index, token);
        return weights.Layers[index];
    }

    internal static T WithCleanup<T>(DenseModelSession session, Func<T> work, Action fault)
    {
        var device = session.gpu;
        try { return device is null ? work() : InferenceRequestCleanup.Execute(work, device.ZeroScratch, fault); }
        finally { Pages(session.file)?.Hints?.EndRequest(); }
    }
}
