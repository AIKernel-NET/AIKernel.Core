using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIKernel.Kernel.Inference;

/// <summary>A verified, bounded tensor payload in a snapshot. Logical shapes are always output-by-input.</summary>
internal sealed record TensorSlice(FileStream File, long Offset, int Rows, int Columns, bool Bfloat, bool Transposed)
{
    internal long Bytes => checked((long)Rows * Columns * (Bfloat ? 2 : 4));
    internal void VerifyDigest(string expected, byte[] buffer)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = Bytes;
        for (long at = 0; at < length; at += buffer.Length)
        {
            var chunk = buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - at));
            ReadExactly(chunk, Offset + at);
            hash.AppendData(chunk);
        }
        if (!expected.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), StringComparison.Ordinal))
            throw new InvalidDataException("Shard tensor differs from the pinned revision.");
    }
    internal void Check()
    {
        if (Rows <= 0 || Columns <= 0 || Offset < 0 || Offset > File.Length - Bytes)
            throw new InvalidDataException("Tensor extent outside its verified file.");
    }
    internal float[] Materialize()
    {
        Check();
        var data = GC.AllocateUninitializedArray<float>(checked(Rows * Columns));
        if (Bfloat) ReadBfloat(data);
        else ReadExactly(MemoryMarshal.AsBytes(data.AsSpan()), Offset);
        return Transposed ? Transpose(data) : data;
    }
    internal void WriteTo(FileStream destination)
    {
        if (Transposed)
        {
            destination.Write(MemoryMarshal.AsBytes(Materialize().AsSpan()));
            return;
        }
        var buffer = new float[Math.Min(32768,checked(Rows * Columns))];
        long samples = (long)Rows * Columns;
        for (long at=0;at<samples;at+=buffer.Length)
        {
            var chunk = buffer.AsSpan(0,(int)Math.Min(buffer.Length,samples-at));
            if (Bfloat) ReadBfloatChunk(chunk,at); else ReadExactly(MemoryMarshal.AsBytes(chunk),Offset+at*4);
            destination.Write(MemoryMarshal.AsBytes(chunk));
        }
    }
    private void ReadBfloat(float[] data)
    {
        for (int at=0;at<data.Length;at+=32768) ReadBfloatChunk(data.AsSpan(at,Math.Min(32768,data.Length-at)),at);
    }
    private void ReadBfloatChunk(Span<float> chunk,long at)
    {
        // Widen in place from the end so the unread 16-bit payload stays intact.
        var words=MemoryMarshal.Cast<float,ushort>(chunk)[..chunk.Length];
        ReadExactly(MemoryMarshal.AsBytes(words), Offset + at * 2L);
        for (int i=chunk.Length-1;i>=0;i--) chunk[i]=BitConverter.Int32BitsToSingle(words[i]<<16);
    }
    private float[] Transpose(float[] input)
    {
        var output = GC.AllocateUninitializedArray<float>(input.Length);
        for (int row = 0; row < Rows; row += 32)
            for (int col = 0; col < Columns; col++)
                for (int r = row; r < Math.Min(Rows, row + 32); r++)
                    output[r * Columns + col] = input[col * Rows + r];
        return output;
    }
    internal void ReadExactly(Span<byte> target, long offset)
    {
        while (!target.IsEmpty)
        {
            int count = RandomAccess.Read(File.SafeFileHandle, target, offset);
            if (count == 0) throw new EndOfStreamException();
            target = target[count..];
            offset += count;
        }
    }
}
