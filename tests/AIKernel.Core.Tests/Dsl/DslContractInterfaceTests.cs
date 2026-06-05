namespace AIKernel.Core.Tests.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Dsl;
using ContractDsl = AIKernel.Abstractions.Dsl;
using DslDtos = AIKernel.Dtos.Dsl;

public sealed class DslContractInterfaceTests
{
    [Fact]
    public async Task Compiler_can_be_used_through_contract_interface()
    {
        ContractDsl.IDslPipelineCompiler compiler =
            new DslPipelineCompiler(new TestCapabilityRegistry());

        var pipeline = await compiler.CompileAsync(new DslDtos.DslDocument(
            new DslDtos.PipelineRootNode([
                new DslDtos.StepNode("start"),
                new DslDtos.CallCapabilityNode(
                    "echo",
                    new Dictionary<string, string>
                    {
                        ["value"] = "from-contract"
                    })
            ])),
            TestContext.Current.CancellationToken);

        var result = await pipeline.ExecuteAsync(new DslDtos.DslPipelineExecutionContext(
            new DslDtos.DslPipelineValue(new Dictionary<string, string>()),
            DateTimeOffset.UnixEpoch),
            TestContext.Current.CancellationToken);

        Assert.Equal("from-contract", result.Output.Data["value"]);
        Assert.True(result.ReplayLogCount >= 2);
        Assert.False(string.IsNullOrWhiteSpace(result.ReplayLogHash));
    }

    [Fact]
    public async Task Capability_registry_can_be_used_through_contract_interface()
    {
        ContractDsl.IDslCapabilityRegistry registry = new DslRomCapabilityRegistry(
            new TestCapabilityRegistry(),
            new DslRomRegistry());

        var result = await registry.InvokeAsync(
            "echo",
            new DslDtos.DslPipelineValue(new Dictionary<string, string>()),
            new Dictionary<string, string>
            {
                ["value"] = "contract-call"
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("contract-call", result.Data["value"]);
    }

    private sealed class TestCapabilityRegistry : IDslCapabilityRegistry
    {
        public bool Contains(string name) => name == "echo";

        public Result<DslPipelineValue> Invoke(
            string name,
            DslPipelineValue input,
            IReadOnlyDictionary<string, string> args)
        {
            if (name != "echo")
            {
                return Result<DslPipelineValue>.Fail(new ErrorContext(
                    "Unknown capability.",
                    "UNKNOWN_CAPABILITY",
                    false));
            }

            return Result<DslPipelineValue>.Success(
                input.With("value", args["value"]));
        }
    }
}
