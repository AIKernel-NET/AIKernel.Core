namespace AIKernel.Kernel.Inference;

// SigLIP2 packed images/individual video frames, with antialiased positional
// interpolation. No Qwen vision RoPE or DeepStack applies to Cosmos3 Edge.
internal sealed class CosmosEdgeVisionEncoder
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosModelDefinition d;
    private readonly SafeTensorWeights.Tensor patch,position,up,down;
    private readonly float[] patchBias,postNorm,postBias,mergeNorm,mergeBias,upBias,downBias;
    private readonly Block[] blocks;
    private sealed record Block(SafeTensorWeights.Tensor Q,SafeTensorWeights.Tensor K,SafeTensorWeights.Tensor V,
        SafeTensorWeights.Tensor O,SafeTensorWeights.Tensor Up,SafeTensorWeights.Tensor Down,
        float[] QB,float[] KB,float[] VB,float[] OB,float[] UB,float[] DB,float[] N1,float[] B1,float[] N2,float[] B2);

    internal CosmosEdgeVisionEncoder(SafeTensorWeights weights,CosmosModelDefinition d)
    {
        this.weights=weights;this.d=d;int w=d.VisionWidth;const string p="model.visual.";
        patch=weights.Require(p+"embeddings.patch_embedding.weight",w,768);
        patchBias=Vector(p+"embeddings.patch_embedding.bias",w);
        position=weights.Require(p+"embeddings.position_embedding.weight",d.VisionPositions,w);
        postNorm=Vector(p+"post_layernorm.weight",w);postBias=Vector(p+"post_layernorm.bias",w);
        blocks=Enumerable.Range(0,d.VisionLayers).Select(i=>
        {
            string l=p+$"encoder.layers.{i}.";
            return new Block(weights.Require(l+"self_attn.q_proj.weight",w,w),weights.Require(l+"self_attn.k_proj.weight",w,w),
                weights.Require(l+"self_attn.v_proj.weight",w,w),weights.Require(l+"self_attn.out_proj.weight",w,w),
                weights.Require(l+"mlp.fc1.weight",d.VisionHidden,w),weights.Require(l+"mlp.fc2.weight",w,d.VisionHidden),
                Vector(l+"self_attn.q_proj.bias",w),Vector(l+"self_attn.k_proj.bias",w),Vector(l+"self_attn.v_proj.bias",w),
                Vector(l+"self_attn.out_proj.bias",w),Vector(l+"mlp.fc1.bias",d.VisionHidden),Vector(l+"mlp.fc2.bias",w),
                Vector(l+"layer_norm1.weight",w),Vector(l+"layer_norm1.bias",w),Vector(l+"layer_norm2.weight",w),Vector(l+"layer_norm2.bias",w));
        }).ToArray();
        const string m="model.projector.";
        mergeNorm=Vector(m+"norm.weight",w);mergeBias=Vector(m+"norm.bias",w);
        up=weights.Require(m+"linear_fc1.weight",d.ProjectorHidden,w*4);down=weights.Require(m+"linear_fc2.weight",d.Width,d.ProjectorHidden);
        upBias=Vector(m+"linear_fc1.bias",d.ProjectorHidden);downBias=Vector(m+"linear_fc2.bias",d.Width);
    }
    private float[] Vector(string name,int n)=>weights.Require(name,n).Read();

    // PyTorch interpolate(mode=bilinear, align_corners=false, antialias=true):
    // separable triangle filter; downsampling widens support before normalization.
    internal static (int Start,float[] Weights)[] ResizeWeights(int source,int target)
    {
        float scale=(float)source/target,support=Math.Max(scale,1);
        return Enumerable.Range(0,target).Select(i=>
        {
            float center=scale*(i+.5f);int start=Math.Max((int)(center-support+.5f),0),end=Math.Min((int)(center+support+.5f),source);
            float[] w=new float[end-start];float sum=0;
            for(int j=0;j<w.Length;j++){w[j]=Math.Max(0,1-Math.Abs((start+j+.5f-center)/support));sum+=w[j];}
            for(int j=0;j<w.Length;j++)w[j]/=sum;
            return (start,w);
        }).ToArray();
    }
    internal CosmosVisionEncoder.Features Forward(CosmosPatches input,CosmosWorkspace arena,ParallelOptions options)
    {
        int w=d.VisionWidth,count=input.Count,frame=input.Height*input.Width,hs=w/d.VisionHeads;
        float attentionScale=(float)Math.Pow(hs,-.5);
        using var work=new CosmosWorkspace();var hidden=work.Rows(count,w);var queries=work.Rows(count,w);var keys=work.Rows(count,w);var values=work.Rows(count,w);
        var attention=work.Rows(count,w);var norm=work.Rent(w);var projection=work.Rent(w);var mlp=work.Rent(d.VisionHidden);var patchInput=work.Rent(768);
        int grid=(int)Math.Sqrt(d.VisionPositions);var horizontal=ResizeWeights(grid,input.Width);var vertical=ResizeWeights(grid,input.Height);
        var resizedX=work.Rows(grid*input.Width,w);var resized=work.Rows(frame,w);
        for(int y=0;y<grid;y++)for(int x=0;x<input.Width;x++)for(int c=0;c<w;c++)
        {
            var filter=horizontal[x];float sum=0;for(int k=0;k<filter.Weights.Length;k++)sum+=position.At(((long)y*grid+filter.Start+k)*w+c)*filter.Weights[k];
            resizedX[y*input.Width+x][c]=sum;
        }
        for(int y=0;y<input.Height;y++)for(int x=0;x<input.Width;x++)for(int c=0;c<w;c++)
        {
            var filter=vertical[y];float sum=0;for(int k=0;k<filter.Weights.Length;k++)sum+=resizedX[(filter.Start+k)*input.Width+x][c]*filter.Weights[k];
            resized[y*input.Width+x][c]=sum;
        }
        for(int t=0;t<input.Time;t++)for(int by=0;by<input.Height/2;by++)for(int bx=0;bx<input.Width/2;bx++)for(int my=0;my<2;my++)for(int mx=0;mx<2;mx++)
        {
            int row=t*frame+((by*(input.Width/2)+bx)*4+my*2+mx);
            input.Values.AsSpan(row*768,768).CopyTo(patchInput);weights.Linear(patch,patchInput,hidden[row],options,patchBias);
            InferenceVectorMath.Add(hidden[row],resized[(by*2+my)*input.Width+bx*2+mx]);
        }
        var scores=work.Rows(options.MaxDegreeOfParallelism,frame);
        foreach(var b in blocks)
        {
            for(int r=0;r<count;r++)
            {
                CosmosMath.LayerNorm(hidden[r],b.N1,b.B1,norm,d.VisionEpsilon);
                weights.Linear(b.Q,norm,queries[r],options,b.QB);weights.Linear(b.K,norm,keys[r],options,b.KB);weights.Linear(b.V,norm,values[r],options,b.VB);
            }
            Parallel.For(0,options.MaxDegreeOfParallelism,options,worker=>
            {
                var score=scores[worker];
                for(int r=worker;r<count;r+=options.MaxDegreeOfParallelism)
                {
                    options.CancellationToken.ThrowIfCancellationRequested();int start=r/frame*frame;
                    for(int h=0;h<d.VisionHeads;h++)
                    {
                        for(int k=0;k<frame;k++)score[k]=InferenceVectorMath.Dot(queries[r].AsSpan(h*hs,hs),keys[start+k].AsSpan(h*hs,hs))*attentionScale;
                        CosmosMath.Softmax(score);var target=attention[r].AsSpan(h*hs,hs);target.Clear();
                        for(int k=0;k<frame;k++)for(int c=0;c<hs;c++)target[c]+=score[k]*values[start+k][h*hs+c];
                    }
                }
            });
            for(int r=0;r<count;r++)
            {
                weights.Linear(b.O,attention[r],projection,options,b.OB);InferenceVectorMath.Add(hidden[r],projection);
                CosmosMath.LayerNorm(hidden[r],b.N2,b.B2,norm,d.VisionEpsilon);weights.Linear(b.Up,norm,mlp,options,b.UB);
                for(int i=0;i<mlp.Length;i++)mlp[i]=CosmosMath.Gelu(mlp[i],true);
                weights.Linear(b.Down,mlp,projection,options,b.DB);InferenceVectorMath.Add(hidden[r],projection);
            }
        }
        var merged=work.Rent(w*4);var expanded=work.Rent(d.ProjectorHidden);var output=arena.Rows(count/4,d.Width);
        for(int row=0;row<output.Length;row++)
        {
            for(int j=0;j<4;j++)
            {
                CosmosMath.LayerNorm(hidden[row*4+j],postNorm,postBias,norm,d.VisionEpsilon);
                CosmosMath.LayerNorm(norm,mergeNorm,mergeBias,merged.AsSpan(j*w,w));
            }
            weights.Linear(up,merged,expanded,options,upBias);for(int i=0;i<expanded.Length;i++)expanded[i]=CosmosMath.Gelu(expanded[i],false);
            weights.Linear(down,expanded,output[row],options,downBias);
        }
        return new(output,[]);
    }
}
