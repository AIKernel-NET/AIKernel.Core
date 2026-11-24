using static AIKernel.Kernel.Inference.MixtureModelSession;

namespace AIKernel.Kernel.Inference;

/// <summary>Applies the routed experts in the order selected by the graph.</summary>
internal sealed class ExpertProjection(GgufWeights file, GgufPagedWeights? pages, ParallelOptions options)
{
    internal void Predict(int index, Layer layer)
    {
        var bank = layer.Gate.Weight;
        pages?.Hints?.Experts(index, bank.Offset, bank.RowByteLength * bank.Rows, options.CancellationToken);
    }

    internal void Multiply(Linear linear, float[] input, float[] output, int expert = -1)
    {
        var tensor = expert < 0 ? linear.Weight : linear.Weight.MatrixSlice(expert);
        int offset = expert < 0 ? 0 : expert * linear.Weight.Rows;
        GptOssMatrix.Multiply(file, tensor, input, output, linear.Bias, offset, options, pages);
    }

    internal void Dispatch(int layer, Layer weights, State state, RoutingPlan routing)
    {
        Span<int> order = stackalloc int[state.Router.Length];
        float total = routing.Select(state.Router, order);
        var active = order[..routing.Active];
        pages?.Hints?.Selected(layer, active);
        Apply(state, weights, active, total);
    }

    private static void Activate(float[] gate, float[] up)
    {
        for (int i = 0; i < gate.Length; i++)
        {
            float g = Math.Min(gate[i], 7);
            gate[i] = g / (1 + MathF.Exp(-1.702f * g)) * (1 + Math.Clamp(up[i], -7, 7));
        }
    }

    private void ProjectExpert(Layer weights, State state, int expert)
    {
        Multiply(weights.Gate, state.Norm, state.Gate, expert);
        Multiply(weights.Up, state.Norm, state.Up, expert);
        Activate(state.Gate, state.Up);
        Multiply(weights.Down, state.Gate, state.Down, expert);
    }

    private static void AddWeighted(float[] accumulator, float[] values, float weight)
    {
        for (int j = 0; j < accumulator.Length; j++) accumulator[j] += weight * values[j];
    }

    private void Apply(State s, Layer w, ReadOnlySpan<int> order, float total)
    {
        Array.Clear(s.Mlp);
        for (int i=0;i<order.Length;i++)
        {
            int e=order[i];
            ProjectExpert(w, s, e);
            float routing=s.Router[e]/total;
            AddWeighted(s.Mlp, s.Down, routing);
        }
        for (int j=0;j<s.X.Length;j++) s.X[j]+=s.Mlp[j];
    }
}
