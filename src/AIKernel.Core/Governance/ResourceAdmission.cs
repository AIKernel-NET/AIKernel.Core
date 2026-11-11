using AIKernel.Common.Results;

namespace AIKernel.Core.Governance;

/// <summary>Model and token dimensions used to estimate one inference.</summary>
public readonly record struct InferenceResourceRequest(long Parameters, int QuantizationBits, long DecodedWeightBytes,
    int Layers, int KvBytesPerTokenPerLayer, int InputTokens, int MaxGeneratedTokens);

/// <summary>Physical page sizes and usable tier budgets for one route.</summary>
public readonly record struct InferenceResourceBudget(int WeightBlockBytes, int KvBlockBytes, long AvailableRamBytes,
    long AvailableVramBytes, long MaxPcieBytes, bool UseGpu, long WeightVramBytes, long KvVramBytes);

/// <summary>Required pages and estimated worst-case PCIe traffic over the request.</summary>
public readonly record struct InferenceResourceEstimate(long EncodedWeightBytes, long KvBlocks,
    int MinimumVramBlocks, int MinimumRamBlocks, long EstimatedPcieBytes);

/// <summary>Evaluates whether an inference can start on the selected route.</summary>
public interface IResourceAdmissibilityPolicy
{
    /// <summary>Returns the estimate or a structured rejection.</summary>
    Result<InferenceResourceEstimate> Evaluate(InferenceResourceRequest request, InferenceResourceBudget budget);
}
