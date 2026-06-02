#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public sealed record ContextAssemblyDecision(
    bool IsAllowed,
    string? Reason = null)
{
    public static ContextAssemblyDecision Allow()
    {
        return new ContextAssemblyDecision(true);
    }

    public static ContextAssemblyDecision Deny(string reason)
    {
        return new ContextAssemblyDecision(false, reason);
    }
}
