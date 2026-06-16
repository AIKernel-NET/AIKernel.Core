namespace AIKernel.Core.Vfs.Abstractions;

/// <summary>[EN] Documents this public package API member. [JA] VfsAuthenticationFailedException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException']/summary" />
public sealed class VfsAuthenticationFailedException : UnauthorizedAccessException
{
    /// <summary>[EN] Documents this public package API member. [JA] VfsAuthenticationFailedException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.#ctor']/summary" />
    public VfsAuthenticationFailedException(string providerId)
        : base($"VFS authentication failed for provider '{providerId}'.")
    {
        ProviderId = providerId;
    }

    /// <summary>[EN] Documents this public package API member. [JA] ProviderId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.ProviderId']/summary" />
    public string ProviderId { get; }
}
