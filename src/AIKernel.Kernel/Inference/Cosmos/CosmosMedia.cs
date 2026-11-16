namespace AIKernel.Kernel.Inference;

/// <summary>One caller-decoded RGB24 frame. Rows are tightly packed; timestamps are seconds from the clip start.</summary>
public sealed record CosmosRgbFrame(int Width, int Height, ReadOnlyMemory<byte> Pixels, double TimestampSeconds = 0);

/// <summary>Ordered user content. Media is local decoded data; the model does not fetch URLs.</summary>
public abstract record CosmosContent
{
    /// <summary>Ordinary user text; model control tokens are not activated.</summary>
    public sealed record Text(string Value) : CosmosContent;
    /// <summary>An RGB image. Temporal packing follows the selected model.</summary>
    public sealed record Image(CosmosRgbFrame Frame) : CosmosContent;
    /// <summary>Sampled RGB video frames with original timestamps, in chronological order.</summary>
    public sealed record Video(IReadOnlyList<CosmosRgbFrame> Frames) : CosmosContent;
}

/// <summary>Bounds for the initial CPU multimodal implementation. Resizing and frame sampling are explicit caller decisions.</summary>
public sealed record CosmosExecutionOptions
{
    /// <summary>Maximum simultaneous matrix workers.</summary>
    public int CpuThreads { get; init; } = 4;
    /// <summary>Enables bounded weight/KV paging and adaptive cache allocation. Null preserves the mapped path.</summary>
    public CosmosMemoryOptions? Memory { get; init; }
    /// <summary>Total prompt and output token admission limit.</summary>
    public int ContextTokens { get; init; } = 2048;
    /// <summary>Total unmerged visual patches per request. A 256×256 image has 256 patches.</summary>
    public int MaximumVisionPatches { get; init; } = 1024;
    /// <summary>Maximum decoded video frames in one content item.</summary>
    public int MaximumVideoFrames { get; init; } = 64;
    /// <summary>Cosmos3 Edge template option. True preserves the published thinking
    /// prefix; false uses its empty-think prefix. Nano's template does not use this option.</summary>
    public bool EnableThinking { get; init; } = true;

    internal void Validate()
    {
        if(CpuThreads is <1 or >64 || ContextTokens is <32 or >32768 || MaximumVisionPatches is <4 or >4096 || MaximumVideoFrames is <2 or >768)
            throw new ArgumentOutOfRangeException(nameof(CosmosExecutionOptions));
    }
}

internal sealed class CosmosPatches : IDisposable
{
    internal readonly float[] Values;
    internal readonly int Time, Height, Width;
    internal readonly double[] Timestamps;
    internal int Count => Time * Height * Width;
    internal int Tokens => Count / 4;
    internal CosmosPatches(float[] values, int time, int height, int width, double[] timestamps)
    { Values=values; Time=time; Height=height; Width=width; Timestamps=timestamps; }
    public void Dispose() { Array.Clear(Values); Array.Clear(Timestamps); }
}

internal static class CosmosMediaProcessor
{
    // Exactly the Qwen3-VL layout: time, block-row, block-column, merge-row,
    // merge-column, channel, temporal-slice, patch-row, patch-column.
    // No hidden resize or synthetic FPS. Caller can supply any multiple of 32.
    internal static CosmosPatches Prepare(IReadOnlyList<CosmosRgbFrame> frames, bool video, CosmosExecutionOptions limits, int temporalPatch=2)
    {
        if(temporalPatch is not (1 or 2))throw new ArgumentOutOfRangeException(nameof(temporalPatch));
        if(frames.Count==0 || frames.Count>(video?limits.MaximumVideoFrames:1)) throw new ArgumentException("Invalid frame count.");
        var first=frames[0]; int height=first.Height, width=first.Width;
        if(height<32 || width<32 || height%32!=0 || width%32!=0 || height>16384 || width>16384)
            throw new ArgumentException("RGB dimensions must be bounded positive multiples of 32; resize before admission.");
        int time=(frames.Count+temporalPatch-1)/temporalPatch, gh=height/16, gw=width/16;
        long patches=(long)time*gh*gw;
        if(patches>limits.MaximumVisionPatches) throw new ArgumentException("Visual patch budget exceeded.");
        for(int f=0;f<frames.Count;f++)
        {
            var frame=frames[f];
            if(frame.Height!=height || frame.Width!=width || frame.Pixels.Length!=checked(height*width*3) ||
                !double.IsFinite(frame.TimestampSeconds) || frame.TimestampSeconds<0 ||
                (f>0 && frame.TimestampSeconds<frames[f-1].TimestampSeconds))
                throw new ArgumentException("Invalid RGB shape or non-monotonic video timestamp.");
        }
        var values=new float[checked((int)patches*768*temporalPatch)];var times=new double[time];int target=0;
        for(int t=0;t<time;t++)
        {
            times[t]=(frames[t*temporalPatch].TimestampSeconds+frames[Math.Min(t*temporalPatch+temporalPatch-1,frames.Count-1)].TimestampSeconds)/2;
            for(int by=0;by<gh/2;by++) for(int bx=0;bx<gw/2;bx++)
            for(int my=0;my<2;my++) for(int mx=0;mx<2;mx++)
            {
                if(temporalPatch==1)
                {
                    // Edge's SigLIP2 projection uses HWC inside each patch.
                    var pixels=frames[t].Pixels.Span;
                    for(int y=0;y<16;y++)for(int x=0;x<16;x++)for(int c=0;c<3;c++)
                        values[target++]=(pixels[(((by*2+my)*16+y)*width+(bx*2+mx)*16+x)*3+c]-127.5f)/127.5f;
                }
                else for(int channel=0;channel<3;channel++)for(int dt=0;dt<temporalPatch;dt++)
                {
                    var pixels=frames[Math.Min(t*temporalPatch+dt,frames.Count-1)].Pixels.Span;
                    for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                    {
                        int offset=(((by*2+my)*16+y)*width+(bx*2+mx)*16+x)*3+channel;
                        values[target++]=(pixels[offset]-127.5f)/127.5f;
                    }
                }
            }
        }
        return new(values,time,gh,gw,times);
    }
}
