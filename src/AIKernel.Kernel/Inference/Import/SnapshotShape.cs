using System.Text.Json;

namespace AIKernel.Kernel.Inference;

internal sealed record SnapshotShape(Qwen3ModelDefinition Shape,int Vocabulary,float Theta,int Positions)
{
    internal (float[],float) Rotary() => DenseRotary.Create(Shape.HeadSize,Theta,1,Positions);

    internal static SnapshotShape Read(SnapshotFiles files,DenseRegistrationShape requested,int context)
    {
        using var config=files.Json("config.json");
        var settings=config.RootElement;
        int positions=settings.GetProperty("max_position_embeddings").GetInt32();
        if (context>positions || !settings.GetProperty("tie_word_embeddings").GetBoolean())
            throw new InvalidDataException("Unsupported context or untied output.");
        return new(requested.Bind(Dimensions(settings)),settings.GetProperty("vocab_size").GetInt32(),
            settings.GetProperty("rope_theta").GetSingle(),positions);
    }
    private static Qwen3ModelDefinition Dimensions(JsonElement settings)
    {
        int Get(string key) => settings.GetProperty(key).GetInt32();
        return new(Get("hidden_size"),Get("intermediate_size"),Get("num_attention_heads"),Get("num_key_value_heads"),
            Get("head_dim"),Get("num_hidden_layers"),settings.GetProperty("rms_norm_eps").GetSingle());
    }
}
