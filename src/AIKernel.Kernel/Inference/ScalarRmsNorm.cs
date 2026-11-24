namespace AIKernel.Kernel.Inference;

internal static class ScalarRmsNorm
{
    internal static void Apply(ReadOnlySpan<float> input,Span<float> output,ReadOnlySpan<float> weights, float epsilon)
    {
        float sum=0;foreach(float x in input)sum+=x*x;float scale=MathF.Pow(sum/input.Length+epsilon,-.5f);
        for(int i=0;i<input.Length;i++)output[i]=(input[i]*scale)*(weights.IsEmpty?1:weights[i]);
    }
}
