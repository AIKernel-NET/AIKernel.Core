namespace AIKernel.Kernel.Inference;

internal sealed class Gemma4PagedWeights(string path, IEnumerable<GgufWeights.Tensor> tensors, Gemma4MemoryOptions options)
    : GgufPagedWeights(path,tensors,options)
{
    internal unsafe void Multiply(GgufWeights.Tensor tensor,float[] x,float[] y,ParallelOptions parallel,bool reference,Gemma4InstructionSet isa=Gemma4InstructionSet.Auto)
    {
        if(x.Length<tensor.Columns||y.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        using var prepared=!reference&&tensor.Type==2&&Gemma4VnniMatrix.IsResidual(isa)?new Gemma4VnniMatrix(x.AsSpan(0,tensor.Columns)):null;
        VisitRows(tensor,parallel.CancellationToken,(address,first,count)=>
        {
            int last=first+count;
            if(prepared is not null){prepared.Multiply(address,last-first,y,first,parallel,isa);return;}
            void Row(int row)
            {
                parallel.CancellationToken.ThrowIfCancellationRequested();
                var encoded=new ReadOnlySpan<byte>((byte*)address+(row-first)*tensor.RowBytes,tensor.RowBytes);
                if(!reference&&Gemma4CpuMatrix.Supported&&tensor.Type is 1 or 2 or 14&&tensor.Columns%32==0)
                    y[row]=Gemma4CpuMatrix.Dot(encoded,x.AsSpan(0,tensor.Columns),tensor.Type,isa);
                else
                {
                    Span<float> tile=stackalloc float[256];var encoding=tensor.Encoding;float sum=0;
                    for(int k=0,offset=0;k<tensor.Columns;k+=encoding.BlockElements,offset+=encoding.BlockBytes)
                    {encoding.Decode(encoded.Slice(offset,encoding.BlockBytes),tile[..encoding.BlockElements]);for(int i=0;i<encoding.BlockElements;i++)sum+=tile[i]*x[k+i];}
                    y[row]=sum;tile.Clear();
                }
            }
            if(parallel.MaxDegreeOfParallelism==1)for(int row=first;row<last;row++)Row(row);
            else Parallel.For(first,last,parallel,Row);
        });
    }
}
