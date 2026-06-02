namespace AIKernel.Kernel;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Scheduling;
using AIKernel.Abstractions.Security;
using AIKernel.Core.Time;
using AIKernel.Dtos.Context;
using AIKernel.Core.Rom;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Enums;
using System.Collections.Immutable;

public sealed class Kernel : IKernel
{
    private readonly IKernelVfsSessionFactory _vfsSessionFactory;
    private readonly IContextAssembler _contextAssembler;
    private readonly IKernelModelProviderSelector _modelProviderSelector;
    private readonly AIKernel.Abstractions.Execution.IKernelExecutor _kernelExecutor;
    private readonly IKernelRequestHasher _requestHasher;
    private readonly IKernelTransactionIdFactory _transactionIdFactory;
    private readonly IKernelClock _clock;

    public Kernel(
        IKernelVfsSessionFactory vfsSessionFactory,
        IContextAssembler contextAssembler,
        IKernelModelProviderSelector modelProviderSelector,
        AIKernel.Abstractions.Execution.IKernelExecutor kernelExecutor,
        IKernelRequestHasher requestHasher,
        IKernelTransactionIdFactory transactionIdFactory,
        IKernelClock? clock = null)
    {
        _vfsSessionFactory = vfsSessionFactory
            ?? throw new ArgumentNullException(nameof(vfsSessionFactory));

        _contextAssembler = contextAssembler
            ?? throw new ArgumentNullException(nameof(contextAssembler));

        _modelProviderSelector = modelProviderSelector
            ?? throw new ArgumentNullException(nameof(modelProviderSelector));

        _kernelExecutor = kernelExecutor
            ?? throw new ArgumentNullException(nameof(kernelExecutor));

        _requestHasher = requestHasher
            ?? throw new ArgumentNullException(nameof(requestHasher));

        _transactionIdFactory = transactionIdFactory
            ?? throw new ArgumentNullException(nameof(transactionIdFactory));

        _clock = clock ?? KernelClock.System();
    }

    public async Task<KernelRequestExecutionResult> ExecuteAsync(
        KernelRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = _clock.Now;

        KernelTransactionSnapshot? transaction = null;
        IContextSnapshot? contextSnapshot = null;

        try
        {
            ValidateRequest(request);

            transaction = new KernelTransactionSnapshot
            {
                TransactionId = _transactionIdFactory.CreateTransactionId(request),
                InputHash = _requestHasher.ComputeHash(request),
                RootRomId = request.RootRomId.Value,
                VfsProviderId = request.VfsProviderId,
                RequestedModelId = request.RequestedModelId,
                StartedAtUtc = startedAtUtc,
                Metadata = request.Metadata
            };

            cancellationToken.ThrowIfCancellationRequested();

            // Side effect boundary:
            // Opens the selected VFS session. Provider-specific I/O starts beyond this boundary.
            await using var session = await _vfsSessionFactory
                .OpenSessionAsync(request, cancellationToken)
                .ConfigureAwait(false);

            // Context transaction boundary:
            // ContextAssembler loads ROMs on-demand, verifies signatures,
            // resolves relations, applies governance, and returns immutable ContextSnapshot.
            contextSnapshot = await _contextAssembler
                .AssembleAsync(
                    session,
                    new ContextAssemblyRequest(
                        RootRomId: request.RootRomId,
                        ParentSnapshotId: request.ParentSnapshotId,
                        Scope: request.Scope),
                    cancellationToken)
                .ConfigureAwait(false);

            // Provider selection boundary:
            // Model selection is not IKernelExecutor responsibility.
            var provider = await _modelProviderSelector
                .SelectAsync(request, contextSnapshot, cancellationToken)
                .ConfigureAwait(false);

            var executionRequest = new KernelExecutionRequest
            {
                ContextSnapshot = contextSnapshot,
                UserInstruction = request.Input,
                PromptOptions = request.PromptOptions,
                ExecutionOptions = request.ExecutionOptions,
                RequestedModelId = request.RequestedModelId
            };

            // Execution boundary:
            // IKernelExecutor generates prompt and calls IModelProvider.
            var result = await _kernelExecutor
                .ExecuteAsync(provider, executionRequest, cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            return CreateCanceledResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc);
        }
        catch (Exception ex) when (IsRejectedException(ex))
        {
            return CreateRejectedResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc,
                ex);
        }
        catch (Exception ex)
        {
            return CreateFailedResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc,
                ex);
        }
    }

    public string GetVersion()
    {
        return typeof(Kernel).Assembly
            .GetName()
            .Version?
            .ToString()
            ?? "0.0.0";
    }

    public Task<KernelExecutionResult> ExecuteAsync(UnifiedContextDto contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return Task.FromResult(new KernelExecutionResult
        {
            Success = true,
            Data = contract.Id,
            ExecutionTime = TimeSpan.Zero
        });
    }

    public Task<AttentionAnalysis> AnalyzeAttentionAsync(OrchestrationContextDto contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var hasPurpose = !string.IsNullOrWhiteSpace(contract.Purpose);
        var hasStructure = !string.IsNullOrWhiteSpace(contract.Structure);
        var signalToNoiseRatio = hasPurpose && hasStructure ? 1.0 : 0.0;

        var detectedPollution = signalToNoiseRatio > 0.0
            ? []
            : new List<FailureMode> { FailureMode.PurposeLost };

        return Task.FromResult(new AttentionAnalysis
        {
            SignalToNoiseRatio = signalToNoiseRatio,
            DetectedPollution = detectedPollution,
            RiskLevel = signalToNoiseRatio > 0.0 ? "Low" : "High"
        });
    }

    public Task<MaterialContextDto> PreprocessMaterialAsync(MaterialContextDto material)
    {
        ArgumentNullException.ThrowIfNull(material);

        return Task.FromResult(material);
    }

    public Task<ExpressionContextDto> PrepareExpressionAsync(ExpressionContextDto expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return Task.FromResult(expression);
    }

    public IProviderRouter GetProviderRouter()
    {
        throw new NotSupportedException(
            "Provider router accessor is not wired in AIKernel.Kernel yet. Use IKernelModelProviderSelector for KernelRequest execution.");
    }

    public IGuard GetGuard()
    {
        throw new NotSupportedException(
            "Guard accessor is not wired in AIKernel.Kernel yet. Use context governance policies for KernelRequest execution.");
    }

    public IPdp GetPdp()
    {
        throw new NotSupportedException(
            "PDP accessor is not wired in AIKernel.Kernel yet. Use context governance policies for KernelRequest execution.");
    }

    private static void ValidateRequest(KernelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            throw new KernelRequestValidationException("Input is required.");
        }

        if (request.RootRomId is null || string.IsNullOrWhiteSpace(request.RootRomId.Value))
        {
            throw new KernelRequestValidationException("RootRomId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.VfsProviderId))
        {
            throw new KernelRequestValidationException("VfsProviderId is required.");
        }

        if (request.VfsCredentials is null)
        {
            throw new KernelRequestValidationException("VfsCredentials is required.");
        }

        if (request.Scope is null)
        {
            throw new KernelRequestValidationException("Scope is required.");
        }

        if (request.PromptOptions is null)
        {
            throw new KernelRequestValidationException("PromptOptions is required.");
        }

        if (request.ExecutionOptions is null)
        {
            throw new KernelRequestValidationException("ExecutionOptions is required.");
        }
    }

    private static bool IsRejectedException(Exception ex)
    {
        return ex is KernelRequestValidationException
            or ContextAssemblyGovernanceException
            or RomSignatureVerificationException
            or RomRequiredMetadataMissingException
            or PromptTokenBudgetExceededException
            or UnsupportedPromptCapabilityException;
    }

    private KernelRequestExecutionResult CreateRejectedResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        Exception exception)
    {
        var completedAtUtc = _clock.Now;

        return new KernelRequestExecutionResult
        {
            ExecutionId = transaction?.TransactionId ?? $"exec:{Guid.NewGuid():N}",
            Status = ExecutionStatus.Rejected,
            ProviderId = "none",
            ModelId = request.RequestedModelId ?? "none",
            ContextSnapshotId = contextSnapshot?.SnapshotId ?? string.Empty,
            ContextHash = contextSnapshot?.ContextHash ?? string.Empty,
            PromptHash = string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0),
            Error = new ExecutionError(
                Code: ResolveRejectedCode(exception),
                Message: exception.Message,
                Detail: exception.GetType().FullName),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Metadata = BuildFailureMetadata(request, transaction, exception)
        };
    }

    private KernelRequestExecutionResult CreateFailedResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc,
        Exception exception)
    {
        var completedAtUtc = _clock.Now;

        return new KernelRequestExecutionResult
        {
            ExecutionId = transaction?.TransactionId ?? $"exec:{Guid.NewGuid():N}",
            Status = ExecutionStatus.Failed,
            ProviderId = "unknown",
            ModelId = request.RequestedModelId ?? "unknown",
            ContextSnapshotId = contextSnapshot?.SnapshotId ?? string.Empty,
            ContextHash = contextSnapshot?.ContextHash ?? string.Empty,
            PromptHash = string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0),
            Error = new ExecutionError(
                Code: "kernel_transaction_failed",
                Message: exception.Message,
                Detail: exception.GetType().FullName),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Metadata = BuildFailureMetadata(request, transaction, exception)
        };
    }

    private KernelRequestExecutionResult CreateCanceledResult(
        KernelRequest request,
        KernelTransactionSnapshot? transaction,
        IContextSnapshot? contextSnapshot,
        DateTimeOffset startedAtUtc)
    {
        var completedAtUtc = _clock.Now;

        return new KernelRequestExecutionResult
        {
            ExecutionId = transaction?.TransactionId ?? $"exec:{Guid.NewGuid():N}",
            Status = ExecutionStatus.Canceled,
            ProviderId = "none",
            ModelId = request.RequestedModelId ?? "none",
            ContextSnapshotId = contextSnapshot?.SnapshotId ?? string.Empty,
            ContextHash = contextSnapshot?.ContextHash ?? string.Empty,
            PromptHash = string.Empty,
            OutputText = null,
            Usage = new ExecutionUsage(
                InputTokens: 0,
                OutputTokens: 0,
                TotalTokens: 0),
            Error = new ExecutionError(
                Code: "canceled",
                Message: "Kernel transaction was canceled."),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Metadata = transaction?.Metadata
                ?? ImmutableDictionary<string, string>.Empty
        };
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
        Exception exception)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        builder["root_rom_id"] = request.RootRomId?.Value ?? string.Empty;
        builder["vfs_provider_id"] = request.VfsProviderId ?? string.Empty;
        builder["requested_model_id"] = request.RequestedModelId ?? string.Empty;
        builder["exception_type"] = exception.GetType().FullName ?? exception.GetType().Name;

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
}
