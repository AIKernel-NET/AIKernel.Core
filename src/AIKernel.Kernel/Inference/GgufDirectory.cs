using System.Collections.Immutable;
using System.Text;

namespace AIKernel.Kernel.Inference;

/// <summary>Bounds for directory inspection, not permission to allocate model execution state.</summary>
public sealed record GgufReadLimits
{
    /// <summary>Maximum encoded header bytes, including tokenizer metadata.</summary>
    public long MaximumHeaderBytes { get; init; } = 64L * 1024 * 1024;
    /// <summary>Maximum metadata entries or tensor descriptors.</summary>
    public int MaximumEntries { get; init; } = 65_536;
    /// <summary>Maximum elements in one metadata array.</summary>
    public int MaximumArrayElements { get; init; } = 1_000_000;
    /// <summary>Maximum UTF-8 bytes in one metadata string.</summary>
    public int MaximumStringBytes { get; init; } = 2 * 1024 * 1024;
    internal void Validate()
    {
        if (MaximumHeaderBytes < 24 || MaximumEntries < 1 || MaximumArrayElements < 1 || MaximumStringBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(GgufReadLimits));
    }
}

internal sealed record GgufDirectory(long Length, long Parameters,
    ImmutableDictionary<string, object> Metadata, ImmutableDictionary<string, GgufWeights.Tensor> Tensors)
{
    internal static GgufDirectory Read(Stream stream, GgufReadLimits? limits = null)
    {
        limits ??= new(); limits.Validate();
        if (!BitConverter.IsLittleEndian || !stream.CanSeek || stream.Length < 24)
            throw new InvalidDataException("Invalid GGUF stream.");
        stream.Position = 0;
        using var r = new BinaryReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
        var metadata = ImmutableDictionary.CreateBuilder<string, object>(StringComparer.Ordinal);
        var tensors = ImmutableDictionary.CreateBuilder<string, GgufWeights.Tensor>(StringComparer.Ordinal);
        long allocatedItems = 0;
        void Check(long bytes = 0)
        {
            if (bytes < 0 || stream.Position > Math.Min(stream.Length, limits.MaximumHeaderBytes) - bytes)
                throw new InvalidDataException("GGUF header is truncated or exceeds its byte limit.");
        }
        int Bound(ulong n, int max) => n <= (ulong)max ? (int)n : throw new InvalidDataException("GGUF count limit exceeded.");
        string Text()
        {
            Check(8); int n = Bound(r.ReadUInt64(), limits.MaximumStringBytes); Check(n);
            var bytes=r.ReadBytes(n);
            if(bytes.Length!=n)throw new InvalidDataException("Truncated GGUF string.");
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        object Value(uint type)
        {
            if (type == 8) return Text();
            if (type == 9)
            {
                Check(12); uint elementType = r.ReadUInt32(); int count = Bound(r.ReadUInt64(), limits.MaximumArrayElements);
                if (elementType == 9 || elementType > 12) throw new InvalidDataException("Unsupported GGUF array element type.");
                allocatedItems = checked(allocatedItems + count);
                if (allocatedItems > limits.MaximumHeaderBytes / 8) throw new InvalidDataException("GGUF metadata allocation limit exceeded.");
                int itemBytes = elementType switch { 0 or 1 or 7 => 1, 2 or 3 => 2, 4 or 5 or 6 => 4, _ => 8 };
                Check(checked((long)count * itemBytes));
                if (elementType == 8) { var strings = new string[count]; for (int i=0;i<count;i++) strings[i]=Text(); return strings; }
                var items = new object[count]; for(int i=0;i<count;i++) items[i]=Value(elementType); return items;
            }
            int size = type switch { 0 or 1 or 7 => 1, 2 or 3 => 2, 4 or 5 or 6 => 4, 10 or 11 or 12 => 8,
                _ => throw new InvalidDataException("Unsupported GGUF metadata type.") };
            Check(size);
            return type switch
            {
                0 => (object)r.ReadByte(), 1 => r.ReadSByte(), 2 => r.ReadUInt16(), 3 => r.ReadInt16(),
                4 => r.ReadUInt32(), 5 => r.ReadInt32(), 6 => r.ReadSingle(), 7 => r.ReadBoolean(),
                10 => r.ReadUInt64(), 11 => r.ReadInt64(), _ => r.ReadDouble()
            };
        }
        try
        {
            if (r.ReadUInt32() != 0x46554747 || r.ReadUInt32() is not (2 or 3)) throw new InvalidDataException("GGUF v2/v3 required.");
            int tensorCount = Bound(r.ReadUInt64(), limits.MaximumEntries), entryCount = Bound(r.ReadUInt64(), limits.MaximumEntries);
            for (int i=0;i<entryCount;i++)
            {
                string name=Text(); Check(4);
                if (metadata.ContainsKey(name)) throw new InvalidDataException("Duplicate GGUF metadata key: " + name);
                metadata.Add(name,Value(r.ReadUInt32()));
            }
            for(int i=0;i<tensorCount;i++)
            {
                string name=Text(); Check(4); int rank=Bound(r.ReadUInt32(),4);
                if (rank < 1) throw new InvalidDataException("Tensor rank must be 1..4.");
                Check(rank*8L+12);
                var shape=new int[rank];
                for(int axis=0;axis<rank;axis++)
                { shape[axis]=Bound(r.ReadUInt64(),int.MaxValue); if(shape[axis]<1)throw new InvalidDataException("Empty tensor dimension."); }
                int type=checked((int)r.ReadUInt32()); long offset=checked((long)r.ReadUInt64());
                if (tensors.ContainsKey(name)) throw new InvalidDataException("Duplicate GGUF tensor name: " + name);
                tensors.Add(name,new(offset,rank>1?shape[1]:1,shape[0],type)
                { Rank=rank,Depth=rank>2?shape[2]:1,Batch=rank>3?shape[3]:1 });
            }
            int alignment=32;
            if(metadata.TryGetValue("general.alignment",out var a))
                alignment=a is uint u&&u<=4096?(int)u:throw new InvalidDataException("Invalid GGUF alignment type.");
            if(alignment<1||alignment>4096||(alignment&(alignment-1))!=0)throw new InvalidDataException("Invalid GGUF alignment.");
            long start=checked((stream.Position+alignment-1)&~(alignment-1L)),parameters=0;
            var ranges=new List<(long Start,long End)>();
            foreach(var pair in tensors.ToArray())
            {
                var tensor=pair.Value;
                if(tensor.Offset%alignment!=0)throw new InvalidDataException("Unaligned tensor offset: "+pair.Key);
                tensor=tensor with {Offset=checked(start+tensor.Offset)};
                long size=tensor.ByteLength;
                if(tensor.Offset>stream.Length||size>stream.Length-tensor.Offset)throw new InvalidDataException("Tensor outside file bounds: "+pair.Key);
                parameters=checked(parameters+tensor.ElementCount);
                ranges.Add((tensor.Offset,checked(tensor.Offset+size)));tensors[pair.Key]=tensor;
            }
            ranges.Sort((x,y)=>x.Start.CompareTo(y.Start));
            for(int i=1;i<ranges.Count;i++)if(ranges[i].Start<ranges[i-1].End)throw new InvalidDataException("Overlapping tensor storage.");
            return new(stream.Length,parameters,metadata.ToImmutable(),tensors.ToImmutable());
        }
        catch(OverflowException e){throw new InvalidDataException("GGUF size overflow.",e);}
        catch(EndOfStreamException e){throw new InvalidDataException("Truncated GGUF header.",e);}
    }
}
