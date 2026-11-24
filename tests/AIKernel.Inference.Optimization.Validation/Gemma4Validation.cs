using AIKernel.Kernel.Inference;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;

internal static class Gemma4Validation
{
    internal static async Task Run(string[] args)
    {
        if(args[0]=="--gemma-tokenize")
        {
            using var file=new GgufWeights(args[1]);var tokenizer=new Gemma4Tokenizer(file);var chat=new Gemma4ChatTemplate(tokenizer);
            var samples=JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[2]))!;
            Console.WriteLine(JsonSerializer.Serialize(samples.Select(text=>new{text,ids=tokenizer.Encode(text),decoded=tokenizer.Decode(tokenizer.Encode(text)),
                chatIds=chat.Chat("You are helpful.",text)})));return;
        }
        int threads=int.Parse(Environment.GetEnvironmentVariable("GEMMA_THREADS")??"4");
        int context=int.Parse(Environment.GetEnvironmentVariable("GEMMA_CONTEXT")??"1024");
        var execution=new Gemma4ExecutionOptions{Backend=Enum.Parse<Gemma4Backend>(Environment.GetEnvironmentVariable("GEMMA_BACKEND")??"Auto",true),
            InstructionSet=Enum.Parse<Gemma4InstructionSet>(Environment.GetEnvironmentVariable("GEMMA_ISA")??"Auto",true),
            CudaDeviceOrdinal=int.Parse(Environment.GetEnvironmentVariable("GEMMA_GPU")??"0"),ProfileMatrices=Environment.GetEnvironmentVariable("GEMMA_PROFILE")=="1",
            Memory=Environment.GetEnvironmentVariable("GEMMA_SWAP_DIRECTORY") is {Length:>0} swap?new Gemma4MemoryOptions{SwapDirectory=swap,
                WeightCacheBytes=long.Parse(Environment.GetEnvironmentVariable("GEMMA_CACHE_BYTES")??"5000000000")}:null};
        var load=Stopwatch.StartNew();
        using GemmaTextSession model = execution == new Gemma4ExecutionOptions()
            ? ModelRegistration.Gemma4E2B.OpenSession(args[1], threads, context, deviceBudgetBytes: 0)
            : new Gemma4E2BCpuModel(args[1],threads,context,4L*1024*1024*1024,execution);
        double loadMs=load.Elapsed.TotalMilliseconds;
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(30));
        if(Environment.GetEnvironmentVariable("GEMMA_PREFAULT")=="1")PrefaultMappedWeights(model);
        if(args[0]=="--gemma-benchmark")
        {
            using var spec=JsonDocument.Parse(File.ReadAllText(args[2]));
            int[] benchPrompt=spec.RootElement.GetProperty("prompt").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            int benchSteps=spec.RootElement.GetProperty("steps").GetInt32();
            int repeat=args.Length>4?int.Parse(args[4]):3;
            if(benchPrompt.Length+benchSteps>context||benchSteps<1||benchSteps>256||repeat<1||repeat>10)throw new ArgumentException("Invalid benchmark bounds.");
            using(var warm=model.CreateState(timeout.Token))model.Forward(benchPrompt[0],0,warm,true);
            var results=new List<object>();
            for(int iteration=0;iteration<repeat;iteration++)
            {
                using var benchState=model.CreateState(timeout.Token);var benchTimer=Stopwatch.StartNew();var ids=new List<int>();
                for(int i=0;i<benchPrompt.Length;i++)model.Forward(benchPrompt[i],i,benchState,i==benchPrompt.Length-1);
                double prefillMs=benchTimer.Elapsed.TotalMilliseconds;
                for(int i=0;i<benchSteps;i++)
                {
                    ids.Add(model.SelectToken(benchState));
                    if(i+1<benchSteps)model.Forward(ids[^1],benchPrompt.Length+i,benchState,true);
                }
                double totalMs=benchTimer.Elapsed.TotalMilliseconds;
                string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MemoryMarshal.AsBytes(ids.ToArray().AsSpan()))).ToLowerInvariant();
                results.Add(new{iteration,prefillMs,decodeMs=totalMs-prefillMs,totalMs,decodeForwardTokens=benchSteps-1,outputTokens=benchSteps,
                    decodeMsPerToken=(totalMs-prefillMs)/Math.Max(1,benchSteps-1),hash,tokens=ids,
                    matrixMs=benchState.MatrixTicks*1000.0/Stopwatch.Frequency,benchState.MatrixCalls});
                Console.Error.WriteLine($"iteration {iteration+1}: {totalMs:F1}ms");
            }
            var process=Process.GetCurrentProcess();
            File.WriteAllText(args[3],JsonSerializer.Serialize(new{backend=model.Backend,threads,context,loadMs,model.WeightBytes,model.GpuWeightBytes,model.GpuResidentBytes,
                gpuForward=model.GpuStatistics is {} gs?new{gs.Calls,gs.Synchronizations,gs.DownloadBytes}:null,
                peakWorkingSetBytes=process.PeakWorkingSet64,workingSetBytes=process.WorkingSet64,promptTokens=benchPrompt.Length,steps=benchSteps,ignoreEos=true,temperature=0,model.MemoryStatistics,results}));return;
        }
        if(args[0]=="--gemma-lifecycle")
        {
            const string system="Answer briefly.",user="日本の首都はどこですか？";
            var first=await model.GenerateAsync(system,user,16,timeout.Token);
            bool cancelled=false,disposed=false;
            using(var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            {try{await model.GenerateAsync(system,"キャンセルする問い合わせ",16,cancel.Token);}catch(OperationCanceledException){cancelled=true;}}
            var second=await model.GenerateAsync(system,user,16,timeout.Token);
            model.Dispose();try{await model.GenerateAsync(system,user,16,timeout.Token);}catch(ObjectDisposedException){disposed=true;}
            if(!cancelled||!disposed||first.Text!=second.Text||first.FinishReason!="stop"||second.FinishReason!="stop")throw new Exception("Gemma lifecycle regression.");
            Console.WriteLine(JsonSerializer.Serialize(new{first,second,cancelled,disposed,repeatedReplyEqual=true}));return;
        }
        if(args[0]=="--gemma-generate")
        {
            var r=await model.GenerateAsync(args[2],args[3],int.Parse(args[4]),timeout.Token);
            Console.WriteLine(JsonSerializer.Serialize(r));return;
        }
        if(args[0]!="--gemma-forward")throw new ArgumentException("Unknown Gemma mode.");
        using var input=JsonDocument.Parse(File.ReadAllText(args[2]));var root=input.RootElement;
        int[] prompt=root.GetProperty("prompt").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        int[] teacher=root.TryGetProperty("teacher",out var t)?t.EnumerateArray().Select(x=>x.GetInt32()).ToArray():[];
        int steps=root.TryGetProperty("steps",out var step)?step.GetInt32():1;
        var tracePositions=root.TryGetProperty("tracePositions",out var trace)?trace.EnumerateArray().Select(x=>x.GetInt32()).ToHashSet():[];
        string destination=args[3];Directory.CreateDirectory(destination);var tokens=new List<int>();var timer=Stopwatch.StartNew();
        using var state=model.CreateState(timeout.Token);
        void Forward(int token,int position,bool logits)
        {
            model.Forward(token,position,state,logits,tracePositions.Contains(position)?(layer,stage,data)=>
                File.WriteAllBytes(Path.Combine(destination,$"p{position}_{layer}_{stage}.f32"),MemoryMarshal.AsBytes(data.AsSpan()).ToArray()):null,captureLogits:true);
        }
        for(int i=0;i<prompt.Length;i++){Forward(prompt[i],i,i==prompt.Length-1);Console.Error.WriteLine($"prefill {i+1}/{prompt.Length}");}
        for(int i=0;i<steps;i++)
        {
            File.WriteAllBytes(Path.Combine(destination,$"logits-{i}.f32"),MemoryMarshal.AsBytes(state.Logits.AsSpan()).ToArray());
            tokens.Add(model.SelectToken(state));
            if(i+1<steps)Forward(i<teacher.Length?teacher[i]:tokens[^1],prompt.Length+i,true);
            Console.Error.WriteLine($"decode {i+1}/{steps}");
        }
        var result=new{tokens,text=model.Tokenizer.Decode(tokens),elapsedMilliseconds=timer.ElapsedMilliseconds,promptLength=prompt.Length,steps,model.MemoryStatistics};
        File.WriteAllText(Path.Combine(destination,"result.json"),JsonSerializer.Serialize(result));Console.WriteLine(JsonSerializer.Serialize(result));
    }
    // Validation only: touch every page of this model's existing map, rather than measuring
    // only the small PLE rows referenced by a short prompt. Does not create another mapping.
    private static unsafe void PrefaultMappedWeights(GemmaTextSession model)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(GemmaTextSession).GetField("file", flags)!;
        var file=(GgufWeights)field.GetValue(model)!;long checksum=0;
        for(long offset=0;offset<file.Length;offset+=4096)checksum+=*file.BytesAt(offset,1);
        Console.Error.WriteLine($"Prefaulted {file.Length} mapped bytes; checksum={checksum}");
    }
}
