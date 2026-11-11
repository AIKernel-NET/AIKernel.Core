using System.Buffers.Binary;
using System.Security.Cryptography;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

public sealed partial class VirtualMemorySpace
{
    private const ulong SnapshotMagic = 0x31534D564B4941; // AIKVMS1
    private const int SnapshotHeaderSize = 32;
    private const int MaxSnapshotState = 64 * 1024;

    /// <summary>Atomically saves materialized pages and caller-owned continuation state.</summary>
    /// <remarks>The file contains plaintext. Keep it immutable while restored views are open.</remarks>
    public Result<SnapshotManifest> Hibernate(string snapshotPath, ReadOnlySpan<byte> continuation = default,
        bool allowPlaintext = false)
    {
        lock (gate)
        {
            if (disposed || arena.BindOnAccess || continuation.Length > MaxSnapshotState)
                return Fail<SnapshotManifest>("snapshot_state_invalid", "The space or continuation cannot be snapshotted.");
            if (arena.Policy.Encrypt && !allowPlaintext)
                return Fail<SnapshotManifest>("snapshot_plaintext_disabled", "The encrypted space requires explicit plaintext snapshot export.");

            string? temporaryPath = null;
            try
            {
                var fullPath = Path.GetFullPath(snapshotPath);
                var stage = fullPath + $".{Guid.NewGuid():N}.tmp";
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 1
                };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

                Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
                using (var output = new FileStream(stage, options))
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    temporaryPath = stage;
                    Span<byte> header = stackalloc byte[SnapshotHeaderSize];
                    header.Clear();
                    BinaryPrimitives.WriteUInt64LittleEndian(header, SnapshotMagic);
                    BinaryPrimitives.WriteInt64LittleEndian(header[8..], BlockCount);
                    BinaryPrimitives.WriteInt32LittleEndian(header[16..], BlockSize);
                    BinaryPrimitives.WriteInt32LittleEndian(header[20..], blocks.Count);
                    BinaryPrimitives.WriteInt32LittleEndian(header[24..], continuation.Length);
                    WriteHashed(output, hash, header);
                    WriteHashed(output, hash, continuation);

                    Span<byte> index = stackalloc byte[sizeof(long)];
                    var ordered = blocks.Keys.ToArray();
                    Array.Sort(ordered);
                    foreach (var key in ordered)
                    {
                        var block = blocks[key];
                        BinaryPrimitives.WriteInt64LittleEndian(index, key);
                        WriteHashed(output, hash, index);
                        if (block.Ram is { } resident)
                        {
                            WriteHashed(output, hash, resident.Memory.Span);
                        }
                        else
                        {
                            var temporary = LoadBytes(block);
                            try { WriteHashed(output, hash, temporary.Memory.Span); }
                            finally { ReturnRam(temporary); }
                        }
                    }
                    hash.GetHashAndReset(digest);
                    output.Write(digest);
                    output.Flush(flushToDisk: true);
                }
                File.Move(stage, fullPath, overwrite: true);
                temporaryPath = null;
                return Result<SnapshotManifest>.Success(new(BlockCount, BlockSize, blocks.Count, Convert.ToHexString(digest)));
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                return Fail<SnapshotManifest>("snapshot_write_failed", ex.Message);
            }
            finally
            {
                if (temporaryPath is not null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }
    }

    /// <summary>Validates the snapshot and maps its pages without repopulating the physical tiers.</summary>
    public static unsafe Result<VirtualMemorySpace> Restore(string snapshotPath, IMemoryMapper? mapper = null,
        int ramBlocks = 4, int vramBlocks = 0, IVirtualMemoryDevice? device = null,
        string? swapDirectory = null, SwapEncryptionPolicy swapPolicy = default)
    {
        IMemoryRegion? region = null;
        VirtualMemorySpace? restored = null;
        try
        {
            mapper ??= OperatingSystem.IsWindows() ? new Win32MemoryMapper() : new PosixMemoryMapper();
            var mapped = mapper.OpenResult(snapshotPath);
            if (!mapped.IsSuccess)
                return Fail<VirtualMemorySpace>("snapshot_mapping_failed", mapped.Error!.Message);

            region = mapped.Value!;
            if (!region.IsMapped || region.Pointer == 0 || region.Length < SnapshotHeaderSize + SHA256.HashSizeInBytes)
                return Fail<VirtualMemorySpace>("snapshot_invalid", "The snapshot is truncated.");
            var address = (byte*)region.Pointer;
            var header = new ReadOnlySpan<byte>(address, SnapshotHeaderSize);
            var blockCount = BinaryPrimitives.ReadInt64LittleEndian(header[8..]);
            var blockSize = BinaryPrimitives.ReadInt32LittleEndian(header[16..]);
            var pageCount = BinaryPrimitives.ReadInt32LittleEndian(header[20..]);
            var stateLength = BinaryPrimitives.ReadInt32LittleEndian(header[24..]);
            var payloadSize = region.Length - SHA256.HashSizeInBytes;
            if (BinaryPrimitives.ReadUInt64LittleEndian(header) != SnapshotMagic ||
                BinaryPrimitives.ReadInt32LittleEndian(header[28..]) != 0 || blockCount < 1 ||
                blockSize < 1 || blockCount > long.MaxValue / blockSize || pageCount < 0 || pageCount > blockCount ||
                stateLength is < 0 or > MaxSnapshotState ||
                payloadSize != SnapshotHeaderSize + (long)stateLength + (blockSize + (long)sizeof(long)) * pageCount)
                return Fail<VirtualMemorySpace>("snapshot_invalid", "The snapshot layout is invalid.");

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (long offset = 0; offset < payloadSize;)
            {
                var length = (int)Math.Min(payloadSize - offset, 1024 * 1024);
                hash.AppendData(new ReadOnlySpan<byte>(address + offset, length));
                offset += length;
            }

            Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
            hash.GetHashAndReset(digest);
            if (!CryptographicOperations.FixedTimeEquals(digest, new ReadOnlySpan<byte>(address + payloadSize, digest.Length)))
                return Fail<VirtualMemorySpace>("snapshot_integrity_failed", "The snapshot digest does not match.");

            var offsets = new Dictionary<long, long>(pageCount);
            var cursor = SnapshotHeaderSize + (long)stateLength;
            long previous = -1;
            for (var i = 0; i < pageCount; i++)
            {
                var index = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(address + cursor, sizeof(long)));
                if (index <= previous || index >= blockCount)
                    return Fail<VirtualMemorySpace>("snapshot_invalid", "The snapshot has an invalid page index.");

                cursor += sizeof(long);
                offsets.Add(index, cursor);
                previous = index;
                cursor += blockSize;
            }

            restored = new VirtualMemorySpace(blockCount, blockSize, ramBlocks,
                swapDirectory ?? Path.GetDirectoryName(Path.GetFullPath(snapshotPath))!,
                vramBlocks, device, swapPolicy: swapPolicy);
            restored.blocks.EnsureCapacity(pageCount);
            foreach (var index in offsets.Keys)
                restored.blocks.Add(index, new Block(index) { HasBacking = true });

            var backing = new SnapshotBacking(region, blockSize, stateLength, offsets);
            restored.arena.Source = backing;
            restored.arena.OwnedSource = backing;
            region = null;
            var result = Result<VirtualMemorySpace>.Success(restored);
            restored = null;
            return result;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<VirtualMemorySpace>("snapshot_restore_failed", ex.Message);
        }
        finally
        {
            // Cleanup must preserve the validation or mapping result.
            try { restored?.Dispose(); }
            catch (Exception ex) when (IsStorageFailure(ex) || ex is AggregateException) { }
            try { region?.Dispose(); }
            catch (Exception ex) when (IsStorageFailure(ex)) { }
        }
    }

    /// <summary>Copies the saved continuation into caller-owned storage.</summary>
    public Result<int> ReadSnapshotState(Span<byte> destination)
    {
        lock (gate)
        {
            if (disposed || source is not SnapshotBacking snapshot || destination.Length < snapshot.State.Length)
                return Fail<int>("snapshot_state_unavailable", "No continuation is available or the buffer is too small.");
            snapshot.State.CopyTo(destination);
            return Result<int>.Success(snapshot.State.Length);
        }
    }

    private static void WriteHashed(FileStream output, IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        output.Write(bytes);
        hash.AppendData(bytes);
    }

    private static bool IsStorageFailure(Exception ex) => ex is IOException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or NotSupportedException or CryptographicException
        or OverflowException or OutOfMemoryException or DllNotFoundException or EntryPointNotFoundException;

    private static Result<T> Fail<T>(string code, string message) =>
        Result<T>.Fail(new ErrorContext(message, code, false) { FailureKind = FailureKind.FailClosed });

    private sealed unsafe class SnapshotBacking(IMemoryRegion region, int blockSize, int stateLength,
        Dictionary<long, long> offsets) : IVirtualMemorySource, IDisposable
    {
        internal ReadOnlySpan<byte> State => new((byte*)region.Pointer + SnapshotHeaderSize, stateLength);

        internal ReadOnlySpan<byte> GetPage(long index) => new((byte*)region.Pointer + offsets[index], blockSize);

        public void ReadBlock(long blockIndex, Span<byte> destination) => GetPage(blockIndex).CopyTo(destination);

        public void Dispose() => region.Dispose();
    }
}
