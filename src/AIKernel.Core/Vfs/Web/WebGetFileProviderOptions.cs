namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions']" />
public sealed class WebGetFileProviderOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.#ctor']" />
    public WebGetFileProviderOptions()
    {
    }

    /// <summary>Initializes a new instance for the WebGetFileProviderOptions AIKernel contract surface. JA: WebGetFileProviderOptions AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.BaseUri']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.BaseUri']" />
    public Uri? BaseUri { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProviderId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProviderId']" />
    public string ProviderId { get; set; } = "web-get-file";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Name']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Name']" />
    public string Name { get; set; } = "Web GET File Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProbePath']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.ProbePath']" />
    public string? ProbePath { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.FromSeconds']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.FromSeconds']" />
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Clock']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.Clock']" />
    public IKernelClock? Clock { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.CredentialValidator']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Web.WebGetFileProviderOptions.CredentialValidator']" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
