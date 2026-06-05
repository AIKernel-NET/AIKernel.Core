namespace AIKernel.Cuda.Libtorch.Cuda13.Capability;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Cuda.Libtorch.Cuda13.Interop;
using AIKernel.Cuda.Libtorch.Cuda13.Model;
using AIKernel.Dtos.Capabilities;

public sealed class LibTorchCapabilityInvoker : ICapabilityModuleInvoker
{
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        return request.Operation switch
        {
            "load_model" => LoadModel(request, cancellationToken),
            "forward" => Forward(request, cancellationToken),
            _ => ValueTask.FromResult(Fail(
                request,
                "LIBTORCH_UNSUPPORTED_OPERATION",
                $"Unsupported LibTorch operation '{request.Operation}'."))
        };
    }

    private static ValueTask<CapabilityInvocationResult> LoadModel(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!request.Arguments.TryGetValue("path", out var path) ||
            string.IsNullOrWhiteSpace(path))
        {
            return ValueTask.FromResult(Fail(
                request,
                "LIBTORCH_MODEL_PATH_REQUIRED",
                "Argument 'path' is required for load_model."));
        }

        var statusOrHandle = NativeMethods.LoadModel(path);

        if (statusOrHandle <= 0)
        {
            return ValueTask.FromResult(Fail(
                request,
                "LIBTORCH_LOAD_MODEL_FAILED",
                $"Native load_model failed with status {statusOrHandle}."));
        }

        var metadata = CreateMetadata(request);
        metadata["model_handle"] = statusOrHandle.ToString(CultureInfo.InvariantCulture);
        metadata["model_path_hash"] = Hash(path);

        return ValueTask.FromResult(Success(
            request,
            outputHash: Hash($"load_model:{statusOrHandle}:{path}"),
            metadata));
    }

    private static ValueTask<CapabilityInvocationResult> Forward(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parse = LlamaForwardRequest.TryCreate(request.Arguments);

        if (!parse.Succeeded || parse.Value is null)
        {
            return ValueTask.FromResult(Fail(
                request,
                "LIBTORCH_FORWARD_REQUEST_INVALID",
                parse.ErrorMessage ?? "Invalid forward request."));
        }

        var forwardRequest = parse.Value;
        var nativeResult = new ForwardResultNative();
        var status = NativeMethods.Forward(
            forwardRequest.ModelHandle,
            forwardRequest.InputIds,
            forwardRequest.InputIds.Length,
            out nativeResult);

        if (status != NativeStatus.Success)
        {
            return ValueTask.FromResult(Fail(
                request,
                "LIBTORCH_FORWARD_FAILED",
                $"Native forward failed with status {status}."));
        }

        var result = LlamaForwardResult.FromNative(nativeResult);
        var metadata = CreateMetadata(request);
        metadata["output_tokens"] = string.Join(",", result.OutputTokenIds);
        metadata["logits_count"] = result.Logits.Count.ToString(CultureInfo.InvariantCulture);

        return ValueTask.FromResult(Success(
            request,
            outputHash: result.OutputHash,
            metadata));
    }

    private static CapabilityInvocationResult Success(
        CapabilityInvocationRequest request,
        string outputHash,
        IReadOnlyDictionary<string, string> metadata)
    {
        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: outputHash,
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);
    }

    private static CapabilityInvocationResult Fail(
        CapabilityInvocationRequest request,
        string errorCode,
        string errorMessage)
    {
        var metadata = CreateMetadata(request);
        metadata["fail_closed"] = "true";
        metadata["failure_origin"] = "libtorch_native_abi";

        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);
    }

    private static Dictionary<string, string> CreateMetadata(
        CapabilityInvocationRequest request)
    {
        var metadata = new Dictionary<string, string>(
            request.Metadata,
            StringComparer.Ordinal)
        {
            ["capability"] = LibTorchCapabilityDescriptor.CapabilityId,
            ["native_library"] = NativeMethods.LibraryName,
            ["operation"] = request.Operation
        };

        return metadata;
    }

    private static string Hash(
        string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
