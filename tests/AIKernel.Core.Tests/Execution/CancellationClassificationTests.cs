namespace AIKernel.Core.Tests.Execution;

using System.Collections.Immutable;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Core.Execution;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Tokenization;

public sealed class CancellationClassificationTests
{
    public static TheoryData<string, string> CancellationCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var stage in new[] { "Capability", "Prompt", "Provider", "Tokenizer" })
                foreach (var kind in new[] { "operation", "task", "derived", "linked_operation", "linked_task", "linked_derived", "foreign_operation", "foreign_task", "foreign_derived" })
                    cases.Add(stage, kind);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CancellationCases))]
    public async Task EveryExecutionBoundaryRecognizesDerivedAndLinkedCancellation(string stage, string kind)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
        using var foreign = new CancellationTokenSource();
        foreign.Cancel();
        if (kind.StartsWith("linked_", StringComparison.Ordinal)) caller.Cancel();
        var token = kind.StartsWith("linked_", StringComparison.Ordinal) ? linked.Token
            : kind.StartsWith("foreign_", StringComparison.Ordinal) ? foreign.Token : CancellationToken.None;
        Assert.NotEqual(caller.Token, token);
        Exception error = kind.EndsWith("derived", StringComparison.Ordinal) ? new DerivedCancellation(token)
            : kind.EndsWith("task", StringComparison.Ordinal) ? new TaskCanceledException("private cancellation detail", null, token)
            : new OperationCanceledException("private cancellation detail", token);
        var actual = await Run(stage, error, caller.Token);
        var expected = await Run(stage, new OperationCanceledException(), TestContext.Current.CancellationToken);
        Assert.Equal(ExecutionStatus.Canceled, actual.Result.Status);
        Assert.Equal("canceled", actual.Result.Error?.Code);
        Assert.Equal("Execution was canceled.", actual.Result.Error?.Message);
        Assert.Equal(FailureKind.FailClosed.ToString(), actual.Result.Metadata[ReplayMetadataKeys.FailureKind]);
        Assert.Equal(stage, actual.Result.Metadata[ReplayMetadataKeys.OriginStep]);
        Assert.Equal(expected.Calls, actual.Calls);
        Assert.Equal(expected.Result.Metadata.OrderBy(p => p.Key), actual.Result.Metadata.OrderBy(p => p.Key));
        Assert.Equal(expected.Result.Metadata[ReplayMetadataKeys.ReplayLogHash], actual.Result.Metadata[ReplayMetadataKeys.ReplayLogHash]);
        Assert.DoesNotContain("private cancellation detail", string.Join(";", actual.Result.Metadata.Values));
    }

    public static TheoryData<string, string> FailureCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var stage in new[] { "Capability", "Prompt", "Provider", "Tokenizer" })
                foreach (var kind in new[] { "ordinary", "timeout", "wrapped", "aggregate" }) cases.Add(stage, kind);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task CancelingTheCallerDoesNotRelabelAnUnrelatedFailure(string stage, string kind)
    {
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        canceled.Cancel();
        Exception error = kind switch
        {
            "timeout" => new TimeoutException("ordinary failure"),
            "wrapped" => new InvalidOperationException("ordinary failure", new OperationCanceledException()),
            "aggregate" => new AggregateException(new OperationCanceledException(), new IOException("ordinary failure")),
            _ => new InvalidOperationException("ordinary failure")
        };
        var actual = await Run(stage, error, canceled.Token);
        Assert.Equal(ExecutionStatus.Failed, actual.Result.Status);
        Assert.Equal("execution_failed", actual.Result.Error?.Code);
        Assert.Equal(error.GetType().FullName, actual.Result.Metadata[ResultMetadataKeys.ExceptionType]);
        Assert.Equal(stage, actual.Calls[^1]);
    }

    [Theory]
    [InlineData("Prompt")]
    [InlineData("Provider")]
    public async Task AwaitingAnActuallyCanceledLinkedTaskReturnsCanceled(string stage)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller.Token);
        caller.Cancel();
        var calls = new List<string>();
        var executor = Create(stage, null, calls, linked.Token);
        var result = await executor.ExecuteAsync(new Provider(stage, null, calls, linked.Token), Request(), caller.Token);
        Assert.Equal(ExecutionStatus.Canceled, result.Status);
        Assert.Equal("canceled", result.Error?.Code);
        Assert.Equal(stage, calls[^1]);
    }

    private static async Task<(KernelRequestExecutionResult Result, List<string> Calls)> Run(string stage, Exception error, CancellationToken token)
    {
        var calls = new List<string>();
        var executor = Create(stage, error, calls);
        return (await executor.ExecuteAsync(new Provider(stage, error, calls), Request(), token), calls);
    }

    private static KernelExecutor Create(string stage, Exception? error, List<string> calls, CancellationToken taskToken = default)
        => new(new Prompt(stage, error, calls, taskToken), new Resolver(stage, error, calls),
            new Tokenizer(stage, error, calls), KernelClock.Replay(DateTimeOffset.UnixEpoch));

    private static KernelExecutionRequest Request() => new()
    {
        ContextSnapshotId = "snapshot:b4", ContextHash = "sha256:b4", ContextBlocks = [], UserInstruction = "test",
        PromptOptions = TestExecutionDefaults.PromptOptions, ExecutionOptions = TestExecutionDefaults.ExecutionOptions,
        RequestedModelId = "model"
    };

    private sealed class Resolver(string stage, Exception? error, List<string> calls) : IModelPromptCapabilityResolver
    {
        public ModelPromptCapability Resolve(IModelProvider provider, KernelExecutionRequest request)
        {
            calls.Add("Capability"); if (stage == "Capability") throw error!;
            return new() { ProviderId = "test", ModelId = "model", MessageFormat = PromptMessageFormat.ChatMessages,
                MaxInputTokens = 128, MaxOutputTokens = 128, SupportedRoles = ["user"], SupportsSystemMessages = true, SystemInstructionRole = "system" };
        }
    }
    private sealed class Prompt(string stage, Exception? error, List<string> calls, CancellationToken taskToken) : IPromptGenerator
    {
        public Task<GeneratedPrompt> GenerateAsync(PromptGenerationRequest request, CancellationToken cancellationToken = default)
        {
            calls.Add("Prompt");
            if (stage == "Prompt") return error is null ? Task.FromCanceled<GeneratedPrompt>(taskToken) : Task.FromException<GeneratedPrompt>(error);
            return Task.FromResult(new GeneratedPrompt { PromptId = "prompt:b4", PromptHash = "sha256:b4-prompt",
                ContextSnapshotId = request.ContextSnapshotId, ContextHash = request.ContextHash, Capability = request.Capability,
                Messages = [new AIKernel.Dtos.Execution.ModelMessage("user", "test")], EstimatedInputTokens = 1,
                Metadata = ImmutableDictionary<string, string>.Empty });
        }
    }
    private sealed class Tokenizer(string stage, Exception? error, List<string> calls) : ITokenizer
    {
        private readonly SimpleTokenizer _inner = new();
        public string TokenizerProfileId => _inner.TokenizerProfileId;
        public string Name => _inner.Name;
        public IReadOnlyList<Token> Tokenize(string text) => _inner.Tokenize(text);
        public string Decode(IReadOnlyList<Token> tokens) => _inner.Decode(tokens);
        public TokenizerStatistics GetStatistics() => _inner.GetStatistics();
        public bool SupportsModel(string name) => _inner.SupportsModel(name);
        public int GetPhysicalCardinality(int count, string device) => _inner.GetPhysicalCardinality(count, device);
        public PaddingInfo GetPaddingInfo(int count, int cardinality) => _inner.GetPaddingInfo(count, cardinality);
        public int CountTokens(string text) { calls.Add("Tokenizer"); if (stage == "Tokenizer") throw error!; return 1; }
    }
    private sealed class Provider(string stage, Exception? error, List<string> calls, CancellationToken taskToken = default) : IModelProvider
    {
        public string ProviderId => "test";
        public string Name => "test";
        public string Version => "0.1.3";
        public IProviderCapabilities GetCapabilities() => throw new NotSupportedException();
        public Task<bool> IsAvailableAsync() => Task.FromResult(true);
        public Task InitializeAsync() => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
        public Task<ProviderHealthStatus> GetHealthAsync() => Task.FromResult(new ProviderHealthStatus(true, "test", DateTime.UnixEpoch, 0));
        public Task<string> GenerateAsync(IReadOnlyList<IModelMessage> messages, CancellationToken cancellationToken = default)
        {
            calls.Add("Provider");
            return stage == "Provider" ? error is null ? Task.FromCanceled<string>(taskToken) : Task.FromException<string>(error) : Task.FromResult("output");
        }
        public Task StreamGenerateAsync(IReadOnlyList<IModelMessage> messages, Func<string, Task> onChunk, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> AnswerAsync(string question, string? context = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class DerivedCancellation(CancellationToken token) : OperationCanceledException("private cancellation detail", token);
}
