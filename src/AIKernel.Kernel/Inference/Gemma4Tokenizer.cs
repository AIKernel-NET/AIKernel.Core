using System.Collections.Immutable;
using System.Text;

namespace AIKernel.Kernel.Inference;

/// <summary>Gemma 4 Unicode BPE: space replacement, byte fallback, no NFC or implicit BOS.</summary>
internal sealed class Gemma4Tokenizer : ITextTokenizer
{
    private readonly Dictionary<string,int> vocab=new(StringComparer.Ordinal);
    private readonly Dictionary<(int,int),(int Id,int Rank)> merges=[];
    private readonly byte[][] decoded;
    private readonly int[] byteIds=new int[256];
    private readonly ImmutableHashSet<int> controls;

    internal Gemma4Tokenizer(GgufWeights file)
        :this(file.Metadata.GetValueOrDefault("tokenizer.ggml.model") as string,file.Tokens,file.Merges,
            ((object[])file.Metadata["tokenizer.ggml.token_type"]).Select(Convert.ToInt32).ToArray()) { }

    internal Gemma4Tokenizer(string? model,string[] tokens,string[] mergeRules,int[] types)
    {
        if(model!="gemma4"||tokens.Length!=types.Length)throw new NotSupportedException("Gemma 4 tokenizer metadata required.");
        // GGUF uses both CONTROL and USER_DEFINED for Gemma's added special tokens.
        controls=Enumerable.Range(0,tokens.Length).Where(i=>types[i] is 3 or 4).ToImmutableHashSet();
        decoded=new byte[tokens.Length][];
        for(int i=0;i<tokens.Length;i++)
        {
            vocab.Add(tokens[i],i);
            string text=tokens[i];
            decoded[i]=text.Length==6&&text.StartsWith("<0x",StringComparison.Ordinal)&&text[5]=='>'
                &&byte.TryParse(text.AsSpan(3,2),System.Globalization.NumberStyles.HexNumber,null,out byte b)
                ?[b]:Encoding.UTF8.GetBytes(text.Replace('▁',' '));
        }
        for(int b=0;b<256;b++)byteIds[b]=vocab[$"<0x{b:X2}>"];
        for(int rank=0;rank<mergeRules.Length;rank++)
        {
            string rule=mergeRules[rank];int split=rule.IndexOf(' ');
            if(split<=0||split==rule.Length-1)throw new InvalidDataException("Invalid Gemma BPE merge.");
            string left=rule[..split],right=rule[(split+1)..];
            if(!vocab.TryGetValue(left,out int a)||!vocab.TryGetValue(right,out int b)||!vocab.TryGetValue(left+right,out int c))
                throw new InvalidDataException("Gemma BPE merge references missing tokens.");
            if(!controls.Contains(c))merges.TryAdd((a,b),(c,rank));
        }
    }

    public List<int> Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var initial=new List<int>();
        foreach(var rune in text.Replace(' ','▁').EnumerateRunes())
        {
            string piece=rune.ToString();
            if(vocab.TryGetValue(piece,out int id)&&!controls.Contains(id))initial.Add(id);
            else foreach(byte b in Encoding.UTF8.GetBytes(piece))initial.Add(byteIds[b]);
        }
        int count=initial.Count;if(count<2)return initial;
        var ids=initial.ToArray();var next=new int[count];var previous=new int[count];
        for(int i=0;i<count;i++){next[i]=i+1<count?i+1:-1;previous[i]=i-1;}
        var queue=new PriorityQueue<(int Left,int Right,int A,int B,int Merged),(int Rank,int Position)>();
        void Offer(int i)
        {
            if(i<0||ids[i]<0)return;int j=next[i];
            if(j>=0&&merges.TryGetValue((ids[i],ids[j]),out var merge))queue.Enqueue((i,j,ids[i],ids[j],merge.Id),(merge.Rank,i));
        }
        for(int i=0;i<count-1;i++)Offer(i);
        while(queue.TryDequeue(out var pair,out _))
        {
            int i=pair.Left,j=pair.Right;
            if(ids[i]!=pair.A||ids[j]!=pair.B||next[i]!=j)continue;
            ids[i]=pair.Merged;ids[j]=-1;next[i]=next[j];
            if(next[i]>=0)previous[next[i]]=i;
            Offer(previous[i]);Offer(i);
        }
        var result=new List<int>();for(int i=0;i>=0;i=next[i])result.Add(ids[i]);return result;
    }
    public string Decode(IEnumerable<int> tokens)
    {
        using var bytes=new MemoryStream();
        foreach(int token in tokens)
        {if((uint)token>=(uint)decoded.Length)throw new ArgumentOutOfRangeException(nameof(tokens));bytes.Write(decoded[token]);}
        return Encoding.UTF8.GetString(bytes.GetBuffer(),0,(int)bytes.Length);
    }
    public int SpecialToken(string text)=>vocab.TryGetValue(text,out int id)&&controls.Contains(id)?id
        :throw new InvalidDataException("Missing Gemma control token: "+text);
    internal bool IsControl(int token)=>controls.Contains(token);
}

/// <summary>Canonical Gemma 4 single-turn text chat with thinking/tools disabled.</summary>
internal sealed class Gemma4ChatTemplate : IChatTemplate
{
    private readonly Gemma4Tokenizer tokenizer;
    private readonly int bos,start,end,channel,channelEnd,toolResponse;
    public ImmutableHashSet<int> StopTokens {get;}
    internal Gemma4ChatTemplate(Gemma4Tokenizer tokenizer)
    {
        this.tokenizer=tokenizer;bos=tokenizer.SpecialToken("<bos>");start=tokenizer.SpecialToken("<|turn>");
        end=tokenizer.SpecialToken("<turn|>");channel=tokenizer.SpecialToken("<|channel>");channelEnd=tokenizer.SpecialToken("<channel|>");
        toolResponse=tokenizer.SpecialToken("<|tool_response>");
        StopTokens=ImmutableHashSet.Create(end,tokenizer.SpecialToken("<eos>"),toolResponse);
    }
    public List<int> Prefix(string trustedSystem)
    {
        var result=new List<int>{bos,start};result.AddRange(tokenizer.Encode("system\n"+trustedSystem.Trim()));
        result.Add(end);result.AddRange(tokenizer.Encode("\n"));return result;
    }
    public List<int> Chat(string system,string user)
    {
        var result=Prefix(system);result.Add(start);result.AddRange(tokenizer.Encode("user\n"+user.Trim()));
        result.Add(end);result.AddRange(tokenizer.Encode("\n"));result.Add(start);result.AddRange(tokenizer.Encode("model\n"));return result;
    }
    public string DecodeReply(List<int> tokens)
    {
        var visible=new List<int>();bool hidden=false;
        foreach(int token in tokens)
        {
            if(token==channel){if(hidden)throw new InvalidDataException("Nested Gemma channel.");hidden=true;continue;}
            if(token==channelEnd){if(!hidden)throw new InvalidDataException("Unmatched Gemma channel.");hidden=false;continue;}
            if(tokenizer.IsControl(token))throw new InvalidDataException("Unexpected Gemma control sequence; text-only generation cannot execute tools.");
            if(!hidden)visible.Add(token);
        }
        if(hidden)throw new InvalidDataException("Incomplete Gemma channel.");
        return tokenizer.Decode(visible).Trim();
    }
}
