namespace AIKernel.Core.Tests.Providers;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Capabilities;
using AIKernel.Core.Providers.SkillProvider;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Dsl;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class SkillProviderTests
{
    [Fact]
    public void ParserReadsOpenAiSkillFrontMatterAndBuildsDsl()
    {
        var parser = new SkillManifestParser();

        var manifest = parser.Parse(
            """
            ---
            name: summarize-text
            description: Summarizes long text into concise form.
            operations:
              - skill.summarize
            metadata:
              short-description: Summarize text
            ---

            # Summarize Text

            ## Steps

            - read input
            - call:llm.summarize
            - return summary
            """,
            "skills/summarize/SKILL.md");

        Assert.Equal("summarize-text", manifest.Name);
        Assert.Equal("Summarizes long text into concise form.", manifest.Description);

        var pipeline = Assert.IsType<PipelineRootNode>(manifest.Dsl.Root);
        Assert.Equal(3, pipeline.Steps.Count);
        Assert.IsType<CallCapabilityNode>(pipeline.Steps[1]);

        var descriptor = manifest.ToDescriptor();
        Assert.Equal("skill.summarize-text", descriptor.CapabilityId);
        Assert.Equal(["skill.summarize"], descriptor.ProvidedOperations);
    }

    [Fact]
    public async Task LoaderRegistersSkillCapabilityModule()
    {
        var root = CreateSkillRoot(
            """
            ---
            name: summarize-text
            description: Summarizes long text into concise form.
            operations: skill.summarize
            ---

            ## Steps
            - read input
            - return summary
            """);
        var registry = new InMemoryCapabilityModuleRegistry();
        var loader = new SkillLoader();

        var descriptors = await loader.LoadAndRegisterAsync(
            root,
            registry,
            cancellationToken: TestContext.Current.CancellationToken);

        var descriptor = Assert.Single(descriptors);
        var contract = await registry.ResolveAsync(
            descriptor.CapabilityId,
            TestContext.Current.CancellationToken);
        Assert.NotNull(contract);
        Assert.Equal("summarize-text", contract.Name);
        Assert.Equal("AIKernel.Core.Providers.SkillProvider", contract.EntryPoint);
        Assert.Equal("false", contract.Metadata["skill.dsl_compiled"]);
    }

    [Fact]
    public async Task ProviderInitializesAndInvokesRegisteredSkill()
    {
        var root = CreateSkillRoot(
            """
            ---
            name: summarize-text
            description: Summarizes long text into concise form.
            operations: skill.summarize
            ---

            ## Steps
            - read input
            - return summary
            """);
        var registry = new InMemoryCapabilityModuleRegistry();
        var provider = new SkillProvider(registry, null, new SkillLoader(), root);

        await provider.InitializeAsync();

        var contract = await registry.ResolveAsync(
            "skill.summarize-text",
            TestContext.Current.CancellationToken);
        Assert.NotNull(contract);
        Assert.True(await provider.IsAvailableAsync());
        Assert.Equal("aikernel.skill", provider.ProviderId);

        var result = await provider.InvokeAsync(
            new CapabilityInvocationRequest(
                "invoke-1",
                "skill.summarize-text",
                "skill.summarize",
                new Dictionary<string, string>(StringComparer.Ordinal),
                InputHash: null,
                ReplayLogHash: null,
                Metadata: new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.OutputHash);
        Assert.Equal("aikernel.skill", result.Metadata["provider"]);
    }

    [Fact]
    public void HostingRegistersBuiltInSkillProvider()
    {
        var services = new ServiceCollection();
        services.AddAIKernelCore();
        using var provider = services.BuildServiceProvider();

        var providers = provider
            .GetServices<IProvider>()
            .Select(x => x.ProviderId)
            .ToArray();

        Assert.Contains("aikernel.skill", providers);

        var registry = provider.GetRequiredService<IProviderRegistry>();
        Assert.Contains("aikernel.skill", registry.GetRegisteredProviders());
    }

    private static string CreateSkillRoot(
        string markdown)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "aikernel-skill-provider-tests",
            Guid.NewGuid().ToString("N"));
        var skillDir = Path.Combine(root, "summarize-text");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), markdown);
        return root;
    }
}
