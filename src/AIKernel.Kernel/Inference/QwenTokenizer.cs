using System.Text;
using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

/// <summary>Qwen byte-level BPE with merge ranks from the model, and no user-injected special tokens.</summary>
internal sealed class QwenTokenizer : ITextTokenizer
{
    private readonly Dictionary<string,int> vocab=new(StringComparer.Ordinal);
    private readonly Dictionary<(int,int),(int Id,int Rank)> merges=[];
    private readonly byte[][] bytes;
    private readonly char[] alphabet=new char[256];
    private readonly int[] single=new int[256];
    private static readonly Regex Pieces=new(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    internal int End {get;}
    private readonly IChatTemplate chat;
    internal QwenTokenizer(GgufWeights file)
    {
        if(file.Metadata.GetValueOrDefault("tokenizer.ggml.model") is not "gpt2" ||
           file.Metadata.GetValueOrDefault("tokenizer.ggml.pre") is not "qwen2")throw new NotSupportedException("Expected Qwen2 byte-level tokenizer.");
        var tokens=file.Tokens;bytes=new byte[tokens.Length][];
        int extra=256;
        for(int b=0;b<256;b++)alphabet[b]=(char)(b is >=33 and <=126 or >=161 and <=172 or >=174 and <=255?b:extra++);
        var reverse=alphabet.Select((c,b)=>(c,b)).ToDictionary(x=>x.c,x=>(byte)x.b);
        for(int i=0;i<tokens.Length;i++)
        {
            vocab.Add(tokens[i],i);
            bytes[i]=tokens[i].All(reverse.ContainsKey)?tokens[i].Select(c=>reverse[c]).ToArray():[];
        }
        for(int b=0;b<256;b++)single[b]=vocab[alphabet[b].ToString()];
        for(int rank=0;rank<file.Merges.Length;rank++)
        {
            var text=file.Merges[rank];int split=text.IndexOf(' ');
            if(split<1)throw new InvalidDataException("Invalid merge.");
            var a=text[..split];var b=text[(split+1)..];
            if(vocab.TryGetValue(a,out int x)&&vocab.TryGetValue(b,out int y)&&vocab.TryGetValue(a+b,out int z))
                merges.TryAdd((x,y),(z,rank));
        }
        End=file.Int("tokenizer.ggml.eos_token_id");chat=new QwenChatTemplate(this,End);
    }
    internal List<int> Encode(string text)
    {
        var result=new List<int>();
        foreach(Match part in Pieces.Matches(text.Normalize(NormalizationForm.FormC)))
        {
            var ids=Encoding.UTF8.GetBytes(part.Value).Select(b=>single[b]).ToList();
            while(ids.Count>1)
            {
                int best=-1,rank=int.MaxValue,merged=0;
                for(int i=0;i<ids.Count-1;i++)if(merges.TryGetValue((ids[i],ids[i+1]),out var m)&&m.Rank<rank){best=i;rank=m.Rank;merged=m.Id;}
                if(best<0)break;ids[best]=merged;ids.RemoveAt(best+1);
            }
            result.AddRange(ids);
        }
        return result;
    }
    internal List<int> Prefix(string system)
        =>chat.Prefix(system);
    internal List<int> Chat(string system,string user)
        =>chat.Chat(system,user);
    internal string DecodeReply(List<int> tokens)
        =>chat.DecodeReply(tokens);
    internal bool IsStopToken(int token)=>chat.StopTokens.Contains(token);
    List<int> ITextTokenizer.Encode(string text)=>Encode(text);
    string ITextTokenizer.Decode(IEnumerable<int> tokens)=>Decode(tokens);
    int ITextTokenizer.SpecialToken(string text)=>vocab.TryGetValue(text,out int id)?id:throw new InvalidDataException("Missing control token: "+text);
    internal string Decode(IEnumerable<int> tokens)=>Encoding.UTF8.GetString(tokens.SelectMany(i=>bytes[i]).ToArray());
}
