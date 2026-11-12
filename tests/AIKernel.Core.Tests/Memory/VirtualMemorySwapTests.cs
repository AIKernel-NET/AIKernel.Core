using System.Security.Cryptography;
using AIKernel.Kernel.Memory;

namespace AIKernel.Core.Tests.Memory;

public sealed class VirtualMemorySwapTests
{
    [Fact]
    public void CiphertextRejectsTamperingAndTheLogicalFileIsZeroed()
    {
        using var path = new TemporarySwap();
        var file = new FileStream(path.FileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var witness = new FileStream(path.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var plaintext = new byte[4096];
        plaintext.AsSpan().Fill(0xB5);
        using (var swap = new VirtualMemorySwap(file, plaintext.Length, new(Encrypt: true, ZeroWipe: true)))
        {
            var version = swap.Write(0, plaintext);
            var persisted = new byte[checked((int)witness.Length)];
            Read(witness, persisted);
            Assert.True(persisted.AsSpan().IndexOf(plaintext) < 0);
            var restored = new byte[plaintext.Length];
            swap.Read(0, version, restored);
            Assert.Equal(plaintext, restored);

            var tagOffset = 12;
            RandomAccess.Write(witness.SafeFileHandle, [(byte)(persisted[tagOffset] ^ 0x80)], tagOffset);
            Assert.ThrowsAny<CryptographicException>(() => swap.Read(0, version, restored));
        }

        var wiped = new byte[checked((int)witness.Length)];
        Read(witness, wiped);
        Assert.True(wiped.AsSpan().IndexOfAnyExcept((byte)0) < 0);
    }

    [Fact]
    public void WarmAuthenticatedSwapBlockOperationsAllocateNoManagedBytes()
    {
        using var path = new TemporarySwap();
        var file = new FileStream(path.FileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1);
        using var swap = new VirtualMemorySwap(file, 4096, new(Encrypt: true, ZeroWipe: false));
        Span<byte> bytes = stackalloc byte[4096];
        bytes.Fill(9);
        for (var i = 0; i < 2048; i++) swap.Read(0, swap.Write(0, bytes), bytes);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++) swap.Read(0, swap.Write(0, bytes), bytes);
        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static void Read(FileStream stream, Span<byte> bytes)
    {
        stream.Position = 0;
        stream.ReadExactly(bytes);
    }

    private sealed class TemporarySwap : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("aikernel-encrypted-swap-");
        internal string FileName => Path.Combine(directory.FullName, "swap.bin");
        public void Dispose() => directory.Delete(recursive: true);
    }
}
