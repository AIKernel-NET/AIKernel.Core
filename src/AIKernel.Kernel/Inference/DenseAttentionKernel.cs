namespace AIKernel.Kernel.Inference;

internal static class DenseAttentionKernel
{
    internal static void Attend(Qwen3Workspace state, int layer, int position, bool ordered)
    {
        int kvSize = state.K.Length;
        var keys = state.Keys[layer];
        var values = state.Values[layer];
        state.K.CopyTo(keys, position * kvSize);
        state.V.CopyTo(values, position * kvSize);
        Parallel.For(0, state.Scores.Length, state.Options, head =>
            AttendHead(state, keys, values, head, position, ordered));
    }

    private static void AttendHead(Qwen3Workspace state, float[] keys, float[] values, int head, int position, bool ordered)
    {
        int heads = state.Scores.Length, headSize = state.Q.Length / heads;
        int kvSize = state.K.Length, kvHeads = kvSize / headSize;
        int offset = head / (heads / kvHeads) * headSize;
        var scores = state.Scores[head];
        var query = state.Q.AsSpan(head * headSize, headSize);
        float maximum = ScoreKeys(query, scores, keys, kvSize, offset, position, ordered);
        CombineAttentionValues(scores, values, state.Attention.AsSpan(head * headSize, headSize), position, offset, maximum, kvSize);
    }

    private static float ScoreKeys(ReadOnlySpan<float> query, float[] scores, float[] keys, int kvSize,
        int offset, int position, bool ordered)
    {
        float maximum = float.NegativeInfinity;
        for (int token = 0; token <= position; token++)
        {
            float score = Dot(query, keys.AsSpan(token * kvSize + offset, query.Length), ordered) / MathF.Sqrt(query.Length);
            scores[token] = score;
            maximum = Math.Max(maximum, score);
        }
        return maximum;
    }

    private static void CombineAttentionValues(float[] scores, float[] values, Span<float> target, int position, int offset, float maximum, int kvSize)
    {
        float sum = 0;
        for (int t = 0; t <= position; t++)
        {
            scores[t] = MathF.Exp(scores[t] - maximum);
            sum += scores[t];
        }
        target.Clear();
        for (int t = 0; t <= position; t++)
        {
            float probability = scores[t] / sum;
            var row = values.AsSpan(t * kvSize + offset, target.Length);
            for (int i = 0; i < target.Length; i++) target[i] += probability * row[i];
        }
    }

    internal static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b, bool preserveQ8Arithmetic)
    {
        if (!preserveQ8Arithmetic) return InferenceVectorMath.Dot(a, b);
        float sum = 0;
        for (int i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }

    internal static void Normalize(ReadOnlySpan<float> x, ReadOnlySpan<float> weights, Span<float> y,
        float eps, bool preserveQ8Arithmetic)
    {
        if (!preserveQ8Arithmetic)
        {
            InferenceVectorMath.Norm(x, weights, y, eps);
            return;
        }
        float sum = Dot(x, x, preserveQ8Arithmetic);
        float scale = 1 / MathF.Sqrt(sum / x.Length + eps);
        for (int i = 0; i < x.Length; i++) y[i] = x[i] * scale * weights[i];
    }
}
