using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Kernel.Inference;

// Controlled reading diagnostics only. No answer is converted to a vote or authorization.
internal static class BonsaiCtgReadingMatrix
{
    internal static async Task Run(string modelPath,string governancePath,string input,string output)
    {
        string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        using var stream=File.OpenRead(modelPath);
        string modelHash=Convert.ToHexStringLower(SHA256.HashData(stream));
        if(modelHash!="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3")throw new InvalidDataException("Unexpected model.");
        _=BonsaiCtgSuite.LoadRules(governancePath);
        using var fixture=JsonDocument.Parse(File.ReadAllText(input));
        var cases=fixture.RootElement.EnumerateArray().ToArray();
        if(cases.Length is <1 or >64||cases.Select(c=>c.GetProperty("id").GetString()).Distinct().Count()!=cases.Length)
            throw new InvalidDataException("Invalid diagnostic cases.");
        // Check the whole frozen diagnostic input before starting inference.
        foreach(var row in cases)
        {
            string system=row.GetProperty("system").GetString()!,user=row.GetProperty("user").GetString()!;
            if(!row.GetProperty("diagnosticOnly").GetBoolean()||system.Length+user.Length>6000||Hash(system+"\n"+user)!=row.GetProperty("promptSha256").GetString())
                throw new InvalidDataException("Diagnostic prompt mismatch.");
        }
        using var model=new Qwen3CpuModel(modelPath,4,2048,"auto");
        model.ConfigureTrustedPrefixCache(1,0);
        var rows=new List<object>();
        foreach(var row in cases)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var answer=await model.GenerateAsync(row.GetProperty("system").GetString()!,row.GetProperty("user").GetString()!,128,timeout.Token);
            rows.Add(new{id=row.GetProperty("id").GetString(),promptSha256=row.GetProperty("promptSha256").GetString(),responseSha256=Hash(answer.Text),answer});
            File.WriteAllText(output,JsonSerializer.Serialize(new{diagnosticOnly=true,modelHash,planned=cases.Length,rows},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
