namespace AIKernel.Core.Vfs.Local;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProviderOptions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProviderOptions']" />
public sealed class LocalFileProviderOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.#ctor']" />
    public LocalFileProviderOptions()
    {
    }

    /// <summary>Initializes a new instance for the LocalFileProviderOptions AIKernel contract surface. JA: LocalFileProviderOptions AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    public LocalFileProviderOptions(
        string rootPath,
        bool allowWrite = true,
        string providerId = "local-file",
        string name = "Local File Provider",
        IKernelClock? clock = null,
        VfsCredentialValidator? credentialValidator = null)
    {
        RootPath = rootPath;
        AllowWrite = allowWrite;
        ProviderId = providerId;
        Name = name;
        Clock = clock;
        CredentialValidator = credentialValidator;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.RootPath']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.RootPath']" />
    public string RootPath { get; set; } = string.Empty;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.AllowWrite']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.AllowWrite']" />
    public bool AllowWrite { get; set; } = true;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.ProviderId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.ProviderId']" />
    public string ProviderId { get; set; } = "local-file";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Name']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Name']" />
    public string Name { get; set; } = "Local File Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Clock']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Clock']" />
    public IKernelClock? Clock { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.CredentialValidator']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.CredentialValidator']" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
