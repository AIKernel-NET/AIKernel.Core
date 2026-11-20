namespace AIKernel.Kernel.Inference;

internal sealed class CosmosTextDecoder
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosModelDefinition d;
    private readonly SafeTensorWeights.Tensor embedding,output;
    private readonly float[] finalNorm;
    private readonly Layer[] layers;
    private sealed record Layer(SafeTensorWeights.Tensor Q,SafeTensorWeights.Tensor K,SafeTensorWeights.Tensor V,SafeTensorWeights.Tensor O,
        SafeTensorWeights.Tensor Gate,SafeTensorWeights.Tensor? Up,SafeTensorWeights.Tensor Down,float[] Norm,float[] FfnNorm,float[] QNorm,float[] KNorm);
    internal CosmosTextDecoder(SafeTensorWeights weights,CosmosModelDefinition d)
    {
        this.weights=weights;this.d=d;const string p="model.language_model.";
        embedding=weights.Require(p+"embed_tokens.weight",d.Vocabulary,d.Width);
        output=d.Tied?embedding:weights.Require("lm_head.weight",d.Vocabulary,d.Width);
        finalNorm=weights.Require(p+"norm.weight",d.Width).Read();
        layers=Enumerable.Range(0,d.Layers).Select(i=>
        {
            string l=p+$"layers.{i}.";int q=d.Heads*d.HeadSize,k=d.KvHeads*d.HeadSize;
            return new Layer(weights.Require(l+"self_attn.q_proj.weight",q,d.Width),weights.Require(l+"self_attn.k_proj.weight",k,d.Width),
                weights.Require(l+"self_attn.v_proj.weight",k,d.Width),weights.Require(l+"self_attn.o_proj.weight",d.Width,q),
                weights.Require(l+(d.IsEdge?"mlp.fc1.weight":"mlp.gate_proj.weight"),d.Hidden,d.Width),d.IsEdge?null:weights.Require(l+"mlp.up_proj.weight",d.Hidden,d.Width),
                weights.Require(l+(d.IsEdge?"mlp.fc2.weight":"mlp.down_proj.weight"),d.Width,d.Hidden),
                weights.Require(l+"input_layernorm.weight",d.Width).Read(),weights.Require(l+"post_attention_layernorm.weight",d.Width).Read(),
                d.IsEdge?[]:weights.Require(l+"self_attn.q_norm.weight",d.HeadSize).Read(),d.IsEdge?[]:weights.Require(l+"self_attn.k_norm.weight",d.HeadSize).Read());
        }).ToArray();
    }
    internal sealed class State : IDisposable
    {
        private readonly CosmosWorkspace arena;
        internal readonly CosmosPagedKv? PagedKv;
        internal readonly float[][] Keys,Values,Scores;
        internal readonly float[] X,Norm,Q,K,V,Attention,Projection,Gate,Up,Logits,Cos,Sin;
        internal readonly ParallelOptions Options;
        internal readonly int Context;
        internal State(CosmosModelDefinition d,int context,int workers,CancellationToken token,CosmosMemoryBudget? budget=null)
        {
            arena=new(budget);Context=context;Options=new(){MaxDegreeOfParallelism=workers,CancellationToken=token};
            long kv=checked((long)d.Layers*context*d.KvHeads*d.HeadSize*8);
            if(budget is null && kv>2_000_000_000)throw new ArgumentException("Cosmos KV allocation exceeds the initial 2 GB workspace limit; enable paging.");
            try
            {
                if(budget is not null)PagedKv=new(d.Layers,d.KvHeads*d.HeadSize,context,budget);
                Keys=arena.Rows(budget is null?d.Layers:1,checked(context*d.KvHeads*d.HeadSize));Values=arena.Rows(budget is null?d.Layers:1,checked(context*d.KvHeads*d.HeadSize));
                X=arena.Rent(d.Width);Norm=arena.Rent(d.Width);Q=arena.Rent(d.Width);K=arena.Rent(d.KvHeads*d.HeadSize);V=arena.Rent(K.Length);
                Attention=arena.Rent(d.Width);Projection=arena.Rent(d.Width);Gate=arena.Rent(d.Hidden);Up=arena.Rent(d.Hidden);
                Logits=arena.Rent(d.Vocabulary);Cos=arena.Rent(d.HeadSize/2);Sin=arena.Rent(d.HeadSize/2);Scores=arena.Rows(workers,context);
            }
            catch{try{PagedKv?.Dispose();}finally{arena.Dispose();}throw;}
        }
        public void Dispose(){try{PagedKv?.Dispose();}finally{arena.Dispose();}}
    }
    internal void Forward(int token,int index,(int T,int H,int W) position,State s,float[]? visual=null,float[][]? deep=null,bool logits=true)
    {
        s.Options.CancellationToken.ThrowIfCancellationRequested();
        if((uint)token>=(uint)d.Vocabulary || (uint)index>=(uint)s.Context)throw new ArgumentOutOfRangeException(nameof(token));
        Embed(token, visual, s.X);
        CosmosMath.TextFrequencies(d,position,s.Cos,s.Sin);
        for(int layer=0;layer<layers.Length;layer++)
            ForwardLayer(layer,index,s,s.X,s.Cos,s.Sin,deep is not null && layer<deep.Length ? deep[layer] : null);
        if(logits) ProjectLogits(s.X,s);
    }

    // Within each layer the tokens stay in causal order. A small weight cache can now
    // serve a whole tile before advancing to the next layer. Decode uses the same kernel.
    internal bool TryPrefill(CosmosPrompt prompt, List<float[]> embeddings, List<float[]>[] deep, State s)
    {
        int tile = Math.Min(32, prompt.Tokens.Count);
        int kv = d.KvHeads * d.HeadSize;
        long bytesPerToken = sizeof(float) * (5L*d.Width + 2L*kv + (d.IsEdge ? 1L : 2L)*d.Hidden + d.HeadSize);
        // Reserve the entire tile before touching KV. Under pressure, retry with a smaller tile.
        var budget = weights.Budget;
        while (tile >= 2)
        {
            try { budget?.Reserve(tile * bytesPerToken); break; }
            catch (OutOfMemoryException) { tile /= 2; }
        }
        if (tile < 2) return false;
        if (weights.PrefillTimer is { } timer) timer.TileTokens = tile;
        try { PrefillTiles(prompt, embeddings, deep, s, tile, kv); }
        finally { budget?.Release(tile * bytesPerToken); }
        return true;
    }

    private void PrefillTiles(CosmosPrompt prompt, List<float[]> embeddings, List<float[]>[] deep, State s, int tile, int kv)
    {
        using var scratch = new CosmosWorkspace();
        var hidden = scratch.Rows(tile, d.Width);
        var norm = scratch.Rows(tile, d.Width);
        var q = scratch.Rows(tile, d.Width);
        var k = scratch.Rows(tile, kv);
        var v = scratch.Rows(tile, kv);
        var attention = scratch.Rows(tile, d.Width);
        var projection = scratch.Rows(tile, d.Width);
        var gate = scratch.Rows(tile, d.Hidden);
        var up = d.IsEdge ? [] : scratch.Rows(tile, d.Hidden);
        var cos = scratch.Rows(tile, d.HeadSize / 2);
        var sin = scratch.Rows(tile, d.HeadSize / 2);
        for (int first = 0; first < prompt.Tokens.Count; first += tile)
        {
            int count = Math.Min(tile, prompt.Tokens.Count - first);
            for (int t = 0; t < count; t++)
            {
                s.Options.CancellationToken.ThrowIfCancellationRequested();
                int index = first + t, image = prompt.VisualIndices[index];
                Embed(prompt.Tokens[index], image < 0 ? null : embeddings[image], hidden[t]);
                CosmosMath.TextFrequencies(d, prompt.Positions[index], cos[t], sin[t]);
            }
            for (int layer = 0; layer < layers.Length; layer++)
            {
                var l = layers[layer];
                for (int t = 0; t < count; t++)
                    InferenceVectorMath.Norm(hidden[t], l.Norm, norm[t], d.Epsilon);
                weights.LinearBatch(l.Q, norm, q, count, s.Options);
                weights.LinearBatch(l.K, norm, k, count, s.Options);
                weights.LinearBatch(l.V, norm, v, count, s.Options);
                for (int t = 0; t < count; t++)
                    Attend(layer, first + t, s, q[t], k[t], v[t], attention[t], cos[t], sin[t], restorePrefix: t == 0);
                weights.LinearBatch(l.O, attention, projection, count, s.Options);
                for (int t = 0; t < count; t++)
                {
                    InferenceVectorMath.Add(hidden[t], projection[t]);
                    InferenceVectorMath.Norm(hidden[t], l.FfnNorm, norm[t], d.Epsilon);
                }
                weights.LinearBatch(l.Gate, norm, gate, count, s.Options);
                if (!d.IsEdge) weights.LinearBatch(l.Up!, norm, up, count, s.Options);
                for (int t = 0; t < count; t++) Activate(gate[t], d.IsEdge ? null : up[t]);
                weights.LinearBatch(l.Down, gate, projection, count, s.Options);
                for (int t = 0; t < count; t++)
                {
                    InferenceVectorMath.Add(hidden[t], projection[t]);
                    int image = prompt.VisualIndices[first + t];
                    if (image >= 0 && layer < deep.Length) InferenceVectorMath.Add(hidden[t], deep[layer][image]);
                }
            }
            if (first + count == prompt.Tokens.Count) ProjectLogits(hidden[count - 1], s);
        }
    }

    private void Embed(int token, float[]? visual, float[] destination)
    {
        if (visual is null) embedding.Row(token, destination);
        else
        {
            if (visual.Length != d.Width) throw new ArgumentException("Visual feature width mismatch.");
            visual.CopyTo(destination, 0);
        }
    }

    private void ProjectLogits(float[] x, State s)
    {
        InferenceVectorMath.Norm(x,finalNorm,s.Norm,d.Epsilon);
        weights.Linear(output,s.Norm,s.Logits,s.Options);
    }

    private void ForwardLayer(int layer, int index, State s, float[] x, float[] cos, float[] sin, float[]? deep)
    {
        var l=layers[layer];InferenceVectorMath.Norm(x,l.Norm,s.Norm,d.Epsilon);
        weights.Linear(l.Q,s.Norm,s.Q,s.Options);weights.Linear(l.K,s.Norm,s.K,s.Options);weights.Linear(l.V,s.Norm,s.V,s.Options);
        Attend(layer,index,s,s.Q,s.K,s.V,s.Attention,cos,sin);
        weights.Linear(l.O,s.Attention,s.Projection,s.Options);InferenceVectorMath.Add(x,s.Projection);
        InferenceVectorMath.Norm(x,l.FfnNorm,s.Norm,d.Epsilon);weights.Linear(l.Gate,s.Norm,s.Gate,s.Options);
        if (!d.IsEdge) weights.Linear(l.Up!,s.Norm,s.Up,s.Options);
        Activate(s.Gate, d.IsEdge ? null : s.Up);
        weights.Linear(l.Down,s.Gate,s.Projection,s.Options);InferenceVectorMath.Add(x,s.Projection);
        if(deep is not null)InferenceVectorMath.Add(x,deep);
    }

    private static void Activate(float[] gate,float[]? up)
    {
        if (up is not null) { InferenceVectorMath.SwiGlu(gate,up); return; }
        for (int i=0;i<gate.Length;i++) { float x=MathF.Max(gate[i],0); gate[i]=x*x; }
    }

    private void Attend(int layer, int index, State s, float[] q, float[] k, float[] v, float[] attention,
        float[] cos, float[] sin, bool restorePrefix = true)
    {
        var timer = weights.PrefillTimer;
        long start = timer?.Start() ?? 0;
        int hs = d.HeadSize, kv = d.KvHeads * hs;
        float scale = (float)Math.Pow(hs,-.5);
        var l = layers[layer];
        for (int h = 0; h < d.Heads; h++)
        {
            var head = q.AsSpan(h * hs, hs);
            if (!d.IsEdge) InferenceVectorMath.Norm(head,l.QNorm,head,d.Epsilon);
            CosmosMath.Rotate(head,cos,sin);
        }
        for (int h = 0; h < d.KvHeads; h++)
        {
            var head = k.AsSpan(h * hs, hs);
            if (!d.IsEdge) InferenceVectorMath.Norm(head,l.KNorm,head,d.Epsilon);
            CosmosMath.Rotate(head,cos,sin);
        }
        var keys = s.Keys[s.PagedKv is null ? layer : 0];
        var values = s.Values[s.PagedKv is null ? layer : 0];
        if (s.PagedKv is not null)
        {
            // The layer staging arrays also hold earlier tokens from this tile.
            if (restorePrefix && index > 0) s.PagedKv.ReadLayer(layer,keys,values);
            s.PagedKv.Append(layer,index,k,v);
        }
        k.CopyTo(keys,index*kv); v.CopyTo(values,index*kv);
        Parallel.For(0,s.Options.MaxDegreeOfParallelism,s.Options,worker =>
        {
            var scores=s.Scores[worker].AsSpan(0,index+1);
            for(int h=worker;h<d.Heads;h+=s.Options.MaxDegreeOfParallelism)
            {
                int kh=h/(d.Heads/d.KvHeads);var query=q.AsSpan(h*hs,hs);
                for(int t=0;t<=index;t++)scores[t]=InferenceVectorMath.Dot(query,keys.AsSpan(t*kv+kh*hs,hs))*scale;
                CosmosMath.Softmax(scores);var target=attention.AsSpan(h*hs,hs);target.Clear();
                for(int t=0;t<=index;t++)for(int c=0;c<hs;c++)target[c]+=scores[t]*values[t*kv+kh*hs+c];
            }
        });
        timer?.AttentionCompleted(start);
    }
}
