namespace AIKernel.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Common.Results;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Core.Rom;
using AIKernel.Core.Time;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Enums;

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
        Exception exception,
        string? providerId = null)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Rejected,
            providerId: ResolveFailureProviderId(request, providerId, "none"),
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
                ResolveRejectedSemanticSlot(exception),
                semanticDeltaLabel: "kernel.facade.reject",
                providerId: providerId));
    }

    public KernelRequestExecutionResult CreateFailedResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        Exception exception,
        string? providerId = null)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Failed,
            providerId: ResolveFailureProviderId(request, providerId, "unknown"),
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
                SemanticSlot.T,
                semanticDeltaLabel: "kernel.facade.fail",
                providerId: providerId));
    }

    public KernelRequestExecutionResult CreateCanceledResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        string? providerId = null)
    {
        return CreateFailureResult(
            request,
            transaction,
            contextSnapshot,
            startedAtUtc,
            ExecutionStatus.Canceled,
            providerId: ResolveFailureProviderId(request, providerId, "none"),
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
                SemanticSlot.T,
                semanticDeltaLabel: "kernel.facade.cancel",
                providerId: providerId));
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
        SemanticSlot semanticSlot,
        string semanticDeltaLabel,
        string? providerId)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var item in request.Metadata ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            builder[item.Key] = item.Value;
        }

        builder[KernelFacadeMetadataKeys.RootRomId] = request.RootRomId?.Value ?? string.Empty;
        builder[KernelFacadeMetadataKeys.ProviderId] = ResolveFailureProviderId(request, providerId, string.Empty);
        builder[KernelFacadeMetadataKeys.VfsProviderId] = request.VfsProviderId ?? string.Empty;
        builder[KernelFacadeMetadataKeys.RequestedModelId] = request.RequestedModelId ?? string.Empty;

        if (exception is not null)
        {
            builder[ResultMetadataKeys.ExceptionType] = exception.GetType().FullName ?? exception.GetType().Name;
        }

        if (transaction is not null)
        {
            builder[KernelFacadeMetadataKeys.TransactionId] = transaction.TransactionId;
            builder[KernelFacadeMetadataKeys.InputHash] = transaction.InputHash;
        }

        builder[ReplayMetadataKeys.FailureKind] = failureKind.ToString();
        builder[ReplayMetadataKeys.OriginStep] = originStep.ToString();
        builder[ReplayMetadataKeys.SemanticSlot] = semanticSlot.ToString();

        AddFailureObservationMetadata(
            builder,
            exception?.Message ?? "Kernel transaction was canceled.",
            failureKind,
            originStep,
            semanticSlot,
            semanticDeltaLabel);

        return builder.ToImmutable();
    }

    private static void AddFailureObservationMetadata(
        ImmutableDictionary<string, string>.Builder builder,
        string message,
        FailureKind failureKind,
        OriginStep originStep,
        SemanticSlot semanticSlot,
        string semanticDeltaLabel)
    {
        var error = new ErrorContext(message, semanticDeltaLabel, false)
        {
            FailureKind = failureKind,
            OriginStep = originStep,
            SemanticSlot = semanticSlot
        };
        var step = ResultStep<string, string>
            .Fail("kernel.facade", error)
            .WithSemanticDelta(new SemanticDelta(
                semanticDeltaLabel,
                originStep,
                semanticSlot));

        builder[ReplayMetadataKeys.StepId] = step.StepId;
        builder[ReplayMetadataKeys.SemanticDelta] = step.SemanticDelta.Label;
        builder[ReplayMetadataKeys.ReplayLogCount] = step.ReplayLog.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder[ReplayMetadataKeys.ReplayLogHash] = step.ReplayLogHash;
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

    private static string ResolveFailureProviderId(
        KernelRequest request,
        string? providerId,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            return providerId;
        }

        if (request.Metadata is not null
            && request.Metadata.TryGetValue(KernelFacadeMetadataKeys.ProviderId, out var requestedProviderId)
            && !string.IsNullOrWhiteSpace(requestedProviderId))
        {
            return requestedProviderId;
        }

        return fallback;
    }

}
