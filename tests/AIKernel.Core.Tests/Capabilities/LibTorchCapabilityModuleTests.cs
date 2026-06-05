namespace AIKernel.Core.Tests.Capabilities;

using AIKernel.Cuda.Libtorch.Cuda13.Capability;
using AIKernel.Cuda.Libtorch.Cuda13.Model;
using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

public sealed class LibTorchCapabilityModuleTests
{
    [Fact]
    public void Create_ReturnsVersionedNativeAbiDescriptor()
    {
        var descriptor = LibTorchCapabilityDescriptor.Create();

        Assert.Equal("libtorch.llama.cuda13.0", descriptor.CapabilityId);
        Assert.Equal("2.12.0", descriptor.Version);
        Assert.Equal(CapabilityModuleKind.NativeLibrary, descriptor.Kind);
        Assert.Equal(CapabilityInvocationMode.NativeAbi, descriptor.InvocationMode);
        Assert.Equal(["forward", "load_model"], descriptor.ProvidedOperations.Order().ToArray());
        Assert.Equal("13.0", descriptor.Metadata["cuda.version"]);
        Assert.Equal("AIKERNEL_LIBTORCH_PATH", descriptor.Metadata["runtime.env"]);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsFailClosedForUnknownOperationWithoutLoadingNativeLibrary()
    {
        var invoker = new LibTorchCapabilityInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-unsupported",
            CapabilityId: LibTorchCapabilityDescriptor.CapabilityId,
            Operation: "unknown",
            Arguments: new Dictionary<string, string>(),
            InputHash: null,
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>());

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LIBTORCH_UNSUPPORTED_OPERATION", result.ErrorCode);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
        Assert.Equal("true", result.Metadata["fail_closed"]);
    }

    [Fact]
    public void TryCreate_ParsesForwardRequestArguments()
    {
        var result = LlamaForwardRequest.TryCreate(
            new Dictionary<string, string>
            {
                ["model_handle"] = "42",
                ["input_ids"] = "1, 2, 3"
            });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(42, result.Value.ModelHandle);
        Assert.Equal([1, 2, 3], result.Value.InputIds);
    }

    [Fact]
    public void TryCreate_RejectsInvalidInputIds()
    {
        var result = LlamaForwardRequest.TryCreate(
            new Dictionary<string, string>
            {
                ["model_handle"] = "42",
                ["input_ids"] = "1, nope"
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains("input_ids", result.ErrorMessage);
    }
}
