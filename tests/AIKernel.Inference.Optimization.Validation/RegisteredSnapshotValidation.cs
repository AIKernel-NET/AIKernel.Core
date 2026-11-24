using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Kernel.Inference;

internal static class RegisteredSnapshotValidation
{
    internal static async Task Run(string[] args)
    {
        var registration = DenseModelRegistration.Parse(File.ReadAllText(args[2]), "qwen3-0.6b");
        using var model = registration.OpenSession(args[1], threads:4, context:128, deviceBudgetBytes:0);
        var dest = Directory.CreateDirectory(args[4]).FullName;
        foreach (var lang in new[]{"en","ja"})
        {
            using var reference = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(args[3],lang+".json")));
            var root=reference.RootElement;
            string system=root.GetProperty("system").GetString()!, user=root.GetProperty("user").GetString()!;
            using var cancelled = new CancellationTokenSource();
            cancelled.CancelAfter(TimeSpan.FromMilliseconds(15));
            try { await model.GenerateAsync(system,user,16,cancelled.Token); throw new InvalidOperationException("Expected cancellation."); }
            catch(OperationCanceledException) when(cancelled.IsCancellationRequested) { }
            var actual=await model.GenerateAsync(system,user,16);
            var expected=root.GetProperty("generated").Deserialize<int[]>()!;
            File.WriteAllText(Path.Combine(dest,lang+"-generation.json"),JsonSerializer.Serialize(actual));
            var logits = Capture(model, root, args[3], dest, lang);
            bool stopped=expected[^1]==151645;
            if (actual.Text!=root.GetProperty("text").GetString() || actual.OutputTokens!=expected.Length-(stopped?1:0) || actual.FinishReason!=(stopped?"stop":"length"))
                throw new InvalidDataException("Generated reply differs from ONNX Runtime.");
            var digest=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(actual.Text)));
            File.WriteAllText(Path.Combine(dest,lang+".json"),JsonSerializer.Serialize(new {actual.Text, tokens=expected,
                tokenIdsSha256=GeneratedTokenDigest.Compute(expected.ToList()), textSha256=digest, actual.FinishReason, cancellationRecovered=true, logits }));
        }
    }

    private static object Capture(DenseModelSession model,JsonElement reference,string source,string dest,string lang)
    {
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var type=typeof(DenseModelSession);
        var tokenizer=(QwenTokenizer)type.GetField("tokenizer",flags)!.GetValue(model)!;
        var prompt=tokenizer.Chat(reference.GetProperty("system").GetString()!,reference.GetProperty("user").GetString()!);
        if (!prompt.SequenceEqual(reference.GetProperty("input").Deserialize<int[]>()!)) throw new InvalidDataException("Tokenizer mismatch.");
        var weights=(IDenseModelWeights)type.GetField("file",flags)!.GetValue(model)!;
        File.WriteAllText(Path.Combine(dest,lang+"-stages.json"),JsonSerializer.Serialize(FirstLayer(weights,prompt,source,lang)));
        var output=type.GetField("output",flags)!.GetValue(model);
        var forward=type.GetMethod("Forward",flags)!;
        var multiply=type.GetMethod("Multiply",flags)!;
        using var state=(Qwen3Workspace)type.GetMethod("AcquireWorkspace",flags)!.Invoke(model,[CancellationToken.None])!;
        int position=0;
        void Advance(int id) => forward.Invoke(model,[id,position++,state,model.CpuBackend,null]);
        foreach(int id in prompt) Advance(id);
        var tokens=reference.GetProperty("generated").Deserialize<int[]>()!;
        var results=new List<object>();
        bool passed=true;
        for(int i=0;i<tokens.Length;i++)
        {
            multiply.Invoke(model,[output,state.Normal,state.Logits,state.Options,model.CpuBackend,null,null]);
            var basename=$"{lang}-{i:00}.f32";
            var bytes=File.ReadAllBytes(Path.Combine(source,basename));
            var original=MemoryMarshal.Cast<byte,float>(bytes);
            if(original.Length!=state.Logits.Length)throw new InvalidDataException("Vocabulary mismatch.");
            double peak=0, squares=0;
            for(int n=0;n<original.Length;n++)
            {
                double diff=(double)state.Logits[n]-original[n];
                peak=Math.Max(peak,Math.Abs(diff)); squares+=diff*diff;
            }
            var hash=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(state.Logits.AsSpan())));
            double rmse=Math.Sqrt(squares/original.Length);
            if(!double.IsFinite(peak) || !double.IsFinite(rmse))throw new ArithmeticException();
            int greedy=Array.IndexOf(state.Logits,state.Logits.Max());
            results.Add(new{step=i,token=greedy,maximumAbsoluteError=peak,rootMeanSquareError=rmse,sha256=hash});
            passed &= peak <= 0.001 && rmse <= 0.0001;
            passed &= greedy==tokens[i];
            if(i+1<tokens.Length)Advance(tokens[i]);
        }
        File.WriteAllText(Path.Combine(dest,lang+"-logits.json"),JsonSerializer.Serialize(results));
        if (!passed) throw new InvalidDataException("The reference logit thresholds were exceeded.");
        return results;
    }

    private static Dictionary<string,double> FirstLayer(IDenseModelWeights file,List<int> prompt,string source,string language)
    {
        var shape=file.Shape;
        var input=new float[shape.Width]; var norm=new float[shape.Width]; var query=new float[shape.Heads*shape.HeadSize];
        var stages=new[]{"embedding","norm","query"};
        var refs=stages.ToDictionary(s=>s,s=>File.ReadAllBytes(Path.Combine(source,language+"-"+s+".f32")));
        var peaks=stages.ToDictionary(s=>s,_=>0.0);
        for(int pos=0;pos<prompt.Count;pos++)
        {
            file.ReadRow(file.Embedding,prompt[pos],input);
            InferenceVectorMath.Norm(input,file.Layers[0].Norm,norm,shape.Epsilon);
            file.Multiply(file.Layers[0].Q,norm,query,new ParallelOptions{MaxDegreeOfParallelism=4},"avx2");
            Compare("embedding",input); Compare("norm",norm); Compare("query",query);
            void Compare(string stage,float[] values)
            {
                var expected=MemoryMarshal.Cast<byte,float>(refs[stage]).Slice(pos*values.Length,values.Length);
                for(int i=0;i<values.Length;i++)peaks[stage]=Math.Max(peaks[stage],Math.Abs((double)values[i]-expected[i]));
            }
        }
        return peaks;
    }
}
