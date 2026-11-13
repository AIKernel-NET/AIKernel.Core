namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Core.Dsl;
using AIKernel.Core.Providers.LocalExecutionProvider;
using ContractDsl = AIKernel.Abstractions.Dsl;
using DslDtos = AIKernel.Dtos.Dsl;

public sealed partial class DslAsyncExecutionTests
{
    [Theory]
    [InlineData(true, "LOCAL_EXECUTION_COMPILE_FAILED")]
    [InlineData(false, "LOCAL_EXECUTION_EXECUTE_FAILED")]
    public async Task LocalExecutionKeepsOrdinaryCompilerAndPipelineFailures(bool duringCompile, string expectedCode)
    {
        var invoker = new LocalExecutionInvoker(new ThrowingCompiler(duringCompile));
        var result = await invoker.InvokeAsync(Request("""{"type":"Pipeline","steps":[]}"""), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal("Expected ordinary failure", result.ErrorMessage);
    }

    [Fact]
    public async Task LocalExecutionKeepsInputEnumerationExceptionsAsFailures()
    {
        var request = Request("""{"type":"Pipeline","steps":[]}""");
        request = request with { Arguments = new ThrowingArguments(request.Arguments) };
        var invoker = new LocalExecutionInvoker(new DslPipelineCompiler(new FailClosedDslCapabilityRegistry()));
        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal("UNHANDLED_EXCEPTION", result.ErrorCode);
        Assert.Equal("Expected enumeration failure", result.ErrorMessage);
    }

    private sealed class ThrowingCompiler(bool duringCompile) : ContractDsl.IDslPipelineCompiler
    {
        public Task<ContractDsl.IKernelPipeline> CompileAsync(DslDtos.DslDocument document, CancellationToken cancellationToken = default)
            => duringCompile ? Task.FromException<ContractDsl.IKernelPipeline>(new InvalidOperationException("Expected ordinary failure"))
                             : Task.FromResult<ContractDsl.IKernelPipeline>(new ThrowingPipeline());
    }
    private sealed class ThrowingPipeline : ContractDsl.IKernelPipeline
    {
        public Task<DslDtos.DslPipelineExecutionResult> ExecuteAsync(DslDtos.DslPipelineExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromException<DslDtos.DslPipelineExecutionResult>(new InvalidOperationException("Expected ordinary failure"));
    }
    private sealed class ThrowingArguments(IReadOnlyDictionary<string, string> source) : IReadOnlyDictionary<string, string>
    {
        public string this[string key] => source[key];
        public IEnumerable<string> Keys => source.Keys;
        public IEnumerable<string> Values => source.Values;
        public int Count => source.Count;
        public bool ContainsKey(string key) => source.ContainsKey(key);
        public bool TryGetValue(string key, out string value) => source.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => throw new InvalidOperationException("Expected enumeration failure");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
