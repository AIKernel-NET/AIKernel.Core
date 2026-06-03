namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;

internal static class DslExecutionErrors
{
    public static ErrorContext InvalidRuntime(string message)
        => new(message, "DSL_RUNTIME_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };

    public static ErrorContext CapabilityException(
        string capabilityName,
        Exception exception)
    {
        var source = ErrorContext.FromException(exception);
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (source.Metadata is not null)
        {
            foreach (var item in source.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        metadata["dsl.capability_name"] = capabilityName;

        return source with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T,
            Metadata = metadata.ToImmutable()
        };
    }
}
