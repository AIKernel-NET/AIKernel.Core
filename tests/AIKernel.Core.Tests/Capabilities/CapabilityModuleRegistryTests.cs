namespace AIKernel.Core.Tests.Capabilities;

using AIKernel.Core.Capabilities;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

public sealed class CapabilityModuleRegistryTests
{
    [Fact]
    public async Task RegisterAsync_ResolvesDescriptorByTrimmedCapabilityId()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;

        await registry.RegisterAsync(
            CreateDescriptor(" aik.tools.observe "),
            cancellationToken);

        var descriptor = await registry.ResolveAsync(
            "aik.tools.observe",
            cancellationToken);

        Assert.NotNull(descriptor);
        Assert.Equal("aik.tools.observe", descriptor.CapabilityId);
        Assert.Equal(CapabilityModuleKind.CliExecutable, descriptor.Kind);
        Assert.Equal(CapabilityInvocationMode.Sandbox, descriptor.InvocationMode);
    }

    [Fact]
    public async Task ListAsync_ReturnsDescriptorsInDeterministicOrder()
    {
        var registry = new InMemoryCapabilityModuleRegistry();
        var cancellationToken = TestContext.Current.CancellationToken;

        await registry.RegisterAsync(
            CreateDescriptor("module.b"),
            cancellationToken);
        await registry.RegisterAsync(
            CreateDescriptor("module.a"),
            cancellationToken);

        var descriptors = await registry.ListAsync(cancellationToken);

        Assert.Equal(
            ["module.a", "module.b"],
            descriptors.Select(x => x.CapabilityId).ToArray());
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullForBlankCapabilityId()
    {
        var registry = new InMemoryCapabilityModuleRegistry();

        var descriptor = await registry.ResolveAsync(
            " ",
            TestContext.Current.CancellationToken);

        Assert.Null(descriptor);
    }

    [Fact]
    public async Task RegisterAsync_RejectsBlankCapabilityId()
    {
        var registry = new InMemoryCapabilityModuleRegistry();

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.RegisterAsync(
                CreateDescriptor(" "),
                TestContext.Current.CancellationToken));
    }

    private static CapabilityModuleDescriptor CreateDescriptor(
        string capabilityId)
    {
        return new CapabilityModuleDescriptor(
            CapabilityId: capabilityId,
            Name: "Observe",
            Kind: CapabilityModuleKind.CliExecutable,
            InvocationMode: CapabilityInvocationMode.Sandbox,
            Version: "0.1.0",
            EntryPoint: "aik-tools",
            ArtifactUri: "file://tools/aik-tools.exe",
            ArtifactHash: "sha256:tools",
            ProvidedOperations: ["Observe"],
            RequiredPermissions: ["read-context"],
            Metadata: new Dictionary<string, string>
            {
                ["schema"] = "capability-module"
            });
    }
}
