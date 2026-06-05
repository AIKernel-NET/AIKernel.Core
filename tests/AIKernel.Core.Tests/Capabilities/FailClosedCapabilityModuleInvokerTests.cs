namespace AIKernel.Core.Tests.Capabilities;

using AIKernel.Core.Capabilities;
using AIKernel.Dtos.Capabilities;

public sealed class FailClosedCapabilityModuleInvokerTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsFailClosedResultWithoutExecutingModule()
    {
        var invoker = new FailClosedCapabilityModuleInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-1",
            CapabilityId: "aik.tools.observe",
            Operation: "Observe",
            Arguments: new Dictionary<string, string>
            {
                ["input"] = "context"
            },
            InputHash: "sha256:input",
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>
            {
                ["caller"] = "test"
            });

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("invoke-1", result.InvocationId);
        Assert.Equal("aik.tools.observe", result.CapabilityId);
        Assert.Null(result.OutputHash);
        Assert.Equal(FailClosedCapabilityModuleInvoker.ErrorCode, result.ErrorCode);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
        Assert.Equal("test", result.Metadata["caller"]);
        Assert.Equal("true", result.Metadata["fail_closed"]);
        Assert.Equal("capability_module_invoker", result.Metadata["failure_origin"]);
        Assert.Equal("Observe", result.Metadata["operation"]);
    }
}
