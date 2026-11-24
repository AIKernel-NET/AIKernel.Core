using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

internal static class SnapshotTensors
{
    internal static Dictionary<string, TensorSlice> ReadOnnx(SnapshotFiles files)
    {
        // The reviewed graph digest pins every edge, operator, attribute and dynamic axis.
        // A modified graph fails verification before this compiled layout can be used.
        using var stream = SnapshotFiles.Resource("onnx-layout.tsv");
        using var reader = new StreamReader(stream);
        var tensors = new Dictionary<string, TensorSlice>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line) AddOnnx(tensors, files, line.Split('\t'));
        return tensors;
    }

    private static void AddOnnx(Dictionary<string, TensorSlice> tensors, SnapshotFiles files, string[] columns)
    {
        string name = columns[0];
        var slice = OnnxPayload(files, columns);
        if (name is "cos_cache" or "sin_cache") return;
        name = name.Replace(".attn.", ".self_attn.").Replace(".MatMul.", ".").Replace(".layernorm.", ".");
        if (name == "model.layers.28.final_norm_layernorm.weight") name = "model.norm.weight";
        tensors.Add(name, slice);
    }
    private static TensorSlice OnnxPayload(SnapshotFiles files, string[] columns)
    {
        int rows = int.Parse(columns[1], CultureInfo.InvariantCulture);
        int cols = int.Parse(columns[2], CultureInfo.InvariantCulture);
        bool transpose = columns[0].Contains(".MatMul.weight", StringComparison.Ordinal);
        var slice = new TensorSlice(files[columns[3]], long.Parse(columns[4], CultureInfo.InvariantCulture),
            transpose ? cols : rows, transpose ? rows : cols, false, transpose);
        slice.Check();
        return slice;
    }

    internal static Dictionary<string, TensorSlice> ReadSafe(SnapshotFiles files)
    {
        var tensors = new Dictionary<string, TensorSlice>(StringComparer.Ordinal);
        foreach (string name in files.Names.Where(n => n.EndsWith(".safetensors", StringComparison.Ordinal)))
            ReadHeader(files[name], tensors);
        if (files.Primary.EndsWith(".index.json", StringComparison.Ordinal)) VerifyIndex(files, tensors);
        return tensors;
    }

    internal static void ReadHeader(FileStream stream, Dictionary<string, TensorSlice> tensors)
    {
        using var header = ParseHeader(stream);
        long start = stream.Position;
        var ranges = ReadEntries(header.RootElement, stream, start, tensors);
        CheckRanges(ranges, start, stream.Length);
    }
    private static JsonDocument ParseHeader(FileStream stream)
    {
        Span<byte> size = stackalloc byte[8];
        stream.Position = 0;
        stream.ReadExactly(size);
        long length = BinaryPrimitives.ReadInt64LittleEndian(size);
        if (length is < 2 or > 16 * 1024 * 1024) throw new InvalidDataException("Invalid tensor header length.");
        var bytes = new byte[(int)length];
        stream.ReadExactly(bytes);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowDuplicateProperties = false });
    }
    private static List<TensorSlice> ReadEntries(JsonElement header, FileStream stream, long start, Dictionary<string, TensorSlice> tensors)
    {
        var extents = new List<TensorSlice>();
        foreach (var entry in header.EnumerateObject())
        {
            if (entry.Name == "__metadata__") continue;
            var slice = Describe(stream, entry.Value, start);
            if (!tensors.TryAdd(entry.Name, slice)) throw new InvalidDataException("Duplicate tensor across shards.");
            extents.Add(slice);
        }
        return extents;
    }
    private static void CheckRanges(List<TensorSlice> extents, long next, long length)
    {
        foreach (var tensor in extents.OrderBy(t => t.Offset))
        {
            if (tensor.Offset != next) throw new InvalidDataException("Overlapping or discontinuous tensor extents.");
            next = checked(next + tensor.Bytes);
        }
        if (next != length) throw new InvalidDataException("Unexpected tensor payload length.");
    }

    private static TensorSlice Describe(FileStream file, JsonElement tensor, long start)
    {
        bool bf16 = tensor.GetProperty("dtype").GetString() switch
        { "BF16" => true, "F32" => false, _ => throw new NotSupportedException("Expected BF16 or F32.") };
        var (rows, cols) = Shape(tensor.GetProperty("shape"));
        var offsets = tensor.GetProperty("data_offsets");
        if (offsets.GetArrayLength() != 2) throw new InvalidDataException("Invalid tensor extent.");
        long offset = offsets[0].GetInt64();
        var slice = new TensorSlice(file, checked(start + offset), rows, cols, bf16, false);
        if (offset < 0 || offsets[1].GetInt64() != checked(offset + slice.Bytes)) throw new InvalidDataException("Invalid tensor length.");
        slice.Check();
        return slice;
    }
    private static (int,int) Shape(JsonElement shape) => shape.GetArrayLength() switch
    {
        1 => (1, shape[0].GetInt32()), 2 => (shape[0].GetInt32(), shape[1].GetInt32()),
        _ => throw new InvalidDataException("Expected vector or matrix.")
    };

    private static void VerifyIndex(SnapshotFiles files, Dictionary<string, TensorSlice> tensors)
    {
        using var doc = files.Json(files.Primary);
        var map = doc.RootElement.GetProperty("weight_map");
        if (tensors.Count != map.GetPropertyCount()) throw new InvalidDataException("Shard index count mismatch.");
        foreach (var entry in map.EnumerateObject())
            if (!tensors.TryGetValue(entry.Name, out var tensor) || tensor.File != files[entry.Value.GetString()!])
                throw new InvalidDataException("Shard index points to the wrong payload.");
        VerifyShardRevision(tensors);
    }

    private static void VerifyShardRevision(Dictionary<string, TensorSlice> tensors)
    {
        using var resource = SnapshotFiles.Resource("safe-digests.tsv");
        using var reader = new StreamReader(resource);
        var buffer = new byte[128 * 1024];
        int verified = 0;
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            if (!tensors.TryGetValue(fields[0], out var slice))
                throw new InvalidDataException("Missing tensor from the pinned revision.");
            slice.VerifyDigest(fields[slice.Bfloat ? 1 : 2], buffer);
            verified++;
        }
        if (verified != tensors.Count) throw new InvalidDataException("Unexpected tensors in shard set.");
    }
}
