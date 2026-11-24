using Microsoft.Win32.SafeHandles;

namespace AIKernel.Kernel.Inference;

internal class GgufPagedWeights(string path, IEnumerable<GgufWeights.Tensor> tensors, GgufMemoryOptions options,
    int pageSize = TensorPageStore.PageBytes, SafeFileHandle? backingHandle = null)
    : TensorPageStore(path, tensors.Select(GgufDenseWeights.Describe), GgufDenseWeights.PageBudget(options), pageSize, backingHandle)
{
    internal void ReadRow(GgufWeights.Tensor tensor, int row, float[] destination)
        => VisitRow(GgufDenseWeights.Describe(tensor), row, bytes => tensor.Encoding.Decode(bytes, destination.AsSpan(0, tensor.Columns)));

    internal void VisitRows(GgufWeights.Tensor tensor, CancellationToken token, Action<nint,int,int> visit)
        => VisitRows(GgufDenseWeights.Describe(tensor), token, visit);
}
