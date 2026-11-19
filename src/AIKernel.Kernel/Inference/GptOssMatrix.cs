using System.Numerics;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

// Sparse expert selection leaves weights in their original read-only file mapping.
internal static unsafe class GptOssMatrix
{
    private static readonly float[] Fp4 = [0,.5f,1,1.5f,2,3,4,6,0,-.5f,-1,-1.5f,-2,-3,-4,-6];
    private static readonly float[] Exponents = Enumerable.Range(0,255).Select(i => MathF.ScaleB(1, i-127)).ToArray();
    private static readonly float[] Halves = Enumerable.Range(0,65536).Select(i => (float)BitConverter.UInt16BitsToHalf((ushort)i)).ToArray();

    internal static void Multiply(GgufWeights file, GgufWeights.Tensor matrix, float[] x, float[] output,
        float[]? bias, int biasOffset, ParallelOptions options)
    {
        if(matrix.Rank>2 || matrix.Type is not (0 or 8 or 39))
            throw new NotSupportedException("Expected an F32, Q8_0 or MXFP4 matrix slice.");
        if(x.Length<matrix.Columns || output.Length<matrix.Rows || biasOffset<0 ||
            bias is not null && biasOffset>bias.Length-matrix.Rows)
            throw new ArgumentException("GPT-OSS matrix dimensions mismatch.");
        int columns = matrix.Columns, stride = matrix.RowBytes;
        nint start = (nint)file.Pointer(matrix);
        int jobs = Math.Min(options.MaxDegreeOfParallelism, matrix.Rows);
        Parallel.For(0, jobs, options, job =>
        {
            Span<float> decoded = stackalloc float[32];
            for (int row = job * matrix.Rows / jobs; row < (job + 1) * matrix.Rows / jobs; row++)
            {
                if ((row & 63) == 0) options.CancellationToken.ThrowIfCancellationRequested();
                byte* w = (byte*)start + (long)row * stride;
                float sum = 0;
                if (matrix.Type == 0)
                    sum = Dot(new ReadOnlySpan<float>(w, columns), x);
                else for (int col = 0; col < columns; col += 32)
                {
                    float scale;
                    if (matrix.Type == 39)
                    {
                        if (*w == 255) throw new InvalidDataException("Invalid MXFP4 exponent.");
                        scale = Exponents[*w++];
                        for (int j = 0; j < 16; j++)
                        {
                            int pair = w[j];
                            decoded[j] = Fp4[pair & 15];
                            decoded[j + 16] = Fp4[pair >> 4];
                        }
                        w += 16;
                    }
                    else
                    {
                        scale = Halves[w[0] | w[1] << 8];
                        w += 2;
                        for (int j = 0; j < 32; j++) decoded[j] = (sbyte)w[j];
                        w += 32;
                    }
                    sum += scale * Dot(decoded, x.AsSpan(col,32));
                }
                output[row] = bias is null ? sum : sum + bias[biasOffset + row];
            }
        });
    }

    internal static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        int n = a.Length / Vector<float>.Count * Vector<float>.Count;
        var left = MemoryMarshal.Cast<float,Vector<float>>(a[..n]);
        var right = MemoryMarshal.Cast<float,Vector<float>>(b[..n]);
        var sum = Vector<float>.Zero;
        for (int i = 0; i < left.Length; i++) sum += left[i] * right[i];
        float result = Vector.Sum(sum);
        for (int i = n; i < a.Length; i++) result += a[i] * b[i];
        return result;
    }
}
