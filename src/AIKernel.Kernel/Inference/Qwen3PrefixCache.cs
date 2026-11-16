namespace AIKernel.Kernel.Inference;

/// <summary>Trusted prefix KV accounting. Bytes cover float payloads, not CLR object overhead.</summary>
public sealed record Qwen3PrefixCacheStatistics(int Entries,long Bytes,long MaximumBytes,long Hits,long Misses,long Evictions);

// The model admission gate serializes all access. Only host-owned system prefixes enter this cache.
internal sealed class Qwen3PrefixCache : IDisposable
{
    internal sealed record Entry(string System,int Count,float[][] Keys,float[][] Values,long Bytes);
    private readonly int maximumEntries;
    private readonly long maximumBytes;
    private readonly Dictionary<string,LinkedListNode<Entry>> entries=new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> lru=new();
    private long bytes,hits,misses,evictions;
    private bool disposed;
    internal Qwen3PrefixCache(int maximumEntries,long maximumBytes)
    {
        if(maximumEntries is <1 or >16||maximumBytes<0)throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        this.maximumEntries=maximumEntries;this.maximumBytes=maximumBytes;
    }
    internal bool Contains(string system)=>entries.ContainsKey(system);
    internal Entry? Get(string system)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!entries.TryGetValue(system,out var node)){misses++;return null;}
        hits++;lru.Remove(node);lru.AddLast(node);return node.Value;
    }
    internal bool Store(string system,int count,float[][] keys,float[][] values,int kvSize)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(count<1||kvSize<1||keys.Length==0||keys.Length!=values.Length)throw new ArgumentException("Invalid prefix shape.");
        int length=checked(count*kvSize);
        if(keys.Any(a=>a.Length<length)||values.Any(a=>a.Length<length))throw new ArgumentException("Prefix exceeds the used KV range.");
        long size=checked((long)length*keys.Length*2*sizeof(float));
        if(size>maximumBytes)return false;
        if(entries.TryGetValue(system,out var old))Remove(old,false);
        // Evict before copying, so temporary old+new prefix payloads cannot exceed the cap.
        while(entries.Count>=maximumEntries||bytes>maximumBytes-size)Remove(lru.First!,true);
        var copiedKeys=new float[keys.Length][];var copiedValues=new float[values.Length][];
        try
        {
            for(int i=0;i<keys.Length;i++)
            {copiedKeys[i]=keys[i].AsSpan(0,length).ToArray();copiedValues[i]=values[i].AsSpan(0,length).ToArray();}
            var entry=new Entry(system,count,copiedKeys,copiedValues,size);
            var node=new LinkedListNode<Entry>(entry);entries.Add(system,node);lru.AddLast(node);bytes+=size;return true;
        }
        catch{Clear(copiedKeys);Clear(copiedValues);throw;}
    }
    private void Remove(LinkedListNode<Entry> node,bool evicted)
    {
        lru.Remove(node);entries.Remove(node.Value.System);bytes-=node.Value.Bytes;
        Clear(node.Value.Keys);Clear(node.Value.Values);if(evicted)evictions++;
    }
    private static void Clear(float[][] arrays){foreach(var a in arrays)if(a is not null)Array.Clear(a);}
    internal Qwen3PrefixCacheStatistics Statistics=>new(entries.Count,bytes,maximumBytes,hits,misses,evictions);
    public void Dispose(){if(disposed)return;while(lru.First is {} node)Remove(node,false);disposed=true;}
}
