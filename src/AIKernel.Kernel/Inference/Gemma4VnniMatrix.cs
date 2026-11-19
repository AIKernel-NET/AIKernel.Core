using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

/// <summary>Three residual Q8 planes per 32 inputs. Explicit, high-accuracy approximation, not bitwise FP32.</summary>
internal sealed unsafe class Gemma4VnniMatrix : IDisposable
{
    private const string Library="aikernel_cpu_accel";
    private readonly int columns,blocks;
    private readonly sbyte[] quantized;
    private readonly float[] scales;
    private readonly int[] sums;
    [DllImport(Library,EntryPoint="ak_cpu_gemma_supports",CallingConvention=CallingConvention.Cdecl)] private static extern uint Supports();
    [DllImport(Library,EntryPoint="ak_cpu_gemma_q4_residual",CallingConvention=CallingConvention.Cdecl)]
    private static extern int Native(byte* weights,nuint bytes,sbyte* input,float* scales,int* sums,uint rows,uint columns,uint backend,float* output);
    internal static bool IsResidual(Gemma4InstructionSet isa)=>isa is Gemma4InstructionSet.AvxVnniResidual or Gemma4InstructionSet.Avx512VnniResidual;
    internal static bool IsAvailable(Gemma4InstructionSet isa)
    {
        if(!IsResidual(isa))return false;
        try{return (Supports()&(isa==Gemma4InstructionSet.AvxVnniResidual?1u:2u))!=0;}
        catch(Exception e)when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return false;}
    }
    internal Gemma4VnniMatrix(ReadOnlySpan<float> input)
    {
        if(input.Length==0||input.Length%32!=0)throw new ArgumentException("Q4 residual input must contain whole blocks.");
        columns=input.Length;blocks=columns/32;quantized=new sbyte[3*columns];scales=new float[3*blocks];sums=new int[3*blocks];
        Span<float> residual=stackalloc float[32];
        try
        {
            for(int block=0;block<blocks;block++)
            {
                input.Slice(block*32,32).CopyTo(residual);
                for(int pass=0;pass<3;pass++)
                {
                    float maximum=0;foreach(float value in residual){if(!float.IsFinite(value))throw new ArithmeticException("Non-finite activation.");maximum=Math.Max(maximum,Math.Abs(value));}
                    float scale=maximum/127;scales[pass*blocks+block]=scale;
                    if(scale==0)continue;
                    int sum=0;
                    for(int i=0;i<32;i++)
                    {
                        int q=Math.Clamp((int)MathF.Round(residual[i]/scale),-127,127);
                        quantized[pass*columns+block*32+i]=(sbyte)q;sum+=q;residual[i]-=q*scale;
                    }
                    sums[pass*blocks+block]=sum;
                }
            }
        }
        catch{Dispose();throw;}
        finally{residual.Clear();}
    }
    internal void Multiply(nint weights,int rows,float[] output,int first,ParallelOptions options,Gemma4InstructionSet isa)
    {
        if(!IsResidual(isa)||rows<1||first<0||rows>output.Length-first)throw new ArgumentException("Invalid residual matrix span.");
        fixed(sbyte* x=quantized)fixed(float* s=scales)fixed(int* sumsPtr=sums)fixed(float* y=output)
        {
            nint xp=(nint)x,sp=(nint)s,ip=(nint)sumsPtr,yp=(nint)y;
            void Work(int chunk)
            {
                options.CancellationToken.ThrowIfCancellationRequested();int row=chunk*64,count=Math.Min(64,rows-row);
                int status=Native((byte*)weights+(long)row*blocks*18,(nuint)((long)count*blocks*18),(sbyte*)xp,(float*)sp,(int*)ip,(uint)count,(uint)columns,
                    isa==Gemma4InstructionSet.AvxVnniResidual?1u:2u,(float*)yp+first+row);
                if(status!=0)throw new PlatformNotSupportedException($"Gemma residual VNNI failed ({status}).");
            }
            int chunks=(rows+63)/64;
            if(options.MaxDegreeOfParallelism==1)for(int i=0;i<chunks;i++)Work(i);
            else Parallel.For(0,chunks,options,Work);
        }
    }
    public void Dispose(){Array.Clear(quantized);Array.Clear(scales);Array.Clear(sums);}
}
