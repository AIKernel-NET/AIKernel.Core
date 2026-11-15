using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

/// <summary>Bounded safetensors reader for the native managed inference path; never deserializes executable objects.</summary>
internal sealed unsafe class SafeTensorWeights : IDisposable
{
    internal sealed record Tensor(string Name, string Type, int[] Shape, long Count, nint Address)
    {
        internal float At(long index)
        {
            if((ulong)index>=(ulong)Count) throw new ArgumentOutOfRangeException(nameof(index));
            byte* p=(byte*)Address;
            return Type switch
            {
                "F32" => Unsafe.ReadUnaligned<float>(p+index*4),
                "BF16" => BitConverter.Int32BitsToSingle(Unsafe.ReadUnaligned<ushort>(p+index*2)<<16),
                "F16" => Gemma4CpuMatrix.HalfToSingle(Unsafe.ReadUnaligned<ushort>(p+index*2)),
                _ => throw new NotSupportedException(Type)
            };
        }
        internal float[] Read()
        {
            var result=new float[checked((int)Count)];for(int i=0;i<result.Length;i++)result[i]=At(i);return result;
        }
        internal void Row(int row, Span<float> result)
        {
            if(Shape.Length!=2 || row<0 || row>=Shape[0] || result.Length!=Shape[1])throw new ArgumentException("Tensor row shape mismatch.");
            long offset=(long)row*Shape[1];for(int i=0;i<result.Length;i++)result[i]=At(offset+i);
        }
        internal float Dot(long offset, ReadOnlySpan<float> input)
        {
            if(offset<0 || offset+input.Length>Count)throw new ArgumentException("Tensor dot shape mismatch.");
            byte* p=(byte*)Address;int i=0;float sum=0;
            fixed(float* x=input)
            {
                if(Avx2.IsSupported && Type=="BF16")
                {
                    var a=Vector256<float>.Zero;var b=a;
                    for(;i+16<=input.Length;i+=16)
                    {
                        var w0=Avx2.ShiftLeftLogical(Avx2.ConvertToVector256Int32(Sse2.LoadVector128((ushort*)p+offset+i)),16).AsSingle();
                        var w1=Avx2.ShiftLeftLogical(Avx2.ConvertToVector256Int32(Sse2.LoadVector128((ushort*)p+offset+i+8)),16).AsSingle();
                        a+=w0*Avx.LoadVector256(x+i);b+=w1*Avx.LoadVector256(x+i+8);
                    }
                    sum=Vector256.Sum(a+b);
                }
                else if(Type=="F32")return InferenceVectorMath.Dot(new ReadOnlySpan<float>((float*)p+offset,input.Length),input);
                for(;i<input.Length;i++)sum+=At(offset+i)*input[i];
            }
            return sum;
        }
    }

    private sealed class Mapping : IDisposable
    {
        internal readonly MemoryMappedFile File;
        internal readonly MemoryMappedViewAccessor View;
        internal readonly nint Pointer;
        internal Mapping(string path)
        {
            File=MemoryMappedFile.CreateFromFile(path,FileMode.Open,null,0,MemoryMappedFileAccess.Read);
            try
            {
                View=File.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);
                byte* p=null;View.SafeMemoryMappedViewHandle.AcquirePointer(ref p);Pointer=(nint)(p+View.PointerOffset);
            }
            catch {File.Dispose();throw;}
        }
        public void Dispose(){View.SafeMemoryMappedViewHandle.ReleasePointer();View.Dispose();File.Dispose();}
    }
    private readonly List<Mapping> maps=[];
    private readonly Dictionary<string,Tensor> tensors=new(StringComparer.Ordinal);
    internal IEnumerable<string> Names => tensors.Keys;

    internal SafeTensorWeights(string directory)
    {
        if(!BitConverter.IsLittleEndian)throw new PlatformNotSupportedException("Little-endian safetensors required.");
        try
        {
            string index=Path.Combine(directory,"model.safetensors.index.json");
            string[] names;
            Dictionary<string,string>? expected=null;
            if(System.IO.File.Exists(index))
            {
                if(new FileInfo(index).Length>16_777_216)throw new InvalidDataException("Shard index exceeds 16 MiB.");
                using var document=JsonDocument.Parse(System.IO.File.ReadAllText(index));
                expected=document.RootElement.GetProperty("weight_map").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.GetString()!,StringComparer.Ordinal);
                if(expected.Count>10000)throw new InvalidDataException("Too many indexed tensors.");
                names=expected.Values.Distinct(StringComparer.Ordinal).ToArray();
            }
            else names=["model.safetensors"];
            if(names.Length is <1 or >32)throw new InvalidDataException("Too many weight shards.");
            foreach(string name in names)
            {
                if(string.IsNullOrWhiteSpace(name)||name!=Path.GetFileName(name)||name.Contains(':')||name.Contains('/')||name.Contains('\\')||!name.EndsWith(".safetensors",StringComparison.Ordinal))
                    throw new InvalidDataException("Shard path must be a local filename.");
                ReadShard(Path.Combine(directory,name));
            }
            if(expected is not null && (!expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(tensors.Keys)))
                throw new InvalidDataException("Shard inventory differs from index.");
        }
        catch{Dispose();throw;}
    }
    private void ReadShard(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        Span<byte> prefix=stackalloc byte[8];stream.ReadExactly(prefix);
        ulong headerLength=BinaryPrimitives.ReadUInt64LittleEndian(prefix);
        if(headerLength is <2 or >16777216 || headerLength>(ulong)Math.Max(0,stream.Length-8))throw new InvalidDataException("Invalid safetensors header.");
        byte[] header=new byte[(int)headerLength];stream.ReadExactly(header);
        using var document=JsonDocument.Parse(header);var entries=new List<(string Name,string Type,int[] Shape,long Count,long Start,long End)>();
        foreach(var item in document.RootElement.EnumerateObject())
        {
            if(item.Name=="__metadata__")continue;
            if(entries.Count>=10000)throw new InvalidDataException("Too many tensors.");
            var value=item.Value;string type=value.GetProperty("dtype").GetString()!;
            int size=type switch{"F32"=>4,"F16" or "BF16"=>2,_=>throw new NotSupportedException("Unsupported weight dtype: "+type)};
            int[] shape=value.GetProperty("shape").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(shape.Length is <1 or >5 || shape.Any(x=>x<=0))throw new InvalidDataException("Invalid tensor dimensions.");
            long count=1;foreach(int n in shape)count=checked(count*n);
            var offsets=value.GetProperty("data_offsets");if(offsets.GetArrayLength()!=2)throw new InvalidDataException("Invalid tensor offsets.");
            long start=offsets[0].GetInt64(),end=offsets[1].GetInt64();
            if(start<0 || end<start || end>(stream.Length-8-(long)headerLength) || end-start!=checked(count*size))throw new InvalidDataException("Tensor range is outside its shard.");
            entries.Add((item.Name,type,shape,count,start,end));
        }
        long cursor=0;
        foreach(var entry in entries.OrderBy(e=>e.Start))
        {if(entry.Start!=cursor)throw new InvalidDataException("Overlapping or non-contiguous tensors.");cursor=entry.End;}
        if(cursor!=stream.Length-8-(long)headerLength)throw new InvalidDataException("Unused trailing tensor data.");
        var map=new Mapping(path);maps.Add(map);
        foreach(var e in entries)
            if(!tensors.TryAdd(e.Name,new(e.Name,e.Type,e.Shape,e.Count,map.Pointer+checked((nint)(8+(long)headerLength+e.Start)))))
                throw new InvalidDataException("Duplicate tensor: "+e.Name);
    }
    internal Tensor Require(string name, params int[] shape)
    {
        if(!tensors.TryGetValue(name,out var t) || !t.Shape.SequenceEqual(shape))throw new InvalidDataException("Missing or malformed tensor: "+name);
        return t;
    }
    internal void Linear(Tensor weights,float[] input,float[] output,ParallelOptions options,float[]? bias=null)
    {
        int rows=output.Length,columns=input.Length;
        if(weights.Count!=(long)rows*columns || (bias is not null&&bias.Length!=rows))throw new ArgumentException("Matrix dimensions mismatch.");
        void Range(int start,int end){for(int r=start;r<end;r++){options.CancellationToken.ThrowIfCancellationRequested();output[r]=weights.Dot((long)r*columns,input)+(bias?[r]??0);}}
        if(options.MaxDegreeOfParallelism==1 || rows<32)Range(0,rows);
        else Parallel.For(0,Math.Min(rows,options.MaxDegreeOfParallelism),options,worker=>
        {int parts=Math.Min(rows,options.MaxDegreeOfParallelism);Range(rows*worker/parts,rows*(worker+1)/parts);});
    }
    public void Dispose(){foreach(var map in maps)map.Dispose();maps.Clear();tensors.Clear();}
}
