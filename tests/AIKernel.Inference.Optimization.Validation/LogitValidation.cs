using AIKernel.Kernel.Inference;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

// Diagnostic-only reflection keeps the exact same harness usable with frozen pre-change DLLs.
// Identical teacher-forced histories prevent a changed greedy token from amplifying comparisons.
internal static class LogitValidation
{
    internal static void Run(string[] args)
    {
        string modelPath=args[1],backend=args[2],destination=args[3];
        int workers=int.TryParse(Environment.GetEnvironmentVariable("AIKERNEL_BENCH_CPU_WORKERS"),out int configured)?configured:4;
        int[]? teacher=args.Length>4?JsonSerializer.Deserialize<int[]>(File.ReadAllText(args[4])):null;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        using var model=backend=="cuda-dp4a"
            ?new Qwen3CpuModel(modelPath,workers,1024,0,1024L*1024*1024,gpuInt8:CudaQ8Kernel.Dp4a)
            :new Qwen3CpuModel(modelPath,workers,1024,backend);
        var type=typeof(Qwen3CpuModel);
        var tokenizer=(QwenTokenizer)type.GetField("tokenizer",flags)!.GetValue(model)!;
        var output=(GgufWeights.Tensor)type.GetField("output",flags)!.GetValue(model)!;
        var forward=type.GetMethod("Forward",flags)!;var multiply=type.GetMethod("Multiply",flags)!;
        using var state=(Qwen3Workspace)type.GetMethod("AcquireWorkspace",flags)!.Invoke(model,[CancellationToken.None])!;
        string selected=backend=="cuda-dp4a"?model.CpuBackend:backend;
        var prompt=tokenizer.Chat("Answer the customer in Japanese. Ask for the error message and model path. Do not invent facts.","サンプルを起動するとモデルが見つからないと表示されます。確認する設定を教えてください。");
        void Forward(int token,int position)=>forward.Invoke(model,[token,position,state,selected,null]);
        for(int i=0;i<prompt.Count;i++)Forward(prompt[i],i);
        var tokens=new List<int>();var checkpoints=new[]{0,1,3,7,15,31};
        using var stream=File.Create(destination+".f32");
        for(int step=0;step<32;step++)
        {
            var call=new object?[multiply.GetParameters().Length];
            call[0]=output;call[1]=state.Normal;call[2]=state.Logits;call[3]=state.Options;call[4]=selected;
            multiply.Invoke(model,call);
            if(state.Logits.Any(x=>!float.IsFinite(x)))throw new ArithmeticException("Non-finite logits");
            int best=0;for(int i=1;i<state.Logits.Length;i++)if(state.Logits[i]>state.Logits[best])best=i;
            tokens.Add(best);
            if(checkpoints.Contains(step))stream.Write(MemoryMarshal.AsBytes(state.Logits.AsSpan()));
            if(step<31)Forward(teacher?[step]??best,prompt.Count+step);
        }
        File.WriteAllText(destination+".tokens.json",JsonSerializer.Serialize(tokens));
        Console.WriteLine(JsonSerializer.Serialize(new{kind="teacher-forced-logits",backend,workers,steps=32,checkpoints,vocabulary=state.Logits.Length,teacherForced=teacher is not null,performanceBenchmark=false,instructionSets=CpuInstructionSets.Available}));
    }
}
