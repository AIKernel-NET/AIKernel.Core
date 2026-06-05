namespace AIKernel.Core.Capabilities;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

public sealed class InMemoryCapabilityModuleRegistry : ICapabilityModuleRegistry
{
    private readonly ConcurrentDictionary<string, CapabilityModuleDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    public ValueTask RegisterAsync(
        CapabilityModuleDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.CapabilityId);

        var normalizedCapabilityId = NormalizeCapabilityId(descriptor.CapabilityId);

        _descriptors[normalizedCapabilityId] = Clone(
            descriptor with
            {
                CapabilityId = normalizedCapabilityId
            });

        return ValueTask.CompletedTask;
    }

    public ValueTask<CapabilityModuleDescriptor?> ResolveAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            return ValueTask.FromResult<CapabilityModuleDescriptor?>(null);
        }

        _descriptors.TryGetValue(
            NormalizeCapabilityId(capabilityId),
            out var descriptor);

        return ValueTask.FromResult(
            descriptor is null ? null : Clone(descriptor));
    }

    public ValueTask<IReadOnlyList<CapabilityModuleDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<CapabilityModuleDescriptor> descriptors = _descriptors
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Clone(x.Value))
            .ToArray();

        return ValueTask.FromResult(descriptors);
    }

    private static string NormalizeCapabilityId(
        string capabilityId)
    {
        return capabilityId.Trim();
    }

    private static CapabilityModuleDescriptor Clone(
        CapabilityModuleDescriptor descriptor)
    {
        var providedOperations = (descriptor.ProvidedOperations ?? [])
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var requiredPermissions = (descriptor.RequiredPermissions ?? [])
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var metadata = (descriptor.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.Ordinal);

        return descriptor with
        {
            ProvidedOperations = providedOperations,
            RequiredPermissions = requiredPermissions,
            Metadata = metadata
        };
    }
}
