using System.Buffers.Binary;
using System.Collections.Immutable;

namespace AIKernel.Kernel.Inference;

/// <summary>GGML storage layout, independently of model architecture and activation precision.</summary>
internal sealed record GgufTensorEncoding(int Type, string Name, int BlockElements, int BlockBytes, bool CpuReference)
{
    private static readonly ImmutableDictionary<int, GgufTensorEncoding> Formats = new[]
    {
        new GgufTensorEncoding(0, "F32", 1, 4, true), new(1, "F16", 1, 2, true),
        new(2, "Q4_0", 32, 18, true), new(8, "Q8_0", 32, 34, true),
        // Q4_K_M is a file quantization preset, not a tensor storage type.
        new(12, "Q4_K", 256, 144, true), new(14, "Q6_K", 256, 210, true),
        new(30, "BF16", 1, 2, true), new(39, "MXFP4", 32, 17, true),
        new(41, "Q1_0", 128, 18, true)
    }.ToImmutableDictionary(x => x.Type);

    internal static GgufTensorEncoding Get(int type) => Formats.TryGetValue(type, out var value) ? value
        : throw new NotSupportedException($"Unsupported GGML tensor storage type {type}.");

    internal long RowBytes(long columns)
    {
        if (columns < 1 || columns % BlockElements != 0)
            throw new InvalidDataException($"{Name} requires rows divisible by {BlockElements}.");
        return checked(columns / BlockElements * BlockBytes);
    }

    // Reference decoders follow the on-disk GGML layout. These do not quantize activations.
    internal void Decode(ReadOnlySpan<byte> source, Span<float> destination)
    {
        if (!CpuReference) throw new NotSupportedException($"No CPU decoder for {Name}.");
        if (source.Length != RowBytes(destination.Length)) throw new ArgumentException("Encoded row length mismatch.");
        for (int offset = 0, index = 0; index < destination.Length; offset += BlockBytes, index += BlockElements)
        {
            var block = source.Slice(offset, BlockBytes);
            var values = destination.Slice(index, BlockElements);
            switch (Type)
            {
                case 0: values[0] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(block)); break;
                case 1: values[0] = Half(block); break;
                case 30: values[0] = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadUInt16LittleEndian(block) << 16); break;
                case 2:
                    float q4Scale = Half(block);
                    for (int j = 0; j < 16; j++)
                    { values[j] = ((block[2+j] & 15)-8)*q4Scale; values[j+16] = ((block[2+j] >> 4)-8)*q4Scale; }
                    break;
                case 8:
                    float q8Scale = Half(block);
                    for (int j = 0; j < 32; j++) values[j] = unchecked((sbyte)block[2+j])*q8Scale;
                    break;
                case 41:
                    float q1Scale = Half(block);
                    for (int j = 0; j < 128; j++) values[j] = ((block[2+j/8] >> (j%8)) & 1) == 0 ? -q1Scale : q1Scale;
                    break;
                case 12:
                    float superScale=Half(block),superMinimum=Half(block[2..]);
                    var scales=block.Slice(4,12);
                    for(int group=0;group<8;group++)
                    {
                        int scaleBits=group<4?scales[group]&63:(scales[group+4]&15)|((scales[group-4]>>6)<<4);
                        int minimumBits=group<4?scales[group+4]&63:(scales[group+4]>>4)|((scales[group]>>6)<<4);
                        float d=superScale*scaleBits,m=superMinimum*minimumBits;
                        for(int k=0;k<32;k++)values[group*32+k]=d*((block[16+(group/2)*32+k]>>((group%2)*4))&15)-m;
                    }
                    break;
                case 14:
                    float q6Scale=Half(block[208..]);
                    for(int i=0;i<256;i++)
                    {
                        int half=i/128,quarter=(i%128)/32,lane=i%32;
                        int low=(block[half*64+(quarter%2)*32+lane]>>(quarter/2*4))&15;
                        int high=(block[128+half*32+lane]>>(quarter*2))&3;
                        int quant=(low|(high<<4))-32;
                        int localScale=unchecked((sbyte)block[192+i/16]);
                        values[i]=(q6Scale*localScale)*quant;
                    }
                    break;
                case 39:
                    // E8M0 shared exponent and E2M1 nibbles (low half, then high half).
                    // 255 is the reserved NaN scale; reject rather than emit a plausible result.
                    if (block[0] == 255) throw new InvalidDataException("MXFP4 has a reserved E8M0 scale.");
                    float scale = MathF.ScaleB(1f, block[0]-127);
                    for (int j = 0; j < 16; j++)
                    { values[j] = Fp4(block[1+j] & 15)*scale; values[j+16] = Fp4(block[1+j] >> 4)*scale; }
                    break;
            }
        }
    }

    private static float Half(ReadOnlySpan<byte> bytes) => (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(bytes));
    private static float Fp4(int bits)
    {
        float magnitude = (bits & 7) switch { 0 => 0, 1 => .5f, 2 => 1, 3 => 1.5f, 4 => 2, 5 => 3, 6 => 4, _ => 6 };
        // GGML's decoded zero is +0 for both zero encodings.
        return magnitude == 0 ? 0 : (bits & 8) == 0 ? magnitude : -magnitude;
    }
}
