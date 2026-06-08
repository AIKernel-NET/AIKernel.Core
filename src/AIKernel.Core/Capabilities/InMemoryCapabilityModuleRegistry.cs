namespace AIKernel.Core.Capabilities;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Dtos.Capabilities;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry']" />
public sealed class InMemoryCapabilityModuleRegistry : ICapabilityModuleRegistry
{
    private readonly ConcurrentDictionary<string, CapabilityModuleDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.RegisterAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.RegisterAsync']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ResolveAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ResolveAsync']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ListAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ListAsync']" />
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
