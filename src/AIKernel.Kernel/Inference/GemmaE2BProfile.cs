namespace AIKernel.Kernel.Inference;

public partial class GemmaTextSession
{
    internal static class GemmaE2BProfile
    {
        internal const int Width=1536,LayerCount=35,Heads=8,PleWidth=256,Vocabulary=262144,Owners=15,Window=512;
        internal const float Epsilon=1e-6f;

        internal static bool IsLocal(int layer) => (layer + 1) % 5 != 0;
        internal static int HiddenWidth(int layer) => layer < Owners ? 6144 : 12288;
        internal static int KeyValueOwner(int layer) => layer < Owners ? layer : IsLocal(layer) ? Owners - 2 : Owners - 1;
        internal static void ValidateDefinition(GgufWeights file, int context)
        {
            if(file.Metadata.GetValueOrDefault("general.architecture") is not "gemma4"||file.Int("gemma4.block_count")!=LayerCount||
                file.Int("gemma4.embedding_length")!=Width||file.Int("gemma4.attention.head_count")!=Heads||file.Int("gemma4.attention.head_count_kv")!=1||
                file.Int("gemma4.attention.shared_kv_layers")!=20||file.Int("gemma4.embedding_length_per_layer_input")!=PleWidth||
                file.Int("gemma4.attention.sliding_window")!=Window||file.Tokens.Length!=Vocabulary||file.Int("gemma4.context_length")<context||
                file.Int("gemma4.attention.key_length")!=512||file.Int("gemma4.attention.value_length")!=512||
                file.Int("gemma4.attention.key_length_swa")!=256||file.Int("gemma4.attention.value_length_swa")!=256||
                file.Float("gemma4.attention.layer_norm_rms_epsilon",0)!=Epsilon||file.Float("gemma4.final_logit_softcapping",0)!=30f||
                file.Float("gemma4.rope.freq_base",0)!=1000000f||file.Float("gemma4.rope.freq_base_swa",0)!=10000f)
                throw new NotSupportedException("Only the Gemma 4 E2B causal text definition is supported.");
            if(file.Metadata.GetValueOrDefault("gemma4.attention.sliding_window_pattern") is not object[] pattern||pattern.Length!=LayerCount||
                pattern.Where((v,i)=>v is not bool b||b!=IsLocal(i)).Any()||
                file.Metadata.GetValueOrDefault("gemma4.feed_forward_length") is not object[] hidden||hidden.Length!=LayerCount||
                hidden.Where((v,i)=>Convert.ToInt32(v)!=HiddenWidth(i)).Any())throw new InvalidDataException("Unsupported Gemma layer pattern.");
            // Every tensor in this tied-embedding definition is required below; reject extra heads,
            // experts or encoders rather than silently executing only an arbitrary subset.
            if(file.Tensors.Count!=541||file.ContainsOutput()||file.Tensors.Values.Any(t=>t.Rank>2||t.Type is not (0 or 1 or 2 or 8 or 12 or 14 or 30)))
                throw new NotSupportedException("Expected the tied-embedding E2B text tensor set; extra heads, experts and encoders are unsupported.");
        }
        internal static (float[], float[]) Rotary(float[] factors)
        {
            var fullFrequencies=new float[256];var localFrequencies=new float[128];
            for(int i=0;i<256;i++)
            {
                if(!float.IsFinite(factors[i])||factors[i]<=0)throw new InvalidDataException("Invalid Gemma proportional RoPE factors.");
                fullFrequencies[i]=1f/MathF.Pow(1000000f,2f*i/512)/factors[i];
            }
            for(int i=0;i<128;i++)localFrequencies[i]=1f/MathF.Pow(10000f,2f*i/256);
            return (fullFrequencies, localFrequencies);
        }
    }
}
