namespace AIKernel.Kernel.Inference;

internal sealed class CosmosVisionEncoder
{
    private readonly SafeTensorWeights weights;
    private readonly CosmosModelDefinition d;
    private readonly SafeTensorWeights.Tensor patch,position;
    private readonly float[] patchBias;
    private readonly Block[] blocks;
    private readonly Merger final;
    private readonly Merger[] deep;
    private sealed record Block(SafeTensorWeights.Tensor Qkv,SafeTensorWeights.Tensor Output,SafeTensorWeights.Tensor Up,SafeTensorWeights.Tensor Down,
        float[] QkvBias,float[] OutputBias,float[] UpBias,float[] DownBias,float[] Norm1,float[] Bias1,float[] Norm2,float[] Bias2);
    private sealed record Merger(SafeTensorWeights.Tensor Up,SafeTensorWeights.Tensor Down,float[] UpBias,float[] DownBias,float[] Norm,float[] Bias,bool Post);
    internal CosmosVisionEncoder(SafeTensorWeights weights,CosmosModelDefinition d)
    {
        this.weights=weights;this.d=d;const string prefix="model.visual.";int w=d.VisionWidth;
        patch=weights.Require(prefix+"patch_embed.proj.weight",w,3,2,16,16);patchBias=V(prefix+"patch_embed.proj.bias",w);
        position=weights.Require(prefix+"pos_embed.weight",d.VisionPositions,w);
        blocks=Enumerable.Range(0,d.VisionLayers).Select(i=>
        {
            string p=prefix+$"blocks.{i}.";
            return new Block(weights.Require(p+"attn.qkv.weight",w*3,w),weights.Require(p+"attn.proj.weight",w,w),
                weights.Require(p+"mlp.linear_fc1.weight",d.VisionHidden,w),weights.Require(p+"mlp.linear_fc2.weight",w,d.VisionHidden),
                V(p+"attn.qkv.bias",w*3),V(p+"attn.proj.bias",w),V(p+"mlp.linear_fc1.bias",d.VisionHidden),V(p+"mlp.linear_fc2.bias",w),
                V(p+"norm1.weight",w),V(p+"norm1.bias",w),V(p+"norm2.weight",w),V(p+"norm2.bias",w));
        }).ToArray();
        final=ReadMerger(prefix+"merger.",false);
        deep=Enumerable.Range(0,d.DeepStack.Length).Select(i=>ReadMerger(prefix+$"deepstack_merger_list.{i}.",true)).ToArray();
    }
    private float[] V(string name,int count)=>weights.Require(name,count).Read();
    private Merger ReadMerger(string p,bool post)
    {
        int w=d.VisionWidth*4,n=post?w:d.VisionWidth;
        return new(weights.Require(p+"linear_fc1.weight",w,w),weights.Require(p+"linear_fc2.weight",d.Width,w),
            V(p+"linear_fc1.bias",w),V(p+"linear_fc2.bias",d.Width),V(p+"norm.weight",n),V(p+"norm.bias",n),post);
    }
    internal sealed record Features(float[][] Embeddings,float[][][] DeepStack);
    internal Features Forward(CosmosPatches input,CosmosWorkspace arena,ParallelOptions options)
    {
        int count=input.Count,w=d.VisionWidth,hs=w/d.VisionHeads,frame=input.Height*input.Width;
        float attentionScale=(float)Math.Pow(hs,-.5);
        using var work=new CosmosWorkspace(weights.Budget);var hidden=work.Rows(count,w);var qkv=work.Rows(count,w*3);
        var norm=work.Rent(w);var projected=work.Rent(w);var patchInput=work.Rent(1536);var mlp=work.Rent(d.VisionHidden);
        var cos=work.Rows(frame,hs/2);var sin=work.Rows(frame,hs/2);
        var positions=work.Rent(checked((int)position.Count));position.CopyTo(positions);
        int grid=(int)Math.Sqrt(d.VisionPositions);
        for(int t=0;t<input.Time;t++)for(int by=0;by<input.Height/2;by++)for(int bx=0;bx<input.Width/2;bx++)for(int my=0;my<2;my++)for(int mx=0;mx<2;mx++)
        {
            options.CancellationToken.ThrowIfCancellationRequested();int y=by*2+my,x=bx*2+mx;
            int row=t*frame+((by*(input.Width/2)+bx)*4+my*2+mx);
            input.Values.AsSpan(row*1536,1536).CopyTo(patchInput);weights.Linear(patch,patchInput,hidden[row],options,patchBias);
            float py=input.Height==1?0:(float)y*(grid-1)/(input.Height-1),px=input.Width==1?0:(float)x*(grid-1)/(input.Width-1);
            int y0=(int)py,x0=(int)px,y1=Math.Min(y0+1,grid-1),x1=Math.Min(x0+1,grid-1);float dy=py-y0,dx=px-x0;
            for(int c=0;c<w;c++)hidden[row][c]+=
                positions[(y0*grid+x0)*w+c]*(1-dy)*(1-dx)+positions[(y0*grid+x1)*w+c]*(1-dy)*dx+
                positions[(y1*grid+x0)*w+c]*dy*(1-dx)+positions[(y1*grid+x1)*w+c]*dy*dx;
            if(t==0)for(int i=0;i<hs/2;i++)
            {
                int coordinate=i<hs/4?y:x;float angle=coordinate*(1/MathF.Pow(10000f,(float)(i%(hs/4)*2)/(hs/2)));
                cos[row][i]=MathF.Cos(angle);sin[row][i]=MathF.Sin(angle);
            }
        }
        var result=new float[deep.Length][][];
        var attention=work.Rows(count,w);
        var scores=Enumerable.Range(0,options.MaxDegreeOfParallelism).Select(_=>work.Rent(frame)).ToArray();
        for(int layer=0;layer<blocks.Length;layer++)
        {
            var b=blocks[layer];
            for(int r=0;r<count;r++)
            {
                CosmosMath.LayerNorm(hidden[r],b.Norm1,b.Bias1,norm);weights.Linear(b.Qkv,norm,qkv[r],options,b.QkvBias);
                for(int h=0;h<d.VisionHeads;h++)
                {CosmosMath.Rotate(qkv[r].AsSpan(h*hs,hs),cos[r%frame],sin[r%frame]);CosmosMath.Rotate(qkv[r].AsSpan(w+h*hs,hs),cos[r%frame],sin[r%frame]);}
            }
            Parallel.For(0,options.MaxDegreeOfParallelism,options,worker=>
            {
                var score=scores[worker];
                for(int r=worker;r<count;r+=options.MaxDegreeOfParallelism)
                {
                    options.CancellationToken.ThrowIfCancellationRequested();int start=r/frame*frame;
                    for(int h=0;h<d.VisionHeads;h++)
                    {
                        for(int k=0;k<frame;k++)score[k]=InferenceVectorMath.Dot(qkv[r].AsSpan(h*hs,hs),qkv[start+k].AsSpan(w+h*hs,hs))*attentionScale;
                        CosmosMath.Softmax(score);var target=attention[r].AsSpan(h*hs,hs);target.Clear();
                        for(int k=0;k<frame;k++)for(int c=0;c<hs;c++)target[c]+=score[k]*qkv[start+k][2*w+h*hs+c];
                    }
                }
            });
            for(int r=0;r<count;r++)
            {
                weights.Linear(b.Output,attention[r],projected,options,b.OutputBias);InferenceVectorMath.Add(hidden[r],projected);
                CosmosMath.LayerNorm(hidden[r],b.Norm2,b.Bias2,norm);weights.Linear(b.Up,norm,mlp,options,b.UpBias);
                for(int i=0;i<mlp.Length;i++)mlp[i]=CosmosMath.Gelu(mlp[i],true);
                weights.Linear(b.Down,mlp,projected,options,b.DownBias);InferenceVectorMath.Add(hidden[r],projected);
            }
            int index=Array.IndexOf(d.DeepStack,layer);if(index>=0)result[index]=Merge(hidden,deep[index],arena,options);
        }
        return new(Merge(hidden,final,arena,options),result);
    }
    private float[][] Merge(float[][] hidden,Merger m,CosmosWorkspace arena,ParallelOptions options)
    {
        using var work=new CosmosWorkspace(weights.Budget);int width=d.VisionWidth;
        var input=work.Rent(width*4);var norm=work.Rent(width*4);var up=work.Rent(width*4);var result=arena.Rows(hidden.Length/4,d.Width);
        for(int row=0;row<result.Length;row++)
        {
            for(int j=0;j<4;j++)hidden[row*4+j].CopyTo(input,j*width);
            if(m.Post)CosmosMath.LayerNorm(input,m.Norm,m.Bias,norm);
            else for(int j=0;j<4;j++)CosmosMath.LayerNorm(input.AsSpan(j*width,width),m.Norm,m.Bias,norm.AsSpan(j*width,width));
            weights.Linear(m.Up,norm,up,options,m.UpBias);for(int i=0;i<up.Length;i++)up[i]=CosmosMath.Gelu(up[i],false);
            weights.Linear(m.Down,up,result[row],options,m.DownBias);
        }
        return result;
    }
}
