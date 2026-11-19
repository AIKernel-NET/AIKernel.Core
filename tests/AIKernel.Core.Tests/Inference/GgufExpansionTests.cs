using AIKernel.Kernel.Inference;
using System.Buffers.Binary;
using System.Text;

namespace AIKernel.Core.Tests.Inference;

public sealed class GgufExpansionTests
{
    [Fact]
    public void F16AndBf16DecodeEveryFiniteBitPatternWithoutConfusingTheirScales()
    {
        byte[] encoded=new byte[2];float[] actual=new float[1];
        foreach(int type in new[]{1,30})
        for(int bits=0;bits<65536;bits++)
        {
            int mantissaBits=type==1?10:7,bias=type==1?15:127;
            int exponent=(bits>>mantissaBits)&(type==1?31:255),fraction=bits&((1<<mantissaBits)-1);
            if(exponent==(type==1?31:255))continue;
            double magnitude=exponent==0?Math.ScaleB((double)fraction,1-bias-mantissaBits)
                :Math.ScaleB(1+fraction/(double)(1<<mantissaBits),exponent-bias);
            float expected=(float)((bits&0x8000)!=0?-magnitude:magnitude);
            BinaryPrimitives.WriteUInt16LittleEndian(encoded,(ushort)bits);
            GgufTensorEncoding.Get(type).Decode(encoded,actual);
            Assert.Equal(BitConverter.SingleToInt32Bits(expected),BitConverter.SingleToInt32Bits(actual[0]));
        }
    }

    [Theory]
    [InlineData(2,32,18)]
    [InlineData(8,32,34)]
    [InlineData(39,32,17)]
    [InlineData(41,128,18)]
    public void PackedBlocksUseTheSpecifiedElementOrderAndSignedValues(int type,int count,int bytes)
    {
        var data=new byte[bytes+1];var packed=data.AsSpan(1);var actual=new float[count];
        if(type==39)packed[0]=128;
        else BinaryPrimitives.WriteUInt16LittleEndian(packed,0x3800); // FP16 0.5
        for(int i=type==39?1:2;i<bytes;i++)packed[i]=(byte)((i*37+11)&255);
        GgufTensorEncoding.Get(type).Decode(packed,actual);
        double[] fp4=[0,.5,1,1.5,2,3,4,6,0,-.5,-1,-1.5,-2,-3,-4,-6];
        for(int i=0;i<count;i++)
        {
            double expected=type switch
            {
                2=>(((packed[2+i%16]>>(i/16*4))&15)-8)*.5,
                8=>((int)packed[2+i]-(packed[2+i]>=128?256:0))*.5,
                39=>fp4[(packed[1+i%16]>>(i/16*4))&15]*2,
                _=>((packed[2+i/8]>>(i%8))&1)==0?-.5:.5
            };
            Assert.Equal((float)expected,actual[i]);
        }
    }

    [Fact]
    public void Mxfp4CoversAllCodesAndExponentBoundaries()
    {
        double[] magnitudes=[0,.5,1,1.5,2,3,4,6];
        foreach(byte exponent in new byte[]{0,1,2,126,127,128,250,254})
        {
            byte[] packed=new byte[17];packed[0]=exponent;
            for(int i=0;i<16;i++)packed[1+i]=(byte)(i|((15-i)<<4));
            float[] result=new float[32];GgufTensorEncoding.Get(39).Decode(packed,result);
            for(int i=0;i<32;i++)
            {
                int code=i<16?i:31-i;
                double value=Math.ScaleB(magnitudes[code&7],exponent-127)*(code>=8?-1:1);
                if(value==0)value=0;
                Assert.Equal((float)value,result[i]);
            }
        }
        Assert.Throws<InvalidDataException>(()=>GgufTensorEncoding.Get(39).Decode(new byte[]{255}.Concat(new byte[16]).ToArray(),new float[32]));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(8)] [InlineData(30)] [InlineData(39)]
    public void ReferenceMatrixMatchesIndependentDotForMultipleRowsAndBlocks(int type)
    {
        const int columns=64,rows=3;
        var encoding=GgufTensorEncoding.Get(type);int rowBytes=(int)encoding.RowBytes(columns);
        var data=new byte[rowBytes*rows];var expected=new double[rows];float[] input=Enumerable.Range(0,columns).Select(i=>(i-31)*.125f).ToArray();
        for(int row=0;row<rows;row++)
        for(int block=0;block<columns/encoding.BlockElements;block++)
        {
            var b=data.AsSpan(row*rowBytes+block*encoding.BlockBytes,encoding.BlockBytes);
            if(type==1)BinaryPrimitives.WriteUInt16LittleEndian(b,BitConverter.HalfToUInt16Bits((Half)(row+block+1)));
            if(type==30)BinaryPrimitives.WriteUInt16LittleEndian(b,(ushort)(BitConverter.SingleToInt32Bits(row+block+1)>>16));
            if(type is 2 or 8)BinaryPrimitives.WriteUInt16LittleEndian(b,BitConverter.HalfToUInt16Bits((Half)(row+block+1)));
            if(type==2)b[2..].Fill(0x99); // both nibbles encode +1
            if(type==8)b[2..].Fill(1);
            if(type==39){b[0]=(byte)(127+row+block);b[1..].Fill(0x22);} // E2M1 +1
            double weight=type==39?Math.Pow(2,row+block):row+block+1;
            for(int i=0;i<encoding.BlockElements;i++)expected[row]+=weight*input[block*encoding.BlockElements+i];
        }
        using var fixture=new Fixture("qwen3",new Spec("w",[columns,rows],type,0,data));
        using var file=new GgufWeights(fixture.Path);float[] actual=new float[rows];
        foreach(int workers in new[]{1,4})
        {
            QuantizedCpuMatrix.Multiply(file,file.Tensors["w"],input,actual,new(){MaxDegreeOfParallelism=workers},"scalar");
            for(int i=0;i<rows;i++)Assert.Equal((float)expected[i],actual[i]);
            if(type is 8 or 39)
            {
                float[] bias=[100,101,102,1,2,3];
                GptOssMatrix.Multiply(file,file.Tensors["w"],input,actual,bias,rows,new(){MaxDegreeOfParallelism=workers});
                for(int i=0;i<rows;i++)Assert.Equal((float)expected[i]+bias[rows+i],actual[i]);
            }
        }
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>QuantizedCpuMatrix.Multiply(file,file.Tensors["w"],input,actual,
            new(){MaxDegreeOfParallelism=1,CancellationToken=cancelled.Token},"scalar"));
        Assert.Throws<NotSupportedException>(()=>QuantizedCpuMatrix.Multiply(file,file.Tensors["w"],input,actual,new(){MaxDegreeOfParallelism=1},"scalar-q8"));
        Assert.Throws<NotSupportedException>(()=>new CudaQuantizedMatrix(file,0,1024*1024));
        Assert.Throws<NotSupportedException>(()=>new VulkanQuantizedMatrix(file,0,1024*1024));
    }

    [Theory]
    [InlineData(0,128)]
    [InlineData(8,34)]
    [InlineData(39,17)]
    public void PagedExpertsKeepRowAndBiasOffsetsAfterEviction(int type,int stride)
    {
        const int rows=3,experts=2;
        byte[] encoded=new byte[rows*experts*stride];
        for(int r=0;r<rows*experts;r++)
        {
            var row=encoded.AsSpan(r*stride,stride);
            if(type==0)System.Runtime.InteropServices.MemoryMarshal.Cast<byte,float>(row).Fill(r+1);
            else if(type==8){row[1]=0x3c;row[2..].Fill((byte)(r+1));}
            else{row[0]=127;row[1..].Fill((byte)((r+1)*17));}
        }
        using var fixture=new Fixture("gpt-oss",new Spec("experts",[32,rows,experts],type,0,encoded));
        using var mapped=new GgufWeights(fixture.Path);
        using var directory=new GgufWeights(fixture.Path,mapTensorData:false);
        var slices=Enumerable.Range(0,experts).Select(e=>directory.Tensors["experts"].MatrixSlice(e)).ToArray();
        // Two rows per page, one resident page. Every slice crosses a page boundary.
        var options=new GgufMemoryOptions { WeightCacheBytes=2*stride, SwapDirectory=Path.GetTempPath() };
        using var pages=new GgufPagedWeights(fixture.Path,slices,options,2*stride,directory.BackingHandle);
        float[] input=Enumerable.Range(1,32).Select(i=>i*.25f).ToArray();
        float[] bias=Enumerable.Range(0,rows*experts).Select(i=>i+.5f).ToArray();
        var parallel=new ParallelOptions { MaxDegreeOfParallelism=2 };
        foreach(int e in new[]{1,0,1})
        {
            float[] expected=new float[rows],actual=new float[rows];
            GptOssMatrix.Multiply(mapped,slices[e],input,expected,bias,e*rows,parallel);
            GptOssMatrix.Multiply(directory,slices[e],input,actual,bias,e*rows,parallel,pages);
            Assert.Equal(expected,actual);
        }
        Assert.Equal(1,pages.Statistics.RamBlocks);
        Assert.Equal(6,pages.Statistics.BackingReads);
        Assert.Equal(4,pages.DistinctPagesRead);
        Assert.Equal(9L*stride,pages.BackingBytesRead);
    }

    [Fact]
    public void ExpertSlicesRetainTheirAxesAndSelectTheCorrectEncodedMatrix()
    {
        var data=new byte[34*2*3];
        for(int row=0;row<6;row++){BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(row*34),0x3c00);data.AsSpan(row*34+2,32).Fill((byte)(row+1));}
        using var fixture=new Fixture("gpt-oss",new Spec("experts",[32,2,3],8,0,data));
        var info=GgufModelInspector.Inspect(fixture.Path);
        Assert.Equal("gpt-oss",info.Architecture);Assert.Equal(new long[]{32,2,3},info.Tensors.Single().Dimensions);
        using var file=new GgufWeights(fixture.Path);var tensor=file.Tensors["experts"];
        float[] rowValues=new float[32],output=new float[2];
        Assert.Throws<ArgumentException>(()=>QuantizedCpuMatrix.Row(file,tensor,0,rowValues));
        Assert.Throws<InvalidDataException>(()=>file.Require("experts",2,32));
        for(int expert=0;expert<3;expert++)
        {
            var slice=tensor.MatrixSlice(expert);
            QuantizedCpuMatrix.Multiply(file,slice,Enumerable.Repeat(1f,32).ToArray(),output,new(){MaxDegreeOfParallelism=1},"scalar");
            Assert.Equal((expert*2+1)*32,output[0]);Assert.Equal((expert*2+2)*32,output[1]);
        }
        Assert.Throws<ArgumentOutOfRangeException>(()=>tensor.MatrixSlice(3));
        Assert.Throws<NotSupportedException>(()=>new Qwen3CpuModel(fixture.Path));
    }

    [Theory]
    [InlineData("gemma4")] [InlineData("gpt-oss")] [InlineData("unknown-model")]
    public void InspectionDoesNotPretendToImplementAnArchitecture(string architecture)
    {
        using var fixture=new Fixture(architecture,new Spec("w",[32,1],39,0,new byte[17]));
        Assert.Equal(architecture,GgufModelInspector.Inspect(fixture.Path).Architecture);
        Assert.Throws<NotSupportedException>(()=>new Qwen3CpuModel(fixture.Path));
    }

    [Fact]
    public void ReaderHandlesLargeVocabularyAnd64BitOffsetsWithoutReadingWeightPayload()
    {
        byte[] header=Fixture.Header("gemma4",[new("token_embd.weight",[32,262144],8,3L*1024*1024*1024,[])]);
        using var stream=new HeaderStream(header,4L*1024*1024*1024);
        var directory=GgufDirectory.Read(stream);var tensor=directory.Tensors.Single().Value;
        Assert.Equal(262144,tensor.Rows);Assert.True(tensor.Offset>int.MaxValue);
        Assert.Equal(32L*262144,directory.Parameters);Assert.True(stream.Position<4096);
        byte[] wide=Fixture.Header("gpt-oss",[new("experts",[1<<20,8192,32],0,0,[])]);
        using var huge=new HeaderStream(wide,2L*1024*1024*1024*1024);
        Assert.Equal(1L<<40,GgufDirectory.Read(huge).Tensors["experts"].ByteLength);
    }

    [Fact]
    public void InspectionRecognizesMixedStorageAndItsReferenceDecoders()
    {
        using var fixture=new Fixture("gemma4",new("a",[256,1],12,0,new byte[144]),new("b",[256,1],14,160,new byte[210]));
        var info=GgufModelInspector.Inspect(fixture.Path);
        Assert.All(info.Tensors,t=>Assert.True(t.HasCpuReferenceDecoder));
        Assert.Equal(354,info.StoredTensorBytes);
        Assert.Throws<NotSupportedException>(()=>GgufTensorEncoding.Get(999));
    }

    [Theory]
    [InlineData(12)] [InlineData(14)]
    public void SuperBlocksDecodeIndependentlyPackedValuesScalesAndHighBits(int type)
    {
        var random=new Random(140016+type);var packed=new byte[type==12?144:210];float[] expected=new float[256];
        if(type==12)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packed,0x3800);BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(2),0x3400);
            int[] scales=[0,15,16,63,1,31,32,62],mins=[63,32,31,1,62,16,15,0];
            for(int i=0;i<4;i++)
            {
                packed[4+i]=(byte)(scales[i]|((scales[i+4]>>4)<<6));
                packed[8+i]=(byte)(mins[i]|((mins[i+4]>>4)<<6));
                packed[12+i]=(byte)((scales[i+4]&15)|((mins[i+4]&15)<<4));
            }
            for(int i=0;i<256;i++)
            {
                int q=random.Next(16),group=i/32;
                packed[16+(group/2)*32+i%32]|=(byte)(q<<((group%2)*4));
                expected[i]=.5f*scales[group]*q-.25f*mins[group];
            }
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(208),0xb800); // negative FP16 scale
            int[] scales=Enumerable.Range(0,16).Select(i=>i==0?-128:i==15?127:i*15-105).ToArray();
            for(int i=0;i<16;i++)packed[192+i]=unchecked((byte)scales[i]);
            for(int i=0;i<256;i++)
            {
                int q=i%64,half=i/128,quarter=i%128/32,lane=i%32;
                packed[half*64+quarter%2*32+lane]|=(byte)((q&15)<<(quarter/2*4));
                packed[128+half*32+lane]|=(byte)((q>>4)<<(quarter*2));
                expected[i]=-.5f*scales[i/16]*(q-32);
            }
        }
        float[] result=new float[256];GgufTensorEncoding.Get(type).Decode(packed,result);Assert.Equal(expected,result);
        using var fixture=new Fixture("gemma4",new Spec("w",[256,1],type,0,packed));using var file=new GgufWeights(fixture.Path);
        float[] input=Enumerable.Range(0,256).Select(i=>(i-128)*.125f).ToArray(),actual=new float[1];
        QuantizedCpuMatrix.Multiply(file,file.Tensors["w"],input,actual,new(){MaxDegreeOfParallelism=1},"scalar");
        double dot=expected.Select((v,i)=>(double)v*input[i]).Sum();Assert.Equal((float)dot,actual[0]);
    }

    [Fact]
    public void ReaderRejectsMalformedStorageAndBoundedMetadataBeforeMapping()
    {
        Spec[] malformed=[new("w",[31,1],2,0,new byte[18]),new("w",[32,0],8,0,[]),new("w",[32,1],999,0,new byte[34]),
            new("w",[32,1],8,1,new byte[34]),new("w",[int.MaxValue,int.MaxValue,int.MaxValue],0,0,[])];
        foreach(var spec in malformed)
        {
            using var fixture=new Fixture("gemma4",spec);
            Assert.ThrowsAny<Exception>(()=>GgufModelInspector.Inspect(fixture.Path));
        }
        using var duplicate=new Fixture("gemma4",new("w",[32],8,0,new byte[34]),new("w",[32],8,64,new byte[34]));
        Assert.Throws<InvalidDataException>(()=>GgufModelInspector.Inspect(duplicate.Path));
        using var overlap=new Fixture("gemma4",new("a",[32],8,0,new byte[34]),new("b",[32],8,32,new byte[34]));
        Assert.Throws<InvalidDataException>(()=>GgufModelInspector.Inspect(overlap.Path));
        using var truncated=new Fixture("gemma4",new Spec("w",[32],8,0,new byte[33]));
        Assert.Throws<InvalidDataException>(()=>GgufModelInspector.Inspect(truncated.Path));
        Assert.Throws<InvalidDataException>(()=>GgufModelInspector.Inspect(truncated.Path,new(){MaximumHeaderBytes=24}));
        Assert.Throws<InvalidDataException>(()=>GgufModelInspector.Inspect(truncated.Path,new(){MaximumStringBytes=2}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>GgufModelInspector.Inspect(truncated.Path,new(){MaximumEntries=0}));
    }

    private sealed record Spec(string Name,int[] Shape,int Type,long Offset,byte[] Data);
    private sealed class Fixture : IDisposable
    {
        internal string Path {get;}=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"aikernel-gguf-"+Guid.NewGuid()+".gguf");
        internal Fixture(string architecture,params Spec[] tensors)
        {
            byte[] header=Header(architecture,tensors);
            using var stream=File.Create(Path);stream.Write(header);
            foreach(var tensor in tensors){stream.Position=header.Length+tensor.Offset;stream.Write(tensor.Data);}
        }
        internal static byte[] Header(string architecture,Spec[] tensors)
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            void Text(string value){byte[] b=Encoding.UTF8.GetBytes(value);writer.Write((ulong)b.Length);writer.Write(b);}
            writer.Write(0x46554747u);writer.Write(3u);writer.Write((ulong)tensors.Length);writer.Write(1ul);
            Text("general.architecture");writer.Write(8u);Text(architecture);
            foreach(var tensor in tensors)
            {Text(tensor.Name);writer.Write((uint)tensor.Shape.Length);foreach(var n in tensor.Shape)writer.Write((ulong)n);writer.Write((uint)tensor.Type);writer.Write((ulong)tensor.Offset);}
            while(stream.Length%32!=0)writer.Write((byte)0);
            return stream.ToArray();
        }
        public void Dispose()=>File.Delete(Path);
    }
    private sealed class HeaderStream(byte[] header,long length) : MemoryStream(header)
    {
        public override long Length=>length;
    }
}
