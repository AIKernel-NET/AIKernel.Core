namespace AIKernel.IntegrationTests;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.ChatHistory;
using AIKernel.Core.Context;
using AIKernel.Core.Security;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;
using AIKernel.Hosting;
using AIKernel.Kernel;
using AIKernel.Providers.MicrosoftAI;
using AIKernel.Providers.MicrosoftAI.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

public sealed class OpenAIHostingExtensionsTests
{
    [Fact]
    public void AddAIKernelKernel_RegistersKernelFacadeAndAccessors()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();
        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider();

        var kernel = provider.GetRequiredService<AIKernel.Abstractions.Kernel.IKernel>();

        Assert.NotNull(kernel);
        Assert.NotNull(kernel.GetProviderRouter());
        Assert.NotNull(kernel.GetGuard());
        Assert.NotNull(kernel.GetPdp());
    }

    [Fact]
    public void AddAIKernelCore_RegistersHistoryRomServices()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHistoryRomRegistry>());
        Assert.NotNull(provider.GetRequiredService<HistoryRomProvider>());
        Assert.NotNull(provider.GetRequiredService<HistoryRomStore>());
    }

    [Fact]
    public async Task AddAIKernelKernel_FailsClosed_ForLegacyContextExecution()
    {
        var services = new ServiceCollection();

        services.AddAIKernelCore();
        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider();

        var kernel = (AIKernel.Kernel.Kernel)provider
            .GetRequiredService<AIKernel.Abstractions.Kernel.IKernel>();

        var result = await kernel.ExecuteAsync(new UnifiedContextDto
        {
            Id = "legacy-context",
            Orchestration = new OrchestrationContextDto
            {
                Purpose = "legacy",
                Structure = "legacy"
            }
        });

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(
            "KernelRequest execution is required for the AIKernel.Core pipeline.",
            result.Error);
    }

    [Fact]
    public async Task WithOpenAI_ResolvesSecretBeforeProviderUse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "sk-config-123456",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:SecretKeyName"] = "OpenAI:ApiKey"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore(configuration)
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, options) =>
                {
                    Assert.Equal("sk-config-123456", options.ApiKey);
                    return new StubChatClient("ok");
                });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        var output = await modelProvider.GenerateAsync(
        [
            new TestModelMessage("user", "hello")
        ],
        TestContext.Current.CancellationToken);

        Assert.Equal("ok", output);
    }

    [Fact]
    public async Task WithOpenAI_UsesDirectApiKey_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, options) =>
                {
                    Assert.Equal("sk-direct-123456", options.ApiKey);
                    return new StubChatClient("direct-ok");
                });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        var output = await modelProvider.GenerateAsync(
        [
            new TestModelMessage("user", "hello")
        ],
        TestContext.Current.CancellationToken);

        Assert.Equal("direct-ok", output);
    }

    [Fact]
    public void WithOpenAI_RegistersPromptCapabilityForKernelExecution()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ProviderId"] = "openai-demo",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456",
                    ["AIKernel:Providers:OpenAI:MaxInputTokens"] = "4096",
                    ["AIKernel:Providers:OpenAI:MaxOutputTokens"] = "512"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capability = provider.GetRequiredService<ModelPromptCapability>();

        Assert.Equal("openai-demo", capability.ProviderId);
        Assert.Equal("gpt-demo", capability.ModelId);
        Assert.Equal(4096, capability.MaxInputTokens);
        Assert.Equal(512, capability.MaxOutputTokens);
        Assert.Contains("user", capability.SupportedRoles);
        Assert.Contains("system", capability.SupportedRoles);
    }

    [Fact]
    public void WithOpenAI_RegisteredPromptCapabilityIsResolvable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ProviderId"] = "openai-demo",
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var modelProvider = provider.GetRequiredService<IModelProvider>();
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var capability = resolver.Resolve(
            modelProvider,
            new KernelExecutionRequest
            {
                ContextSnapshotId = "snapshot:openai-hosting",
                ContextHash = "sha256:openai-hosting",
                ContextBlocks = [],
                UserInstruction = "hello",
                PromptOptions = CreatePromptOptions(),
                ExecutionOptions = CreateExecutionOptions(),
                RequestedModelId = "gpt-demo"
            });

        Assert.Equal("openai-demo", capability.ProviderId);
        Assert.Equal("gpt-demo", capability.ModelId);
    }

    [Fact]
    public async Task WithOpenAI_RegistersDefaultProviderCapabilities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capabilities = provider.GetRequiredService<IProviderCapabilities>();
        var modelProvider = provider.GetRequiredService<IModelProvider>();

        await modelProvider.InitializeAsync();

        Assert.IsType<OpenAICompatibleProviderCapabilities>(capabilities);
        Assert.True(capabilities.SupportsOperation("chat"));
        Assert.True(capabilities.SupportsDataType("text"));
        Assert.True(await modelProvider.IsAvailableAsync());
    }

    [Fact]
    public void WithOpenAI_DoesNotReplaceCustomProviderCapabilities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-demo",
                    ["AIKernel:Providers:OpenAI:ApiKey"] = "sk-direct-123456"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore()
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var capabilities = provider.GetRequiredService<IProviderCapabilities>();

        Assert.IsType<TestProviderCapabilities>(capabilities);
    }

    [Fact]
    public void OpenAIOptionsValidator_FailsClosed_ForInvalidTokenLimits()
    {
        var validator = new OpenAICompatibleProviderOptionsValidator();

        var result = validator.Validate(
            name: null,
            new OpenAICompatibleProviderOptions
            {
                ModelId = "gpt-demo",
                ApiKey = "sk-direct-123456",
                MaxInputTokens = 0,
                MaxOutputTokens = 0
            });

        Assert.True(result.Failed);
        Assert.Contains(
            "MaxInputTokens must be greater than zero.",
            result.Failures);
        Assert.Contains(
            "MaxOutputTokens must be greater than zero when specified.",
            result.Failures);
    }

    [Fact]
    public async Task WithOpenAI_FailsClosed_WhenSecretIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AIKernel:Providers:OpenAI:ModelId"] = "gpt-test",
                    ["AIKernel:Providers:OpenAI:SecretKeyName"] = "OpenAI:ApiKey"
                })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderCapabilities, TestProviderCapabilities>();

        services
            .AddAIKernelCore(configuration)
            .WithOpenAI(
                configuration.GetSection("AIKernel:Providers:OpenAI"),
                (_, _) => new StubChatClient("ok"));

        using var provider = services.BuildServiceProvider(validateScopes: true);

        var hostedServices = provider.GetServices<IHostedService>();

        await Assert.ThrowsAsync<SecureCredentialNotFoundException>(
            async () =>
            {
                foreach (var hostedService in hostedServices)
                {
                    await hostedService.StartAsync(TestContext.Current.CancellationToken);
                }
            });
    }

    [Fact]
    public void WithOpenAI_Extension_BelongsToProviderAssembly()
    {
        var assembly = typeof(
            AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions
        ).Assembly;

        Assert.Equal(
            "AIKernel.Providers.MicrosoftAI",
            assembly.GetName().Name);
    }

    [Fact]
    public void MicrosoftAIProvider_DoesNotReferenceKernelFacadeOrHosting()
    {
        var assembly = typeof(
            AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions
        ).Assembly;

        var referencedAssemblies = assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(
            "AIKernel.Kernel",
            referencedAssemblies);

        Assert.DoesNotContain(
            "AIKernel.Hosting",
            referencedAssemblies);
    }

    private sealed record TestModelMessage(
        string Role,
        string Content) : IModelMessage;

    private static IContextSnapshot CreateContextSnapshot()
    {
        return new AssembledContextSnapshot(
            snapshotId: "snapshot:openai-hosting",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:openai-hosting",
            context: new ContextCollectionSnapshot([]));
    }

    private static PromptGenerationOptions CreatePromptOptions()
    {
        return new PromptGenerationOptions
        {
            OverflowPolicy = PromptOverflowPolicy.FailClosed,
            IncludeContextHash = true,
            IncludeSourceMetadata = true
        };
    }

    private static ExecutionOptions CreateExecutionOptions()
    {
        return new ExecutionOptions
        {
            Temperature = 0,
            TopP = 1,
            MaxOutputTokens = 128,
            StopSequences = []
        };
    }

    private sealed class StubChatClient : IChatClient
    {
        private readonly string _output;

        public StubChatClient(string output)
        {
            _output = output;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, _output))
                {
                    ModelId = options?.ModelId
                });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
