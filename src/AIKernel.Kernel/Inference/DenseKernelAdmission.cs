namespace AIKernel.Kernel.Inference;

internal static class DenseKernelAdmission
{
    internal static void Check(GgufWeights file, bool binaryKernel)
    {
        foreach(var tensor in file.Tensors.Values)
        {
            if(tensor.Rank>2)throw new NotSupportedException("Qwen3 dense cannot execute expert or multimodal tensors.");
            if(!tensor.Encoding.CpuReference)throw new NotSupportedException($"No CPU kernel for {tensor.Encoding.Name}.");
            if(binaryKernel && tensor.Encoding.Name is not ("F32" or "Q1_0"))
                throw new NotSupportedException("The selected Qwen3 kernels require F32/Q1_0 weights.");
        }
    }
}
