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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TiledPrefillMatchesSequentialLogitsAndKvAcrossTheTileBoundary(bool edge)
    {
        using var folder = new Folder();
        const int width=16, hidden=24, kv=8, vocab=32, layers=2, length=35;
        var d = new CosmosModelDefinition(width,hidden,layers,2,1,8,vocab,1e-6f,10000,[2,1,1],true,
            8,16,1,1,4,16,edge ? 1 : 2,2,edge ? [] : [0,1],1,2,3,4,5,128)
            { Family=edge ? "cosmos3_edge" : "cosmos3_omni" };
        var header = new Dictionary<string,object>();
        using var data = new MemoryStream();
        var rng = new Random(602);
        void Add(string name, params int[] shape)
        {
            long start=data.Length;
            int n=shape.Aggregate(1,(a,b)=>a*b);
            Span<byte> word=stackalloc byte[2];
            for(int i=0;i<n;i++)
            {
                float value = shape.Length==1 ? 1f : (float)(rng.NextDouble()-.5)*.125f;
                BinaryPrimitives.WriteUInt16LittleEndian(word,(ushort)(BitConverter.SingleToUInt32Bits(value)>>16));
                data.Write(word);
            }
            header.Add("model.language_model."+name,new {dtype="BF16",shape,data_offsets=new[]{start,data.Length}});
        }
        Add("embed_tokens.weight",vocab,width); Add("norm.weight",width);
        for(int layer=0;layer<layers;layer++)
        {
            string p=$"layers.{layer}.";
            Add(p+"self_attn.q_proj.weight",width,width); Add(p+"self_attn.k_proj.weight",kv,width);
            Add(p+"self_attn.v_proj.weight",kv,width); Add(p+"self_attn.o_proj.weight",width,width);
            Add(p+(edge ? "mlp.fc1.weight" : "mlp.gate_proj.weight"),hidden,width);
            Add(p+(edge ? "mlp.fc2.weight" : "mlp.down_proj.weight"),width,hidden);
            Add(p+"input_layernorm.weight",width); Add(p+"post_attention_layernorm.weight",width);
            if(!edge) { Add(p+"mlp.up_proj.weight",hidden,width); Add(p+"self_attn.q_norm.weight",kv); Add(p+"self_attn.k_norm.weight",kv); }
        }
        Save(folder.Path,header,data.ToArray());
        using var mapped = new SafeTensorWeights(folder.Path);
        using var budget = new CosmosMemoryBudget(new() { SwapDirectory=folder.Path,WeightCacheBytes=CosmosPagedWeights.PageBytes },()=>64L<<20);
        using var paged = new SafeTensorWeights(folder.Path,budget);
        var sequential = new CosmosTextDecoder(mapped,d);
        var tiled = new CosmosTextDecoder(paged,d);
        using var original = new CosmosTextDecoder.State(d,length+1,2,default);
        using var actual = new CosmosTextDecoder.State(d,length+1,2,default,budget);
        using var prompt = new CosmosPrompt();
        List<float[]> embeddings=[Enumerable.Range(0,width).Select(i=>i*.02f).ToArray()];
        List<float[]>[] deep=edge ? [] : [[embeddings[0]],[embeddings[0]]];
        for(int i=0;i<length;i++)
        {
            int token=i%vocab, image=i is 31 or 32 ? 0 : -1;
            prompt.Tokens.Add(token);prompt.VisualIndices.Add(image);prompt.Positions.Add((i,i/2,i/3));
            sequential.Forward(token,i,prompt.Positions[i],original,image<0 ? null : embeddings[image],
                image<0 ? null : deep.Select(x=>x[image]).ToArray(),i==length-1);
        }
        long before=budget.Statistics.WeightBackingReads;
        paged.PrefillTimer = new CosmosPrefillTimer();
        Assert.True(tiled.TryPrefill(prompt,embeddings,deep,actual));
        var profile = paged.PrefillTimer.Snapshot();
        paged.PrefillTimer = null;
        Assert.Equal(32,profile.TileTokens);
        Assert.Equal(2*layers*(edge ? 6 : 7)+1,profile.ProjectionPageVisits);
        Assert.Equal(System.Runtime.Intrinsics.X86.Avx2.IsSupported ? "avx2-fp64" : "scalar-fp64",profile.Bf16DotKernel);
        Assert.True(profile.ProjectionRowsMilliseconds > 0);
        Assert.True(profile.ProjectionPageSetupMilliseconds > 0);
        Assert.True(profile.AttentionMilliseconds > 0);
        // Two tiles. Each projection has one page, even though the entire cache holds only one page.
        int projections=edge ? 6 : 7;
        Assert.InRange(budget.Statistics.WeightBackingReads-before,1,2*(layers*projections+1)+1);
        static int[] Bits(float[] x)=>Array.ConvertAll(x,BitConverter.SingleToInt32Bits);
        Assert.Equal(Bits(original.Logits),Bits(actual.Logits));
        for(int layer=0;layer<layers;layer++)
        {
            actual.PagedKv!.ReadLayer(layer,actual.Keys[0],actual.Values[0]);
            Assert.Equal(Bits(original.Keys[layer]),Bits(actual.Keys[0]));
            Assert.Equal(Bits(original.Values[layer]),Bits(actual.Values[0]));
        }
        sequential.Forward(3,length,(length,length,length),original);
        tiled.Forward(3,length,(length,length,length),actual);
        Assert.Equal(Bits(original.Logits),Bits(actual.Logits));
    }
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
