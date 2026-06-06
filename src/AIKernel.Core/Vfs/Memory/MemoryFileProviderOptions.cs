namespace AIKernel.Core.Vfs.Memory;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

public sealed class MemoryFileProviderOptions
{
    public MemoryFileProviderOptions()
    {
    }

    public MemoryFileProviderOptions(
        string providerId = "memory-file",
        string name = "Memory File Provider",
        IKernelClock? clock = null,
        VfsCredentialValidator? credentialValidator = null)
    {
        ProviderId = providerId;
        Name = name;
        Clock = clock;
        CredentialValidator = credentialValidator;
    }

    public string ProviderId { get; set; } = "memory-file";

    public string Name { get; set; } = "Memory File Provider";

    public IKernelClock? Clock { get; set; }

    public VfsCredentialValidator? CredentialValidator { get; set; }
}
