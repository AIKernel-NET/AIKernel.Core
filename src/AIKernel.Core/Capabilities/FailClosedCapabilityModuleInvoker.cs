namespace AIKernel.Core.Capabilities;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

public sealed class FailClosedCapabilityModuleInvoker : ICapabilityModuleInvoker
{
    public const string ErrorCode = "CAPABILITY_MODULE_INVOKER_NOT_CONFIGURED";

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
            ["fail_closed"] = "true",
            ["failure_origin"] = "capability_module_invoker",
            ["operation"] = request.Operation
        };

        var result = new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: ErrorCode,
            ErrorMessage: "No capability module invoker is configured for this host.",
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);

        return ValueTask.FromResult(result);
    }
}
