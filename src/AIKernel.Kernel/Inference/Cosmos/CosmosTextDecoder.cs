namespace AIKernel.Kernel.Inference;

internal sealed class CosmosTextDecoder
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosModelDefinition d;
    private readonly SafeTensorWeights.Tensor embedding,output;
    private readonly float[] finalNorm;
    private readonly Layer[] layers;
    private sealed record Layer(SafeTensorWeights.Tensor Q,SafeTensorWeights.Tensor K,SafeTensorWeights.Tensor V,SafeTensorWeights.Tensor O,
        SafeTensorWeights.Tensor Gate,SafeTensorWeights.Tensor Up,SafeTensorWeights.Tensor Down,float[] Norm,float[] FfnNorm,float[] QNorm,float[] KNorm);
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
                weights.Require(l+"mlp.gate_proj.weight",d.Hidden,d.Width),weights.Require(l+"mlp.up_proj.weight",d.Hidden,d.Width),
                weights.Require(l+"mlp.down_proj.weight",d.Width,d.Hidden),
                weights.Require(l+"input_layernorm.weight",d.Width).Read(),weights.Require(l+"post_attention_layernorm.weight",d.Width).Read(),
                weights.Require(l+"self_attn.q_norm.weight",d.HeadSize).Read(),weights.Require(l+"self_attn.k_norm.weight",d.HeadSize).Read());
        }).ToArray();
    }
    internal sealed class State : IDisposable
    {
        private readonly CosmosWorkspace arena=new();
        internal readonly float[][] Keys,Values,Scores;
        internal readonly float[] X,Norm,Q,K,V,Attention,Projection,Gate,Up,Logits,Cos,Sin;
        internal readonly ParallelOptions Options;
        internal readonly int Context;
        internal State(CosmosModelDefinition d,int context,int workers,CancellationToken token)
        {
            Context=context;Options=new(){MaxDegreeOfParallelism=workers,CancellationToken=token};
            long kv=checked((long)d.Layers*context*d.KvHeads*d.HeadSize*8);
            if(kv>2_000_000_000)throw new ArgumentException("Cosmos KV allocation exceeds the initial 2 GB workspace limit.");
            try
            {
                Keys=arena.Rows(d.Layers,checked(context*d.KvHeads*d.HeadSize));Values=arena.Rows(d.Layers,checked(context*d.KvHeads*d.HeadSize));
                X=arena.Rent(d.Width);Norm=arena.Rent(d.Width);Q=arena.Rent(d.Width);K=arena.Rent(d.KvHeads*d.HeadSize);V=arena.Rent(K.Length);
                Attention=arena.Rent(d.Width);Projection=arena.Rent(d.Width);Gate=arena.Rent(d.Hidden);Up=arena.Rent(d.Hidden);
                Logits=arena.Rent(d.Vocabulary);Cos=arena.Rent(d.HeadSize/2);Sin=arena.Rent(d.HeadSize/2);Scores=arena.Rows(workers,context);
            }
            catch{arena.Dispose();throw;}
        }
        public void Dispose()=>arena.Dispose();
    }
    internal void Forward(int token,int index,(int T,int H,int W) position,State s,float[]? visual=null,float[][]? deep=null,bool logits=true)
    {
        s.Options.CancellationToken.ThrowIfCancellationRequested();
        if((uint)token>=(uint)d.Vocabulary || (uint)index>=(uint)s.Context)throw new ArgumentOutOfRangeException(nameof(token));
        if(visual is null)embedding.Row(token,s.X);else
        {if(visual.Length!=d.Width)throw new ArgumentException("Visual feature width mismatch.");visual.CopyTo(s.X,0);}
        CosmosMath.TextFrequencies(d,position,s.Cos,s.Sin);int kv=d.KvHeads*d.HeadSize,hs=d.HeadSize;
        for(int layer=0;layer<layers.Length;layer++)
        {
            var l=layers[layer];InferenceVectorMath.Norm(s.X,l.Norm,s.Norm,d.Epsilon);
            weights.Linear(l.Q,s.Norm,s.Q,s.Options);weights.Linear(l.K,s.Norm,s.K,s.Options);weights.Linear(l.V,s.Norm,s.V,s.Options);
            for(int h=0;h<d.Heads;h++)
            {var v=s.Q.AsSpan(h*hs,hs);InferenceVectorMath.Norm(v,l.QNorm,v,d.Epsilon);CosmosMath.Rotate(v,s.Cos,s.Sin);}
            for(int h=0;h<d.KvHeads;h++)
            {var v=s.K.AsSpan(h*hs,hs);InferenceVectorMath.Norm(v,l.KNorm,v,d.Epsilon);CosmosMath.Rotate(v,s.Cos,s.Sin);}
            s.K.CopyTo(s.Keys[layer],index*kv);s.V.CopyTo(s.Values[layer],index*kv);
            var keys=s.Keys[layer];var values=s.Values[layer];
            Parallel.For(0,s.Options.MaxDegreeOfParallelism,s.Options,worker=>
            {
                var scores=s.Scores[worker].AsSpan(0,index+1);
                for(int h=worker;h<d.Heads;h+=s.Options.MaxDegreeOfParallelism)
                {
                    int kh=h/(d.Heads/d.KvHeads);var query=s.Q.AsSpan(h*hs,hs);
                    for(int t=0;t<=index;t++)scores[t]=InferenceVectorMath.Dot(query,keys.AsSpan(t*kv+kh*hs,hs))/MathF.Sqrt(hs);
                    CosmosMath.Softmax(scores);var target=s.Attention.AsSpan(h*hs,hs);target.Clear();
                    for(int t=0;t<=index;t++)for(int c=0;c<hs;c++)target[c]+=scores[t]*values[t*kv+kh*hs+c];
                }
            });
            weights.Linear(l.O,s.Attention,s.Projection,s.Options);InferenceVectorMath.Add(s.X,s.Projection);
            InferenceVectorMath.Norm(s.X,l.FfnNorm,s.Norm,d.Epsilon);weights.Linear(l.Gate,s.Norm,s.Gate,s.Options);weights.Linear(l.Up,s.Norm,s.Up,s.Options);
            InferenceVectorMath.SwiGlu(s.Gate,s.Up);weights.Linear(l.Down,s.Gate,s.Projection,s.Options);InferenceVectorMath.Add(s.X,s.Projection);
            if(deep is not null&&layer<deep.Length)InferenceVectorMath.Add(s.X,deep[layer]);
        }
        if(logits){InferenceVectorMath.Norm(s.X,finalNorm,s.Norm,d.Epsilon);weights.Linear(output,s.Norm,s.Logits,s.Options);}
    }
}
