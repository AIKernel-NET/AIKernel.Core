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
        internal CosmosPagedWeights? Paging { get; set; }
        internal float At(long index)
        {
            if((ulong)index>=(ulong)Count) throw new ArgumentOutOfRangeException(nameof(index));
            if(Paging is not null){Span<float> value=stackalloc float[1];Paging.Read(this,index,value);return value[0];}
            byte* p=(byte*)Address;
            return Type switch
            {
                "F32" => Unsafe.ReadUnaligned<float>(p+index*4),
                "BF16" => BitConverter.Int32BitsToSingle(Unsafe.ReadUnaligned<ushort>(p+index*2)<<16),
                "F16" => Gemma4CpuMatrix.HalfToSingle(Unsafe.ReadUnaligned<ushort>(p+index*2)),
                _ => throw new NotSupportedException(Type)
            };
        }
        internal void CopyTo(Span<float> destination)
        {
            if(destination.Length!=Count)throw new ArgumentException("Tensor destination shape mismatch.");
            CopyRange(0,destination);
        }
        internal float[] Read()
        {
            var result=new float[checked((int)Count)];CopyRange(0,result);return result;
        }
        internal void Row(int row, Span<float> result)
        {
            if(Shape.Length!=2 || row<0 || row>=Shape[0] || result.Length!=Shape[1])throw new ArgumentException("Tensor row shape mismatch.");
            CopyRange((long)row*Shape[1],result);
        }
        internal void CopyRange(long offset, Span<float> result)
        {
            if (offset < 0 || offset > Count - result.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (Paging is not null) { Paging.Read(this, offset, result); return; }
            if (Type == "F32")
            {
                new ReadOnlySpan<float>((float*)Address + offset, result.Length).CopyTo(result);
                return;
            }
            int i = 0;
            if (Type == "BF16")
            {
                ushort* source = (ushort*)Address + offset;
                if (Avx2.IsSupported)
                {
                    fixed (float* dest = result)
                        for (; i <= result.Length - 8; i += 8) Avx.Store(dest + i, Bfloat(source + i));
                }
                for (; i < result.Length; i++)
                    result[i] = BitConverter.Int32BitsToSingle(Unsafe.ReadUnaligned<ushort>(source + i) << 16);
                return;
            }
            for (; i < result.Length; i++) result[i] = At(offset + i);
        }
        internal float Dot(long offset, ReadOnlySpan<float> input)
        {
            if(offset<0 || offset>Count-input.Length)throw new ArgumentException("Tensor dot shape mismatch.");
            if(Paging is not null)return Paging.Dot(this,offset,input);
            byte* p=(byte*)Address;int i=0;float sum=0;
            fixed(float* x=input)
            {
                if(Avx2.IsSupported && Type=="BF16")
                {
                    // BF16 weights times FP32 activations need up to 32
                    // significant product bits. Accumulate in FP64 and round
                    // once, avoiding long-row cancellation in the vision/LLM path.
                    var low=Vector256<double>.Zero;var high=low;
                    for(;i+8<=input.Length;i+=8)
                    {
                        var w=Bfloat((ushort*)p+offset+i);var value=Avx.LoadVector256(x+i);
                        low+=Avx.ConvertToVector256Double(w.GetLower())*Avx.ConvertToVector256Double(value.GetLower());
                        high+=Avx.ConvertToVector256Double(w.GetUpper())*Avx.ConvertToVector256Double(value.GetUpper());
                    }
                    double exact=Vector256.Sum(low+high);
                    for(;i<input.Length;i++)exact+=(double)At(offset+i)*input[i];
                    return (float)exact;
                }
                else if(Type=="BF16")
                {
                    double exact=0;for(;i<input.Length;i++)exact+=(double)At(offset+i)*input[i];return (float)exact;
                }
                else if(Type=="F32")return InferenceVectorMath.Dot(new ReadOnlySpan<float>((float*)p+offset,input.Length),input);
                for(;i<input.Length;i++)sum+=At(offset+i)*input[i];
            }
            return sum;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Bfloat(ushort* p)=>Avx2.ShiftLeftLogical(Avx2.ConvertToVector256Int32(Sse2.LoadVector128(p)),16).AsSingle();
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
    private readonly CosmosPagedWeights? paging;
    internal CosmosMemoryBudget? Budget { get; }
    // Scoped to text prefill while the model's generation gate is held.
    internal CosmosPrefillTimer? PrefillTimer { get; set; }
    private readonly Dictionary<string,Tensor> tensors=new(StringComparer.Ordinal);
    internal IEnumerable<string> Names => tensors.Keys;
    internal long StoredBytes(bool includeVision)=>tensors.Values.Where(t=>includeVision||t.Name.StartsWith("model.language_model.",StringComparison.Ordinal)||t.Name=="lm_head.weight").Sum(t=>checked(t.Count*(t.Type=="F32"?4:2)));

    internal SafeTensorWeights(string directory,CosmosMemoryBudget? budget=null)
    {
        if(!BitConverter.IsLittleEndian)throw new PlatformNotSupportedException("Little-endian safetensors required.");
        Budget=budget;if(budget is not null)paging=new(budget);
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
            paging?.Initialize();
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
        Mapping? map=null;if(paging is null){map=new Mapping(path);maps.Add(map);}
        var added=new List<(Tensor Tensor,long Offset)>();
        foreach(var e in entries)
        {
            long offset=checked(8+(long)headerLength+e.Start);
            var tensor=new Tensor(e.Name,e.Type,e.Shape,e.Count,map is null?0:map.Pointer+checked((nint)offset));
            if(!tensors.TryAdd(e.Name,tensor))throw new InvalidDataException("Duplicate tensor: "+e.Name);
            added.Add((tensor,offset));
        }
        paging?.Add(path,added);
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
        var timer = PrefillTimer;
        timer?.Matrix(weights.Type);
        if(paging is not null){paging.Linear(weights,input,output,options,bias,timer);return;}
        long start = timer?.Start() ?? 0;
        void Range(int start,int end){for(int r=start;r<end;r++){options.CancellationToken.ThrowIfCancellationRequested();output[r]=weights.Dot((long)r*columns,input)+(bias?[r]??0);}}
        if(options.MaxDegreeOfParallelism==1 || rows<32)Range(0,rows);
        else Parallel.For(0,Math.Min(rows,options.MaxDegreeOfParallelism),options,worker=>
        {int parts=Math.Min(rows,options.MaxDegreeOfParallelism);Range(rows*worker/parts,rows*(worker+1)/parts);});
        timer?.RowsCompleted(start);
    }
    internal void LinearBatch(Tensor tensor, float[][] inputs, float[][] outputs, int count, ParallelOptions options)
    {
        if (count < 1 || count > inputs.Length || count > outputs.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        int columns = inputs[0].Length, rows = outputs[0].Length;
        if (tensor.Shape.Length != 2 || tensor.Shape[0] != rows || tensor.Shape[1] != columns)
            throw new ArgumentException("Matrix dimensions mismatch.");
        for (int t = 1; t < count; t++)
            if (inputs[t].Length != columns || outputs[t].Length != rows)
                throw new ArgumentException("All token projections must use the same matrix dimensions.");
        if (paging is null)
        {
            for (int t = 0; t < count; t++) Linear(tensor, inputs[t], outputs[t], options);
            return;
        }
        PrefillTimer?.Matrix(tensor.Type);
        paging.LinearBatch(tensor, inputs, outputs, count, options, PrefillTimer);
    }
    public void Dispose(){try{paging?.Dispose();}finally{foreach(var map in maps)map.Dispose();maps.Clear();tensors.Clear();}}
}
