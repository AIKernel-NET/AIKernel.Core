using AIKernel.Common.Results;
using AIKernel.Core.Governance;

namespace AIKernel.Kernel.Governance;

/// <summary>Bounds the resident working set and PCIe paging for sequential transformer execution.</summary>
public sealed class ResourceAdmissibilityPolicy : IResourceAdmissibilityPolicy
{
    /// <inheritdoc />
    public Result<InferenceResourceEstimate> Evaluate(InferenceResourceRequest request, InferenceResourceBudget budget)
    {
        if (request.Parameters <= 0 || request.QuantizationBits is < 1 or > 64 ||
            request.DecodedWeightBytes <= 0 || request.Layers <= 0 || request.KvBytesPerTokenPerLayer <= 0 ||
            request.InputTokens <= 0 || request.MaxGeneratedTokens <= 0 || budget.WeightBlockBytes <= 0 ||
            budget.KvBlockBytes < request.KvBytesPerTokenPerLayer || budget.AvailableRamBytes < 0 ||
            budget.AvailableVramBytes < 0 || budget.MaxPcieBytes < 0 || budget.WeightVramBytes < 0 || budget.KvVramBytes < 0 ||
            (!budget.UseGpu && (budget.WeightVramBytes != 0 || budget.KvVramBytes != 0)))
            return Reject("resource_request_invalid", "The model, token, or resource budget is invalid.");

        try
        {
            checked
            {
                var steps = (long)request.InputTokens + request.MaxGeneratedTokens - 1;
                var rowsPerBlock = budget.KvBlockBytes / request.KvBytesPerTokenPerLayer;
                var kvBlocks = DivideRoundUp(steps, rowsPerBlock) * request.Layers;
                var encoded = DivideRoundUp(request.Parameters * request.QuantizationBits, 8);
                var weights = DivideRoundUp(Math.Max(encoded, request.DecodedWeightBytes), budget.WeightBlockBytes)
                    * budget.WeightBlockBytes;
                var kvBytes = kvBlocks * budget.KvBlockBytes;
                var hostMinimum = (long)budget.WeightBlockBytes + budget.KvBlockBytes;
                var deviceKv = budget.KvVramBytes > 0;
                var deviceMinimum = budget.UseGpu ? (long)budget.WeightBlockBytes +
                    (deviceKv ? budget.KvBlockBytes : 0) : 0;
                if (budget.AvailableRamBytes < hostMinimum || budget.AvailableVramBytes < deviceMinimum ||
                    budget.UseGpu && budget.WeightVramBytes < budget.WeightBlockBytes ||
                    deviceKv && budget.KvVramBytes < budget.KvBlockBytes)
                    return Reject("resource_budget_exceeded", "The resident RAM or VRAM budget cannot hold a layer's working pages.");

                // Weight residency persists between passes. A missed KV page can travel in
                // either direction for each score/value pass or a cache write.
                var residentWeights = Math.Min(weights, Math.Min(budget.WeightVramBytes, budget.AvailableVramBytes -
                    (deviceKv ? budget.KvBlockBytes : 0)));
                var residentKv = deviceKv ? Math.Min(kvBytes, Math.Min(budget.KvVramBytes, budget.AvailableVramBytes - residentWeights)) : 0;
                var pcie = budget.UseGpu ? weights + (weights - residentWeights) * (steps - 1) : 0;
                if (deviceKv)
                    pcie += kvBytes + (kvBytes - residentKv) * steps * 4;
                if (pcie > budget.MaxPcieBytes)
                    return Reject("resource_pcie_budget_exceeded", "The estimated PCIe traffic exceeds the configured SLA.");

                return Result<InferenceResourceEstimate>.Success(new(encoded, kvBlocks,
                    budget.UseGpu ? (deviceKv ? 2 : 1) : 0, 2, pcie));
            }
        }
        catch (OverflowException)
        {
            return Reject("resource_budget_exceeded", "The inference exceeds the representable resource budget.");
        }
    }

    private static long DivideRoundUp(long value, long divisor) => 1 + (value - 1) / divisor;

    private static Result<InferenceResourceEstimate> Reject(string code, string message) =>
        Result<InferenceResourceEstimate>.Fail(new ErrorContext(message, code, false) { FailureKind = FailureKind.FailClosed });
}
