using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Row-aligned immutable pages; the model is never mapped in this execution mode.</summary>
internal sealed class Gemma4PagedWeights : IVirtualMemorySource,IDisposable
{
    internal const int PageBytes=4*1024*1024;
    private sealed record Layout(GgufWeights.Tensor Tensor,int RowsPerPage,long FirstPage,int Pages);
    private readonly Dictionary<long,Layout> layouts=new();
    private readonly List<(long Offset,int Bytes)> pages=new();
    private readonly SafeFileHandle handle;
    private readonly VirtualMemorySpace space;
    private readonly Gemma4MemoryOptions options;
    private readonly Process process=Process.GetCurrentProcess();
    private int accesses;
    internal long CacheCapacityBytes{get;}
    internal Gemma4PagedWeights(string path,IEnumerable<GgufWeights.Tensor> tensors,Gemma4MemoryOptions options)
    {
        if(options.MaximumResidentBytes<256L*1024*1024||options.RuntimeReserveBytes<64L*1024*1024||
            options.WeightCacheBytes<PageBytes||options.WeightCacheBytes>options.MaximumResidentBytes-options.RuntimeReserveBytes-3L*PageBytes)
            throw new ArgumentOutOfRangeException(nameof(options),"The RAM cache must leave runtime and transient-buffer headroom.");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SwapDirectory);this.options=options;
        foreach(var tensor in tensors.DistinctBy(t=>t.Offset))
        {
            if(tensor.RowBytes>PageBytes||tensor.Rank>2)throw new NotSupportedException("Gemma tensor exceeds virtual page shape.");
            int count=PageBytes/tensor.RowBytes,n=(tensor.Rows+count-1)/count;
            layouts.Add(tensor.Offset,new(tensor,count,pages.Count,n));
            for(int row=0;row<tensor.Rows;row+=count)pages.Add((tensor.Offset+row*tensor.RowByteLength,Math.Min(count,tensor.Rows-row)*tensor.RowBytes));
        }
        int capacity=checked((int)Math.Min(pages.Count,options.WeightCacheBytes/PageBytes));CacheCapacityBytes=(long)capacity*PageBytes;
        handle=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileOptions.RandomAccess);
        try{space=new(pages.Count,PageBytes,capacity,options.SwapDirectory,source:this);CheckBudget();}
        catch
        {
            try{space?.Dispose();}
            finally{handle.Dispose();process.Dispose();}
            throw;
        }
    }
    public void ReadBlock(long index,Span<byte> destination)
    {
        var page=pages[checked((int)index)];var target=destination[..page.Bytes];long offset=page.Offset;
        while(!target.IsEmpty)
        {int count=RandomAccess.Read(handle,target,offset);if(count==0)throw new EndOfStreamException("Truncated Gemma backing file.");target=target[count..];offset+=count;}
        destination[page.Bytes..].Clear();
    }
    internal VirtualMemoryStatistics Statistics=>space.GetStatistics();
    internal void CheckBudget()
    {
        process.Refresh();long rss=process.WorkingSet64;
        if(rss+options.RuntimeReserveBytes+3L*PageBytes<=options.MaximumResidentBytes)return;
        space.TrimRam(0);process.Refresh();
        if(process.WorkingSet64+options.RuntimeReserveBytes+3L*PageBytes>options.MaximumResidentBytes)
            throw new OutOfMemoryException("Non-pageable process memory leaves insufficient headroom within the configured Gemma RSS ceiling.");
    }
    private void CheckPage(){if((accesses++&15)==0)CheckBudget();}
    internal void ReadRow(GgufWeights.Tensor tensor,int row,float[] destination)
    {
        if((uint)row>=(uint)tensor.Rows||destination.Length<tensor.Columns)throw new ArgumentOutOfRangeException(nameof(row));
        var layout=layouts[tensor.Offset];CheckPage();
        space.UseComputeBlock(layout.FirstPage+row/layout.RowsPerPage,(bytes,_)=>
            tensor.Encoding.Decode(bytes.Slice((row%layout.RowsPerPage)*tensor.RowBytes,tensor.RowBytes),destination.AsSpan(0,tensor.Columns)));
    }
    internal unsafe void Multiply(GgufWeights.Tensor tensor,float[] x,float[] y,ParallelOptions parallel,bool reference,Gemma4InstructionSet isa=Gemma4InstructionSet.Auto)
    {
        if(x.Length<tensor.Columns||y.Length<tensor.Rows)throw new ArgumentException("Matrix dimensions mismatch.");
        var layout=layouts[tensor.Offset];
        using var prepared=!reference&&tensor.Type==2&&Gemma4VnniMatrix.IsResidual(isa)?new Gemma4VnniMatrix(x.AsSpan(0,tensor.Columns)):null;
        for(int page=0;page<layout.Pages;page++)
        {
            parallel.CancellationToken.ThrowIfCancellationRequested();CheckPage();int first=page*layout.RowsPerPage,last=Math.Min(tensor.Rows,first+layout.RowsPerPage);
            space.UseComputeBlock(layout.FirstPage+page,(bytes,_)=>
            {
                fixed(byte* pointer=bytes)
                {
                    nint address=(nint)pointer;
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
                }
            });
        }
    }
    public void Dispose(){try{space.Dispose();}finally{handle.Dispose();process.Dispose();}}
}
