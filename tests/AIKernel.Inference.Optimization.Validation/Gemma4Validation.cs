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
        using var model=new Gemma4E2BCpuModel(args[1],4,1024);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(30));
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
                File.WriteAllBytes(Path.Combine(destination,$"p{position}_{layer}_{stage}.f32"),MemoryMarshal.AsBytes(data.AsSpan()).ToArray()):null);
        }
        for(int i=0;i<prompt.Length;i++){Forward(prompt[i],i,i==prompt.Length-1);Console.Error.WriteLine($"prefill {i+1}/{prompt.Length}");}
        for(int i=0;i<steps;i++)
        {
            File.WriteAllBytes(Path.Combine(destination,$"logits-{i}.f32"),MemoryMarshal.AsBytes(state.Logits.AsSpan()).ToArray());
            tokens.Add(Gemma4E2BCpuModel.Argmax(state.Logits));
            if(i+1<steps)Forward(i<teacher.Length?teacher[i]:tokens[^1],prompt.Length+i,true);
            Console.Error.WriteLine($"decode {i+1}/{steps}");
        }
        var result=new{tokens,text=model.Tokenizer.Decode(tokens),elapsedMilliseconds=timer.ElapsedMilliseconds,promptLength=prompt.Length,steps};
        File.WriteAllText(Path.Combine(destination,"result.json"),JsonSerializer.Serialize(result));Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
