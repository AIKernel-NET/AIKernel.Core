namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Core.Time;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;

public abstract class FileProviderBase(
    string providerId,
    string name,
    IKernelClock? clock,
    VfsCredentialValidator? credentialValidator) : IVfsProvider
{
    private long _sessionSequence;

    public string ProviderId { get; } = string.IsNullOrWhiteSpace(providerId)
            ? throw new ArgumentException("ProviderId is required.", nameof(providerId))
            : providerId;

    public string Name { get; } = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name;

    /// <summary>
    /// VFS Provider 配下の Session に必ず貫通させる IKernelClock です。
    ///
    /// 設計意図:
    /// VFS は ContextSnapshot / Deterministic Replay の接地層です。
    /// ここで DateTime.UtcNow や DateTime.Now を直接使うと、同じ入力でも実行時刻によって
    /// VfsFileSnapshot の作成時刻・更新時刻が変わり、Replay の再現性が崩れます。
    ///
    /// そのため、Provider が保持する Clock を Session 生成時に必ず渡し、
    /// 時刻参照を IKernelClock に一元化します。
    /// </summary>
    protected IKernelClock Clock { get; } = clock ?? KernelClock.System();

    public Task<IVfsSession> OpenSessionAsync(IVfsCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (credentialValidator is not null && !credentialValidator(credentials))
        {
            throw new VfsAuthenticationFailedException(ProviderId);
        }

        var sessionId = $"{ProviderId}:{Interlocked.Increment(ref _sessionSequence):D8}";

        return OpenSessionCoreAsync(sessionId);
    }

    public virtual Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    public virtual Task<VfsProviderHealth> GetHealthAsync()
    {
        return Task.FromResult(new VfsProviderHealth
        {
            IsHealthy = true,
            Message = "OK",
            CheckedAtUtc = Clock.Now
        });
    }

    protected abstract Task<IVfsSession> OpenSessionCoreAsync(string sessionId);
}
