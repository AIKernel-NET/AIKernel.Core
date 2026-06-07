namespace AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException']" />
public sealed class VfsAuthenticationFailedException : UnauthorizedAccessException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.#ctor']" />
    public VfsAuthenticationFailedException(string providerId)
        : base($"VFS authentication failed for provider '{providerId}'.")
    {
        ProviderId = providerId;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.ProviderId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Abstractions.VfsAuthenticationFailedException.ProviderId']" />
    public string ProviderId { get; }
}
