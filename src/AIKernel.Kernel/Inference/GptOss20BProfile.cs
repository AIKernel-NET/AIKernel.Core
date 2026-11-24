namespace AIKernel.Kernel.Inference;

public partial class MixtureModelSession
{
    internal static class GptOss20BProfile
    {
        internal const int Width = 2880, Head = 64, Heads = 64, KvHeads = 8, Experts = 32, Layers = 24;
        internal const int KvWidth = Head * KvHeads, AttentionWidth = Head * Heads;
        internal const int Vocab = 201088;
        internal const int ActiveExperts = 4;
        internal static void Validate(GgufWeights file)
        {
            if (file.Metadata.GetValueOrDefault("general.architecture") is not "gpt-oss" ||
                file.Metadata.GetValueOrDefault("tokenizer.ggml.pre") is not "gpt-4o" ||
                file.Metadata.GetValueOrDefault("gpt-oss.rope.scaling.type") is not "yarn" || file.Tokens.Length != Vocab)
                throw new NotSupportedException("Expected GPT-OSS-20B with an o200k Harmony tokenizer.");
            ValidateDimensions(file);
            ValidateRotary(file);
        }

        private static void ValidateDimensions(GgufWeights file)
        {
            RequireInt(file, "block_count",Layers); RequireInt(file, "embedding_length",Width);
            RequireInt(file, "expert_feed_forward_length",Width); RequireInt(file, "expert_count",Experts); RequireInt(file, "expert_used_count",ActiveExperts);
            RequireInt(file, "attention.head_count",Heads); RequireInt(file, "attention.head_count_kv",KvHeads);
            RequireInt(file, "attention.key_length",Head); RequireInt(file, "attention.value_length",Head); RequireInt(file, "attention.sliding_window",128);
            RequireInt(file, "rope.scaling.original_context_length",4096);
        }

        private static void RequireInt(GgufWeights file, string key, int expected)
        {
            if (file.Int("gpt-oss." + key) != expected) throw new InvalidDataException("Unsupported GPT-OSS " + key);
        }

        private static void ValidateRotary(GgufWeights file)
        {
            if (file.Float("gpt-oss.rope.freq_base",0) != 150000 || file.Float("gpt-oss.rope.scaling.factor",0) != 32 ||
                file.Float("gpt-oss.attention.layer_norm_rms_epsilon",0) != 1e-5f ||
                file.Float("gpt-oss.rope.scaling.yarn_beta_fast",32) != 32 || file.Float("gpt-oss.rope.scaling.yarn_beta_slow",1) != 1)
                throw new InvalidDataException("Unsupported GPT-OSS rotary or normalization parameters.");
        }

        internal static float[] Rotary()
        {
            var frequencies = new float[Head / 2];
            double boundary(double rotations) => (Head/2) * Math.Log(4096/(rotations*2*Math.PI))/Math.Log(150000);
            double low = boundary(32), high = boundary(1);
            for (int i=0; i<frequencies.Length; i++)
            {
                double ramp = Math.Clamp((i-low)/(high-low),0,1);
                frequencies[i] = (float)((1-ramp+ramp/32)/Math.Pow(150000,2d*i/Head));
            }
            return frequencies;
        }
    }
}
