using AIKernel.Core.Vfs.VfsGit;
using AIKernel.Enums;

namespace AIKernel.Core.Tests.Vfs;

public sealed class VfsGitCapabilityContractTests
{
    [Fact]
    public void ToContract_ExposesCoreOwnedVfsGitBoundary()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.0",
            ["repository_mode"] = "readonly"
        };

        var contract = VfsGitCapabilityContracts.ToContract(
            new VfsGitCapabilityDescriptor(
                "tools.vfs.git",
                "readonly",
                metadata));

        Assert.Equal("tools.vfs.git", contract.CapabilityId);
        Assert.Equal("VFS Git", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("AIKernel.Core.Vfs.VfsGit", contract.EntryPoint);
        Assert.Equal(["vfs.git.read", "vfs.git.list", "vfs.git.checkout"], contract.ProvidedOperations);
        Assert.Equal(["vfs.read", "git.read"], contract.RequiredPermissions);
        Assert.Same(metadata, contract.Metadata);
    }

    [Fact]
    public void GitVfsStore_CreateSnapshotOrdersPathsDeterministically()
    {
        var store = new GitVfsStore("repo");
        var snapshot = store.CreateSnapshot(
            new GitCommit("abc123"),
            ["b.txt", "a.txt", "b.txt", " "]);

        Assert.Equal("repo", snapshot.RepositoryPath);
        Assert.Equal("abc123", snapshot.Commit.Sha);
        Assert.Equal(["a.txt", "b.txt"], snapshot.Paths);
    }
}
