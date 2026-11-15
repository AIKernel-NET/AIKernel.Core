namespace AIKernel.Kernel.Inference;

/// <summary>Bounded FP32 reference path for new storage formats, before architecture-specific optimization.</summary>
internal static unsafe class ReferenceCpuMatrix
{
    internal static void Multiply(GgufWeights file,GgufWeights.Tensor tensor,float[] input,float[] output,ParallelOptions options)
    {
        var encoding=tensor.Encoding;
        if(!encoding.CpuReference)throw new NotSupportedException($"No CPU kernel for {encoding.Name}.");
        if(tensor.Rank>2||input.Length<tensor.Columns||output.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        options.CancellationToken.ThrowIfCancellationRequested();
        nint address=(nint)file.Pointer(tensor);
        // Fixed, per-worker decoded block; never an expanded copy of the model or expert matrix.
        void Row(int row)
        {
            Span<float> block=stackalloc float[256];
            try
            {
                float sum=0;
                byte* source=(byte*)address+row*tensor.RowByteLength;
                var values=block[..encoding.BlockElements];
                for(int column=0;column<tensor.Columns;column+=encoding.BlockElements,source+=encoding.BlockBytes)
                {
                    if((column&1023)==0)options.CancellationToken.ThrowIfCancellationRequested();
                    encoding.Decode(new ReadOnlySpan<byte>(source,encoding.BlockBytes),values);
                    for(int j=0;j<values.Length;j++)sum+=values[j]*input[column+j];
                }
                output[row]=sum;
            }
            finally{block.Clear();}
        }
        if(options.MaxDegreeOfParallelism==1)for(int row=0;row<tensor.Rows;row++)Row(row);
        else Parallel.For(0,tensor.Rows,options,Row);
    }
}
