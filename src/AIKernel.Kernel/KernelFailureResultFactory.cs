namespace AIKernel.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Common.Results;
using AIKernel.Core.Execution;
using AIKernel.Core.Rom;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

internal sealed class KernelFailureResultFactory
{
    private readonly IKernelClock _clock;
    private readonly KernelExecutionIdFactory _executionIdFactory = new();

    public KernelFailureResultFactory(IKernelClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public KernelRequestExecutionResult CreateRejectedResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        Exception exception)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Rejected,
            providerId: "none",
            modelIdFallback: "none",
            errorCode: ResolveRejectedCode(exception),
            errorMessage: exception.Message,
            errorDetail: exception.GetType().FullName,
            metadata: BuildFailureMetadata(
                request,
                transaction,
                exception,
                FailureKind.Reject,
                OriginStep.KernelFacade,
                ResolveRejectedSemanticSlot(exception)));
    }

    public KernelRequestExecutionResult CreateFailedResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        Exception exception)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Failed,
            providerId: "unknown",
            modelIdFallback: "unknown",
            errorCode: "kernel_transaction_failed",
            errorMessage: exception.Message,
            errorDetail: exception.GetType().FullName,
            metadata: BuildFailureMetadata(
                request,
                transaction,
                exception,
                FailureKind.FailClosed,
                OriginStep.KernelFacade,
                SemanticSlot.T));
    }

    public KernelRequestExecutionResult CreateCanceledResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Canceled,
            providerId: "none",
            modelIdFallback: "none",
            errorCode: "canceled",
            errorMessage: "Kernel transaction was canceled.",
            errorDetail: null,
            metadata: BuildFailureMetadata(
                request,
                transaction,
                exception: null,
                FailureKind.FailClosed,
                OriginStep.KernelFacade,
                SemanticSlot.T));
    }

    private KernelRequestExecutionResult CreateFailureResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        ExecutionStatus status,
        string providerId,
        string modelIdFallback,
        string errorCode,
        string errorMessage,
        string? errorDetail,
        ImmutableDictionary<string, string> metadata)
    {
        var executionId = transaction?.TransactionId
            ?? ResolveFallbackExecutionId(request, status, errorCode);

        return new KernelRequestExecutionResult
        {
            ExecutionId = executionId,
            Status = status,
            ProviderId = providerId,
            ModelId = request.RequestedModelId ?? modelIdFallback,
            ContextSnapshotId = contextSnapshot?.SnapshotId ?? string.Empty,
            ContextHash = contextSnapshot?.ContextHash ?? string.Empty,
            PromptHash = string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0),
            Error = new ExecutionError(
                Code: errorCode,
                Message: errorMessage,
                Detail: errorDetail),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = _clock.Now,
            Metadata = metadata
        };
    }

    private string ResolveFallbackExecutionId(
        KernelRequest request,
        ExecutionStatus status,
        string errorCode)
    {
        var result = _executionIdFactory.TryCreateFallbackExecutionId(request, status);

        return result.IsSuccess
            ? result.Value!
            : $"exec:failed:{ResolveFailureCode(errorCode, result.Error?.Code)}";
    }

    private static string ResolveFailureCode(string errorCode, string? resultErrorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? resultErrorCode ?? "ERROR"
            : errorCode;
    }

    private static string ResolveRejectedCode(Exception exception)
    {
        return exception switch
        {
            KernelRequestValidationException => "invalid_kernel_request",
            ContextAssemblyGovernanceException => "context_rejected",
            RomSignatureVerificationException => "rom_signature_verification_failed",
            RomRequiredMetadataMissingException => "rom_required_metadata_missing",
            PromptTokenBudgetExceededException => "prompt_token_budget_exceeded",
            UnsupportedPromptCapabilityException => "unsupported_prompt_capability",
            _ => "kernel_transaction_rejected"
        };
    }

    private static ImmutableDictionary<string, string> BuildFailureMetadata(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        Exception? exception,
        FailureKind failureKind,
        OriginStep originStep,
        SemanticSlot semanticSlot)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        builder["root_rom_id"] = request.RootRomId?.Value ?? string.Empty;
        builder["vfs_provider_id"] = request.VfsProviderId ?? string.Empty;
        builder["requested_model_id"] = request.RequestedModelId ?? string.Empty;
        builder["failure_kind"] = failureKind.ToString();
        builder["origin_step"] = originStep.ToString();
        builder["semantic_slot"] = semanticSlot.ToString();

        if (exception is not null)
        {
            builder["exception_type"] = exception.GetType().FullName ?? exception.GetType().Name;
        }

        if (transaction is not null)
        {
            builder["transaction_id"] = transaction.TransactionId;
            builder["input_hash"] = transaction.InputHash;
        }

        foreach (var item in request.Metadata)
        {
            builder[item.Key] = item.Value;
        }

        return builder.ToImmutable();
    }

    private static SemanticSlot ResolveRejectedSemanticSlot(Exception exception)
    {
        return exception switch
        {
            ContextAssemblyGovernanceException
                or RomSignatureVerificationException
                or RomRequiredMetadataMissingException => SemanticSlot.G,
            _ => SemanticSlot.T
        };
    }

}
