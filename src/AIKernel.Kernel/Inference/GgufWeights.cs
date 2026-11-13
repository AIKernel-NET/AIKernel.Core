using System.IO.MemoryMappedFiles;
using System.Text;

namespace AIKernel.Kernel.Inference;

/// <summary>Read-only GGUF mapping. Quantized weights remain encoded and are shared by the OS page cache.</summary>
internal sealed unsafe class GgufWeights : IDisposable
{
    internal sealed record Tensor(long Offset, int Rows, int Columns, int Type)
    {
        public int RowBytes => Type switch { 0 => checked(Columns * 4), 41 when Columns % 128 == 0 => Columns / 128 * 18,
            _ => throw new NotSupportedException("This CPU path accepts F32 and Q1_0 (g128) weights.") };
    }
    private readonly MemoryMappedFile map;
    private readonly MemoryMappedViewAccessor view;
    private byte* address;
    internal readonly Dictionary<string, object> Metadata = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, Tensor> Tensors = new(StringComparer.Ordinal);
    internal long Length { get; }
    internal string[] Tokens => (string[])Metadata["tokenizer.ggml.tokens"];
    internal string[] Merges => (string[])Metadata["tokenizer.ggml.merges"];

    internal GgufWeights(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Length = stream.Length;
        if (!BitConverter.IsLittleEndian || Length < 24) throw new InvalidDataException("Invalid GGUF file.");
        using var reader = new BinaryReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
        if (reader.ReadUInt32() != 0x46554747 || reader.ReadUInt32() is not (2 or 3)) throw new InvalidDataException("GGUF v2/v3 required.");
        var tensors = Bound(reader.ReadUInt64(), 65536);
        var entries = Bound(reader.ReadUInt64(), 65536);
        for (int i=0;i<entries;i++)
        {
            var name=Text(reader);
            Metadata.Add(name, Value(reader, reader.ReadUInt32()));
        }
        if (Metadata.GetValueOrDefault("general.architecture") is not "qwen3") throw new NotSupportedException("Expected Qwen3 dense architecture.");
        for(int i=0;i<tensors;i++)
        {
            var name=Text(reader); var rank=Bound(reader.ReadUInt32(),4);
            if(rank is <1 or >2) throw new NotSupportedException("Only vectors and matrices are supported.");
            var cols=Bound(reader.ReadUInt64(),200000);
            var rows=rank==2?Bound(reader.ReadUInt64(),200000):1;
            var type=checked((int)reader.ReadUInt32()); var offset=checked((long)reader.ReadUInt64());
            Tensors.Add(name,new(offset,rows,cols,type));
        }
        int alignment=Int("general.alignment",32);
        if(alignment<1 || alignment>4096 || (alignment&(alignment-1))!=0) throw new InvalidDataException("Invalid alignment.");
        long start=checked((stream.Position+alignment-1)&~(alignment-1L));
        long parameters=0;
        foreach(var pair in Tensors.ToArray())
        {
            var t=pair.Value with {Offset=checked(start+pair.Value.Offset)};
            long size=checked((long)t.Rows*t.RowBytes);
            if(t.Rows<1 || t.Columns<1 || t.Offset<start || t.Offset>Length || size>Length-t.Offset)
                throw new InvalidDataException("Tensor outside file bounds.");
            parameters=checked(parameters+(long)t.Rows*t.Columns);
            Tensors[pair.Key]=t;
        }
        if(parameters>4_100_000_000L) throw new NotSupportedException("CPU demo model exceeds the four-billion-parameter class.");
        map=MemoryMappedFile.CreateFromFile(stream,null,0,MemoryMappedFileAccess.Read,HandleInheritability.None,true);
        try {view=map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);view.SafeMemoryMappedViewHandle.AcquirePointer(ref address);}
        catch {map.Dispose();throw;}
    }
    internal int Int(string name,int fallback=-1)=>Metadata.TryGetValue(name,out var v)?Convert.ToInt32(v,System.Globalization.CultureInfo.InvariantCulture):fallback;
    internal float Float(string name,float fallback)=>Metadata.TryGetValue(name,out var v)?Convert.ToSingle(v,System.Globalization.CultureInfo.InvariantCulture):fallback;
    internal Tensor Require(string name,int rows,int cols)
    {
        if(!Tensors.TryGetValue(name,out var t)||t.Rows!=rows||t.Columns!=cols)throw new InvalidDataException("Tensor shape mismatch: "+name);
        return t;
    }
    internal byte* Pointer(Tensor t)=>address+view.PointerOffset+t.Offset;
    internal float[] Vector(Tensor t)
    {
        if(t.Type!=0 || t.Rows!=1)throw new InvalidDataException("F32 normalization weights required.");
        return new ReadOnlySpan<float>(Pointer(t),t.Columns).ToArray();
    }
    private static int Bound(ulong n,int max)=>n<=(ulong)max?(int)n:throw new InvalidDataException("GGUF metadata limit exceeded.");
    private static string Text(BinaryReader r)
    {
        int n=Bound(r.ReadUInt64(),2*1024*1024);var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();
        return new UTF8Encoding(false,true).GetString(b);
    }
    private static object Value(BinaryReader r,uint type)=>type switch
    {
        0=>r.ReadByte(),1=>r.ReadSByte(),2=>r.ReadUInt16(),3=>r.ReadInt16(),4=>r.ReadUInt32(),5=>r.ReadInt32(),
        6=>r.ReadSingle(),7=>r.ReadBoolean(),8=>Text(r),9=>Array(r),10=>r.ReadUInt64(),11=>r.ReadInt64(),12=>r.ReadDouble(),
        _=>throw new InvalidDataException("Unsupported metadata type.")
    };
    private static object Array(BinaryReader r)
    {
        uint type=r.ReadUInt32();int n=Bound(r.ReadUInt64(),300000);
        if(type==9)throw new InvalidDataException("Nested metadata arrays are unsupported.");
        if(type==8){var values=new string[n];for(int i=0;i<n;i++)values[i]=Text(r);return values;}
        // Retain token kinds; scalar metadata arrays are bounded.
        var a=new object[n];for(int i=0;i<n;i++)a[i]=Value(r,type);return a;
    }
    public void Dispose(){if(address==null)return;view.SafeMemoryMappedViewHandle.ReleasePointer();address=null;view.Dispose();map.Dispose();}
}
