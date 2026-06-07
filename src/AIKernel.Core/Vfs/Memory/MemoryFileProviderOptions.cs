namespace AIKernel.Core.Vfs.Memory;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions']" />
public sealed class MemoryFileProviderOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.#ctor']" />
    public MemoryFileProviderOptions()
    {
    }

    /// <summary>Initializes a new instance for the MemoryFileProviderOptions AIKernel contract surface. JA: MemoryFileProviderOptions AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.ProviderId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.ProviderId']" />
    public string ProviderId { get; set; } = "memory-file";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Name']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Name']" />
    public string Name { get; set; } = "Memory File Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Clock']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.Clock']" />
    public IKernelClock? Clock { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.CredentialValidator']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Vfs.Memory.MemoryFileProviderOptions.CredentialValidator']" />
    public VfsCredentialValidator? CredentialValidator { get; set; }
}
