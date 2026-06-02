namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

public sealed class KernelExecutionIdFactory
{
    public string CreateExecutionId(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = CanonicalizeExecutionState(
            request,
            status,
            promptHash,
            resultDiscriminator,
            startedAt,
            executionSequence);

        return CreateSha256ExecutionId(payload);
    }

    public string CreateFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = CanonicalizeFallbackState(request, status);

        return CreateSha256ExecutionId(payload);
    }

    private static string CanonicalizeExecutionState(
        KernelExecutionRequest request,
        ExecutionStatus status,
        string promptHash,
        string resultDiscriminator,
        DateTimeOffset startedAt,
        long executionSequence)
    {
        // SemanticStateHash boundary:
        // The fields below are the canonical execution-state material.
        // Phase-2 can replace the SHA-256 wrapper without changing callers.
        return string.Join(
            '\n',
            request.ContextSnapshot.ContextHash,
            request.ContextSnapshot.SnapshotId,
            request.RequestedModelId ?? string.Empty,
            promptHash,
            status.ToString(),
            resultDiscriminator,
            startedAt.Ticks.ToString("D20"),
            executionSequence.ToString("D16"));
    }

    private static string CanonicalizeFallbackState(
        KernelRequest request,
        ExecutionStatus status)
    {
        // SemanticStateHash boundary:
        // This is used before a full ContextSnapshot exists, so the request
        // contract itself is the canonical fallback material.
        return string.Join(
            '\n',
            request.Input ?? string.Empty,
            request.RootRomId?.Value ?? string.Empty,
            request.VfsProviderId ?? string.Empty,
            request.ParentSnapshotId ?? string.Empty,
            request.RequestedModelId ?? string.Empty,
            status.ToString());
    }

    private static string CreateSha256ExecutionId(string canonicalPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var hash = SHA256.HashData(bytes);

        return "exec:sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
