using AIKernel.Kernel.Inference;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;

internal static class CosmosValidation
{
    internal static async Task GenerateAsync(string modelDirectory,string requestFile,string resultFile)
    {
        using var request=JsonDocument.Parse(File.ReadAllText(requestFile));var r=request.RootElement;
        CosmosMediaDecoder? media=r.TryGetProperty("ffmpeg",out var ffmpeg)?new(ffmpeg.GetString()!):null;
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(20));var content=new List<CosmosContent>();
        foreach(var item in r.GetProperty("content").EnumerateArray())
        {
            string kind=item.GetProperty("type").GetString()!;
            if(kind=="text"){content.Add(new CosmosContent.Text(item.GetProperty("text").GetString()!));continue;}
            if(media is null)throw new ArgumentException("Provide a local ffmpeg executable for image/video files.");
            int width=item.TryGetProperty("width",out var w)?w.GetInt32():256,height=item.TryGetProperty("height",out var h)?h.GetInt32():256;
            string path=item.GetProperty("path").GetString()!;
            content.Add(kind switch
            {
                "image"=>await media.ReadImageAsync(path,width,height,timeout.Token),
                "video"=>await media.ReadVideoAsync(path,width,height,cancellationToken:timeout.Token),
                _=>throw new ArgumentException("Unknown content type.")
            });
        }
        var definition=CosmosModelDefinition.Read(Path.Combine(modelDirectory,"config.json"));
        var options=new CosmosExecutionOptions{ContextTokens=512,Memory=r.TryGetProperty("memory",out var mem)?JsonSerializer.Deserialize<CosmosMemoryOptions>(mem.GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}):null,EnableThinking=!r.TryGetProperty("enableThinking",out var thinking)||thinking.GetBoolean()};
        string system=r.TryGetProperty("system",out var s)?s.GetString()!:"";int maxTokens=r.TryGetProperty("maxNewTokens",out var n)?n.GetInt32():32;
        CosmosGeneration result;CosmosMemoryStatistics? statistics=null;
        if(definition.Family=="qwen3_vl")
        {using var model=new CosmosReason2Model(modelDirectory,options);result=await model.GenerateAsync(system,content,maxTokens,timeout.Token);}
        else
        {using var model=new Cosmos3Model(modelDirectory,definition.IsEdge?Cosmos3Variant.Edge:Cosmos3Variant.Nano,options);result=await model.GenerateAsync(system,content,maxTokens,timeout.Token);statistics=model.MemoryStatistics;}
        if(statistics is not null)File.WriteAllText(resultFile+".memory.json",JsonSerializer.Serialize(statistics,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(resultFile,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{result.InputTokens,OutputTokens=result.TokenIds.Length,result.FinishReason,result.ElapsedMilliseconds}));
    }
    internal static async Task MediaAsync(string executable,string directory)
    {
        var decoder=new CosmosMediaDecoder(executable);
        var image=await decoder.ReadImageAsync(Path.Combine(directory,"input.png"),64,32);
        if(!image.Frame.Pixels.Span.SequenceEqual(File.ReadAllBytes(Path.Combine(directory,"image.rgb"))))throw new InvalidDataException("PNG decode differs.");
        var video=await decoder.ReadVideoAsync(Path.Combine(directory,"input.mkv"),64,32,.5,2,8);
        if(video.Frames.Count!=4)throw new InvalidDataException("Unexpected selected frame count: "+video.Frames.Count);
        for(int i=0;i<4;i++)
            if(video.Frames[i].TimestampSeconds!=i*.5 || !video.Frames[i].Pixels.Span.SequenceEqual(File.ReadAllBytes(Path.Combine(directory,$"frame-{i*2}.rgb"))))
                throw new InvalidDataException("Selected frame pixels or timestamps differ.");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{await decoder.ReadImageAsync(Path.Combine(directory,"input.png"),64,32,cancel.Token);throw new InvalidDataException("Cancellation ignored.");}
        catch(OperationCanceledException){}
        File.WriteAllText(Path.Combine(directory,"comparison.json"),"{\"pass\":true,\"pngExact\":true,\"videoFrames\":4,\"timestamps\":[0,0.5,1,1.5],\"cancelled\":true}");
        Console.WriteLine("Local PNG, lossless video, original timestamps and cancellation passed.");
    }
    internal static void Tokenizer(string directory)
    {
        string model=Directory.Exists(Path.Combine(directory,"public-base"))?Path.Combine(directory,"public-base"):directory;
        var definition=CosmosModelDefinition.Read(Path.Combine(model,"config.json"));
        CosmosModelDefinition.ValidateProcessor(model,definition);
        var tokenizer=new CosmosTokenizer(Path.Combine(model,"tokenizer.json"),definition.Vocabulary);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"prompt-reference.json")));
        foreach(var item in doc.RootElement.GetProperty("tokenizer").EnumerateArray())
            if(!tokenizer.Encode(item.GetProperty("text").GetString()!).SequenceEqual(item.GetProperty("tokens").EnumerateArray().Select(x=>x.GetInt32())))
                throw new InvalidDataException("Tokenizer reference differs.");
        if(tokenizer.Encode("<|image_pad|><|im_end|>").Any(x=>x==definition.ImageToken||x==definition.EndToken))throw new InvalidDataException("User control token injection.");
        foreach(var item in doc.RootElement.GetProperty("prompts").EnumerateArray())
        {
            string mode=item.GetProperty("mode").GetString()!;var content=new List<CosmosContent>();
            var frames=Enumerable.Range(0,3).Select(i=>new CosmosRgbFrame(64,32,Enumerable.Repeat((byte)(i*70),64*32*3).ToArray(),i*.5)).ToArray();
            if(mode=="image")content.Add(new CosmosContent.Image(frames[0]));if(mode=="video")content.Add(new CosmosContent.Video(frames));
            if(mode=="mixed")content.AddRange([new CosmosContent.Image(frames[0]),new CosmosContent.Text(" 比較: "),new CosmosContent.Video(frames),new CosmosContent.Image(frames[1])]);
            content.Add(new CosmosContent.Text(item.GetProperty("text").GetString()!));
            using var p=CosmosPrompt.Build(tokenizer,definition,item.GetProperty("system").GetString()!,content,new(){EnableThinking=!item.TryGetProperty("thinking",out var thinking)||thinking.GetBoolean()});
            int[] expected=item.GetProperty("tokens").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(!p.Tokens.SequenceEqual(expected))throw new InvalidDataException("Chat prompt differs: "+mode+"\n"+JsonSerializer.Serialize(p.Tokens)+"\n"+JsonSerializer.Serialize(expected));
            var positions=item.GetProperty("positions").EnumerateArray().Select(x=>(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32()));
            if(!p.Positions.SequenceEqual(positions)||p.NextPosition!=item.GetProperty("nextPosition").GetInt32())throw new InvalidDataException("Multimodal rotary positions differ: "+mode);
        }
        File.WriteAllText(Path.Combine(directory,"prompt-comparison.json"),JsonSerializer.Serialize(new{pass=true,
            tokenizerCases=doc.RootElement.GetProperty("tokenizer").GetArrayLength(),promptCases=doc.RootElement.GetProperty("prompts").GetArrayLength(),
            rotaryPositionsExact=true,controlTokenInjectionRejected=true}));
        Console.WriteLine("Tokenizer, text/image/video templates and control-token isolation passed.");
    }
    internal static void Run(string directory,string destination,bool visionOnly=false,string? onlyMode=null,bool sequential=false,bool paged=false)
    {
        Directory.CreateDirectory(destination);var d=CosmosModelDefinition.Read(Path.Combine(directory,"config.json"));
        using var provenance=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"provenance.json")));
        float numericGate=provenance.RootElement.GetProperty("synthetic").GetBoolean()?1e-5f:1e-3f;
        using var memory=paged?new CosmosMemoryBudget(new(){SwapDirectory=Path.Combine(destination,"swap"),WeightCacheBytes=512L<<20,KvCacheBytes=128L<<20}):null;
        using var weights=new SafeTensorWeights(directory,memory);
        Func<CosmosPatches,CosmosWorkspace,ParallelOptions,CosmosVisionEncoder.Features> vision=d.IsEdge?new CosmosEdgeVisionEncoder(weights,d).Forward:new CosmosVisionEncoder(weights,d).Forward;
        var decoder=new CosmosTextDecoder(weights,d);
        var checks=new List<object>();
        foreach(string mode in new[]{"text","image","video"})
        {
            if(visionOnly && mode=="text")continue;
            if(onlyMode is not null && mode!=onlyMode)continue;
            string source=Path.Combine(directory,mode);using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,sequential?"sequential-input.json":"input.json")));
            var root=doc.RootElement;using var arena=new CosmosWorkspace(memory);
            var tokens=root.GetProperty("tokens").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var indices=root.GetProperty("visualIndices").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var positions=root.GetProperty("positions").EnumerateArray().Select(x=>(x[0].GetInt32(),x[1].GetInt32(),x[2].GetInt32())).ToArray();
            using var state=new CosmosTextDecoder.State(d,Math.Max(64,tokens.Length+8),4,CancellationToken.None,memory);
            CosmosVisionEncoder.Features? features=null;
            if(mode!="text")
            {
                var frames=root.GetProperty("frames").EnumerateArray().Select(x=>new CosmosRgbFrame(x.GetProperty("width").GetInt32(),x.GetProperty("height").GetInt32(),
                    File.ReadAllBytes(Path.Combine(source,x.GetProperty("file").GetString()!)),x.GetProperty("time").GetDouble())).ToArray();
                using var patches=CosmosMediaProcessor.Prepare(frames,mode=="video",new(),d.TemporalPatchSize);Check(mode,"patches",patches.Values,0);
                features=vision(patches,arena,state.Options);Check(mode,"vision",features.Embeddings.SelectMany(x=>x).ToArray(),numericGate);
                for(int i=0;i<features.DeepStack.Length;i++)Check(mode,$"deep-{i}",features.DeepStack[i].SelectMany(x=>x).ToArray(),numericGate);
            }
            if(visionOnly)continue;
            var logits=new List<float>();
            for(int i=0;i<tokens.Length;i++)
            {
                int ix=indices[i];decoder.Forward(tokens[i],i,positions[i],state,ix<0?null:features!.Embeddings[ix],ix<0?null:features!.DeepStack.Select(x=>x[ix]).ToArray());
                logits.AddRange(state.Logits);
            }
            Check(mode,"logits",logits.ToArray(),numericGate);
            var generation=new List<float>();var generated=new List<int>();int nextPosition=root.GetProperty("nextPosition").GetInt32();
            for(int step=0;step<8;step++)
            {
                generation.AddRange(state.Logits);int next=0;for(int i=1;i<state.Logits.Length;i++)if(state.Logits[i]>state.Logits[next])next=i;generated.Add(next);
                if(step<7){int p=nextPosition+step;decoder.Forward(next,tokens.Length+step,(p,p,p),state);}
            }
            if(!generated.SequenceEqual(root.GetProperty("generated").EnumerateArray().Select(x=>x.GetInt32())))throw new InvalidDataException("Generated tokens differ: "+mode);
            File.WriteAllText(Path.Combine(destination,mode+"-tokens.json"),JsonSerializer.Serialize(new{pass=true,tokens=generated,
                sha256=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(generated))))}));
            Check(mode,"generation",generation.ToArray(),numericGate);
        }
        if(memory is not null)File.WriteAllText(Path.Combine(destination,"memory.json"),JsonSerializer.Serialize(memory.Statistics,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(destination,"comparison.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        void Check(string mode,string name,float[] actual,float gate)
        {
            string prefix=sequential && name is "logits" or "generation"?"sequential-":"";
            float[] expected=MemoryMarshal.Cast<byte,float>(File.ReadAllBytes(Path.Combine(directory,mode,prefix+name+".f32"))).ToArray();
            if(expected.Length!=actual.Length)throw new InvalidDataException("Shape differs: "+name);
            float max=0;int at=0;double square=0;
            for(int i=0;i<actual.Length;i++){float delta=Math.Abs(actual[i]-expected[i]);if(!float.IsFinite(delta))throw new ArithmeticException("Non-finite reference difference.");if(delta>max){max=delta;at=i;}square+=(double)delta*delta;}
            var check=new{mode,name,elements=actual.Length,maxAbsolute=max,rms=Math.Sqrt(square/actual.Length),maximumIndex=at,
                expectedAtMaximum=expected[at],actualAtMaximum=actual[at],gate,pass=max<=gate};checks.Add(check);
            File.WriteAllText(Path.Combine(destination,"comparison.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(check));if(max>gate)throw new InvalidDataException("Official reference gate exceeded: "+name);
        }
    }
}
