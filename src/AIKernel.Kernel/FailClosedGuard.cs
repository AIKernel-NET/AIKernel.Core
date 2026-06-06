namespace AIKernel.Kernel;

using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

internal sealed class FailClosedGuard : IGuard
{
    public static FailClosedGuard Instance { get; } = new();

    private FailClosedGuard()
    {
    }

    public Task<bool> CanExecuteAsync(
        IPrincipal principal,
        string action,
        string resource)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CanAccessContextAsync(
        IPrincipal principal,
        UnifiedContextDto contract)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CanReadAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }

    public Task<bool> CanWriteAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }

    public Task<GuardAction> EnforceAsync(
        IPrincipal principal,
        string action,
        string resource)
    {
        return Task.FromResult(GuardAction.Block);
    }

    public Task<GuardAction> OnFailureModeDetectedAsync(
        FailureMode mode,
        string context)
    {
        return Task.FromResult(GuardAction.Block);
    }
}
