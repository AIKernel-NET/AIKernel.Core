namespace AIKernel.Core.Capabilities;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker']/summary" />
public sealed class FailClosedCapabilityModuleInvoker : ICapabilityModuleInvoker
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker.ErrorCode']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker.ErrorCode']/summary" />
    public const string ErrorCode = "CAPABILITY_MODULE_INVOKER_NOT_CONFIGURED";

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker.InvokeAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.FailClosedCapabilityModuleInvoker.InvokeAsync']/summary" />
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
