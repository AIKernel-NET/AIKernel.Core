namespace AIKernel.Kernel;

using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

internal sealed class FailClosedGuard : IGuard
{
    /// <summary>
    /// EN: Executes Instance.
    /// EN: Documentation for public API. JA: Instance を実行します。
    /// </summary>
    public static FailClosedGuard Instance { get; } = new();

    private FailClosedGuard()
    {
    }
    /// <summary>
    /// EN: Gets CanExecuteAsync.
    /// EN: Documentation for public API. JA: CanExecuteAsync を取得します。
    /// </summary>

    public Task<bool> CanExecuteAsync(
        IPrincipal principal,
        string action,
        string resource)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets CanAccessContextAsync.
    /// EN: Documentation for public API. JA: CanAccessContextAsync を取得します。
    /// </summary>

    public Task<bool> CanAccessContextAsync(
        IPrincipal principal,
        UnifiedContextDto contract)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets CanReadAsync.
    /// EN: Documentation for public API. JA: CanReadAsync を取得します。
    /// </summary>

    public Task<bool> CanReadAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets CanWriteAsync.
    /// EN: Documentation for public API. JA: CanWriteAsync を取得します。
    /// </summary>

    public Task<bool> CanWriteAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets EnforceAsync.
    /// EN: Documentation for public API. JA: EnforceAsync を取得します。
    /// </summary>

    public Task<GuardAction> EnforceAsync(
        IPrincipal principal,
        string action,
        string resource)
    {
        return Task.FromResult(GuardAction.Block);
    }
    /// <summary>
    /// EN: Gets OnFailureModeDetectedAsync.
    /// EN: Documentation for public API. JA: OnFailureModeDetectedAsync を取得します。
    /// </summary>

    public Task<GuardAction> OnFailureModeDetectedAsync(
        FailureMode mode,
        string context)
    {
        return Task.FromResult(GuardAction.Block);
    }
}
