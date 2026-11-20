using Microsoft.Win32.SafeHandles;
using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

/// <summary>Immutable, row-aligned safetensors pages. The original shard is the SSD backing, with no duplicate swap.</summary>
internal sealed unsafe class CosmosPagedWeights : IVirtualMemorySource,IDisposable
{
    internal const int PageBytes=4*1024*1024;
    internal sealed record Layout(SafeTensorWeights.Tensor Tensor,SafeFileHandle File,long Offset,long FirstPage,int ElementsPerPage,int Pages);
    private readonly List<CosmosWeightPage> pages=[];
    private readonly Dictionary<string,Layout> layouts=new(StringComparer.Ordinal);
    private readonly List<SafeFileHandle> files=[];
    private ICosmosWeightPages? space;
    internal CosmosMemoryBudget Budget { get; }
    internal CosmosPagedWeights(CosmosMemoryBudget budget)=>Budget=budget;
    internal void Add(string path,IEnumerable<(SafeTensorWeights.Tensor Tensor,long Offset)> tensors)
    {
        var file=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.Read,FileOptions.RandomAccess);files.Add(file);
        foreach(var (tensor,offset) in tensors)
        {
            int size=tensor.Type=="F32"?4:2;
            long row=tensor.Shape.Length==1?tensor.Count:tensor.Count/tensor.Shape[0];
            if(row>PageBytes/size)throw new NotSupportedException("Cosmos encoded row exceeds the paging block size.");
            int elements=checked((int)(PageBytes/(row*size)*row));int count=checked((int)((tensor.Count+elements-1)/elements));
            var layout=new Layout(tensor,file,offset,pages.Count,elements,count);layouts.Add(tensor.Name,layout);tensor.Paging=this;
            for(long first=0;first<tensor.Count;first+=elements)
                pages.Add(new(file,checked(offset+first*size),checked((int)Math.Min(elements,tensor.Count-first)*size)));
        }
    }
    internal void Initialize()
    {
        int capacity=checked((int)Math.Min(pages.Count,Budget.Options.WeightCacheBytes/PageBytes));
        if(capacity<1)throw new InvalidDataException("Empty Cosmos weight store.");
        space = OperatingSystem.IsWindowsVersionAtLeast(6, 2)
            ? new WindowsCosmosWeightPages(pages)
            : new BufferedCosmosWeightPages(new(pages.Count,PageBytes,1,Budget.Options.SwapDirectory,source:this));
        Budget.RegisterWeights(space,capacity);
    }
    public void ReadBlock(long index,Span<byte> destination)
    {
        var page=pages[checked((int)index)];var target=destination[..page.Bytes];long offset=page.Offset;
        while(!target.IsEmpty){int read=RandomAccess.Read(page.File,target,offset);if(read==0)throw new EndOfStreamException("Truncated Cosmos shard.");target=target[read..];offset+=read;}
        destination[page.Bytes..].Clear();
    }
    internal void Read(SafeTensorWeights.Tensor tensor,long start,Span<float> output)
    {
        var layout=layouts[tensor.Name];int done=0;int size=tensor.Type=="F32"?4:2;
        fixed(float* target=output)
        {
            nint address=(nint)target;
            while(done<output.Length)
            {
                long index=start+done;int offset=(int)(index%layout.ElementsPerPage),n=Math.Min(output.Length-done,layout.ElementsPerPage-offset),destination=done;
                Budget.PageBoundary();space!.Visit(checked((int)(layout.FirstPage+index/layout.ElementsPerPage)),false,(bytes,_)=>
                {
                    fixed(byte* p=bytes)
                    {
                        var view=new SafeTensorWeights.Tensor(tensor.Name,tensor.Type,[n],n,(nint)(p+offset*size));
                        view.CopyRange(0,new Span<float>((float*)address+destination,n));
                    }
                });done+=n;
            }
        }
    }
    internal float Dot(SafeTensorWeights.Tensor tensor,long offset,ReadOnlySpan<float> input)
    {
        var layout=layouts[tensor.Name];int local=(int)(offset%layout.ElementsPerPage);
        if(input.Length>layout.ElementsPerPage-local)throw new ArgumentException("Dot must stay within a row-aligned page.");
        float result=0;int n=input.Length,size=tensor.Type=="F32"?4:2;
        fixed(float* x=input)
        {
            nint address=(nint)x;Budget.PageBoundary();
            space!.Visit(checked((int)(layout.FirstPage+offset/layout.ElementsPerPage)),false,(bytes,_)=>
            {fixed(byte* p=bytes)result=new SafeTensorWeights.Tensor(tensor.Name,tensor.Type,[n],n,(nint)(p+local*size)).Dot(0,new ReadOnlySpan<float>((void*)address,n));});
        }
        return result;
    }
    internal void Linear(SafeTensorWeights.Tensor tensor,float[] input,float[] output,ParallelOptions options,float[]? bias,CosmosPrefillTimer? timer)
    {
        int columns = input.Length;
        VisitMatrixRows(tensor, columns, output.Length, options, timer, (view, first, start, end) =>
        {
            for (int row = start; row < end; row++)
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                output[first + row] = view.Dot((long)row * columns, input) + (bias?[first + row] ?? 0);
            }
        });
    }

    internal void LinearBatch(SafeTensorWeights.Tensor tensor,float[][] inputs,float[][] outputs,int count,ParallelOptions options,CosmosPrefillTimer? timer)
    {
        int columns = inputs[0].Length;
        VisitMatrixRows(tensor, columns, outputs[0].Length, options, timer, (view, first, start, end) =>
        {
            for (int row = start; row < end; row++)
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                for (int t = 0; t < count; t++)
                    // Match Linear's addition of a zero bias, including signed zero.
                    outputs[t][first + row] = view.Dot((long)row * columns, inputs[t]) + 0f;
            }
        });
    }

    private void VisitMatrixRows(SafeTensorWeights.Tensor tensor, int columns, int rows, ParallelOptions options,
        CosmosPrefillTimer? timer, Action<SafeTensorWeights.Tensor,int,int,int> compute)
    {
        var layout=layouts[tensor.Name];
        if(layout.ElementsPerPage%columns!=0)throw new ArgumentException("Matrix rows must align to paging layout.");
        int rowsPerPage=layout.ElementsPerPage/columns;
        for(int page=0;page<layout.Pages;page++)
        {
            options.CancellationToken.ThrowIfCancellationRequested();Budget.PageBoundary();int first=page*rowsPerPage,count=Math.Min(rowsPerPage,rows-first);
            long pageStart = timer?.Start() ?? 0;
            space!.Visit(checked((int)(layout.FirstPage+page)),true,(bytes,_)=>
            {
                timer?.PageReady(pageStart);
                long rowsStart = timer?.Start() ?? 0;
                fixed(byte* p=bytes)
                {
                    var view=new SafeTensorWeights.Tensor(tensor.Name,tensor.Type,[count,columns],(long)count*columns,(nint)p);
                    int parts=Math.Min(count,options.MaxDegreeOfParallelism);
                    if(parts<=1||count<32)compute(view,first,0,count);
                    else Parallel.For(0,parts,options,worker=>compute(view,first,count*worker/parts,count*(worker+1)/parts));
                }
                timer?.RowsCompleted(rowsStart);
            });
        }
    }
    public void Dispose(){try{space?.Dispose();}finally{foreach(var file in files)file.Dispose();files.Clear();}}
}
