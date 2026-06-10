namespace AIKernel.Core.Providers.MinimalRuntimeProvider;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

/// <summary>
/// [EN] Deterministic no-op invoker for minimal runtime health and boot validation.
/// [JA] minimal runtime の health / boot 検証用の決定論的 no-op invoker です。
/// </summary>
public sealed class MinimalRuntimeInvoker : ICapabilityModuleInvoker
{
    private const string OutputPayload = "{\"status\":\"ok\"}";

    /// <summary>
    /// [EN] Executes the InvokeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InvokeAsync 操作を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(
            request.Metadata,
            StringComparer.Ordinal)
        {
            ["provider"] = MinimalRuntimeProvider.ProviderIdValue,
            ["operation"] = request.Operation,
            ["status"] = "ok"
        };

        return ValueTask.FromResult(UnsupportedOperation(request).Match(
            () => CreateSuccess(request, metadata),
            error => CreateFailure(request, metadata, error.Code, error.Message)));
    }

    private static CapabilityInvocationResult CreateSuccess(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata)
        => new(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OutputPayload)))
                .ToLowerInvariant(),
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

    private static CapabilityInvocationResult CreateFailure(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata,
        string code,
        string message)
        => new(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: code,
            ErrorMessage: message,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);

    private static Option<MonadicError> UnsupportedOperation(
        CapabilityInvocationRequest request)
        => MonadicDecision.ErrorUnless(
            IsSupported(request),
            "MINIMAL_RUNTIME_UNSUPPORTED_OPERATION",
            "MinimalRuntimeInvoker only supports aikernel.runtime.ping / runtime.ping.");

    private static bool IsSupported(CapabilityInvocationRequest request)
        => string.Equals(request.CapabilityId, "aikernel.runtime.ping", StringComparison.Ordinal) &&
           string.Equals(request.Operation, "runtime.ping", StringComparison.Ordinal);
}
