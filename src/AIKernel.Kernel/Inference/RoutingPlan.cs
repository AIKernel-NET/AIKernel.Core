namespace AIKernel.Kernel.Inference;

/// <summary>Deterministic sparse dispatch. Each score row is scratch owned by one request.</summary>
internal sealed class RoutingPlan
{
    private readonly long[] dispatches;
    internal int Active { get; }

    internal RoutingPlan(int resources, int active)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(active, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(active, resources);
        dispatches = new long[resources];
        Active = active;
    }

    internal float Select(float[] scores, Span<int> order)
    {
        if (scores.Length != dispatches.Length || order.Length != scores.Length)
            throw new ArgumentException("Routing dimensions do not match the graph.");
        for (int i = 0; i < order.Length; i++) order[i] = i;
        order.Sort((left, right) => Compare(scores, left, right));
        return NormalizeSelected(scores, order[..Active]);
    }

    private float NormalizeSelected(float[] scores, ReadOnlySpan<int> selected)
    {
        float peak = scores[selected[0]], total = 0;
        foreach (int id in selected)
        {
            scores[id] = MathF.Exp(scores[id] - peak);
            total += scores[id];
            Interlocked.Increment(ref dispatches[id]);
        }
        return total;
    }

    private static int Compare(float[] scores, int left, int right)
    {
        int rank = scores[right].CompareTo(scores[left]);
        return rank == 0 ? left.CompareTo(right) : rank;
    }

    internal long[] Snapshot()
    {
        var result = new long[dispatches.Length];
        for (int i = 0; i < result.Length; i++) result[i] = Interlocked.Read(ref dispatches[i]);
        return result;
    }
}
