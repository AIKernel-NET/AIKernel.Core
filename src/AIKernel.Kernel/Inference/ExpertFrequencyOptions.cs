namespace AIKernel.Kernel.Inference;

/// <summary>Host RAM retention for immutable expert weights in the paged GPT-OSS CPU backend.</summary>
public sealed record ExpertFrequencyOptions
{
    /// <summary>Enables frequency-based retention within the existing weight cache.</summary>
    public bool Enabled { get; init; }

    /// <summary>Number of recent router invocations retained separately for each layer (2–256).</summary>
    public int Window { get; init; } = 8;

    /// <summary>Minimum selections in a complete window before promotion.</summary>
    public int PromoteAt { get; init; } = 3;

    /// <summary>At or below this count a retained expert is eligible for demotion.</summary>
    public int DemoteAt { get; init; } = 1;

    /// <summary>Minimum layer visits between promotion and frequency-based demotion.</summary>
    public int MinimumResidency { get; init; } = 4;

    /// <summary>Maximum bytes of the existing RAM cache assigned to retained expert pages.</summary>
    public long MaximumBytes { get; init; } = 512L << 20;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Window, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Window, 256);
        ArgumentOutOfRangeException.ThrowIfNegative(DemoteAt);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PromoteAt, DemoteAt);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PromoteAt, Window);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumResidency, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MinimumResidency, 4096);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumBytes, 1L << 20);
    }
}

/// <summary>The retention class for immutable pages. Both classes share the same physical RAM budget.</summary>
public enum ExpertRamClass { Demand, Retained }

/// <summary>A placement transition. Visit counts are per layer. Page counts cover Gate, Up and Down.</summary>
public sealed record ExpertPlacementDecision(long Sequence, int Layer, int Expert, long Visit, int Selections,
    ExpertRamClass From, ExpertRamClass To, int Pages, int BudgetPages, int UsedPages, bool BudgetPressure);

/// <summary>Snapshot of the most recent transitions, with sequence numbers to detect lost history.</summary>
public sealed record ExpertPlacementSnapshot(ExpertFrequencyOptions Policy, long TotalDecisions, int RetainedPages,
    IReadOnlyList<ExpertPlacementDecision> Decisions);

/// <summary>Read-only host diagnostics for frequency-based expert retention.</summary>
public static class ExpertPlacementDiagnostics
{
    /// <summary>Returns the most recent 4096 decisions. Reading never faults or touches weight pages.</summary>
    public static ExpertPlacementSnapshot? GetExpertPlacement(this MixtureModelSession model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return (model.pages as FrequencyPagedWeights)?.Placement.Snapshot();
    }
}
