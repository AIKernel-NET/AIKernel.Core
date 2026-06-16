namespace AIKernel.Core.Capabilities;

using System.Collections.Concurrent;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;

/// <summary>EN: Documentation for public API. JA: InMemoryCapabilityModuleRegistry を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry']/summary" />
public sealed class InMemoryCapabilityModuleRegistry : ICapabilityModuleRegistry
{
    private readonly ConcurrentDictionary<string, CapabilityModuleDescriptor> _descriptors =
        new(StringComparer.Ordinal);

    /// <summary>EN: Documentation for public API. JA: RegisterAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.RegisterAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.RegisterAsync']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: ResolveAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ResolveAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ResolveAsync']/summary" />
    public ValueTask<CapabilityModuleDescriptor?> ResolveAsync(
        string capabilityId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = ReadCapabilityId(capabilityId)
            .Bind(FindDescriptor);

        return ValueTask.FromResult(
            CloneOrNull(descriptor));
    }

    /// <summary>EN: Documentation for public API. JA: ListAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ListAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Capabilities.InMemoryCapabilityModuleRegistry.ListAsync']/summary" />
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

    private Option<CapabilityModuleDescriptor> FindDescriptor(
        string capabilityId)
    {
        if (_descriptors.TryGetValue(NormalizeCapabilityId(capabilityId), out var descriptor))
        {
            return Option<CapabilityModuleDescriptor>.Some(descriptor);
        }

        return Option<CapabilityModuleDescriptor>.None();
    }

    private static Option<string> ReadCapabilityId(
        string capabilityId)
    {
        if (!string.IsNullOrWhiteSpace(capabilityId))
        {
            return Option<string>.Some(capabilityId);
        }

        return Option<string>.None();
    }

    private static CapabilityModuleDescriptor? CloneOrNull(
        Option<CapabilityModuleDescriptor> descriptor)
        => descriptor.Match<CapabilityModuleDescriptor?>(
            () => null,
            Clone);

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
