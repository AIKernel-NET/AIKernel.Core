using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace AIKernel.Kernel.Inference;

// Data-driven Qwen3-VL dense definition. Cosmos Reason2 uses this architecture;
// the checkpoint, tokenizer and processor must still come from the requested model.
internal sealed record CosmosModelDefinition(
    int Width, int Hidden, int Layers, int Heads, int KvHeads, int HeadSize, int Vocabulary,
    float Epsilon, float RopeTheta, int[] RopeSections, bool Tied,
    int VisionWidth, int VisionHidden, int VisionLayers, int VisionHeads, int VisionPositions,
    int PatchSize, int TemporalPatchSize, int MergeSize, int[] DeepStack,
    int ImageToken, int VideoToken, int VisionStart, int VisionEnd, int EndToken, int MaximumContext)
{
    internal string Family { get; init; } = "qwen3_vl";
    internal bool IsEdge => Family == "cosmos3_edge";
    internal int ProjectorHidden { get; init; }
    internal float VisionEpsilon { get; init; } = 1e-6f;
    internal static CosmosModelDefinition Read(string path)
    {
        if(new FileInfo(path).Length>1_048_576)throw new InvalidDataException("Model configuration exceeds 1 MiB.");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        string family = root.GetProperty("model_type").GetString()!;
        bool edge = family == "cosmos3_edge";
        if (family is not ("qwen3_vl" or "cosmos3_edge" or "cosmos3_omni"))
            throw new NotSupportedException("Unsupported Cosmos architecture.");
        var t = root.GetProperty("text_config"); var v = root.GetProperty("vision_config");
        int I(JsonElement e, string key) => e.GetProperty(key).GetInt32();
        int[] A(JsonElement e, string key) => e.GetProperty(key).EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var rope = t.TryGetProperty("rope_parameters", out var rp) ? rp : t.GetProperty("rope_scaling");
        if (rope.GetProperty("rope_type").GetString() != "default" ||
            (!edge && !rope.GetProperty("mrope_interleaved").GetBoolean()) ||
            t.GetProperty("attention_bias").GetBoolean() || t.GetProperty("hidden_act").GetString() != (edge ? "relu2" : "silu") ||
            (edge && t.TryGetProperty("mlp_bias",out var mb) && mb.GetBoolean()) ||
            v.GetProperty("hidden_act").GetString() != "gelu_pytorch_tanh")
            throw new NotSupportedException("Unsupported Cosmos attention, activation or rotary configuration.");
        var d = new CosmosModelDefinition(I(t,"hidden_size"), I(t,"intermediate_size"), I(t,"num_hidden_layers"),
            I(t,"num_attention_heads"), I(t,"num_key_value_heads"), I(t,"head_dim"), I(t,"vocab_size"),
            t.GetProperty("rms_norm_eps").GetSingle(),
            (rope.TryGetProperty("rope_theta", out var theta) ? theta : t.GetProperty("rope_theta")).GetSingle(),
            A(rope,"mrope_section"), root.GetProperty("tie_word_embeddings").GetBoolean(),
            I(v,"hidden_size"), I(v,"intermediate_size"), I(v,edge?"num_hidden_layers":"depth"), I(v,edge?"num_attention_heads":"num_heads"), I(v,edge?"num_patches":"num_position_embeddings"),
            I(v,"patch_size"), edge?1:I(v,"temporal_patch_size"), I(v,"spatial_merge_size"), edge?[]:A(v,"deepstack_visual_indexes"),
            I(root,"image_token_id"), I(root,"video_token_id"), I(root,"vision_start_token_id"), I(root,"vision_end_token_id"), I(t,"eos_token_id"),I(t,"max_position_embeddings"))
        { Family=family, ProjectorHidden=edge?I(root,"projector_hidden_size"):I(v,"hidden_size")*4,
            VisionEpsilon=edge?v.GetProperty("layer_norm_eps").GetSingle():1e-6f };
        if (d.Width is < 8 or > 4096 || d.Hidden is < 8 or > 16384 || d.Layers is < 1 or > 64 ||
            d.Heads < 1 || d.KvHeads < 1 || d.Heads % d.KvHeads != 0 || d.HeadSize < 4 || d.HeadSize % 2 != 0 ||
            d.Width != d.Heads * d.HeadSize || d.Vocabulary is < 16 or > 300000 ||
            !float.IsFinite(d.Epsilon) || d.Epsilon <= 0 || !float.IsFinite(d.RopeTheta) || d.RopeTheta <= 1 ||
            d.RopeSections.Length != 3 || d.RopeSections.Any(x=>x<0) || d.RopeSections.Sum()!=d.HeadSize/2 ||
            d.RopeSections[1]*3 > d.HeadSize/2+1 || d.RopeSections[2]*3 > d.HeadSize/2+1 ||
            d.VisionWidth is < 8 or > 2048 || d.VisionHeads < 1 || d.VisionWidth % (d.VisionHeads*4) != 0 ||
            d.VisionHidden is < 8 or > 8192 || d.VisionLayers is < 1 or > 64 ||
            d.VisionPositions < 4 || d.VisionPositions > 16384 || (int)Math.Sqrt(d.VisionPositions)*(int)Math.Sqrt(d.VisionPositions)!=d.VisionPositions ||
            d.PatchSize != 16 || d.TemporalPatchSize != (edge?1:2) || d.MergeSize != 2 || I(v,edge?"num_channels":"in_channels") != 3 || (!edge && I(v,"out_hidden_size") != d.Width) ||
            d.ProjectorHidden is <8 or >16384 || !float.IsFinite(d.VisionEpsilon) || d.VisionEpsilon<=0 ||
            d.MaximumContext is <32 or >4_194_304 || d.DeepStack.Length > d.Layers || !d.DeepStack.SequenceEqual(d.DeepStack.Distinct().Order()) ||
            d.DeepStack.Any(x => x < 0 || x >= d.VisionLayers) ||
            new[]{d.ImageToken,d.VideoToken,d.VisionStart,d.VisionEnd,d.EndToken}.Any(x=>x<0||x>=d.Vocabulary))
            throw new NotSupportedException("Unsupported or unbounded Cosmos model dimensions.");
        return d;
    }

    internal static void ValidateProcessor(string directory,CosmosModelDefinition? definition=null)
    {
        bool edge=definition?.IsEdge==true;
        foreach(string name in new[]{"preprocessor_config.json","video_preprocessor_config.json"})
        {
            string path=Path.Combine(directory,name);if(new FileInfo(path).Length>131072)throw new InvalidDataException("Processor metadata too large.");
            using var document=JsonDocument.Parse(File.ReadAllText(path));var p=document.RootElement;
            if(p.GetProperty("patch_size").GetInt32()!=16 || (p.TryGetProperty("temporal_patch_size",out var temporal)?temporal.GetInt32():1)!=(edge?1:2) || p.GetProperty("merge_size").GetInt32()!=2 ||
                !p.GetProperty("image_mean").EnumerateArray().Select(x=>x.GetSingle()).SequenceEqual(new[]{.5f,.5f,.5f}) ||
                !p.GetProperty("image_std").EnumerateArray().Select(x=>x.GetSingle()).SequenceEqual(new[]{.5f,.5f,.5f}) ||
                new[]{"do_normalize","do_rescale","do_convert_rgb"}.Any(k=>p.TryGetProperty(k,out var flag)&&!flag.GetBoolean()) ||
                (p.TryGetProperty("rescale_factor",out var scale)&&Math.Abs(scale.GetDouble()-1d/255)>1e-12))
                throw new NotSupportedException("Checkpoint processor differs from the validated RGB patch contract.");
        }
        string templatePath=Path.Combine(directory,edge?"chat_template.jinja":"chat_template.json");
        if(new FileInfo(templatePath).Length>131072)throw new InvalidDataException("Chat template too large.");
        string text=File.ReadAllText(templatePath);
        if(!edge){using var template=JsonDocument.Parse(text);text=template.RootElement.GetProperty("chat_template").GetString()!;}
        // This is deliberately a compatibility gate, not an arbitrary Jinja evaluator.
        // A different checkpoint template must receive its own independent token tests.
        string expected=edge?"7120ee6666468d4e9b2dc11e133ac5c2fa765fa5907706bf0f906270aa5510c8":"3636d0f0bd6bef02654cdffdc447b79cb2cef8ab02cc75267345946291a489e4";
        if(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))!=expected)
            throw new NotSupportedException("This chat template has not been validated for Cosmos inference.");
    }
}
