using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AIKernel.Kernel.Inference;

/// <summary>Optional local FFmpeg adapter for image/video files. FFmpeg is a media decoder only; all model computation stays in AIKernel.</summary>
public sealed class CosmosMediaDecoder
{
    private readonly string executable;
    /// <summary>Uses a caller-installed FFmpeg executable; nothing is downloaded or installed by the SDK.</summary>
    public CosmosMediaDecoder(string ffmpegExecutable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegExecutable);executable=Path.GetFullPath(ffmpegExecutable);
        if(!File.Exists(executable))throw new FileNotFoundException("FFmpeg executable not found.",executable);
    }
    /// <summary>Decodes one local image to explicit RGB dimensions, which must be multiples of 32.</summary>
    public async Task<CosmosContent.Image> ReadImageAsync(string path,int width=256,int height=256,CancellationToken cancellationToken=default)
    {
        var frames=await Decode(path,width,height,1,1,1,false,cancellationToken).ConfigureAwait(false);
        return new(frames[0]);
    }
    /// <summary>Samples a bounded clip, preserving selected source-frame timestamps rather than inventing FPS.
    /// Requested dimensions are explicit (no model-dependent automatic resize). Sampling starts at clip time zero.</summary>
    public async Task<CosmosContent.Video> ReadVideoAsync(string path,int width=256,int height=256,double intervalSeconds=.5,
        double durationSeconds=4,int maximumFrames=8,CancellationToken cancellationToken=default)
    {
        if(!double.IsFinite(intervalSeconds)||intervalSeconds<.05||intervalSeconds>60||!double.IsFinite(durationSeconds)||durationSeconds<=0||durationSeconds>60||maximumFrames is <2 or >64)
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        return new(await Decode(path,width,height,maximumFrames,intervalSeconds,durationSeconds,true,cancellationToken).ConfigureAwait(false));
    }
    private async Task<CosmosRgbFrame[]> Decode(string path,int width,int height,int count,double interval,double duration,bool video,CancellationToken token)
    {
        if(width is <32 or >2048 || height is <32 or >2048 || width%32!=0 || height%32!=0 || (long)width*height*count>16_777_216)
            throw new ArgumentOutOfRangeException(nameof(width),"Decoded media exceeds the adapter pixel budget.");
        string source=Path.GetFullPath(path);if(!File.Exists(source))throw new FileNotFoundException("Local media not found.",source);
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        string filter=(video?$"select='isnan(prev_selected_t)+gte(t-prev_selected_t,{interval.ToString("R",CultureInfo.InvariantCulture)})',":"")+
            $"scale={width}:{height}:flags=bicubic,showinfo";
        foreach(string arg in new[]{"-hide_banner","-nostdin","-loglevel","info","-threads","1","-filter_threads","1","-protocol_whitelist","file,pipe","-copyts","-start_at_zero","-i",source,
            "-map","0:v:0","-an","-sn","-dn","-t",duration.ToString("R",CultureInfo.InvariantCulture),"-vf",filter,
            "-frames:v",count.ToString(CultureInfo.InvariantCulture),"-fps_mode","passthrough","-threads","1","-pix_fmt","rgb24","-f","rawvideo","pipe:1"})start.ArgumentList.Add(arg);
        using var process=new Process{StartInfo=start};using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(2));
        if(!process.Start())throw new IOException("Could not start the media decoder.");
        void Stop(){try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}}
        async Task<byte[]> Read(Stream stream,int limit)
        {
            try
            {
                using var output=new MemoryStream();byte[] buffer=new byte[8192];
                try
                {
                    int n;while((n=await stream.ReadAsync(buffer,timeout.Token).ConfigureAwait(false))>0)
                    {if(output.Length+n>limit)throw new InvalidDataException("Media decoder output exceeded its budget.");output.Write(buffer,0,n);}
                    return output.ToArray();
                }
                finally{Array.Clear(buffer);if(output.TryGetBuffer(out var data))data.AsSpan().Clear();}
            }
            catch{Stop();throw;}
        }
        byte[]? pixels=null;
        try
        {
            var stdout=Read(process.StandardOutput.BaseStream,checked(width*height*3*count));var stderr=Read(process.StandardError.BaseStream,262144);
            try{await Task.WhenAll(stdout,stderr).ConfigureAwait(false);}finally{if(stdout.IsCompletedSuccessfully)pixels=stdout.Result;}
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            if(process.ExitCode!=0)throw new InvalidDataException("FFmpeg could not decode this local media file.");
            int bytes=checked(width*height*3);if(pixels!.Length==0 || pixels.Length%bytes!=0)throw new InvalidDataException("Incomplete RGB frame from decoder.");
            var times=Regex.Matches(Encoding.UTF8.GetString(stderr.Result),@"\bn:\s*\d+\s+pts:.*?\bpts_time:([+\-0-9.eE]+)",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1))
                .Select(m=>double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)).ToArray();
            int frames=pixels.Length/bytes;if(times.Length!=frames)throw new InvalidDataException("Decoded frames and original timestamps differ.");
            var result=new CosmosRgbFrame[frames];
            for(int i=0;i<frames;i++)
            {
                if(!double.IsFinite(times[i])||times[i]<0||(i>0&&times[i]<times[i-1]))throw new InvalidDataException("Invalid source-frame timestamps.");
                result[i]=new(width,height,pixels.AsMemory(i*bytes,bytes).ToArray(),video?times[i]:0);
            }
            return result;
        }
        catch{Stop();throw;}
        finally{if(pixels is not null)Array.Clear(pixels);}
    }
}
