namespace AIKernel.Core.Memory;

/// <summary>Describes the dimensions and SHA-256 digest of a block snapshot.</summary>
public readonly record struct SnapshotManifest(long BlockCount, int BlockSize, int StoredBlocks, string Sha256);
