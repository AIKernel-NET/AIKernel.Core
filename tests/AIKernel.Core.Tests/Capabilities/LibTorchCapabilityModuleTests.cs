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
        Assert.Equal(["forward", "load_model", "unload_model"], descriptor.ProvidedOperations.Order().ToArray());
        Assert.Equal("cdecl", descriptor.Metadata["abi.calling_convention"]);
        Assert.Equal("libtorch_bridge", descriptor.Metadata["abi.library"]);
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
    public async Task InvokeAsync_ReturnsFailClosedForCapabilityIdMismatch()
    {
        var invoker = new LibTorchCapabilityInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-mismatched-capability",
            CapabilityId: "other.capability",
            Operation: "load_model",
            Arguments: new Dictionary<string, string>
            {
                ["path"] = "model.pt"
            },
            InputHash: null,
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>());

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LIBTORCH_CAPABILITY_ID_MISMATCH", result.ErrorCode);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
        Assert.Equal("true", result.Metadata["fail_closed"]);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsFailClosedWhenNativeLoadCannotExecute()
    {
        var invoker = new LibTorchCapabilityInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-load-missing-native",
            CapabilityId: LibTorchCapabilityDescriptor.CapabilityId,
            Operation: "load_model",
            Arguments: new Dictionary<string, string>
            {
                ["path"] = "missing-model.pt"
            },
            InputHash: null,
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>());

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
        Assert.Equal("true", result.Metadata["fail_closed"]);
        Assert.Equal("libtorch_native_abi", result.Metadata["failure_origin"]);
    }

    [Fact]
    public async Task InvokeAsync_RejectsInvalidUnloadModelHandle()
    {
        var invoker = new LibTorchCapabilityInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-unload-invalid-handle",
            CapabilityId: LibTorchCapabilityDescriptor.CapabilityId,
            Operation: "unload_model",
            Arguments: new Dictionary<string, string>
            {
                ["model_handle"] = "0"
            },
            InputHash: null,
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>());

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("LIBTORCH_MODEL_HANDLE_INVALID", result.ErrorCode);
        Assert.Equal("true", result.Metadata["fail_closed"]);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsFailClosedWhenNativeUnloadCannotExecute()
    {
        var invoker = new LibTorchCapabilityInvoker();
        var request = new CapabilityInvocationRequest(
            InvocationId: "invoke-unload-missing-native",
            CapabilityId: LibTorchCapabilityDescriptor.CapabilityId,
            Operation: "unload_model",
            Arguments: new Dictionary<string, string>
            {
                ["model_handle"] = "42"
            },
            InputHash: null,
            ReplayLogHash: "sha256:replay",
            Metadata: new Dictionary<string, string>());

        var result = await invoker.InvokeAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("sha256:replay", result.ReplayLogHash);
        Assert.Equal("true", result.Metadata["fail_closed"]);
        Assert.Equal("libtorch_native_abi", result.Metadata["failure_origin"]);
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

    [Fact]
    public void TryCreate_RejectsEmptyInputIds()
    {
        var result = LlamaForwardRequest.TryCreate(
            new Dictionary<string, string>
            {
                ["model_handle"] = "42",
                ["input_ids"] = ", ,"
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains("at least one", result.ErrorMessage);
    }

    [Fact]
    public void TryCreate_RejectsInputIdsOverLimit()
    {
        var inputIds = string.Join(
            ",",
            Enumerable.Range(0, LlamaForwardRequest.MaxInputTokens + 1));

        var result = LlamaForwardRequest.TryCreate(
            new Dictionary<string, string>
            {
                ["model_handle"] = "42",
                ["input_ids"] = inputIds
            });

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains("at most", result.ErrorMessage);
    }
}
