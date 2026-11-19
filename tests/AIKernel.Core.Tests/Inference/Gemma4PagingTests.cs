using AIKernel.Kernel.Inference;
using AIKernel.Kernel.Memory;
using AIKernel.Core.Memory;
using System.Runtime.InteropServices;

namespace AIKernel.Core.Tests.Inference;

public sealed class Gemma4PagingTests
{
    [Fact]
    public void RejectedResidentBudgetClosesTheVirtualMemoryArena()
    {
        var dir=Directory.CreateTempSubdirectory("gemma-budget-");string path=Path.Combine(dir.FullName,"weights.bin");
        try
        {
            File.WriteAllBytes(path,new byte[64]);
            Assert.Throws<OutOfMemoryException>(()=>new Gemma4PagedWeights(path,[new GgufWeights.Tensor(0,1,32,1)],new()
            {
                SwapDirectory=dir.FullName,WeightCacheBytes=Gemma4PagedWeights.PageBytes,
                MaximumResidentBytes=256L<<20,RuntimeReserveBytes=240L<<20
            }));
            // The arena was created before RSS admission failed; no swap file or backing handle may survive.
            using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
            Assert.Single(dir.EnumerateFileSystemInfos());
        }
        finally{File.Delete(path);dir.Delete();}
    }
    [Fact]
    public void EncodedRowsEvictAndReloadThroughBoundedVirtualMemory()
    {
        var dir=Directory.CreateTempSubdirectory("gemma-paging-");string path=Path.Combine(dir.FullName,"weights.bin");
        int rows=Gemma4PagedWeights.PageBytes/64+1;var tensor=new GgufWeights.Tensor(0,rows,32,1);
        try
        {
            using(var file=File.Create(path))
            {file.SetLength(tensor.ByteLength);file.Write(MemoryMarshal.AsBytes(Enumerable.Repeat((Half)2,32).ToArray().AsSpan()));file.Position=tensor.ByteLength-64;file.Write(MemoryMarshal.AsBytes(Enumerable.Repeat((Half)3,32).ToArray().AsSpan()));}
            using(var weights=new Gemma4PagedWeights(path,[tensor],new(){SwapDirectory=dir.FullName,WeightCacheBytes=Gemma4PagedWeights.PageBytes}))
            {
                float[] row=new float[32];weights.ReadRow(tensor,0,row);Assert.All(row,v=>Assert.Equal(2,v));
                weights.ReadRow(tensor,rows-1,row);Assert.All(row,v=>Assert.Equal(3,v));
                weights.ReadRow(tensor,0,row);Assert.All(row,v=>Assert.Equal(2,v));
                Assert.Equal(3,weights.Statistics.BackingReads);Assert.Equal(1,weights.Statistics.RamBlocks);Assert.Equal(1,weights.Statistics.SwapBlocks);
                float[] output=new float[rows];weights.Multiply(tensor,Enumerable.Repeat(1f,32).ToArray(),output,new(){MaxDegreeOfParallelism=2},false);
                Assert.Equal(64,output[0]);Assert.Equal(96,output[^1]);Assert.All(output[1..^1],v=>Assert.Equal(0,v));
                Assert.Equal(Gemma4PagedWeights.PageBytes,weights.CacheCapacityBytes);Assert.Equal(1,weights.Statistics.RamBlocks);
            }
        }
        finally{File.Delete(path);dir.Delete();}
    }
    [Fact]
    public void TrimmingVirtualMemoryPreservesEncryptedDirtyPages()
    {
        var dir=Directory.CreateTempSubdirectory("gemma-trim-");
        try
        {
            using(var memory=new VirtualMemorySpace(3,64,2,dir.FullName,swapPolicy:new SwapEncryptionPolicy(true,true)))
            {
                memory.WriteBlock(0,Enumerable.Repeat((byte)17,64).ToArray());memory.WriteBlock(1,Enumerable.Repeat((byte)29,64).ToArray());
                memory.TrimRam(0);Assert.Equal(0,memory.GetStatistics().RamBlocks);Assert.Equal(2,memory.GetStatistics().SwapBlocks);
                byte[] result=new byte[64];memory.ReadBlock(0,result);Assert.All(result,v=>Assert.Equal(17,v));
                memory.ReadBlock(1,result);Assert.All(result,v=>Assert.Equal(29,v));Assert.Equal(2,memory.GetStatistics().SwapReads);
                Assert.Throws<ArgumentOutOfRangeException>(()=>memory.TrimRam(-1));
            }
            Assert.Empty(dir.EnumerateFileSystemInfos());
        }
        finally{dir.Delete();}
    }
}
