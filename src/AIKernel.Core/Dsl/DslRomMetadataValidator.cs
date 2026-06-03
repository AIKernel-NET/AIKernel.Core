namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslRomMetadataValidator
{
    public static ErrorContext? ValidateCanonicalIdentity(DslRomMetadata metadata)
    {
        var parsed = DslRomPath.ParseCapabilityName(metadata.CapabilityName);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var expectedCapabilityName = DslRomPath.CreateCapabilityName(
            metadata.Namespace,
            metadata.Name);
        if (expectedCapabilityName.IsFailure)
        {
            return expectedCapabilityName.Error!;
        }

        if (!string.Equals(
                expectedCapabilityName.Value,
                metadata.CapabilityName,
                StringComparison.Ordinal))
        {
            return Error("DSL ROM capability name must match dsl://{namespace}/{name}.");
        }

        var expectedPath = DslRomPath.Create(metadata.Namespace, metadata.Name);
        if (expectedPath.IsFailure)
        {
            return expectedPath.Error!;
        }

        return string.Equals(
            expectedPath.Value,
            metadata.Path,
            StringComparison.Ordinal)
            ? null
            : Error("DSL ROM path must match rom/dsl/{namespace}/{name}.json.");
    }

    private static ErrorContext Error(string message)
        => new(message, "DSL_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        };
}
