using static AIKernel.Kernel.Inference.GemmaTextSession.GemmaE2BProfile;

namespace AIKernel.Kernel.Inference;


public partial class GemmaTextSession
{
    internal static class GemmaLayerInput
    {
        internal static void Prepare(State state, float[] norm, GgufWeights.Tensor projection,
            Action<GgufWeights.Tensor, float[], float[]> multiply)
        {
            Scale(state.Ple, MathF.Sqrt(PleWidth));
            multiply(projection, state.X, state.ProjectedPle);
            Scale(state.ProjectedPle, 1f / MathF.Sqrt(Width));
            for (int layer = 0; layer < LayerCount; layer++)
            {
                var row = state.ProjectedPle.AsSpan(layer * PleWidth, PleWidth);
                Norm(row, row, norm);
            }
            for (int i = 0; i < state.Ple.Length; i++)
                state.Ple[i] = (state.ProjectedPle[i] + state.Ple[i]) * (float)Math.Pow(2, -.5);
        }

        internal static void Apply(State state, Layer layer, int index, Action<GgufWeights.Tensor, float[], float[]> multiply)
        {
            multiply(layer.PleGate, state.X, state.PleGate);
            for (int i = 0; i < PleWidth; i++)
                state.PleGate[i] = Gelu(state.PleGate[i]) * state.Ple[index * PleWidth + i];
            multiply(layer.PleOutput, state.PleGate, state.Temp);
            Norm(state.Temp, state.Temp, layer.PostPle);
            Add(state.X, state.Temp);
            Scale(state.X, layer.Scale);
        }
    }
}
