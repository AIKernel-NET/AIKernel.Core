namespace AIKernel.Kernel.Inference;

/// <summary>One owning layer's cache. Consumers reference this object and never append to it.</summary>
internal sealed class Gemma4KvCache : IDisposable
{
    internal readonly float[] Keys,Values;
    internal int Width {get;}
    internal int Capacity {get;}
    internal int Count {get;private set;}
    internal int Start=>Math.Max(0,Count-Capacity);
    private readonly bool sliding;
    internal Gemma4KvCache(int width,int context,int window)
    {
        if(width<1||context<1||window<0)throw new ArgumentOutOfRangeException(nameof(width));
        Width=width;sliding=window>0;Capacity=sliding?Math.Min(context,window):context;
        Keys=new float[checked(width*Capacity)];Values=new float[Keys.Length];
    }
    internal void Append(int position,ReadOnlySpan<float> key,ReadOnlySpan<float> value)
    {
        if(position!=Count||key.Length!=Width||value.Length!=Width||(!sliding&&position>=Capacity))
            throw new ArgumentException("Invalid KV append position or shape.");
        int offset=position%Capacity*Width;key.CopyTo(Keys.AsSpan(offset,Width));value.CopyTo(Values.AsSpan(offset,Width));Count++;
    }
    internal ReadOnlySpan<float> Key(int position)=>Keys.AsSpan(Offset(position),Width);
    internal ReadOnlySpan<float> Value(int position)=>Values.AsSpan(Offset(position),Width);
    private int Offset(int position)
    {
        if(position<Start||position>=Count)throw new ArgumentOutOfRangeException(nameof(position));
        return position%Capacity*Width;
    }
    public void Dispose(){Array.Clear(Keys);Array.Clear(Values);Count=0;}
}
