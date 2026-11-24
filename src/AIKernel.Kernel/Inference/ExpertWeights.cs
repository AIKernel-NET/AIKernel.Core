using static AIKernel.Kernel.Inference.MixtureModelSession;

namespace AIKernel.Kernel.Inference;

internal static class ExpertWeights
{
    internal static unsafe void ReadRow(GgufWeights file, GgufWeights.Tensor tensor, int row, float[] output)
    {
        var encoded = new ReadOnlySpan<byte>(file.Pointer(tensor) + (long)row * tensor.RowBytes, tensor.RowBytes);
        tensor.Encoding.Decode(encoded, output);
    }

    internal static GgufWeights.Tensor Require(GgufWeights file, string name, int rows, int columns, int type)
    {
        var tensor = file.Require(name,rows,columns);
        if (tensor.Type != type) throw new InvalidDataException("Unexpected storage format: "+name);
        return tensor;
    }
    internal static float[] Vector(GgufWeights file, string name, int columns) => file.ReadFloat32(Require(file, name,1,columns,0));
    internal static Layer Load(GgufWeights file, int index, int width, int experts, int attentionWidth, int kvWidth, int heads)
    {
        string prefix = $"blk.{index}.";
        Linear Projection(string key, int rows, int columns, int type = 8) =>
            new(Require(file, prefix+key+".weight",rows,columns,type), Vector(file, prefix+key+".bias",rows));
        return new(Vector(file, prefix+"attn_norm.weight",width),Projection("attn_q",attentionWidth,width),
            Projection("attn_k",kvWidth,width),Projection("attn_v",kvWidth,width),Projection("attn_output",width,attentionWidth),
            Vector(file, prefix+"attn_sinks.weight",heads),Vector(file, prefix+"post_attention_norm.weight",width),
            Projection("ffn_gate_inp",experts,width,0), Expert(file, prefix, width, experts, "ffn_gate_exps"),Expert(file, prefix, width, experts, "ffn_up_exps"),Expert(file, prefix, width, experts, "ffn_down_exps"));
    }

    private static Linear Expert(GgufWeights file, string prefix, int width, int experts, string key)
    {
        if (!file.Tensors.TryGetValue(prefix + key + ".weight", out var tensor) || tensor.Rank != 3 || tensor.Type != 39 ||
            tensor.Rows != width || tensor.Columns != width || tensor.Depth != experts)
            throw new InvalidDataException("Invalid expert bank: " + key);
        var bias = Require(file, prefix + key + ".bias", experts, width, 0);
        return new(tensor, file.ReadFloat32(bias));
    }
}
