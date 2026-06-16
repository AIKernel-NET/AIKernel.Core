namespace AIKernel.Kernel;

using System.Collections.Immutable;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Kernel;
using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Scheduling;
using AIKernel.Abstractions.Security;
using AIKernel.Core.Context;
using AIKernel.Core.Execution;
using AIKernel.Core.Time;
using AIKernel.Dtos.Context;
using AIKernel.Core.Rom;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;
using AIKernel.Dtos.Security;
using AIKernel.Enums;

/// <summary>EN: Documentation for public API. JA: Kernel を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Kernel']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Kernel']/summary" />
public sealed class Kernel : IKernel
{
    private readonly IKernelVfsSessionFactory _vfsSessionFactory;
    private readonly IContextAssembler _contextAssembler;
    private readonly IKernelModelProviderSelector _modelProviderSelector;
    private readonly AIKernel.Abstractions.Execution.IKernelExecutor _kernelExecutor;
    private readonly IKernelRequestHasher _requestHasher;
    private readonly IKernelTransactionIdFactory _transactionIdFactory;
    private readonly IProviderRouter _providerRouter;
    private readonly IGuard _guard;
    private readonly IPdp _pdp;
    private readonly IKernelClock _clock;
    private readonly KernelFailureResultFactory _failureResultFactory;

    /// <summary>EN: Documentation for public API. JA: Kernel を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.#ctor']/summary" />
    public Kernel(
        IKernelVfsSessionFactory vfsSessionFactory,
        IContextAssembler contextAssembler,
        IKernelModelProviderSelector modelProviderSelector,
        AIKernel.Abstractions.Execution.IKernelExecutor kernelExecutor,
        IKernelRequestHasher requestHasher,
        IKernelTransactionIdFactory transactionIdFactory,
        IKernelClock? clock = null,
        IProviderRouter? providerRouter = null,
        IGuard? guard = null,
        IPdp? pdp = null)
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

        _providerRouter = providerRouter ?? FailClosedProviderRouter.Instance;
        _guard = guard ?? FailClosedGuard.Instance;
        _pdp = pdp ?? FailClosedPdp.Instance;
        _clock = clock ?? KernelClock.System();
        _failureResultFactory = new KernelFailureResultFactory(_clock);
    }

    /// <summary>EN: Documentation for public API. JA: ExecuteAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.ExecuteAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.ExecuteAsync']/summary" />
    public async Task<KernelRequestExecutionResult> ExecuteAsync(
        KernelRequest request,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = _clock.Now;

        KernelTransactionSnapshot? transaction = null;
        IContextSnapshot? contextSnapshot = null;
        string? selectedProviderId = null;

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
            selectedProviderId = provider.ProviderId;

            var executionRequest = new KernelExecutionRequest
            {
                ContextSnapshotId = contextSnapshot.SnapshotId,
                ContextHash = contextSnapshot.ContextHash,
                ContextBlocks = CreateContextPromptBlocks(contextSnapshot, request.PromptOptions.IncludeSourceMetadata),
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

            return CompleteSuccessfulResult(
                request,
                transaction,
                contextSnapshot,
                result,
                provider.ProviderId);
        }
        catch (OperationCanceledException)
        {
            return _failureResultFactory.CreateCanceledResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc,
                selectedProviderId);
        }
        catch (Exception ex) when (IsRejectedException(ex))
        {
            return _failureResultFactory.CreateRejectedResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc,
                ex,
                selectedProviderId);
        }
        catch (Exception ex)
        {
            return _failureResultFactory.CreateFailedResult(
                request,
                transaction,
                contextSnapshot,
                startedAtUtc,
                ex,
                selectedProviderId);
        }
    }

    private static IReadOnlyList<ContextPromptBlock> CreateContextPromptBlocks(
        IContextSnapshot snapshot,
        bool includeSourceMetadata)
    {
        return snapshot.Context.GetAll()
            .OrderBy(fragment => fragment.Category)
            .ThenBy(fragment => fragment.FragmentId, StringComparer.Ordinal)
            .Select(fragment => new ContextPromptBlock(
                Id: fragment.FragmentId,
                Category: fragment.Category.ToString(),
                Content: fragment.Content,
                Priority: ResolveContextPriority(fragment.Category),
                Metadata: includeSourceMetadata
                    ? fragment.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)))
            .ToArray();
    }

    private static int ResolveContextPriority(ContextCategory category)
    {
        return category switch
        {
            ContextCategory.Governance => 1000,
            ContextCategory.Orchestration => 900,
            ContextCategory.Material => 700,
            ContextCategory.History => 500,
            ContextCategory.Expression => 100,
            _ => 300
        };
    }

    /// <summary>EN: Documentation for public API. JA: GetVersion を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetVersion']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetVersion']/summary" />
    public string GetVersion()
    {
        return typeof(Kernel).Assembly
            .GetName()
            .Version?
            .ToString()
            ?? "0.0.0";
    }

    /// <summary>EN: Documentation for public API. JA: ExecuteAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.ExecuteAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.ExecuteAsync']/summary" />
    public Task<KernelExecutionResult> ExecuteAsync(UnifiedContextDto contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        return Task.FromResult(new KernelExecutionResult
        {
            Success = false,
            Data = null,
            Error = "KernelRequest execution is required for the AIKernel.Core pipeline.",
            FailureModes = [FailureMode.ReasoningStopped],
            ExecutionTime = TimeSpan.Zero
        });
    }

    /// <summary>EN: Documentation for public API. JA: AnalyzeAttentionAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.AnalyzeAttentionAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.AnalyzeAttentionAsync']/summary" />
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

    /// <summary>EN: Documentation for public API. JA: PreprocessMaterialAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.PreprocessMaterialAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.PreprocessMaterialAsync']/summary" />
    public Task<MaterialContextDto> PreprocessMaterialAsync(MaterialContextDto material)
    {
        ArgumentNullException.ThrowIfNull(material);

        return Task.FromResult(material);
    }

    /// <summary>EN: Documentation for public API. JA: PrepareExpressionAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.PrepareExpressionAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.PrepareExpressionAsync']/summary" />
    public Task<ExpressionContextDto> PrepareExpressionAsync(ExpressionContextDto expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return Task.FromResult(expression);
    }

    /// <summary>EN: Documentation for public API. JA: GetProviderRouter を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetProviderRouter']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetProviderRouter']/summary" />
    public IProviderRouter GetProviderRouter()
    {
        return _providerRouter;
    }

    /// <summary>EN: Documentation for public API. JA: GetGuard を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetGuard']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetGuard']/summary" />
    public IGuard GetGuard()
    {
        return _guard;
    }

    /// <summary>EN: Documentation for public API. JA: GetPdp を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetPdp']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.Kernel.GetPdp']/summary" />
    public IPdp GetPdp()
    {
        return _pdp;
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

        if (request.Credentials is null)
        {
            throw new KernelRequestValidationException("Credentials is required.");
        }

        if (request.Scope is null)
        {
            throw new KernelRequestValidationException("Scope is required.");
        }

        if (request.Scope.Metadata is null)
        {
            throw new KernelRequestValidationException("Scope.Metadata is required.");
        }

        if (request.PromptOptions is null)
        {
            throw new KernelRequestValidationException("PromptOptions is required.");
        }

        if (request.ExecutionOptions is null)
        {
            throw new KernelRequestValidationException("ExecutionOptions is required.");
        }

        if (request.Metadata is null)
        {
            throw new KernelRequestValidationException("Metadata is required.");
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

    private static KernelRequestExecutionResult CompleteSuccessfulResult(
        KernelRequest request,
        KernelTransactionSnapshot transaction,
        IContextSnapshot contextSnapshot,
        KernelRequestExecutionResult result,
        string providerId)
    {
        return result with
        {
            ProviderId = providerId,
            ModelId = request.RequestedModelId ?? result.ModelId,
            ContextSnapshotId = contextSnapshot.SnapshotId,
            ContextHash = contextSnapshot.ContextHash,
            Error = result.Status == ExecutionStatus.Succeeded
                ? null
                : result.Error,
            Metadata = BuildSuccessfulMetadata(
                request,
                transaction,
                result,
                providerId)
        };
    }

    private static ImmutableDictionary<string, string> BuildSuccessfulMetadata(
        KernelRequest request,
        KernelTransactionSnapshot transaction,
        KernelRequestExecutionResult result,
        string providerId)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        foreach (var item in request.Metadata)
        {
            builder[item.Key] = item.Value;
        }

        foreach (var item in result.Metadata ?? Enumerable.Empty<KeyValuePair<string, string>>())
        {
            builder[item.Key] = item.Value;
        }

        builder[KernelFacadeMetadataKeys.RootRomId] = request.RootRomId.Value;
        builder[KernelFacadeMetadataKeys.ProviderId] = providerId;
        builder[KernelFacadeMetadataKeys.VfsProviderId] = request.VfsProviderId;
        builder[KernelFacadeMetadataKeys.RequestedModelId] = request.RequestedModelId ?? string.Empty;
        builder[KernelFacadeMetadataKeys.TransactionId] = transaction.TransactionId;
        builder[KernelFacadeMetadataKeys.InputHash] = transaction.InputHash;

        return builder.ToImmutable();
    }

}
