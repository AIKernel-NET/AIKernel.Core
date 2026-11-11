using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

internal sealed unsafe class VirtualMemorySwap : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly FileStream file;
    private readonly int blockSize;
    private readonly long stride;
    private readonly bool wipe;
    private readonly AesGcm? cipher;
    private readonly byte* ciphertext;
    private ulong sequence;

    internal VirtualMemorySwap(FileStream file, int blockSize, SwapEncryptionPolicy policy)
    {
        this.file = file;
        this.blockSize = blockSize;
        wipe = policy.ZeroWipe;
        stride = blockSize + (policy.Encrypt ? NonceSize + TagSize : 0L);
        if (policy.Encrypt)
        {
            Span<byte> key = stackalloc byte[32];
            try
            {
                RandomNumberGenerator.Fill(key);
                cipher = new AesGcm(key, TagSize);
                ciphertext = (byte*)NativeMemory.Alloc((nuint)blockSize);
            }
            catch
            {
                cipher?.Dispose();
                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    internal ulong Write(long slot, ReadOnlySpan<byte> data)
    {
        var offset = checked(slot * stride);
        if (cipher is null)
        {
            RandomAccess.Write(file.SafeFileHandle, data, offset);
            return 0;
        }

        var version = checked(++sequence);
        Span<byte> header = stackalloc byte[NonceSize + TagSize];
        Span<byte> associated = stackalloc byte[sizeof(long)];
        header[..NonceSize].Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(header, version);
        BinaryPrimitives.WriteInt64LittleEndian(associated, slot);
        var encrypted = new Span<byte>(ciphertext, blockSize);
        cipher.Encrypt(header[..NonceSize], data, encrypted, header[NonceSize..], associated);
        RandomAccess.Write(file.SafeFileHandle, header, offset);
        RandomAccess.Write(file.SafeFileHandle, encrypted, offset + header.Length);
        return version;
    }

    internal void Read(long slot, ulong version, Span<byte> data)
    {
        var offset = checked(slot * stride);
        if (cipher is null)
        {
            ReadExactly(data, offset);
            return;
        }

        Span<byte> header = stackalloc byte[NonceSize + TagSize];
        Span<byte> associated = stackalloc byte[sizeof(long)];
        ReadExactly(header, offset);
        if (BinaryPrimitives.ReadUInt64LittleEndian(header) != version)
            throw new CryptographicException("The swap page nonce has changed.");
        BinaryPrimitives.WriteInt64LittleEndian(associated, slot);
        var encrypted = new Span<byte>(ciphertext, blockSize);
        ReadExactly(encrypted, offset + header.Length);
        cipher.Decrypt(header[..NonceSize], encrypted, header[NonceSize..], data, associated);
    }

    internal void Clear(long slot)
    {
        if (wipe)
            Overwrite(checked(slot * stride), stride);
    }

    private void ReadExactly(Span<byte> data, long offset)
    {
        while (!data.IsEmpty)
        {
            var count = RandomAccess.Read(file.SafeFileHandle, data, offset);
            if (count == 0) throw new EndOfStreamException("The swap file ended inside a block.");
            data = data[count..];
            offset += count;
        }
    }

    private void Overwrite(long offset, long count)
    {
        Span<byte> zeroes = stackalloc byte[4096];
        zeroes.Clear();
        while (count > 0)
        {
            var length = (int)Math.Min(count, zeroes.Length);
            RandomAccess.Write(file.SafeFileHandle, zeroes[..length], offset);
            offset += length;
            count -= length;
        }
        RandomAccess.FlushToDisk(file.SafeFileHandle);
    }

    public void Dispose()
    {
        try
        {
            if (wipe) Overwrite(0, file.Length);
        }
        finally
        {
            cipher?.Dispose();
            if (ciphertext != null)
            {
                CryptographicOperations.ZeroMemory(new Span<byte>(ciphertext, blockSize));
                NativeMemory.Free(ciphertext);
            }
            file.Dispose();
        }
    }
}
