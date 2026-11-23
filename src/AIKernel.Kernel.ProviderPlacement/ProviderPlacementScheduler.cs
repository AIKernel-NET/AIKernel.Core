using System.Collections.Immutable;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;

namespace AIKernel.Kernel.Providers;

/// <summary>[EN] Deterministic v1 provider placement. [JA] 決定的な v1 Provider 配置。</summary>
public sealed class ProviderPlacementScheduler(TimeProvider? timeProvider = null) : IProviderPlacementScheduler
{
    private static readonly StringComparer IdComparer = StringComparer.Ordinal;
    private static readonly StringComparer CapabilityComparer = StringComparer.OrdinalIgnoreCase;
    private const ExecutionMode SupportedModes = ExecutionMode.Single | ExecutionMode.Batch | ExecutionMode.Streaming;

    /// <inheritdoc />
    public ValueTask<PlacementDecision> ResolvePlacementAsync(
        ProviderRequirements requirements,
        IReadOnlyList<NodeCapabilityDescriptor> nodes,
        IReadOnlyDictionary<string, NodeProviderRegistrationSnapshot> registrations,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(registrations);
        if (string.IsNullOrWhiteSpace(requirements.ProviderId) || string.IsNullOrWhiteSpace(requirements.ModelId) ||
            requirements.WorkingSetBytes < 0 || !ValidModes(requirements.RequiredMode) ||
            requirements.RequiredModalities is null || !requirements.RequiredModalities.All(Enum.IsDefined) ||
            requirements.RequiredFeatures is null || requirements.RequiredIsaExtensions is null)
            throw new ProviderOperationException(new ExecutionError(
                ProviderErrorCodes.InvalidRequest, "Provider placement requirements are invalid."));

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var registeredByNodeId = registrations.ToDictionary(pair => pair.Key, pair => pair.Value, IdComparer);
        var candidates = new List<RankedCandidate>();
        var exclusions = ImmutableArray.CreateBuilder<PlacementExclusion>();
        foreach (var group in nodes.OrderBy(node => node?.NodeId, IdComparer).GroupBy(node => node?.NodeId, IdComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = group.First();
            var reason = group.Count() == 1
                ? CheckNode(node, requirements, registeredByNodeId, now, candidates)
                : PlacementReasonCodes.DescriptorInvalid;
            if (reason is not null)
                exclusions.Add(Exclude(node?.NodeId ?? "", reason));
        }

        if (candidates.Count == 0 && exclusions.Count == 0)
            exclusions.Add(Exclude("", PlacementReasonCodes.NoNodes));

        var ordered = candidates
            .OrderByDescending(entry => entry.ModelResident)
            .ThenByDescending(entry => entry.Candidate.Target == ExecutionTarget.Cuda)
            .ThenBy(entry => entry.Load.ActiveJobs)
            .ThenBy(entry => entry.Load.QueuedJobs)
            .ThenBy(entry => entry.Candidate.NodeId, IdComparer)
            .ThenBy(entry => entry.Candidate.GpuDeviceId, IdComparer)
            .Select(entry => entry.Candidate)
            .ToImmutableArray();
        return ValueTask.FromResult(new PlacementDecision
        {
            Selected = ordered.FirstOrDefault(),
            Candidates = ordered,
            Exclusions = exclusions.ToImmutable()
        });
    }

    /// <summary>[EN] Turns an empty decision into a public operation failure. [JA] 候補ゼロを公開エラーに変換します。</summary>
    public static PlacementCandidate RequireCandidate(PlacementDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return decision.Selected ?? throw new ProviderOperationException(new ExecutionError(
            ProviderErrorCodes.NoCandidateNode, "No eligible node is available for this provider.",
            string.Join(", ", decision.Exclusions.Select(exclusion => exclusion.ReasonCode).Distinct(IdComparer))));
    }

    private static string? CheckNode(
        NodeCapabilityDescriptor? node,
        ProviderRequirements request,
        Dictionary<string, NodeProviderRegistrationSnapshot> registrations,
        DateTimeOffset now,
        List<RankedCandidate> candidates)
    {
        if (node is null || string.IsNullOrWhiteSpace(node.NodeId))
            return PlacementReasonCodes.DescriptorInvalid;
        if (node.SchemaVersion != ProviderPlacementAbi.SchemaVersion)
            return PlacementReasonCodes.DescriptorSchemaUnknown;
        if (!IsFresh(node.ObservedAtUtc, now))
            return PlacementReasonCodes.DescriptorStale;
        if (!registrations.TryGetValue(node.NodeId, out var snapshot))
            return PlacementReasonCodes.ProviderNotRegistered;
        if (snapshot is null || snapshot.Providers is null || !ValidNode(node))
            return PlacementReasonCodes.DescriptorInvalid;
        if (!IsFresh(snapshot.ObservedAtUtc, now))
            return PlacementReasonCodes.DescriptorStale;

        var providers = snapshot.Providers.Where(entry =>
            IdComparer.Equals(entry?.Descriptor?.ProviderId, request.ProviderId)).ToArray();
        if (providers.Length == 0)
            return PlacementReasonCodes.ProviderNotRegistered;
        if (providers.Length != 1)
            return PlacementReasonCodes.DescriptorInvalid;

        var registration = providers[0];
        var provider = registration.Descriptor;
        if (provider.SchemaVersion != ProviderPlacementAbi.SchemaVersion)
            return PlacementReasonCodes.DescriptorSchemaUnknown;
        if (!ValidProvider(provider) || registration.ResidentModelIds is null)
            return PlacementReasonCodes.DescriptorInvalid;
        if (!provider.ModelIds.Contains(request.ModelId, IdComparer))
            return PlacementReasonCodes.ProviderNotRegistered;
        if (!request.RequiredModalities.All(provider.InputModalities.Contains))
            return PlacementReasonCodes.ModalityUnsupported;
        if ((provider.Modes & request.RequiredMode) != request.RequiredMode)
            return PlacementReasonCodes.ModeUnsupported;
        if (!request.RequiredFeatures.All(name => provider.Extension?.Features.Contains(name, IdComparer) == true))
            return PlacementReasonCodes.FeatureUnsupported;
        if (!request.RequiredIsaExtensions.All(isa => node.Cpu.IsaExtensions.Contains(isa, CapabilityComparer)))
            return PlacementReasonCodes.IsaUnsupported;
        if (request.Precision is not null && !provider.Precisions.Contains(request.Precision, CapabilityComparer))
            return PlacementReasonCodes.PrecisionUnsupported;

        var workingSet = Math.Max(request.WorkingSetBytes, provider.Memory.WorkingSetBytes);
        var swap = node.MemoryTiers.SingleOrDefault(tier => CapabilityComparer.Equals(tier.Tier, "Swap"))?.AvailableBytes ?? 0;
        if (workingSet - node.Memory.RamAvailableBytes > swap)
            return PlacementReasonCodes.MemoryInsufficient;

        var resident = registration.ResidentModelIds.Contains(request.ModelId, IdComparer);
        candidates.Add(new(new PlacementCandidate { NodeId = node.NodeId, Target = ExecutionTarget.Cpu }, resident, node.Load));
        if (provider.ExecutionTargets.Contains(ExecutionTarget.Cuda))
        {
            foreach (var gpu in node.Gpus.Where(gpu => !string.IsNullOrWhiteSpace(gpu.CudaComputeCapability)))
                candidates.Add(new(new PlacementCandidate
                {
                    NodeId = node.NodeId, Target = ExecutionTarget.Cuda, GpuDeviceId = gpu.DeviceId
                }, resident, node.Load));
        }
        return null;
    }

    private static bool IsFresh(DateTimeOffset timestamp, DateTimeOffset now) =>
        timestamp <= now && now - timestamp <= ProviderPlacementAbi.SnapshotLifetime;

    private static bool ValidModes(ExecutionMode modes) => modes != ExecutionMode.None && (modes & ~SupportedModes) == 0;

    private static bool ValidNode(NodeCapabilityDescriptor node) =>
        node.Cpu is { LogicalCores: > 0, IsaExtensions: not null } &&
        node.Load is { ActiveJobs: >= 0, QueuedJobs: >= 0 } &&
        node.Memory is { RamTotalBytes: >= 0, RamAvailableBytes: >= 0 } memory &&
        memory.RamAvailableBytes <= memory.RamTotalBytes &&
        node.Gpus is not null && node.Gpus.All(gpu => gpu is not null && !string.IsNullOrWhiteSpace(gpu.DeviceId)) &&
        node.Gpus.Select(gpu => gpu.DeviceId).Distinct(IdComparer).Count() == node.Gpus.Count &&
        node.MemoryTiers is not null && node.MemoryTiers.All(tier => tier is not null &&
            tier.CapacityBytes is null or >= 0 && tier.AvailableBytes is null or >= 0 &&
            !(tier.AvailableBytes > tier.CapacityBytes)) &&
        node.MemoryTiers.Select(tier => tier.Tier).Distinct(CapabilityComparer).Count() == node.MemoryTiers.Count;

    private static bool ValidProvider(ProviderCapabilityDescriptor provider) =>
        Enum.IsDefined(provider.Kind) && ValidModes(provider.Modes) &&
        provider.InputModalities is not null && provider.InputModalities.All(Enum.IsDefined) &&
        provider.ExecutionTargets is not null && provider.ExecutionTargets.Contains(ExecutionTarget.Cpu) &&
        provider.ExecutionTargets.All(Enum.IsDefined) && provider.ModelIds is not null &&
        provider.Precisions is not null && provider.Memory is { WorkingSetBytes: >= 0, Basis: "measured" or "estimated" } &&
        (provider.Extension is null || provider.Extension.Kind == provider.Kind && provider.Extension.Features is not null) &&
        provider.Metadata is not null && provider.Metadata.All(IsPublicMetadata);

    private static bool IsPublicMetadata(KeyValuePair<string, string> field)
    {
        var value = field.Value;
        if (value is null || value.StartsWith('/') || value.StartsWith('\\') ||
            value.Length >= 2 && char.IsAsciiLetter(value[0]) && value[1] == ':' ||
            value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("-----BEGIN ", StringComparison.Ordinal) ||
            value.StartsWith("sk-", StringComparison.Ordinal) ||
            value.StartsWith("hf_", StringComparison.Ordinal) ||
            value.StartsWith("ghp_", StringComparison.Ordinal))
            return false;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.IsFile || uri.UserInfo.Length != 0 || uri.Query.Length != 0))
            return false;
        return !field.Key.Split('.', '-', '_', ' ').Any(part =>
            CapabilityComparer.Equals(part, "token") || CapabilityComparer.Equals(part, "secret") ||
            CapabilityComparer.Equals(part, "credential") || CapabilityComparer.Equals(part, "password") ||
            CapabilityComparer.Equals(part, "apikey") || CapabilityComparer.Equals(part, "authorization"));
    }

    private static PlacementExclusion Exclude(string nodeId, string reason) => new()
    {
        NodeId = nodeId,
        ReasonCode = reason,
        Message = reason switch
        {
            PlacementReasonCodes.ProviderNotRegistered => "The provider or model is not registered on this node.",
            PlacementReasonCodes.ModalityUnsupported => "The provider does not support the requested modality.",
            PlacementReasonCodes.ModeUnsupported => "The provider does not support the requested execution mode.",
            PlacementReasonCodes.FeatureUnsupported => "The provider does not support the requested feature.",
            PlacementReasonCodes.IsaUnsupported => "The node does not support the required CPU instruction set.",
            PlacementReasonCodes.PrecisionUnsupported => "The provider does not support the requested precision.",
            PlacementReasonCodes.MemoryInsufficient => "Available RAM and AIKernel swap are insufficient.",
            PlacementReasonCodes.DescriptorStale => "The node observation or registration snapshot is stale.",
            PlacementReasonCodes.DescriptorSchemaUnknown => "The node or provider descriptor schema is unsupported.",
            PlacementReasonCodes.NoNodes => "No node was provided for placement.",
            _ => "A node or provider descriptor is invalid."
        }
    };

    private sealed record RankedCandidate(PlacementCandidate Candidate, bool ModelResident, NodeLoadInfo Load);
}
