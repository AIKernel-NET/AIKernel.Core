using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

internal sealed class HarmonyChatTemplate
{
    private readonly GgufByteBpe bpe;
    private readonly int start, end, message, channel, stop;
    private const string Upper = @"[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]";
    private const string Lower = @"[\p{Ll}\p{Lm}\p{Lo}\p{M}]";
    private const string Contraction = @"(?i:'s|'t|'re|'ve|'m|'ll|'d)?";
    private const string Lead = @"[^\r\n\p{L}\p{N}]?";
    private static readonly Regex Pieces = new(Lead+Upper+"*"+Lower+"+"+Contraction+"|"+Lead+Upper+"+"+Lower+"*"+Contraction+
        @"|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal HarmonyChatTemplate(GgufWeights file)
    {
        bpe = new(file);
        start=bpe.SpecialToken("<|start|>"); end=bpe.SpecialToken("<|end|>"); message=bpe.SpecialToken("<|message|>");
        channel=bpe.SpecialToken("<|channel|>"); stop=bpe.SpecialToken("<|return|>");
        if (stop != file.Int("tokenizer.ggml.eos_token_id")) throw new InvalidDataException("Invalid Harmony terminator.");
    }

    internal List<int> Encode(string system, string user)
    {
        var ids = new List<int>();
        void AddMessage(string role, string text)
        {
            ids.Add(start); ids.AddRange(bpe.Encode(role,Pieces)); ids.Add(message);
            ids.AddRange(bpe.Encode(text,Pieces,unicodeScalars:true)); ids.Add(end);
        }
        if (system.Length > 0) AddMessage("system",system);
        AddMessage("user",user);
        ids.Add(start); ids.AddRange(bpe.Encode("assistant",Pieces)); ids.Add(channel);
        ids.AddRange(bpe.Encode("final",Pieces)); ids.Add(message);
        return ids;
    }

    internal bool IsStop(int token) => token == stop;
    internal static void RequireText(int token)
    {
        if (token >= 199998) throw new InvalidDataException("Unexpected Harmony control in final answer.");
    }
    internal string Decode(List<int> tokens) => bpe.Decode(tokens);
}
