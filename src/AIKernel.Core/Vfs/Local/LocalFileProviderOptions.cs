namespace AIKernel.Core.Vfs.Local;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

public sealed class LocalFileProviderOptions
{
    public LocalFileProviderOptions()
    {
    }

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

    public string RootPath { get; set; } = string.Empty;

    public bool AllowWrite { get; set; } = true;

    public string ProviderId { get; set; } = "local-file";

    public string Name { get; set; } = "Local File Provider";

    public IKernelClock? Clock { get; set; }

    public VfsCredentialValidator? CredentialValidator { get; set; }
}
