namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using AIKernel.Common.Results;

internal static class DslExecutionErrors
{
    /// <summary>
    /// EN: Executes InvalidRuntime.
    /// [EN] Documents this public package API member. [JA] InvalidRuntime を実行します。
    /// </summary>
    public static ErrorContext InvalidRuntime(string message)
        => new(message, "DSL_RUNTIME_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };
    /// <summary>
    /// EN: Executes PredicateRejected.
    /// [EN] Documents this public package API member. [JA] PredicateRejected を実行します。
    /// </summary>

    public static ErrorContext PredicateRejected(string message)
        => new(message, "DSL_PREDICATE_REJECTED", false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };
    /// <summary>
    /// EN: Gets CapabilityException.
    /// [EN] Documents this public package API member. [JA] CapabilityException を取得します。
    /// </summary>

    public static ErrorContext CapabilityException(
        string capabilityName,
        Exception exception)
        => CapabilityException(capabilityName, ErrorContext.FromException(exception));
    /// <summary>
    /// EN: Gets CapabilityException.
    /// [EN] Documents this public package API member. [JA] CapabilityException を取得します。
    /// </summary>

    public static ErrorContext CapabilityException(
        string capabilityName,
        ErrorContext source)
    {
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
    /// <summary>
    /// EN: Gets CapabilityReturnedNull.
    /// [EN] Documents this public package API member. [JA] CapabilityReturnedNull を取得します。
    /// </summary>

    public static ErrorContext CapabilityReturnedNull(
        string capabilityName)
    {
        return InvalidPipelineValue(
            "Capability returned a successful null DSL value.",
            OriginStep.Capability,
            capabilityName);
    }
    /// <summary>
    /// EN: Gets InvalidPipelineValue.
    /// [EN] Documents this public package API member. [JA] InvalidPipelineValue を取得します。
    /// </summary>

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
    /// <summary>
    /// EN: Gets ClockException.
    /// [EN] Documents this public package API member. [JA] ClockException を取得します。
    /// </summary>

    public static ErrorContext ClockException(
        Exception exception,
        SemanticDelta loopDelta)
        => ClockException(ErrorContext.FromException(exception), loopDelta);
    /// <summary>
    /// EN: Gets ClockException.
    /// [EN] Documents this public package API member. [JA] ClockException を取得します。
    /// </summary>

    public static ErrorContext ClockException(
        ErrorContext source,
        SemanticDelta loopDelta)
    {
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
