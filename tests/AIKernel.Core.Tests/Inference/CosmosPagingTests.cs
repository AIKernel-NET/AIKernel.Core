using System.Buffers.Binary;
using System.Text.Json;
using AIKernel.Core.Memory;
using AIKernel.Kernel.Inference;
using AIKernel.Kernel.Memory;

namespace AIKernel.Core.Tests.Inference;

public sealed class CosmosPagingTests
{
    private sealed class Folder:IDisposable
    {
        internal string Path {get;}=Directory.CreateTempSubdirectory("cosmos-paging-").FullName;
        public void Dispose()=>Directory.Delete(Path,true);
    }
    [Theory]
    [InlineData("BF16")] [InlineData("F16")] [InlineData("F32")]
    public void ShardsEvictAndReloadWithoutChangingDotOrRows(string dtype)
    {
        using var folder=new Folder();int size=dtype=="F32"?4:2,columns=127,rows=CosmosPagedWeights.PageBytes/(columns*size)+3;
        byte[] data=new byte[rows*columns*size];
        for(int i=0;i<rows*columns;i++)
        {
            float value=(i%67-33)*.03125f;
            if(size==4)BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i*size),value);
            else BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i*size),dtype=="BF16"?(ushort)(BitConverter.SingleToInt32Bits(value)>>16):BitConverter.HalfToUInt16Bits((Half)value));
        }
        byte[] header=JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,object>{{"w",new{dtype,shape=new[]{rows,columns},data_offsets=new[]{0,data.Length}}}});
        string path=System.IO.Path.Combine(folder.Path,"model.safetensors");
        using(var file=File.Create(path)){Span<byte> prefix=stackalloc byte[8];BinaryPrimitives.WriteUInt64LittleEndian(prefix,(ulong)header.Length);file.Write(prefix);file.Write(header);file.Write(data);}
        using(var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path,WeightCacheBytes=CosmosPagedWeights.PageBytes},()=>64L<<20))
        using(var paged=new SafeTensorWeights(folder.Path,budget))
        using(var mapped=new SafeTensorWeights(folder.Path))
        {
            var a=paged.Require("w",rows,columns);var b=mapped.Require("w",rows,columns);
            float[] x=Enumerable.Range(0,columns).Select(i=>(i%13-6)*.0625f).ToArray();float[] actual=new float[rows],expected=new float[rows];
            mapped.Linear(b,x,expected,new(){MaxDegreeOfParallelism=2});paged.Linear(a,x,actual,new(){MaxDegreeOfParallelism=2});Assert.Equal(expected,actual);
            float[] row=new float[columns],reference=new float[columns];
            foreach(int index in new[]{0,rows-1,0}){a.Row(index,row);b.Row(index,reference);Assert.Equal(reference,row);Assert.Equal(b.Dot((long)index*columns,x),a.Dot((long)index*columns,x));}
            Assert.True(budget.Statistics.WeightBackingReads>=4);Assert.Equal(CosmosPagedWeights.PageBytes,budget.Statistics.WeightResidentBytes);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(()=>paged.Linear(a,x,actual,new(){MaxDegreeOfParallelism=2,CancellationToken=cancel.Token}));
        }
        using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
        Assert.Single(Directory.GetFiles(folder.Path)); // Immutable weights do not create duplicate swap.
    }
    [Fact]
    public void KvPagesAreLazyEncryptedAndRoundTripAfterEviction()
    {
        using var folder=new Folder();using var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path,KvCacheBytes=1L<<20},()=>64L<<20);
        const int width=1024,context=130;var expected=new float[context*width];float[] keys=new float[expected.Length],values=new float[expected.Length];
        using(var cache=new CosmosPagedKv(3,width,context,budget))
        {
            Assert.Equal(0,budget.Statistics.KvResidentBytes);
            for(int pos=0;pos<context;pos++)for(int layer=0;layer<3;layer++)
            {
                float[] key=Enumerable.Repeat((float)(pos+1),width).ToArray(),value=Enumerable.Repeat((float)(-pos-1),width).ToArray();cache.Append(layer,pos,key,value);
            }
            Assert.True(Directory.GetFiles(folder.Path).Length>0);
            for(int layer=0;layer<3;layer++)
            {
                cache.ReadLayer(layer,keys,values);
                for(int pos=0;pos<context;pos++){Assert.Equal(pos+1,keys[pos*width]);Assert.Equal(-pos-1,values[(pos+1)*width-1]);}
            }
            Assert.True(budget.Statistics.KvSwapReads>0);Assert.InRange(budget.Statistics.KvResidentBytes,1,1L<<20);
        }
        Assert.Empty(Directory.GetFiles(folder.Path));Assert.True(budget.Statistics.KvSwapReads>0);
    }
    [Fact]
    public void LogicalKvBeyondTwoGigabytesUsesOnlyLayerStagingAndLazyPages()
    {
        using var folder=new Folder();using var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path,
            MaximumResidentBytes=256L<<20,RuntimeReserveBytes=64L<<20,KvCacheBytes=1L<<20},()=>64L<<20);
        var definition=new CosmosModelDefinition(256,512,64,4,4,64,32,1e-6f,10000,[12,10,10],true,
            8,16,1,1,4,16,2,2,[],1,2,3,4,5,32768);
        const int context=16384;
        Assert.Throws<ArgumentException>(()=>new CosmosTextDecoder.State(definition,context,1,CancellationToken.None));
        using(var state=new CosmosTextDecoder.State(definition,context,1,CancellationToken.None,budget))
        {
            Assert.Single(state.Keys);Assert.Single(state.Values);
            Assert.Equal(0,budget.Statistics.KvResidentBytes);Assert.InRange(budget.Statistics.LiveWorkspaceBytes,1,64L<<20);
            state.PagedKv!.Append(63,0,Enumerable.Repeat(1f,256).ToArray(),Enumerable.Repeat(2f,256).ToArray());
            state.PagedKv.ReadLayer(63,state.Keys[0],state.Values[0]);
            Assert.Equal(1f,state.Keys[0][255]);Assert.Equal(2f,state.Values[0][255]);
        }
        Assert.Equal(0,budget.Statistics.LiveWorkspaceBytes);Assert.Empty(Directory.GetFiles(folder.Path));
    }
    [Fact]
    public void RejectedSecondKvOwnerCannotDetachActiveRequest()
    {
        using var folder=new Folder();using var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path},()=>64L<<20);
        using var first=new CosmosPagedKv(1,16,64,budget);
        first.Append(0,0,Enumerable.Repeat(1f,16).ToArray(),Enumerable.Repeat(2f,16).ToArray());
        Assert.Throws<InvalidOperationException>(()=>new CosmosPagedKv(1,16,64,budget));
        Assert.True(budget.Statistics.KvResidentBytes>0);float[] keys=new float[16],values=new float[16];first.ReadLayer(0,keys,values);
        Assert.All(keys,v=>Assert.Equal(1,v));Assert.All(values,v=>Assert.Equal(2,v));
    }
    [Fact]
    public void WorkspacePressureShrinksAndReleasesCacheBudget()
    {
        using var folder=new Folder();using var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path,MaximumResidentBytes=256L<<20,RuntimeReserveBytes=64L<<20,WeightCacheBytes=128L<<20},()=>64L<<20);
        using var space=new VirtualMemorySpace(40,4<<20,1,folder.Path);budget.RegisterWeights(space,32);
        Assert.Equal(128L<<20,budget.Statistics.WeightCacheCapacityBytes);
        budget.Reserve(80L<<20);Assert.Equal(48L<<20,budget.Statistics.WeightCacheCapacityBytes);
        budget.Release(80L<<20);budget.Check();Assert.Equal(128L<<20,budget.Statistics.WeightCacheCapacityBytes);
        Assert.Throws<OutOfMemoryException>(()=>budget.Reserve(129L<<20));Assert.Equal(0,budget.Statistics.LiveWorkspaceBytes);
        Assert.True(budget.Statistics.BudgetAdjustments>=3);
    }
    [Fact]
    public void ProvenanceRejectsTamperingAndMismatchedTrustedManifest()
    {
        using var folder=new Folder();string shard=System.IO.Path.Combine(folder.Path,"reasoner.safetensors");
        File.WriteAllBytes(shard,[1,2,3,4]);
        string hash=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(shard)));
        File.WriteAllText(System.IO.Path.Combine(folder.Path,"model.safetensors.index.json"),"{\"weight_map\":{\"w\":\"reasoner.safetensors\"}}");
        string manifest=System.IO.Path.Combine(folder.Path,"import-manifest.json");
        var metadata=new Dictionary<string,string>();
        foreach(string name in new[]{"config.json","tokenizer.json","preprocessor_config.json","video_preprocessor_config.json","chat_template.jinja"})
        {
            string file=System.IO.Path.Combine(folder.Path,name);File.WriteAllText(file,"{}");
            metadata.Add(name,Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))));
        }
        File.WriteAllText(manifest,JsonSerializer.Serialize(new{repository="nvidia/Cosmos3-Edge",revision=new string('a',40),metadataSha256=metadata,shards=new[]{new{output="reasoner.safetensors",outputSha256=hash}}}));
        string pin=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(manifest)));
        Assert.True(CosmosArtifactVerifier.Verify(folder.Path,"cosmos3_edge",pin).TrustedManifestPinned);
        Assert.Throws<InvalidDataException>(()=>CosmosArtifactVerifier.Verify(folder.Path,"cosmos3_omni",pin));
        Assert.Throws<InvalidDataException>(()=>CosmosArtifactVerifier.Verify(folder.Path,"cosmos3_edge",new string('0',64)));
        File.WriteAllText(System.IO.Path.Combine(folder.Path,"tokenizer.json"),"{\"changed\":true}");
        Assert.Throws<InvalidDataException>(()=>CosmosArtifactVerifier.Verify(folder.Path,"cosmos3_edge",pin));
        File.WriteAllText(System.IO.Path.Combine(folder.Path,"tokenizer.json"),"{}");
        File.WriteAllBytes(shard,[4,3,2,1]);Assert.Throws<InvalidDataException>(()=>CosmosArtifactVerifier.Verify(folder.Path,"cosmos3_edge",pin));
    }
    [Fact]
    public void ProtectedResidentCapacityCannotBeReclaimed()
    {
        using var folder=new Folder();using var budget=new CosmosMemoryBudget(new(){SwapDirectory=folder.Path,MaximumResidentBytes=256L<<20,
            RuntimeReserveBytes=64L<<20,ProtectedResidentBytes=32L<<20},()=>64L<<20);
        using var space=new VirtualMemorySpace(40,4<<20,1,folder.Path);budget.RegisterWeights(space,40);
        Assert.Equal(96L<<20,budget.Statistics.WeightCacheCapacityBytes);
        Assert.Throws<OutOfMemoryException>(()=>budget.Reserve(96L<<20));
    }
    [Fact]
    public void VmCapacityChangesAreSharedByForksAndPreserveDirtyPages()
    {
        using var folder=new Folder();using var space=new VirtualMemorySpace(4,64,3,folder.Path,swapPolicy:new SwapEncryptionPolicy(true,true));
        space.WriteBlock(0,Enumerable.Repeat((byte)17,64).ToArray());space.WriteBlock(1,Enumerable.Repeat((byte)29,64).ToArray());space.WriteBlock(2,Enumerable.Repeat((byte)41,64).ToArray());
        using var fork=space.ForkSpace().Value!;fork.SetRamCapacity(1);Assert.Equal(1,space.GetStatistics().RamBlocks);
        byte[] actual=new byte[64];space.ReadBlock(0,actual);Assert.All(actual,x=>Assert.Equal(17,x));fork.ReadBlock(1,actual);Assert.All(actual,x=>Assert.Equal(29,x));
        space.SetRamCapacity(4);Assert.Equal(3,fork.GetAvailableBlocks().RamBlocks);Assert.Throws<ArgumentOutOfRangeException>(()=>space.SetRamCapacity(0));
    }
}
