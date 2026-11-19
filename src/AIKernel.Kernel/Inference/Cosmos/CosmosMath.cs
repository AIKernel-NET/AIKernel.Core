namespace AIKernel.Kernel.Inference;

internal sealed class CosmosWorkspace(CosmosMemoryBudget? budget=null) : IDisposable
{
    private readonly List<float[]> buffers=[];
    internal CosmosMemoryBudget? Budget => budget;
    internal float[] Rent(int count)
    {
        long bytes=checked((long)count*sizeof(float));budget?.Reserve(bytes);
        try{var a=new float[count];buffers.Add(a);return a;}catch{budget?.Release(bytes);throw;}
    }
    internal float[][] Rows(int rows,int columns)=>Enumerable.Range(0,rows).Select(_=>Rent(columns)).ToArray();
    public void Dispose(){foreach(var a in buffers){Array.Clear(a);budget?.Release((long)a.Length*sizeof(float));}buffers.Clear();}
}

internal static class CosmosMath
{
    internal static void LayerNorm(ReadOnlySpan<float> x,ReadOnlySpan<float> weight,ReadOnlySpan<float> bias,Span<float> result,float epsilon=1e-6f)
    {
        // Long vision rows otherwise accumulate mean/variance error through all
        // 27 layers. Keep reduction statistics in double, then the graph in FP32.
        double mean=0;foreach(float v in x)mean+=v;mean/=x.Length;
        double variance=0;foreach(float v in x){double diff=v-mean;variance+=diff*diff;}
        float center=(float)mean,inverse=(float)(1/Math.Sqrt(variance/x.Length+epsilon));
        for(int i=0;i<x.Length;i++)result[i]=(x[i]-center)*inverse*weight[i]+bias[i];
    }
    internal static void Softmax(Span<float> scores)
    {
        float max=float.NegativeInfinity;foreach(float v in scores)max=Math.Max(max,v);
        float total=0;for(int i=0;i<scores.Length;i++){scores[i]=MathF.Exp(scores[i]-max);total+=scores[i];}
        if(!float.IsFinite(total)||total<=0)throw new ArithmeticException("Non-finite Cosmos attention.");
        for(int i=0;i<scores.Length;i++)scores[i]/=total;
    }
    internal static float Gelu(float x,bool approximate)
    {
        if(approximate)return .5f*x*(1+MathF.Tanh(.7978845608028654f*(x+.044715f*x*x*x)));
        // erf approximation, max absolute error < 1.5e-7. Checked against the
        // independent official FP32 graph; not claimed bitwise PyTorch parity.
        double z=Math.Abs(x/Math.Sqrt(2)),t=1/(1+.3275911*z);
        double erf=1-(((((1.061405429*t-1.453152027)*t)+1.421413741)*t-.284496736)*t+.254829592)*t*Math.Exp(-z*z);
        return (float)(.5*x*(1+Math.CopySign(erf,x)));
    }
    internal static void Rotate(Span<float> x,ReadOnlySpan<float> cos,ReadOnlySpan<float> sin)
    {
        int half=x.Length/2;
        for(int i=0;i<half;i++){float a=x[i],b=x[i+half];x[i]=a*cos[i]-b*sin[i];x[i+half]=b*cos[i]+a*sin[i];}
    }
    internal static void TextFrequencies(CosmosModelDefinition d,(int T,int H,int W) position,Span<float> cos,Span<float> sin)
    {
        for(int i=0;i<d.HeadSize/2;i++)
        {
            int pos=i%3==1&&i<d.RopeSections[1]*3?position.H:i%3==2&&i<d.RopeSections[2]*3?position.W:position.T;
            float angle=pos*(1/MathF.Pow(d.RopeTheta,(float)(2*i)/d.HeadSize));cos[i]=MathF.Cos(angle);sin[i]=MathF.Sin(angle);
        }
    }
}
