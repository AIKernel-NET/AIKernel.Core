namespace AIKernel.Core.Vfs.Memory;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions']/summary" />
public sealed class MemoryFileProviderOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.#ctor']/summary" />
    public MemoryFileProviderOptions()
    {
    }

    /// <summary>
    /// [EN] Initializes memory file provider options with provider metadata.
    /// [JA] provider metadata を指定して memory file provider options を初期化します。
    /// </summary>
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.ProviderId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.ProviderId']/summary" />
    public string ProviderId { get; set; } = "memory-file";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Name']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Name']/summary" />
    public string Name { get; set; } = "Memory File Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Clock']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Clock']/summary" />
    public IKernelClock? Clock { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.CredentialValidator']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.CredentialValidator']/summary" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
