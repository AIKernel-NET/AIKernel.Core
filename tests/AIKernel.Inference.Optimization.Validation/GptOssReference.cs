using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using AIKernel.Kernel.Inference;

internal static class GptOssReference
{
    internal static async Task<bool> Run(string checkpoint, string referencePath, string outputPath)
    {
        using var reference=JsonDocument.Parse(await File.ReadAllTextAsync(referencePath));
        using var stream=File.OpenRead(checkpoint);
        string digest=Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
        if (digest!=reference.RootElement.GetProperty("checkpointSha256").GetString())
            throw new InvalidDataException("Checkpoint digest differs from the reference.");
        using var model=new GptOss20BCpuModel(checkpoint,4,128);
        var rows=new List<object>();
        bool passed=true;
        foreach (var sample in reference.RootElement.GetProperty("answers").EnumerateArray())
        {
            string user=sample.GetProperty("user").GetString()!;
            int[] tokens=sample.GetProperty("tokens").Deserialize<int[]>()!;
            if (tokens.Length<2 || tokens[^1]!=200002) throw new InvalidDataException("Expected a terminated reference sample.");
            var hash=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(tokens.AsSpan(0,tokens.Length-1))));
            bool inputMatch=model.EncodeChat("",user).SequenceEqual(sample.GetProperty("promptTokens").Deserialize<int[]>()!);
            var actual=await model.GenerateAsync("",user,8);
            bool matches=inputMatch && actual.GeneratedTokenSha256==hash && actual.Text==sample.GetProperty("output").GetString() &&
                actual.FinishReason=="stop" && actual.OutputTokens==tokens.Length-1;
            rows.Add(new {user,inputMatch,matches,actual}); passed &= matches;
        }
        if (rows.Count==0) throw new InvalidDataException("No reference samples.");
        await File.WriteAllTextAsync(outputPath,JsonSerializer.Serialize(new {digest,passed,rows},new JsonSerializerOptions{WriteIndented=true}));
        return passed;
    }
}
