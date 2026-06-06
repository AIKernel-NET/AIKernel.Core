namespace AIKernel.Core.Vfs.Web;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

public sealed class WebGetFileProviderOptions
{
    public WebGetFileProviderOptions()
    {
    }

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

    public Uri? BaseUri { get; set; }

    public string ProviderId { get; set; } = "web-get-file";

    public string Name { get; set; } = "Web GET File Provider";

    public string? ProbePath { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public IKernelClock? Clock { get; set; }

    public VfsCredentialValidator? CredentialValidator { get; set; }
}
