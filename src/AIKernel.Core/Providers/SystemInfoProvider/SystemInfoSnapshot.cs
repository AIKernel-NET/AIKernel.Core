namespace AIKernel.Core.Providers.SystemInfoProvider;

internal sealed record SystemInfoSnapshot(
    string KernelVersion,
    IReadOnlyList<SystemProviderInfo> Providers,
    IReadOnlyList<SystemCapabilityInfo> Capabilities,
    SystemVfsInfo VfsInfo,
    SystemRuntimeInfo RuntimeInfo);

internal sealed record SystemProviderInfo(
    string ProviderId,
    string Name,
    string Version);

internal sealed record SystemCapabilityInfo(
    string CapabilityId,
    IReadOnlyList<string> Operations,
    string? ProviderId);

internal sealed record SystemVfsInfo(
    string BackendType,
    string? RootPath,
    string MountStatus);

internal sealed record SystemRuntimeInfo(
    string DslVersion,
    string PipelineEngineVersion);
