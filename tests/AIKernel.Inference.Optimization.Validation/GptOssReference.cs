using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Kernel.Inference;

internal static partial class GptOssReference
{
    [LibraryImport("libc",EntryPoint="posix_fadvise")]
    private static partial int AdviseFile(Microsoft.Win32.SafeHandles.SafeFileHandle handle,long offset,long length,int advice);

    internal static async Task<bool> Run(string checkpoint, string referencePath, string outputPath,
        GgufMemoryOptions? memory=null, int? sampleIndex=null, bool coldFileCache=false)
    {
        using var reference=JsonDocument.Parse(await File.ReadAllTextAsync(referencePath));
        using var stream=File.OpenRead(checkpoint);
        string digest=Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
        if (digest!=reference.RootElement.GetProperty("checkpointSha256").GetString())
            throw new InvalidDataException("Checkpoint digest differs from the reference.");
        if(coldFileCache)
        {
            if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("File-cache advice requires Linux.");
            const int dontNeed=4;
            int error=AdviseFile(stream.SafeFileHandle,0,0,dontNeed);
            if(error!=0)throw new IOException($"Could not discard cached checkpoint pages (errno {error}).");
        }
        var samples=reference.RootElement.GetProperty("answers");
        if(sampleIndex is int i && (uint)i>=(uint)samples.GetArrayLength()) throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        var load=Stopwatch.StartNew();
        using var model = ModelRegistration.GptOss20B.OpenSession(checkpoint, 4, 128, memory, deviceBudgetBytes: 0);
        load.Stop();
        var rows=new List<object>();
        bool passed=true;
        foreach (var sample in sampleIndex.HasValue ? new[]{samples[sampleIndex.Value]} : samples.EnumerateArray().ToArray())
        {
            string user=sample.GetProperty("user").GetString()!;
            int[] tokens=sample.GetProperty("tokens").Deserialize<int[]>()!;
            if (tokens.Length<2 || tokens[^1]!=200002) throw new InvalidDataException("Expected a terminated reference sample.");
            var hash=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(tokens.AsSpan(0,tokens.Length-1))));
            bool inputMatch=model.EncodeChat("",user).SequenceEqual(sample.GetProperty("promptTokens").Deserialize<int[]>()!);
            var generated=new List<int>();
            long? diskBefore=LinuxReadBytes();
            var elapsed=Stopwatch.StartNew();
            var actual=await model.GenerateAsync("",user,8,default,generated.Add);
            elapsed.Stop();
            bool matches=inputMatch && generated.SequenceEqual(tokens) && actual.GeneratedTokenSha256==hash && actual.Text==sample.GetProperty("output").GetString() &&
                actual.FinishReason=="stop" && actual.OutputTokens==tokens.Length-1;
            rows.Add(new {user,inputMatch,matches,generatedTokens=generated,responseMilliseconds=elapsed.Elapsed.TotalMilliseconds,
                linuxStorageReadBytes=LinuxReadBytes()-diskBefore,actual}); passed &= matches;
        }
        if (rows.Count==0) throw new InvalidDataException("No reference samples.");
        var stats=model.MemoryStatistics;
        if (memory is not null) passed &= stats is { BackingReads: > 0 } && stats.Value.BackingReads > model.DistinctPagesRead;
        using var process=Process.GetCurrentProcess();
        long peakRssBytes=process.PeakWorkingSet64;
        if (memory is not null) passed &= peakRssBytes<=memory.MaximumResidentBytes;
        await File.WriteAllTextAsync(outputPath,JsonSerializer.Serialize(new {digest,passed,rows,coldFileCache,
            initializationMilliseconds=load.Elapsed.TotalMilliseconds,peakRssBytes,maximumResidentBytes=memory?.MaximumResidentBytes,weightCacheBytes=memory?.WeightCacheBytes,
            statistics=stats,model.BackingBytesRead,model.DistinctPagesRead,model.WeightCacheCapacityBytes},new JsonSerializerOptions{WriteIndented=true}));
        return passed;
    }

    private static long? LinuxReadBytes()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/self/io")) return null;
        foreach (string line in File.ReadLines("/proc/self/io"))
            if (line.StartsWith("read_bytes:",StringComparison.Ordinal) && long.TryParse(line.AsSpan(11),out long bytes)) return bytes;
        return null;
    }
}
