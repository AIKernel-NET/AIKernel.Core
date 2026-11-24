using System.Collections.Immutable;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

/// <summary>Read-only GGUF mapping. Encoded weights remain shared by the OS page cache.</summary>
internal sealed unsafe class GgufWeights : IDisposable
{
    internal sealed record Tensor(long Offset, int Rows, int Columns, int Type)
    {
        internal int Rank { get; init; } = 2;
        internal int Depth { get; init; } = 1;
        internal int Batch { get; init; } = 1;
        internal GgufTensorEncoding Encoding => GgufTensorEncoding.Get(Type);
        internal long RowByteLength => Encoding.RowBytes(Columns);
        public int RowBytes => checked((int)RowByteLength);
        internal long ByteLength => checked(RowByteLength * Rows * Depth * Batch);
        internal long ElementCount => checked((long)Columns * Rows * Depth * Batch);
        internal ImmutableArray<long> Dimensions => new long[]{Columns,Rows,Depth,Batch}.Take(Rank).ToImmutableArray();
        internal Tensor MatrixSlice(int depth, int batch = 0)
        {
            if((uint)depth>=(uint)Depth||(uint)batch>=(uint)Batch)throw new ArgumentOutOfRangeException(nameof(depth));
            return this with {Offset=checked(Offset+((long)batch*Depth+depth)*Rows*RowByteLength),Depth=1,Batch=1,Rank=2};
        }
    }
    private readonly MemoryMappedFile? map;
    private readonly MemoryMappedViewAccessor? view;
    private readonly FileStream? directReads;
    private byte* address;
    internal readonly ImmutableDictionary<string, object> Metadata;
    internal readonly ImmutableDictionary<string, Tensor> Tensors;
    internal long Length { get; }
    internal long Parameters { get; }
    internal string[] Tokens => (string[])Metadata["tokenizer.ggml.tokens"];
    internal string[] Merges => (string[])Metadata["tokenizer.ggml.merges"];

    internal GgufWeights(string path, bool mapTensorData = true)
        : this(File.OpenRead(path), mapTensorData) { }

    internal GgufWeights(FileStream stream, bool mapTensorData)
    {
        try
        {
            var directory=GgufDirectory.Read(stream);
            Length=directory.Length;Parameters=directory.Parameters;Metadata=directory.Metadata;Tensors=directory.Tensors;
            if (!mapTensorData)
            {
                directReads = stream;
                return;
            }
            map=MemoryMappedFile.CreateFromFile(stream,null,0,MemoryMappedFileAccess.Read,HandleInheritability.None,true);
            try {view=map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);view.SafeMemoryMappedViewHandle.AcquirePointer(ref address);}
            catch {map.Dispose();throw;}
        }
        finally { if (directReads is null) stream.Dispose(); }
    }
    internal Microsoft.Win32.SafeHandles.SafeFileHandle? BackingHandle => directReads?.SafeFileHandle;
    internal int Int(string name,int fallback=-1)=>Metadata.TryGetValue(name,out var v)?Convert.ToInt32(v,System.Globalization.CultureInfo.InvariantCulture):fallback;
    internal float Float(string name,float fallback)=>Metadata.TryGetValue(name,out var v)?Convert.ToSingle(v,System.Globalization.CultureInfo.InvariantCulture):fallback;
    internal Tensor Require(string name,int rows,int cols)
    {
        if(!Tensors.TryGetValue(name,out var t)||t.Rank>2||t.Rows!=rows||t.Columns!=cols)throw new InvalidDataException("Tensor shape mismatch: "+name);
        return t;
    }
    internal byte* BytesAt(long offset, int length)
    {
        ObjectDisposedException.ThrowIf(address==null,this);
        if (offset < 0 || length < 0 || offset > Length || length > Length - offset)throw new ArgumentOutOfRangeException(nameof(offset));
        return address + view!.PointerOffset + offset;
    }
    internal byte* Pointer(Tensor t)
    {
        ObjectDisposedException.ThrowIf(address==null,this);
        if(t.Offset<0||t.Offset>Length||t.ByteLength>Length-t.Offset)throw new InvalidDataException("Tensor view outside file bounds.");
        return address+view!.PointerOffset+t.Offset;
    }
    // Small immutable parameters retained by the graph, including the per-expert biases.
    internal float[] ReadFloat32(Tensor t)
    {
        if (t.Type != 0 || t.Offset < 0 || t.ByteLength > Length - t.Offset)
            throw new InvalidDataException("Expected an in-bounds F32 tensor.");
        int count = checked((int)t.ElementCount);
        if (directReads is null) return new ReadOnlySpan<float>(Pointer(t),count).ToArray();
        var values = new float[count];
        directReads.Position = t.Offset;
        directReads.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }
    internal float[] Vector(Tensor t)
    {
        if(t is not { Rank: <= 2, Rows: 1, Type: 0 or 1 or 30 })throw new InvalidDataException("Floating point normalization weights required.");
        var result = new float[t.Columns];
        if (directReads is null)
        {
            t.Encoding.Decode(new ReadOnlySpan<byte>(Pointer(t), t.RowBytes), result);
        }
        else
        {
            var encoded = new byte[t.RowBytes];
            directReads.Position = t.Offset;
            directReads.ReadExactly(encoded);
            t.Encoding.Decode(encoded, result);
        }
        return result;
    }
    public void Dispose(){directReads?.Dispose();if(address==null)return;view!.SafeMemoryMappedViewHandle.ReleasePointer();address=null;view.Dispose();map!.Dispose();}
}
