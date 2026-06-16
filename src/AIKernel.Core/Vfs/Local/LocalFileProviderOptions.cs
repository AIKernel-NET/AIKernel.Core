namespace AIKernel.Core.Vfs.Local;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <summary>EN: Documentation for public API. JA: LocalFileProviderOptions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProviderOptions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Local.LocalFileProviderOptions']/summary" />
public sealed class LocalFileProviderOptions
{
    /// <summary>EN: Documentation for public API. JA: LocalFileProviderOptions を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.#ctor']/summary" />
    public LocalFileProviderOptions()
    {
    }

    /// <summary>
    /// [EN] Initializes local file provider options with root path and provider metadata.
    /// [JA] root path と provider metadata を指定して local file provider options を初期化します。
    /// </summary>
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

    /// <summary>EN: Documentation for public API. JA: RootPath を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.RootPath']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.RootPath']/summary" />
    public string RootPath { get; set; } = string.Empty;

    /// <summary>EN: Documentation for public API. JA: AllowWrite を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.AllowWrite']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.AllowWrite']/summary" />
    public bool AllowWrite { get; set; } = true;

    /// <summary>EN: Documentation for public API. JA: ProviderId を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.ProviderId']/summary" />
    public string ProviderId { get; set; } = "local-file";

    /// <summary>EN: Documentation for public API. JA: Name を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Name']/summary" />
    public string Name { get; set; } = "Local File Provider";

    /// <summary>EN: Documentation for public API. JA: Clock を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Clock']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.Clock']/summary" />
    public IKernelClock? Clock { get; set; }

    /// <summary>EN: Documentation for public API. JA: CredentialValidator を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.CredentialValidator']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Local.LocalFileProviderOptions.CredentialValidator']/summary" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
