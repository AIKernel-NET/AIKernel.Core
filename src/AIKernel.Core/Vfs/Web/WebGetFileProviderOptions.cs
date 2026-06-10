namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions']/summary" />
public sealed class WebGetFileProviderOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.#ctor']/summary" />
    public WebGetFileProviderOptions()
    {
    }

    /// <summary>
    /// [EN] Initializes web GET file provider options with endpoint and provider metadata.
    /// [JA] endpoint と provider metadata を指定して web GET file provider options を初期化します。
    /// </summary>
    public WebGetFileProviderOptions(
        Uri baseUri,
        string providerId = "web-get-file",
        string name = "Web GET File Provider",
        string? probePath = null,
        TimeSpan? timeout = null,
        IKernelClock? clock = null,
        VfsCredentialValidator? credentialValidator = null)
    {
        BaseUri = baseUri;
        ProviderId = providerId;
        Name = name;
        ProbePath = probePath;
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        Clock = clock;
        CredentialValidator = credentialValidator;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.BaseUri']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.BaseUri']/summary" />
    public Uri? BaseUri { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProviderId']/summary" />
    public string ProviderId { get; set; } = "web-get-file";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Name']/summary" />
    public string Name { get; set; } = "Web GET File Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProbePath']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProbePath']/summary" />
    public string? ProbePath { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.FromSeconds']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.FromSeconds']/summary" />
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Clock']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Clock']/summary" />
    public IKernelClock? Clock { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.CredentialValidator']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.CredentialValidator']/summary" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
