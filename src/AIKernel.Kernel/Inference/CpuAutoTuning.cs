using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

/// <summary>Operator configuration for bounded calibration of immutable, verified model weights.</summary>
public sealed record CpuAutoTuningOptions(string ProfileDirectory,string ModelSha256)
{
    /// <summary>Soft calibration deadline; at most one already-started matrix probe can overrun it.</summary>
    public TimeSpan CalibrationBudget {get;init;}=TimeSpan.FromSeconds(2);
    /// <summary>Also measure worker counts up to the configured ceiling. False holds the supplied worker count fixed.</summary>
    public bool TuneThreadCount {get;init;}=true;
    /// <summary>Age after which a saved profile must be measured again.</summary>
    public TimeSpan ProfileMaxAge {get;init;}=TimeSpan.FromHours(24);
}

/// <summary>Selected supported kernels and provenance. Measurements are sampled GEMV work, not a throughput guarantee.</summary>
public sealed record CpuTuningStatus(string PrefillBackend,string DecodeBackend,string Source,
    string ProfileKey,long CalibrationMilliseconds,int SamplesPerBackend,int PrefillThreads=1,int DecodeThreads=1);

internal sealed record CpuKernelChoice(string Backend,int Threads)
{
    internal string Id=>Backend+"@"+Threads;
}

internal sealed class CpuKernelTuner
{
    internal static void AddFloatCandidates(List<string> candidates)
    {
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported && System.Runtime.Intrinsics.X86.Fma.IsSupported) candidates.Add("avx2");
        if (System.Runtime.Intrinsics.X86.Avx512F.IsSupported) candidates.Add("avx512");
    }

    internal sealed record Score(string Phase,string Backend,double MedianMilliseconds,double EmaMilliseconds,int Samples);
    internal sealed record Observation(string Phase,string Backend,int ContextBucket,double EmaMillisecondsPerToken,long Tokens);
    internal sealed record Profile(int Schema,string Identity,DateTimeOffset Updated,string Prefill,string Decode,
        Score[] Scores,Observation[] Observations);
    private readonly CpuAutoTuningOptions options;
    private readonly string identity,path;
    private readonly string[] candidates;
    private readonly Dictionary<string,CpuKernelChoice> choices;
    private Profile? profile;
    private readonly Dictionary<string,Observation> observations=new(StringComparer.Ordinal);
    internal CpuKernelTuner(CpuAutoTuningOptions options,string identity,IEnumerable<CpuKernelChoice> candidates)
    {
        ArgumentNullException.ThrowIfNull(options.ModelSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ProfileDirectory);
        if(options.ModelSha256.Length!=64||!options.ModelSha256.All(Uri.IsHexDigit))throw new ArgumentException("A verified SHA-256 model identity is required.");
        if(options.CalibrationBudget<TimeSpan.FromMilliseconds(10)||options.CalibrationBudget>TimeSpan.FromSeconds(10)
            ||options.ProfileMaxAge<TimeSpan.FromMinutes(1)||options.ProfileMaxAge>TimeSpan.FromDays(7))throw new ArgumentOutOfRangeException(nameof(options));
        this.options=options;this.identity=identity;choices=candidates.ToDictionary(c=>c.Id,StringComparer.Ordinal);this.candidates=choices.Keys.ToArray();
        if(this.candidates.Length==0)throw new ArgumentException("A supported CPU backend is required.");
        path=Path.Combine(Path.GetFullPath(options.ProfileDirectory),identity+".json");
    }
    internal static string Identity(CpuAutoTuningOptions options,int threads,int context,string shape)
    {
        string brand="unknown";
        if(X86Base.IsSupported)
        {
            var max=X86Base.CpuId(unchecked((int)0x80000000),0);
            if((uint)max.Eax>=0x80000004)
            {
                var bytes=new List<byte>();
                for(uint leaf=0x80000002;leaf<=0x80000004;leaf++)
                {var cpu=X86Base.CpuId(unchecked((int)leaf),0);foreach(int value in new[]{cpu.Eax,cpu.Ebx,cpu.Ecx,cpu.Edx})bytes.AddRange(BitConverter.GetBytes(value));}
                brand=Encoding.ASCII.GetString(bytes.ToArray()).Trim('\0',' ');
            }
        }
        string affinity="unavailable";
        try{if(OperatingSystem.IsLinux()||OperatingSystem.IsWindows())affinity=Process.GetCurrentProcess().ProcessorAffinity.ToString("X");}catch(PlatformNotSupportedException){}catch(System.ComponentModel.Win32Exception){}
        var key=string.Join("|","gemv-phase-v1",options.ModelSha256.ToLowerInvariant(),shape,brand,threads,context,
            RuntimeInformation.FrameworkDescription,RuntimeInformation.OSDescription,RuntimeInformation.ProcessArchitecture,
            Environment.ProcessorCount,affinity,Avx.IsSupported,Avx2.IsSupported,Fma.IsSupported,Avx512F.IsSupported,
            typeof(QuantizedCpuMatrix).Module.ModuleVersionId);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
    internal CpuTuningStatus Tune(Func<string,CpuKernelChoice,double> probe,Func<CpuKernelChoice,bool> validate,bool refresh,CancellationToken cancellationToken)
    {
        var watch=Stopwatch.StartNew();
        profile=Load()??profile;
        if(profile is not null&&!refresh&&DateTimeOffset.UtcNow-profile.Updated<=options.ProfileMaxAge)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(validate(choices[profile.Prefill])&&validate(choices[profile.Decode]))return Status("cache",watch.ElapsedMilliseconds);
            profile=null;
        }
        var valid=new List<string>();
        foreach(string candidate in candidates)
        {cancellationToken.ThrowIfCancellationRequested();if(validate(choices[candidate]))valid.Add(candidate);}
        if(valid.Count==0)throw new ArithmeticException("No CPU tuning candidate passed the scalar check.");
        if(valid.Count==1)
        {
            profile=new(1,identity,DateTimeOffset.UtcNow,valid[0],valid[0],[],observations.Values.ToArray());
            Save();return Status("single-supported-backend",watch.ElapsedMilliseconds);
        }
        var samples=new Dictionary<(string Phase,string Backend),List<double>>();
        foreach(string phase in new[]{"prefill","decode"})foreach(string backend in valid)
        {
            samples[(phase,backend)]=[];
            cancellationToken.ThrowIfCancellationRequested();
            if(watch.Elapsed>=options.CalibrationBudget)return Fallback(valid,"budget-exhausted",watch.ElapsedMilliseconds);
            _=probe(phase,choices[backend]); // Warm JIT and the sampled mapped ranges outside scored runs.
        }
        for(int round=0;round<3;round++)foreach(string phase in new[]{"prefill","decode"})
        foreach(string backend in round%2==0?valid:valid.AsEnumerable().Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(watch.Elapsed>=options.CalibrationBudget)return Fallback(valid,"budget-exhausted",watch.ElapsedMilliseconds);
            double elapsed=probe(phase,choices[backend]);
            if(!double.IsFinite(elapsed)||elapsed<=0)throw new ArithmeticException("Invalid CPU probe timing.");
            samples[(phase,backend)].Add(elapsed);
        }
        var scores=samples.Select(pair=>{
            double median=pair.Value.Order().ElementAt(1);
            var old=profile?.Scores.FirstOrDefault(x=>x.Phase==pair.Key.Phase&&x.Backend==pair.Key.Backend);
            return new Score(pair.Key.Phase,pair.Key.Backend,median,old is null?median:old.EmaMilliseconds*.75+median*.25,(old?.Samples??0)+3);
        }).ToArray();
        string Select(string phase,string? incumbent)
        {
            var choices=scores.Where(s=>s.Phase==phase).ToArray();
            var current=choices.FirstOrDefault(x=>x.Backend==incumbent)??choices.First(x=>x.Backend==valid[0]);
            var best=choices.MinBy(x=>x.EmaMilliseconds)!;
            return best.EmaMilliseconds<current.EmaMilliseconds*.95?best.Backend:current.Backend;
        }
        profile=new(1,identity,DateTimeOffset.UtcNow,Select("prefill",profile?.Prefill),Select("decode",profile?.Decode),scores,observations.Values.ToArray());
        Save();return Status("calibrated",watch.ElapsedMilliseconds);
    }
    private CpuTuningStatus Fallback(List<string> valid,string source,long milliseconds)
    {
        string pre=valid.Contains(profile?.Prefill??"")?profile!.Prefill:valid[0];
        string dec=valid.Contains(profile?.Decode??"")?profile!.Decode:valid[0];
        return new(choices[pre].Backend,choices[dec].Backend,source,identity,milliseconds,0,choices[pre].Threads,choices[dec].Threads);
    }
    private CpuTuningStatus Status(string source,long milliseconds)=>new(choices[profile!.Prefill].Backend,choices[profile.Decode].Backend,source,identity,milliseconds,
        profile.Scores.Length==0?0:profile.Scores.Min(s=>s.Samples),choices[profile.Prefill].Threads,choices[profile.Decode].Threads);
    internal void Observe(string phase,string backend,int contextTokens,int tokens,double milliseconds)
    {
        if(tokens<=0||!double.IsFinite(milliseconds)||milliseconds<0)return;
        int bucket=64;while(bucket<contextTokens&&bucket<2048)bucket*=2;
        string key=phase+"/"+backend+"/"+bucket;
        observations.TryGetValue(key,out var old);
        double normalized=milliseconds/tokens;
        observations[key]=new(phase,backend,bucket,old is null?normalized:old.EmaMillisecondsPerToken*.9+normalized*.1,(old?.Tokens??0)+tokens);
        // Online samples are observability only: unmatched prompts must not bias kernel selection.
        // Save them with the next idle calibration, never write input or output text to the profile.
    }
    private Profile? Load()
    {
        try
        {
            if(!File.Exists(path)||new FileInfo(path).Length>65536||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return null;
            var value=JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
            if(value is null||value.Schema!=1||value.Identity!=identity||value.Updated>DateTimeOffset.UtcNow.AddMinutes(1)
                ||!candidates.Contains(value.Prefill)||!candidates.Contains(value.Decode)||value.Scores is null||value.Observations is null
                ||value.Scores.Length>32||value.Observations.Length>128)return null;
            if(value.Scores.Any(s=>s is null||s.Phase is not ("prefill" or "decode")||!candidates.Contains(s.Backend)
                ||s.Samples<3||!double.IsFinite(s.MedianMilliseconds)||s.MedianMilliseconds<=0||!double.IsFinite(s.EmaMilliseconds)||s.EmaMilliseconds<=0))return null;
            if(candidates.Length>1&&new[]{"prefill","decode"}.Any(phase=>!value.Scores.Any(s=>s.Phase==phase&&s.Backend==(phase=="prefill"?value.Prefill:value.Decode))))return null;
            foreach(var item in value.Observations)
                if(item is not null&&(item.Phase is "prefill" or "decode")&&candidates.Contains(item.Backend)&&item.ContextBucket is >=64 and <=2048
                    &&double.IsFinite(item.EmaMillisecondsPerToken)&&item.EmaMillisecondsPerToken>=0&&item.Tokens is >=0 and <1000000000000)
                    observations.TryAdd(item.Phase+"/"+item.Backend+"/"+item.ContextBucket,item);
            return value;
        }
        catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(JsonException){return null;}
    }
    private void Save()
    {
        string? temporary=null;
        try
        {
            string directory=Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return;
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,profile);stream.Flush(true);}
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            File.Move(temporary,path,true);temporary=null;
        }
        catch(IOException){}catch(UnauthorizedAccessException){}
        finally{if(temporary is not null)try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
