namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Core.Time;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;

/// <summary>[EN] Documents this public package API member. [JA] FileProviderBase を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.FileProviderBase']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.FileProviderBase']/summary" />
public abstract class FileProviderBase(
    string providerId,
    string name,
    IKernelClock? clock,
    VfsCredentialValidator? credentialValidator) : IVfsProvider
{
    private long _sessionSequence;

    /// <summary>[EN] Documents this public package API member. [JA] ProviderId を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.IsNullOrWhiteSpace']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.IsNullOrWhiteSpace']/summary" />
    public string ProviderId { get; } = string.IsNullOrWhiteSpace(providerId)
            ? throw new ArgumentException("ProviderId is required.", nameof(providerId))
            : providerId;

    /// <summary>
    /// [EN] Gets the provider display name.
    /// [JA] provider の表示名を取得します。
    /// </summary>
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

    /// <summary>[EN] Documents this public package API member. [JA] OpenSessionAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.OpenSessionAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.OpenSessionAsync']/summary" />
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

    /// <summary>[EN] Documents this public package API member. [JA] IsAvailableAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.IsAvailableAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.IsAvailableAsync']/summary" />
    public virtual Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(false);
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetHealthAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.GetHealthAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.FileProviderBase.GetHealthAsync']/summary" />
    public virtual Task<VfsProviderHealth> GetHealthAsync()
    {
        return Task.FromResult(new VfsProviderHealth
        {
            IsHealthy = false,
            Message = "Provider health is not implemented.",
            CheckedAtUtc = Clock.Now
        });
    }

    /// <summary>EN: Executes the OpenSessionCoreAsync operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenSessionCoreAsync 操作を実行します。</summary>
    protected abstract Task<IVfsSession> OpenSessionCoreAsync(string sessionId);
}
