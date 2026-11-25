namespace AIKernel.Kernel.Inference;

/// <summary>Sliding-window decisions for immutable CPU pages. A promotion takes effect on normal demand.</summary>
internal sealed class ExpertRetention(ExpertFrequencyOptions policy, int pageSize)
{
    internal const int HistoryLimit = 4096;
    private readonly SortedDictionary<int, ExpertPlacementLayer> layers = new();
    private readonly Queue<ExpertPlacementDecision> decisions = new();
    private readonly Lock sync = new();
    private int retainedPages;
    private long sequence;

    internal ExpertPlacementSnapshot Snapshot()
    {
        lock (sync) return new(policy, sequence, retainedPages, decisions.ToArray());
    }

    internal void Observe(int layer, ReadOnlySpan<int> selected, ExpertPageRanges ranges, long capacity)
    {
        lock (sync)
        {
            int budget = Budget(capacity);
            if (!layers.TryGetValue(layer, out var state)) layers.Add(layer, state = new(new(policy.Window), ranges));
            var window = state.Window;
            window.Advance(selected);
            if (window.Visits < policy.Window) return;
            Demote(layer, window, ranges, budget);
            Admit(layer, window, selected, ranges, budget);
        }
    }

    private int Budget(long capacity) => (int)Math.Clamp(Math.Min(policy.MaximumBytes / pageSize,
        capacity / pageSize - 1), 0, int.MaxValue);

    internal void Limit(long capacity)
    {
        lock (sync)
        {
            int budget = Budget(capacity);
            if (retainedPages <= budget) return;
            foreach (var (layer, state) in layers)
                RelievePressure(layer, state, budget);
        }
    }

    private void RelievePressure(int layer, ExpertPlacementLayer state, int budget)
    {
        for (int expert = 0; expert < 32 && retainedPages > budget; expert++)
            if (state.Window.IsRetained(expert))
                Change(layer, expert, state.Window, state.Ranges, budget, retain: false, pressure: true);
    }

    private void Demote(int layer, ExpertSelectionWindow window, ExpertPageRanges ranges, int budget)
    {
        for (int expert = 0; expert < 32; expert++)
        {
            if (!window.IsRetained(expert)) continue;
            if (window.Age(expert) >= policy.MinimumResidency && window.Count(expert) <= policy.DemoteAt)
                Change(layer, expert, window, ranges, budget, retain: false, pressure: false);
        }
    }

    private void Admit(int layer, ExpertSelectionWindow window, ReadOnlySpan<int> selected, ExpertPageRanges ranges, int budget)
    {
        // Highest frequency wins even when only one slot remains; equal counts keep router rank.
        Span<int> ranked = stackalloc int[selected.Length];
        selected.CopyTo(ranked);
        window.Rank(ranked);
        foreach (int expert in ranked)
        {
            if (window.Count(expert) < policy.PromoteAt) break;
            if (!window.IsRetained(expert) && ranges.PageCount(expert) <= budget - retainedPages)
                Change(layer, expert, window, ranges, budget, retain: true, pressure: false);
        }
    }

    private void Change(int layer, int expert, ExpertSelectionWindow window, ExpertPageRanges ranges, int budget, bool retain, bool pressure)
    {
        int pages = ranges.SetRetention(expert, retain);
        retainedPages += retain ? pages : -pages;
        window.SetRetained(expert, retain);
        if (decisions.Count == HistoryLimit) decisions.Dequeue();
        var from = retain ? ExpertRamClass.Demand : ExpertRamClass.Retained;
        var to = retain ? ExpertRamClass.Retained : ExpertRamClass.Demand;
        decisions.Enqueue(new(++sequence, layer, expert, window.Visits, window.Count(expert),
            from, to, pages, budget, retainedPages, pressure));
    }
}

internal sealed record ExpertPlacementLayer(ExpertSelectionWindow Window, ExpertPageRanges Ranges);
