namespace AIKernel.Kernel;

using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

internal sealed class FailClosedGuard : IGuard
{
    /// <summary>
    /// EN: Executes Instance.
    /// [EN] Documents this public package API member. [JA] Instance を実行します。
    /// </summary>
    public static FailClosedGuard Instance { get; } = new();

    private FailClosedGuard()
    {
    }
    /// <summary>
    /// EN: Gets CanExecuteAsync.
    /// [EN] Documents this public package API member. [JA] CanExecuteAsync を取得します。
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
    /// [EN] Documents this public package API member. [JA] CanAccessContextAsync を取得します。
    /// </summary>

    public Task<bool> CanAccessContextAsync(
        IPrincipal principal,
        UnifiedContextDto contract)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets CanReadAsync.
    /// [EN] Documents this public package API member. [JA] CanReadAsync を取得します。
    /// </summary>

    public Task<bool> CanReadAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets CanWriteAsync.
    /// [EN] Documents this public package API member. [JA] CanWriteAsync を取得します。
    /// </summary>

    public Task<bool> CanWriteAsync(
        IPrincipal principal,
        string resource)
    {
        return Task.FromResult(false);
    }
    /// <summary>
    /// EN: Gets EnforceAsync.
    /// [EN] Documents this public package API member. [JA] EnforceAsync を取得します。
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
    /// [EN] Documents this public package API member. [JA] OnFailureModeDetectedAsync を取得します。
    /// </summary>

    public Task<GuardAction> OnFailureModeDetectedAsync(
        FailureMode mode,
        string context)
    {
        return Task.FromResult(GuardAction.Block);
    }
}
