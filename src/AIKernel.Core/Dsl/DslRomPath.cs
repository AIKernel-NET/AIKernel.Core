namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;
using AIKernel.Core.Vfs.Abstractions;

public static class DslRomPath
{
    private const string CapabilityPrefix = "dsl://";

    public static Result<string> Create(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"rom/dsl/{value.Namespace}/{value.Name}.json");
    }

    public static Result<string> CreateCapabilityName(string @namespace, string name)
    {
        var identity = ValidateIdentity(@namespace, name);
        return identity.Map(value => $"{CapabilityPrefix}{value.Namespace}/{value.Name}");
    }

    public static bool IsDslCapability(string capabilityName)
        => capabilityName is not null &&
           capabilityName.StartsWith(CapabilityPrefix, StringComparison.Ordinal);

    public static Result<(string Namespace, string Name)> ParseCapabilityName(
        string capabilityName)
    {
        if (string.IsNullOrWhiteSpace(capabilityName))
        {
            return Invalid("DSL ROM capability name is required.");
        }

        if (!capabilityName.StartsWith(CapabilityPrefix, StringComparison.Ordinal))
        {
            return Invalid("DSL ROM capability name must start with dsl://.");
        }

        var relative = capabilityName[CapabilityPrefix.Length..];
        var separator = relative.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 || separator == relative.Length - 1)
        {
            return Invalid("DSL ROM capability name must be dsl://{namespace}/{name}.");
        }

        return ValidateIdentity(
            relative[..separator],
            relative[(separator + 1)..]);
    }

    private static Result<(string Namespace, string Name)> ValidateIdentity(
        string @namespace,
        string name)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            return Invalid("DSL ROM namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Invalid("DSL ROM name is required.");
        }

        try
        {
            var normalizedNamespace = VfsPathRules.Normalize(@namespace);
            var normalizedName = VfsPathRules.Normalize(name);

            if (normalizedNamespace.Contains('/', StringComparison.Ordinal) ||
                normalizedName.Contains('/', StringComparison.Ordinal))
            {
                return Invalid("DSL ROM namespace and name must be single path segments.");
            }

            return Result<(string Namespace, string Name)>.Success(
                (normalizedNamespace, normalizedName));
        }
        catch (ArgumentException ex)
        {
            return Invalid(ex.Message);
        }
    }

    private static Result<(string Namespace, string Name)> Invalid(string message)
        => Result<(string Namespace, string Name)>.Fail(new ErrorContext(
            message,
            "DSL_ROM_ERROR",
            false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.G
        });
}
