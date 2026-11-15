using System.Collections.Immutable;

namespace AIKernel.Kernel.Inference;

// Token encoding and conversation control are independent contracts. Future
// Gemma/Harmony implementations must not inherit Qwen's special tokens or NFC policy.
internal interface ITextTokenizer
{
    List<int> Encode(string text);
    string Decode(IEnumerable<int> tokens);
    int SpecialToken(string text);
}

internal interface IChatTemplate
{
    ImmutableHashSet<int> StopTokens { get; }
    List<int> Prefix(string trustedSystem);
    List<int> Chat(string system,string user);
    string DecodeReply(List<int> tokens);
}

internal sealed class QwenChatTemplate : IChatTemplate
{
    private readonly ITextTokenizer tokenizer;
    private readonly int start,end,thinkStart,thinkEnd;
    public ImmutableHashSet<int> StopTokens { get; }
    internal QwenChatTemplate(ITextTokenizer tokenizer,int end)
    {
        this.tokenizer=tokenizer;this.end=end;
        start=tokenizer.SpecialToken("<|im_start|>");thinkStart=tokenizer.SpecialToken("<think>");thinkEnd=tokenizer.SpecialToken("</think>");
        if(end!=tokenizer.SpecialToken("<|im_end|>"))throw new NotSupportedException("Unsupported chat boundary.");
        StopTokens=ImmutableHashSet.Create(end);
    }
    public List<int> Prefix(string trustedSystem)
    {
        var ids=new List<int>{start};ids.AddRange(tokenizer.Encode("system\n"+trustedSystem));ids.Add(end);ids.AddRange(tokenizer.Encode("\n"));
        ids.Add(start);return ids;
    }
    public List<int> Chat(string system,string user)
    {
        var ids=Prefix(system);ids.AddRange(tokenizer.Encode("user\n"+user));ids.Add(end);ids.AddRange(tokenizer.Encode("\n"));
        ids.Add(start);ids.AddRange(tokenizer.Encode("assistant\n"));
        ids.Add(thinkStart);ids.AddRange(tokenizer.Encode("\n\n"));ids.Add(thinkEnd);ids.AddRange(tokenizer.Encode("\n\n"));return ids;
    }
    public string DecodeReply(List<int> tokens)
    {
        int lastEnd=tokens.LastIndexOf(thinkEnd);
        var answer=lastEnd>=0?tokens.Skip(lastEnd+1).ToList():tokens;
        if(answer.Contains(thinkStart)||answer.Contains(start)||answer.Contains(end))
            throw new InvalidDataException("Incomplete or unexpected model control sequence.");
        string text=tokenizer.Decode(answer).Trim();
        if(text.Length==0)throw new InvalidDataException("The model did not produce a final answer.");
        return text;
    }
}
