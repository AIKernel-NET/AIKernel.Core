using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Providers;
using AIKernel.Kernel.Inference;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Providers;

/// <summary>[EN] Combines v0.1.4 memory and ISA accounting with host measurements. [JA] v0.1.4 のメモリ管理と ISA 検出をホストの観測値と組み合わせます。</summary>
public sealed class LocalNodeCapabilityDescriptorSource(
    string nodeId,
    VirtualMemorySpace memorySpace,
    Func<NodeMemoryInfo> observeRam,
    Func<NodeMemoryTierInfo?> observeSwap,
    Func<NodeLoadInfo> observeLoad,
    Func<IReadOnlyList<NodeGpuInfo>>? observeGpus = null,
    TimeProvider? timeProvider = null) : INodeCapabilityDescriptorSource
{
    /// <inheritdoc />
    public ValueTask<NodeCapabilityDescriptor> GetNodeCapabilityDescriptorAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var hostRam = observeRam();
        var arena = memorySpace.GetRamUsage();
        var ram = Math.Min(arena.CapacityBytes - arena.ResidentBytes, SaturatingAdd(hostRam.RamAvailableBytes, arena.SpareBytes));
        var swap = observeSwap();
        if (swap is not null && !string.Equals(swap.Tier, nameof(VirtualMemoryTier.Swap), StringComparison.Ordinal))
            throw new InvalidOperationException("The node swap observation must describe AIKernel swap.");

        var tiers = ImmutableArray.CreateBuilder<NodeMemoryTierInfo>(3);
        tiers.Add(new NodeMemoryTierInfo
        {
            Tier = nameof(VirtualMemoryTier.Ram), CapacityBytes = arena.CapacityBytes, AvailableBytes = ram
        });
        var (_, vramBlocks) = memorySpace.GetAvailableBlocks();
        if (vramBlocks > 0)
            tiers.Add(new NodeMemoryTierInfo { Tier = nameof(VirtualMemoryTier.Vram), AvailableBytes = (long)vramBlocks * memorySpace.BlockSize });
        if (swap is not null)
            tiers.Add(swap);

        return ValueTask.FromResult(new NodeCapabilityDescriptor
        {
            SchemaVersion = ProviderPlacementAbi.SchemaVersion,
            NodeId = nodeId,
            Cpu = new NodeCpuInfo
            {
                Architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                LogicalCores = Environment.ProcessorCount,
                IsaExtensions = CpuInstructionSets.Available.Select(path => path switch
                {
                    "avx" => "AVX",
                    "avx2" => "AVX2",
                    "avx512" => "AVX512F",
                    "avx-vnni-q8" => "AVX-VNNI",
                    "avx512-vnni-q8" => "AVX512VNNI",
                    "amx-int8-q8" => "AMX-INT8",
                    _ => null
                }).OfType<string>().ToImmutableArray()
            },
            Memory = hostRam with { RamAvailableBytes = Math.Min(hostRam.RamTotalBytes, ram) },
            Gpus = observeGpus?.Invoke().ToImmutableArray() ?? [],
            MemoryTiers = tiers.ToImmutable(),
            Load = observeLoad(),
            ObservedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow()
        });
    }

    private static long SaturatingAdd(long first, long second) =>
        first > long.MaxValue - second ? long.MaxValue : first + second;
}
