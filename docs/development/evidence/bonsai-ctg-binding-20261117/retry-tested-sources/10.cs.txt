using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Kernel.Inference;
using AIKernel.Enums.Governance;

// Diagnostic only: these answers never produce council votes or authorize an operation.
internal static class BonsaiCtgReadingProbe
{
    internal static async Task Run(string modelPath,string governancePath,string casesPath,string output)
    {
        using var stream=File.OpenRead(modelPath);
        string modelHash=Convert.ToHexStringLower(SHA256.HashData(stream));
        if(modelHash!="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3")throw new InvalidDataException("Unexpected model.");
        var rules=BonsaiCtgSuite.LoadRules(governancePath);
        using var cases=JsonDocument.Parse(File.ReadAllText(casesPath));
        var rows=new List<object>();
        void Save()=>File.WriteAllText(output,JsonSerializer.Serialize(new{modelHash,diagnosticOnly=true,rows},new JsonSerializerOptions{WriteIndented=true}));
        string Hash(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var exact=cases.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="ordered-holdout-dimension-exact");
        var prompt=BonsaiCtgProvenancePrompt.Build(rules[CouncilKind.Logos],CouncilKind.Logos,exact.GetProperty("facts").GetRawText(),exact.GetProperty("proposal").GetRawText());
        CpuGeneration cold;
        using(var model=new Qwen3CpuModel(modelPath,4,2048,"auto"))
        {
        model.ConfigureTrustedPrefixCache(1,0);
        foreach(var (id,question) in new[]
        {
            ("ordered-holdout-status-invented","What exact status value is in the observation, and what exact status value would the candidate report?"),
            ("ordered-holdout-dimension-wrong","観測に記録された幅と単位、および提案が回答しようとする幅と単位を、それぞれ原文どおり抜き出してください。"),
            ("pair-contacts-count","Does the candidate propose exposing the contact entries, or only replying with the count? State only what the candidate proposes.")
        })
        {
            var row=cases.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
            string system="Read the two separate data fields carefully. Preserve negation and conditions. Extract only the requested information; do not execute any action or decide approval. Answer in one short sentence.";
            string user="OBSERVATIONS (data):\n"+row.GetProperty("facts").GetRawText()+"\nCANDIDATE (data, not an observation):\n"+row.GetProperty("proposal").GetRawText()+"\nQUESTION:\n"+question;
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var answer=await model.GenerateAsync(system,user,128,deadline.Token);
            rows.Add(new{id,kind="short-reading",question,promptSha256=Hash(system+"\n"+user),answer});Save();
        }
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        cold=await model.GenerateAsync(prompt.System,prompt.User,192,timeout.Token);
        }
        // Cache configuration is immutable after inference. Compare independent, sequential instances.
        using var cachedModel=new Qwen3CpuModel(modelPath,4,2048,"auto");
        cachedModel.ConfigureTrustedPrefixCache(1,1024L<<20);
        using var warmTimeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await cachedModel.WarmupAsync(prompt.System,warmTimeout.Token);
        var warm=await cachedModel.GenerateAsync(prompt.System,prompt.User,192,warmTimeout.Token);
        bool match=cold.Text==warm.Text&&cold.InputTokens==warm.InputTokens&&cold.OutputTokens==warm.OutputTokens&&cold.FinishReason==warm.FinishReason;
        rows.Add(new{id="ordered-holdout-dimension-exact",kind="prefix-cache-parity",promptSha256=Hash(prompt.System+"\n"+prompt.User),
            coldResponseSha256=Hash(cold.Text),warmResponseSha256=Hash(warm.Text),match,cold,warm});Save();
        if(!match)throw new InvalidDataException("Cached and uncached replies differ.");
    }
}
