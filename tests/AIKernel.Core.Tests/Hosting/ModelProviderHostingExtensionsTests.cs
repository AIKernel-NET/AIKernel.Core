namespace AIKernel.Core.Tests.Hosting;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Routing;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Routing;
using AIKernel.Enums;
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class ModelProviderHostingExtensionsTests
{
    [Fact]
    public void WithModelProvider_RegistersExternalProviderAndCapability()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
                CreateCapability("external-capability", "rh-prime-phase"));

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "external-capability");
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var capability = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-prime-phase"));

        Assert.Equal("external-capability", capability.ProviderId);
        Assert.Equal("rh-prime-phase", capability.ModelId);
        Assert.Same(
            modelProvider,
            provider.GetRequiredService<IProvider>());
        Assert.Contains(
            "external-capability",
            provider.GetRequiredService<IProviderRegistry>().GetRegisteredProviders());
    }

    [Fact]
    public void WithModelProvider_FactorySupportsExecutableAdapterProviders()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider(
                _ => new ExternalCapabilityProvider("tools-process-adapter"),
                CreateCapability("tools-process-adapter", "tools-cli"));

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "tools-process-adapter");
        var capability = provider
            .GetRequiredService<IModelPromptCapabilityResolver>()
            .Resolve(modelProvider, CreateExecutionRequest("tools-cli"));

        Assert.Equal("tools-process-adapter", capability.ProviderId);
        Assert.Equal("tools-cli", capability.ModelId);
        Assert.Contains(
            "tools-process-adapter",
            provider.GetRequiredService<IProviderRegistry>().GetRegisteredProviders());
    }

    [Fact]
    public void WithModelProvider_FactoryKeepsMultipleSameTypeRegistrationsDistinct()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider(
                _ => new ExternalCapabilityProvider("tools-process-adapter"),
                CreateCapability("tools-process-adapter", "tools-cli"))
            .WithModelProvider(
                _ => new ExternalCapabilityProvider("rh-process-adapter"),
                CreateCapability("rh-process-adapter", "rh-cli"));

        using var provider = services.BuildServiceProvider();

        var providerIds = provider
            .GetServices<IModelProvider>()
            .Select(modelProvider => modelProvider.ProviderId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var registeredIds = provider
            .GetRequiredService<IProviderRegistry>()
            .GetRegisteredProviders()
            .Where(providerId => !providerId.StartsWith("aikernel.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["rh-process-adapter", "tools-process-adapter"],
            providerIds);
        Assert.Equal(providerIds, registeredIds);
    }

    [Fact]
    public void WithModelProvider_RegistersMultipleCapabilitiesForOneProvider()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
            [
                CreateCapability("external-capability", "rh-prime-phase"),
                CreateCapability("external-capability", "rh-interference-energy")
            ]);

        using var provider = services.BuildServiceProvider();

        var modelProvider = Assert.Single(
            provider.GetServices<IModelProvider>(),
            x => x.ProviderId == "external-capability");
        var resolver = provider.GetRequiredService<IModelPromptCapabilityResolver>();

        var primePhase = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-prime-phase"));
        var interferenceEnergy = resolver.Resolve(
            modelProvider,
            CreateExecutionRequest("rh-interference-energy"));

        Assert.Equal("rh-prime-phase", primePhase.ModelId);
        Assert.Equal("rh-interference-energy", interferenceEnergy.ModelId);
    }

    [Fact]
    public async Task WithModelProvider_RegistersCapabilityRegistrySnapshot()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
                CreateCapability("external-capability", "rh-prime-phase"));

        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<ICapabilityRegistry>();
        var capacity = await registry.GetCapabilityAsync(
            "external-capability",
            TestContext.Current.CancellationToken);
        var candidates = await registry.ResolveCandidatesAsync(
            new AIKernel.Dtos.Rules.RuleEvaluationContext(
                "context",
                "routing",
                new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.NotNull(capacity);
        Assert.Equal(["external-capability"], candidates);
    }

    [Fact]
    public void WithModelProvider_RejectsInvalidCapabilityAtRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(
            () => services
                .AddAIKernelCore()
                .WithModelProvider<ExternalCapabilityProvider>(
                    new ModelPromptCapability
                    {
                        ProviderId = "",
                        ModelId = "model"
                    }));

        Assert.Equal(
            "ModelPromptCapability.ProviderId is required. (Parameter 'capability')",
            exception.Message);
    }

    [Fact]
    public void WithModelProvider_FactoryRejectsInvalidCapabilityAtResolution()
    {
        var services = new ServiceCollection();

        services
            .AddAIKernelCore()
            .WithModelProvider<ExternalCapabilityProvider>(
                _ => new ModelPromptCapability
                {
                    ProviderId = "",
                    ModelId = "model"
                });

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<ArgumentException>(
            () => provider.GetRequiredService<ModelPromptCapability>());

        Assert.Equal(
            "ModelPromptCapability.ProviderId is required. (Parameter 'capability')",
            exception.Message);
    }

    [Fact]
    public async Task WithExecutionPipeline_ExecutesTheSelectedModelAndIsolatesPhaseInputs()
    {
        const string plan = """{"steps":["Summarize the verified source."]}""";
        const string answer = "Summary of the verified source.";
        var prompts = new List<string>();
        var examples = new List<string> { "original example" };
        var chosen = new ExternalCapabilityProvider("selected", messages =>
        {
            prompts.Add(string.Join("\n", messages.Select(message => message.Content)));
            examples[0] = "changed during execution";
            return prompts.Count == 1 ? plan : answer;
        });
        var other = new ExternalCapabilityProvider("other", _ => throw new InvalidOperationException());
        var polisher = new RecordingPolisher();
        var services = new ServiceCollection();
        services.AddSingleton<IOutputPolisher>(polisher);
        services.AddAIKernelCore()
            .WithModelProvider(_ => other, CreateCapability("other", "default"))
            .WithModelProvider(_ => chosen, CreateCapability("selected", "default"))
            .WithExecutionPipeline("selected", "default");

        using var host = services.BuildServiceProvider();
        var orchestrator = host.GetRequiredService<PipelineOrchestrator>();
        Assert.Same(orchestrator, host.GetRequiredService<IPipelineOrchestrator>());
        var cancellationToken = TestContext.Current.CancellationToken;
        var (result, dump) = await orchestrator.ExecuteWithReplayAsync(
            CreatePipelineContext(examples), SignatureResult(isValid: true), cancellationToken);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.Equal(plan, result.Logic.SerializedRepresentation);
        Assert.Equal(answer, result.FinalOutput);
        Assert.Collection(prompts,
            structure =>
            {
                Assert.Contains("task input", structure, StringComparison.Ordinal);
                Assert.Contains("source input", structure, StringComparison.Ordinal);
                Assert.DoesNotContain("expression input", structure, StringComparison.Ordinal);
                Assert.DoesNotContain("internal policy data", structure, StringComparison.Ordinal);
            },
            generation =>
            {
                Assert.Contains(plan, generation, StringComparison.Ordinal);
                Assert.DoesNotContain("source input", generation, StringComparison.Ordinal);
                Assert.DoesNotContain("internal policy data", generation, StringComparison.Ordinal);
            });
        Assert.Equal(answer, polisher.Input);
        var expression = Assert.Single(polisher.ExpressionFragments);
        Assert.Equal("expression input", expression.Content);
        Assert.Equal("original example", Assert.Single(expression.Examples));

        Assert.NotNull(dump);
        Assert.Equal(answer, dump.GenerationOutput);
        var modification = new ModificationContext
        {
            Reason = "Verify recorded result",
            TargetPhase = "replay",
            ModificationData = string.Empty,
            ModifiedBy = "test"
        };
        var replayed = await orchestrator.ReplayFromDumpAsync(dump, modification, cancellationToken);
        Assert.True(replayed.IsSuccessful);
        Assert.Equal(result.FinalOutput, replayed.FinalOutput);

        var tampered = await orchestrator.ReplayFromDumpAsync(
            dump with { GenerationOutput = "tampered" }, modification, cancellationToken);
        Assert.False(tampered.IsSuccessful);
        Assert.Empty(tampered.FinalOutput);
    }

    [Theory]
    [InlineData(false, "default", "unused", 0)]
    [InlineData(true, "unregistered", "unused", 0)]
    [InlineData(true, "default", "not a structure", 1)]
    [InlineData(true, "default", "{\"steps\":[]}", 1)]
    public async Task WithExecutionPipeline_StopsBeforeLaterPhasesOnRejectedInput(
        bool signatureValid, string model, string structure, int expectedProviderCalls)
    {
        var calls = 0;
        var provider = new ExternalCapabilityProvider("selected", _ =>
        {
            calls++;
            return structure;
        });
        var polisher = new RecordingPolisher();
        var services = new ServiceCollection();
        services.AddSingleton<IOutputPolisher>(polisher);
        services.AddAIKernelCore()
            .WithModelProvider(_ => provider, CreateCapability("selected", "default"))
            .WithExecutionPipeline("selected", model);

        using var host = services.BuildServiceProvider();
        var result = await host.GetRequiredService<IPipelineOrchestrator>().ExecuteAsync(
            CreatePipelineContext([]), SignatureResult(signatureValid), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.Empty(result.FinalOutput);
        Assert.Equal(expectedProviderCalls, calls);
        Assert.Null(polisher.Input);
    }

    [Fact]
    public async Task WithExecutionPipeline_RejectsPolishThatChangesTheGeneratedAnswer()
    {
        var output = new Queue<string>(["""{"steps":["Answer the question."]}""", "Original answer."]);
        var provider = new ExternalCapabilityProvider("selected", _ => output.Dequeue());
        var services = new ServiceCollection();
        services.AddSingleton<IOutputPolisher>(new RecordingPolisher("Altered answer."));
        services.AddAIKernelCore()
            .WithModelProvider(_ => provider, CreateCapability("selected", "default"))
            .WithExecutionPipeline("selected", "default");

        using var host = services.BuildServiceProvider();
        var result = await host.GetRequiredService<IPipelineOrchestrator>().ExecuteAsync(
            CreatePipelineContext([]), SignatureResult(isValid: true), TestContext.Current.CancellationToken);

        Assert.Empty(output);
        Assert.False(result.IsSuccessful);
        Assert.Empty(result.FinalOutput);
        Assert.Equal("Polish altered the generated answer.", result.ErrorMessage);
    }

    private static ContextCollectionSnapshot CreatePipelineContext(IReadOnlyList<string> examples)
        => new([
            new ContextFragment { FragmentId = "task", Category = ContextCategory.Orchestration, Content = "task input" },
            new ContextFragment { FragmentId = "source", Category = ContextCategory.Material, Content = "source input" },
            new ContextFragment { FragmentId = "policy", Category = ContextCategory.Governance, Content = "internal policy data" },
            new ExpressionFragment { FragmentId = "style", Content = "expression input", Examples = examples }
        ]);

    private static SignatureVerificationResult SignatureResult(bool isValid)
        => new()
        {
            IsValid = isValid,
            SignerId = "test-signer",
            TrustScore = isValid ? 1 : 0,
            Message = isValid ? "verified" : "invalid",
            VerificationTimestamp = DateTime.UnixEpoch
        };

    private sealed class RecordingPolisher(string? replacement = null) : IOutputPolisher
    {
        public ModelCapacityVector RequiredCapacity { get; } = new();
        public string? Input { get; private set; }
        public IReadOnlyList<ExpressionFragment> ExpressionFragments { get; private set; } = [];

        public Task<string> RenderAsync(RawLogic logic, ExpressionContext expressionContext, CancellationToken ct)
        {
            Input = logic.SerializedRepresentation;
            ExpressionFragments = expressionContext.ExpressionBuffer.Fragments;
            return Task.FromResult(replacement ?? Input);
        }
    }

    private static ModelPromptCapability CreateCapability(
        string providerId,
        string modelId)
    {
        return new ModelPromptCapability
        {
            ProviderId = providerId,
            ModelId = modelId,
            MessageFormat = PromptMessageFormat.ChatMessages,
            MaxInputTokens = 2048,
            MaxOutputTokens = 512,
            SupportedRoles = ["user"],
            SystemInstructionRole = "user"
        };
    }

    private static KernelExecutionRequest CreateExecutionRequest(
        string modelId)
    {
        return new KernelExecutionRequest
        {
            ContextSnapshotId = "snapshot:external-capability",
            ContextHash = "sha256:external-capability",
            ContextBlocks = [],
            UserInstruction = "run capability",
            PromptOptions = TestExecutionDefaults.PromptOptions,
            ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
            RequestedModelId = modelId
        };
    }

    private static IContextSnapshot CreateContextSnapshot()
    {
        return new AssembledContextSnapshot(
            snapshotId: "snapshot:external-capability",
            parentSnapshotId: null,
            createdAtUtc: DateTimeOffset.UnixEpoch,
            contextHash: "sha256:external-capability",
            context: new ContextCollectionSnapshot([]));
    }

    private sealed class ExternalCapabilityProvider : IModelProvider
    {
        public ExternalCapabilityProvider()
            : this("external-capability")
        {
        }

        private readonly Func<IReadOnlyList<IModelMessage>, string>? _generate;

        public ExternalCapabilityProvider(
            string providerId,
            Func<IReadOnlyList<IModelMessage>, string>? generate = null)
        {
            ProviderId = providerId;
            _generate = generate;
        }

        public string ProviderId { get; }

        public string Name => "External Capability Provider";

        public string Version => "0.1.3";

        public IProviderCapabilities GetCapabilities()
        {
            return new ExternalProviderCapabilities();
        }

        public Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(true);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }

        public Task<ProviderHealthStatus> GetHealthAsync()
        {
            return Task.FromResult(new ProviderHealthStatus(
                IsHealthy: true,
                Message: "OK",
                CheckedAt: DateTime.UnixEpoch,
                ResponseTimeMs: 0));
        }

        public Task<string> GenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_generate?.Invoke(messages) ?? "external output");
        }

        public Task StreamGenerateAsync(
            IReadOnlyList<IModelMessage> messages,
            Func<string, Task> onChunk,
            CancellationToken cancellationToken = default)
        {
            return onChunk("external output");
        }

        public Task<string> AnswerAsync(
            string question,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("external output");
        }
    }

    private sealed class ExternalProviderCapabilities : IProviderCapabilities
    {
        public IReadOnlyList<string> SupportedOperations => [];

        public IReadOnlyList<string> SupportedDataTypes => [];

        public int MaxConcurrentConnections => 1;

        public RateLimitInfo? RateLimit => null;

        public ModelCapacityVector Vector => new();

        public IDictionary<string, float>? GetDynamicCapacities(
            IExecutionConstraints constraints)
        {
            return null;
        }

        public ICapabilityProfile? GetCapabilityProfile()
        {
            return null;
        }

        public bool SupportsOperation(
            string operation)
        {
            return false;
        }

        public bool SupportsDataType(
            string dataType)
        {
            return false;
        }

        public bool SupportsQuantization(
            string quantizationLevel)
        {
            return false;
        }

        public bool SupportsQueryAugmentation => false;

        public bool SupportsQueryDecomposition => false;

        public bool SupportsQueryRouting => false;

        public int MaxQueryParts => 0;

        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        public bool SupportsQueryProcessingOperation(
            string operation)
        {
            return false;
        }

        public bool SupportsEmbedding => false;

        public int? EmbeddingDimensions => null;

        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
