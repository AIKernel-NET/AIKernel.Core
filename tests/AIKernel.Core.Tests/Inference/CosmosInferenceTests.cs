using System.Buffers.Binary;
using System.Text.Json;
using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class CosmosInferenceTests
{
    [Fact]
    public void EdgePatchesUseHwcAndKeepEveryVideoFrame()
    {
        byte[] rgb=new byte[32*32*3];
        for(int i=0;i<rgb.Length;i+=3){rgb[i]=255;rgb[i+1]=127;rgb[i+2]=0;}
        using var patches=CosmosMediaProcessor.Prepare([new(32,32,rgb,0),Frame(0,.5),Frame(255,1)],true,new(),1);
        Assert.Equal(3,patches.Time);Assert.Equal(new[]{0d,.5,1},patches.Timestamps);
        Assert.Equal(1,patches.Values[0]);Assert.Equal((127-127.5f)/127.5f,patches.Values[1]);Assert.Equal(-1,patches.Values[2]);
        Assert.Equal(-1,patches.Values[4*768]);Assert.Equal(1,patches.Values[8*768]);
    }

    [Theory]
    [InlineData(16,4)] [InlineData(16,6)] [InlineData(4,6)] [InlineData(4,4)]
    public void EdgePositionResizePreservesConstantsAndBounds(int source,int target)
    {
        var filters=CosmosEdgeVisionEncoder.ResizeWeights(source,target);
        Assert.Equal(target,filters.Length);
        foreach(var (start,weights) in filters)
        {
            Assert.InRange(start,0,source-1);Assert.InRange(start+weights.Length,1,source);
            Assert.InRange(Math.Abs(weights.Sum()-1),0,1e-6f);Assert.All(weights,w=>Assert.InRange(w,0,1));
        }
    }
    [Fact]
    public void VideoPatchesPadOddFrameAndPreserveSourceTime()
    {
        var frames=new[]{Frame(0,0),Frame(255,.5),Frame(127,1.1)};
        using var p=CosmosMediaProcessor.Prepare(frames,true,new());
        Assert.Equal(2,p.Time);Assert.Equal(new[]{.25,1.1},p.Timestamps);
        Assert.Equal(-1,p.Values[0]);Assert.Equal(1,p.Values[256]);
        Assert.Equal(p.Values[4*1536],p.Values[4*1536+256]);
        float[] data=p.Values;p.Dispose();Assert.All(data,x=>Assert.Equal(0,x));
    }
    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidVideoTimeFailsBeforeAllocation(double time)
        =>Assert.Throws<ArgumentException>(()=>CosmosMediaProcessor.Prepare([Frame(0,time)],true,new()));

    [Fact]
    public void NonMonotonicVideoAndOversizedMediaAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>CosmosMediaProcessor.Prepare([Frame(0,1),Frame(0,0)],true,new()));
        Assert.Throws<ArgumentException>(()=>CosmosMediaProcessor.Prepare([new(64,64,new byte[64*64*3])],false,new(){MaximumVisionPatches=4}));
        Assert.Throws<ArgumentException>(()=>CosmosMediaProcessor.Prepare([new(32,32,new byte[3])],false,new()));
    }
    [Theory]
    [InlineData("BF16")] [InlineData("F16")] [InlineData("F32")]
    public void StoredWeightDotMatchesIndependentDecodedValues(string dtype)
    {
        using var folder=new Folder();int size=dtype=="F32"?4:2;byte[] data=new byte[33*3*size];var decoded=new float[99];
        for(int i=0;i<decoded.Length;i++)
        {
            float value=(i%17-8)*.0625f;
            if(dtype=="F32")BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i*size),value);
            else BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i*size),dtype=="BF16"?(ushort)(BitConverter.SingleToInt32Bits(value)>>16):BitConverter.HalfToUInt16Bits((Half)value));
            decoded[i]=value;
        }
        Save(folder.Path,new Dictionary<string,object>{{"w",new{dtype,shape=new[]{3,33},data_offsets=new[]{0,data.Length}}}},data);
        using var store=new SafeTensorWeights(folder.Path);var tensor=store.Require("w",3,33);float[] x=Enumerable.Range(0,33).Select(i=>(i-16)*.03125f).ToArray();
        var actual=new float[3];store.Linear(tensor,x,actual,new(){MaxDegreeOfParallelism=2});
        for(int row=0;row<3;row++)Assert.Equal(Enumerable.Range(0,33).Sum(i=>decoded[row*33+i]*x[i]),actual[row]);
        Assert.Throws<InvalidDataException>(()=>store.Require("w",33,3));
        Assert.Throws<ArgumentOutOfRangeException>(()=>tensor.At(99));
    }
    [Theory]
    [InlineData(64)] [InlineData(127)] [InlineData(2048)] [InlineData(4097)]
    public void LongBfloatDotAndTailsMatchDoubleReference(int columns)
    {
        using var folder=new Folder();var random=new Random(71);byte[] data=new byte[columns*2];var decoded=new float[columns];var x=new float[columns];
        double expected=0;
        for(int i=0;i<columns;i++)
        {
            ushort bits=(ushort)(BitConverter.SingleToInt32Bits((float)(random.NextDouble()-.5))>>16);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i*2),bits);decoded[i]=BitConverter.Int32BitsToSingle(bits<<16);
            x[i]=(float)(random.NextDouble()*2-1);expected+=(double)decoded[i]*x[i];
        }
        Save(folder.Path,new Dictionary<string,object>{{"w",new{dtype="BF16",shape=new[]{1,columns},data_offsets=new[]{0,data.Length}}}},data);
        using var store=new SafeTensorWeights(folder.Path);float[] actual=new float[1];store.Linear(store.Require("w",1,columns),x,actual,new(){MaxDegreeOfParallelism=1});
        Assert.InRange(Math.Abs(actual[0]-expected),0,0.000063);
    }
    [Fact]
    public void MalformedWeightRangesAndShardTraversalFail()
    {
        using var folder=new Folder();Save(folder.Path,new Dictionary<string,object>{{"w",new{dtype="F32",shape=new[]{4},data_offsets=new[]{0,16}}}},new byte[8]);
        Assert.Throws<InvalidDataException>(()=>new SafeTensorWeights(folder.Path));
        File.WriteAllText(System.IO.Path.Combine(folder.Path,"model.safetensors.index.json"),"{\"weight_map\":{\"w\":\"../outside.safetensors\"}}");
        Assert.Throws<InvalidDataException>(()=>new SafeTensorWeights(folder.Path));
    }
    [Fact]
    public void OverlappingTensorsFail()
    {
        using var folder=new Folder();Save(folder.Path,new Dictionary<string,object>{
            {"a",new{dtype="F32",shape=new[]{2},data_offsets=new[]{0,8}}},
            {"b",new{dtype="F32",shape=new[]{2},data_offsets=new[]{4,12}}}},new byte[12]);
        Assert.Throws<InvalidDataException>(()=>new SafeTensorWeights(folder.Path));
    }
    [Fact]
    public void RequestScratchIsClearedBeforeRelease()
    {
        var arena=new CosmosWorkspace();float[] a=arena.Rent(100);Array.Fill(a,37);arena.Dispose();Assert.All(a,x=>Assert.Equal(0,x));
    }
    [Fact]
    public void MediaInAnEarlierTurnConsumesTheSamePatchBudget()
    {
        var options = new CosmosExecutionOptions { MaximumVisionPatches = 8 };
        using var first = CosmosMediaProcessor.Prepare([Frame(0, 0)], false, options, 1);
        using var second = CosmosMediaProcessor.Prepare([Frame(255, 0)], false, options, 1, first.Count);
        Assert.Throws<ArgumentException>(() => CosmosMediaProcessor.Prepare([Frame(0, 0)], false, options, 1, first.Count + second.Count));
    }

    private static CosmosRgbFrame Frame(byte value,double time)=>new(32,32,Enumerable.Repeat(value,32*32*3).ToArray(),time);
    private static void Save(string path,object header,byte[] data)
    {
        byte[] json=JsonSerializer.SerializeToUtf8Bytes(header);using var stream=File.Create(System.IO.Path.Combine(path,"model.safetensors"));
        Span<byte> length=stackalloc byte[8];BinaryPrimitives.WriteUInt64LittleEndian(length,(ulong)json.Length);stream.Write(length);stream.Write(json);stream.Write(data);
    }
    private sealed class Folder:IDisposable
    {
        internal readonly string Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"aikernel-cosmos-"+Guid.NewGuid().ToString("N"));
        internal Folder()=>Directory.CreateDirectory(Path);
        public void Dispose(){foreach(string file in Directory.EnumerateFiles(Path))File.Delete(file);Directory.Delete(Path);}
    }
}
