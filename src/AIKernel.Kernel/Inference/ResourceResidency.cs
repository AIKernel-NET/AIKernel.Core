namespace AIKernel.Kernel.Inference;

/// <summary>Page counts within a single AIKernel-managed backing. Reading these counts does not fault in pages.</summary>
public readonly record struct ResourceResidency(int TotalPages, int ResidentPages)
{
    internal ResourceResidency Include(ResourceResidency next) =>
        new(TotalPages + next.TotalPages, ResidentPages + next.ResidentPages);
}
