namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

public sealed class ContextAssemblyGovernanceException : ContextAssemblyException
{
    public ContextAssemblyGovernanceException(RomId romId, string? reason)
        : base($"Context assembly governance denied ROM. RomId='{romId.Value}'. Reason='{reason ?? "unspecified"}'.")
    {
        RomId = romId;
        Reason = reason;
    }

    public RomId RomId { get; }

    public string? Reason { get; }
}
