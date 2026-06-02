#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;
using System.Collections.Immutable;

public sealed record KernelRequest
{
    public required string Input { get; init; }

    public required RomId RootRomId { get; init; }

    public required string VfsProviderId { get; init; }

    public required IVfsCredentials VfsCredentials { get; init; }

    public required ContextAssemblyScope Scope { get; init; }

    public required PromptGenerationOptions PromptOptions { get; init; }

    public required ExecutionOptions ExecutionOptions { get; init; }

    public string? RequestedModelId { get; init; }

    public string? ParentSnapshotId { get; init; }

    public ImmutableDictionary<string, string> Metadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}