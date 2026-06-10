using AIKernel.Core.Storage;
using AIKernel.Enums;

namespace AIKernel.Core.Tests.Storage;

public sealed class RomStorageCapabilityContractTests
{
    [Fact]
    public void ToContract_ExposesCoreOwnedRomStorageBoundary()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = "0.1.0",
            ["storage_uri"] = "rom://core/storage"
        };

        var contract = RomStorageCapabilityContracts.ToContract(
            new RomStorageCapabilityDescriptor(
                "tools.rom",
                "rom",
                metadata));

        Assert.Equal("tools.rom", contract.CapabilityId);
        Assert.Equal("ROM Storage", contract.Name);
        Assert.Equal(CapabilityModuleKind.ManagedAssembly, contract.Kind);
        Assert.Equal(CapabilityInvocationMode.AssemblyReference, contract.InvocationMode);
        Assert.Equal("AIKernel.Core.Storage", contract.EntryPoint);
        Assert.Equal(["rom.save", "rom.load", "rom.list"], contract.ProvidedOperations);
        Assert.Equal(["rom.read", "rom.write"], contract.RequiredPermissions);
        Assert.Same(metadata, contract.Metadata);
    }
}
