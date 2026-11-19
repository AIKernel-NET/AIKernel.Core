using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

internal sealed class CosmosTokenizer
{
    private readonly Dictionary<string,int> vocab=new(StringComparer.Ordinal);
    private readonly Dictionary<(int,int),(int Id,int Rank)> merges=[];
    private readonly byte[][] bytes;
    private readonly int[] single=new int[256];
    private readonly HashSet<int> special=[];
    private readonly HashSet<int> added=[];
    private readonly Regex pieces;
    private readonly bool normalize;
    private static readonly Regex Pieces=new(@"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    private static readonly Regex EdgePieces=new(@"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+|[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    internal CosmosTokenizer(string path,int vocabulary)
    {
        if(new FileInfo(path).Length>33_554_432)throw new InvalidDataException("Tokenizer metadata exceeds 32 MiB.");
        using var document=JsonDocument.Parse(File.ReadAllText(path));var root=document.RootElement;
        var model=root.GetProperty("model");
        normalize=root.GetProperty("normalizer").ValueKind!=JsonValueKind.Null;
        if(model.GetProperty("type").GetString()!="BPE" || (normalize && root.GetProperty("normalizer").GetProperty("type").GetString()!="NFC"))
            throw new NotSupportedException("Expected supported Cosmos byte-level BPE tokenizer.");
        pieces=normalize?Pieces:EdgePieces;
        var pre=root.GetProperty("pre_tokenizer");
        if(pre.GetProperty("type").GetString()!="Sequence")throw new NotSupportedException("Unsupported pre-tokenizer.");
        var stages=pre.GetProperty("pretokenizers");
        if(stages.GetArrayLength()!=2 || stages[0].GetProperty("pattern").GetProperty("Regex").GetString()!=pieces.ToString() ||
            stages[0].GetProperty("behavior").GetString()!="Isolated" || stages[0].GetProperty("invert").GetBoolean() ||
            stages[1].GetProperty("type").GetString()!="ByteLevel" || stages[1].GetProperty("add_prefix_space").GetBoolean() || stages[1].GetProperty("use_regex").GetBoolean())
            throw new NotSupportedException("Unsupported Qwen pre-tokenization definition.");
        bytes=new byte[vocabulary][];
        var alphabet=new char[256];int extra=256;
        for(int b=0;b<256;b++)alphabet[b]=(char)(b is >=33 and <=126 or >=161 and <=172 or >=174 and <=255?b:extra++);
        var reverse=alphabet.Select((c,b)=>(c,b)).ToDictionary(x=>x.c,x=>(byte)x.b);
        foreach(var entry in model.GetProperty("vocab").EnumerateObject())
        {
            int id=entry.Value.GetInt32();if((uint)id>=(uint)vocabulary || bytes[id] is not null)throw new InvalidDataException("Tokenizer ID outside vocabulary.");
            vocab.Add(entry.Name,id);bytes[id]=entry.Name.Select(c=>reverse[c]).ToArray();
        }
        foreach(var entry in root.GetProperty("added_tokens").EnumerateArray())
        {
            int id=entry.GetProperty("id").GetInt32();string content=entry.GetProperty("content").GetString()!;
            if((uint)id>=(uint)vocabulary || (bytes[id] is not null && (!vocab.TryGetValue(content,out int existing)||existing!=id)) || entry.GetProperty("lstrip").GetBoolean() || entry.GetProperty("rstrip").GetBoolean() ||
                entry.GetProperty("single_word").GetBoolean() || entry.GetProperty("normalized").GetBoolean())throw new NotSupportedException("Unsupported added token.");
            vocab.TryAdd(content,id);added.Add(id);if(entry.GetProperty("special").GetBoolean())special.Add(id);bytes[id]=Encoding.UTF8.GetBytes(content);
        }
        for(int b=0;b<256;b++)single[b]=vocab[alphabet[b].ToString()];
        int rank=0;
        foreach(var entry in model.GetProperty("merges").EnumerateArray())
        {
            string[] pair=entry.ValueKind==JsonValueKind.Array?entry.EnumerateArray().Select(x=>x.GetString()!).ToArray():entry.GetString()!.Split(' ');
            if(pair.Length!=2)throw new InvalidDataException("Malformed BPE merge.");
            merges.Add((vocab[pair[0]],vocab[pair[1]]),(vocab[pair[0]+pair[1]],rank++));
        }
    }
    internal int Special(string text)=>vocab.TryGetValue(text,out int id)&&special.Contains(id)?id:throw new InvalidDataException("Missing special token: "+text);
    internal int Added(string text)=>vocab.TryGetValue(text,out int id)&&added.Contains(id)?id:throw new InvalidDataException("Missing added token: "+text);
    internal List<int> Encode(string text)
    {
        var result=new List<int>();
        foreach(Match part in pieces.Matches(normalize?text.Normalize(NormalizationForm.FormC):text))
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
    internal string Decode(IEnumerable<int> tokens)=>Encoding.UTF8.GetString(tokens.Where(i=>!special.Contains(i)).SelectMany(i=>
        (uint)i<(uint)bytes.Length && bytes[i] is not null?bytes[i]:throw new InvalidDataException("Unassigned output token.")).ToArray());
}

internal sealed class CosmosPrompt : IDisposable
{
    internal readonly List<int> Tokens=[];
    internal readonly List<(int T,int H,int W)> Positions=[];
    internal readonly List<int> VisualIndices=[];
    internal readonly List<CosmosPatches> Media=[];
    internal int NextPosition;
    public void Dispose()
    {
        foreach(var p in Media)p.Dispose();CollectionsMarshal.AsSpan(Tokens).Clear();CollectionsMarshal.AsSpan(Positions).Clear();
        CollectionsMarshal.AsSpan(VisualIndices).Clear();Tokens.Clear();Positions.Clear();VisualIndices.Clear();
    }
    internal static CosmosPrompt Build(CosmosTokenizer tokenizer,CosmosModelDefinition d,string system,IReadOnlyList<CosmosContent> content,CosmosExecutionOptions options,
        IReadOnlyList<CosmosChatTurn>? history=null)
    {
        var p=new CosmosPrompt();int position=0,visual=0,patches=0;var pending=new StringBuilder();
        void Add(int id){p.Tokens.Add(id);p.Positions.Add((position,position,position));p.VisualIndices.Add(-1);position++;}
        void Flush(){foreach(int id in tokenizer.Encode(pending.ToString()))Add(id);pending.Clear();}
        void Control(string token){Flush();Add(tokenizer.Special(token));}
        try
        {
            // The published Edge Jinja macro leaves one leading newline.
            if(d.IsEdge)pending.Append('\n');
            long historyLength = history?.Sum(t => t.AssistantReply.Length + t.UserContent.OfType<CosmosContent.Text>().Sum(x => (long)x.Value.Length)) ?? 0;
            if(historyLength+system.Length+content.OfType<CosmosContent.Text>().Sum(x=>(long)x.Value.Length)>options.ContextTokens*64L || content.Count>256)
                throw new ArgumentException("Text or content item budget exceeded.");
            if(d.IsEdge || !string.IsNullOrEmpty(system)){Control("<|im_start|>");pending.Append("system\n").Append(system);Control("<|im_end|>");pending.Append('\n');}
            void AppendUser(IReadOnlyList<CosmosContent> userContent)
            {
                Control("<|im_start|>");pending.Append("user\n");
                foreach(var item in userContent)
                {
                    if(item is CosmosContent.Text text){pending.Append(text.Value);continue;}
                    bool video=item is CosmosContent.Video;
                    CosmosPatches media=item switch
                    {
                        CosmosContent.Image img=>CosmosMediaProcessor.Prepare([img.Frame],false,options,d.TemporalPatchSize,patches),
                        CosmosContent.Video clip=>CosmosMediaProcessor.Prepare(clip.Frames,true,options,d.TemporalPatchSize,patches),
                        _=>throw new ArgumentException("Unsupported content.")
                    };
                    p.Media.Add(media);patches=checked(patches+media.Count);
                    if(patches>options.MaximumVisionPatches)throw new ArgumentException("Total visual patch budget exceeded.");
                    // Cosmos3 Nano's pinned Qwen3-VL processor preserves the outer
                    // video wrapper; Edge's dedicated processor removes it.
                    bool wrapped=video && d.Family=="cosmos3_omni";
                    if(wrapped)Control("<|vision_start|>");
                    for(int t=0;t<media.Time;t++)
                    {
                        if(video)pending.Append('<').Append(media.Timestamps[t].ToString("F1",CultureInfo.InvariantCulture)).Append(" seconds>");
                        Control("<|vision_start|>");int h=media.Height/2,w=media.Width/2;
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                        {p.Tokens.Add(video?d.VideoToken:d.ImageToken);p.Positions.Add((position,position+y,position+x));p.VisualIndices.Add(visual++);}
                        position+=Math.Max(h,w);Control("<|vision_end|>");
                    }
                    if(wrapped)Control("<|vision_end|>");
                }
                Control("<|im_end|>");pending.Append('\n');
            }
            foreach (var turn in history ?? [])
            {
                AppendUser(turn.UserContent);
                Control("<|im_start|>");pending.Append("assistant\n");
                if (d.IsEdge) { Flush(); Add(tokenizer.Added("<think>")); Add(tokenizer.Added("</think>")); }
                pending.Append(turn.AssistantReply);
                Control("<|im_end|>");pending.Append('\n');
            }
            AppendUser(content);
            Control("<|im_start|>");pending.Append("assistant\n");Flush();
            if(d.IsEdge)
            {
                Add(tokenizer.Added("<think>"));
                if(options.EnableThinking){pending.Append('\n');Flush();}else Add(tokenizer.Added("</think>"));
            }
            if(p.Tokens.Count>options.ContextTokens)throw new ArgumentException("Multimodal prompt exceeds context capacity.");
            p.NextPosition=position;return p;
        }
        catch{p.Dispose();throw;}
    }
}
