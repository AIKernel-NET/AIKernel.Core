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

    public static ErrorContext CapabilityReturnedNull(
        string capabilityName)
    {
        return InvalidPipelineValue(
            "Capability returned a successful null DSL value.",
            OriginStep.Capability,
            capabilityName);
    }

    public static ErrorContext InvalidPipelineValue(
        string message,
        OriginStep originStep,
        string? capabilityName = null)
    {
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(capabilityName))
        {
            metadata["dsl.capability_name"] = capabilityName;
        }

        return new ErrorContext(message, "DSL_RUNTIME_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = originStep,
            SemanticSlot = SemanticSlot.T,
            Metadata = metadata.ToImmutable()
        };
    }

    public static ErrorContext ClockException(
        Exception exception,
        SemanticDelta loopDelta)
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

        if (loopDelta.Metadata is not null)
        {
            foreach (var item in loopDelta.Metadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return source with
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T,
            Metadata = metadata.ToImmutable()
        };
    }
}
