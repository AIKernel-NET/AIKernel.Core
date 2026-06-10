namespace AIKernel.Core.Providers.SkillProvider;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Dsl;
using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

/// <summary>
/// [EN] Built-in OS-level provider that exposes OpenAI-compatible Skill.MD files as AIKernel capability modules.
/// [JA] OpenAI 互換 Skill.MD を AIKernel capability module として公開する組み込み OS-level provider です。
/// </summary>
public sealed class SkillProvider :
    IProvider,
    ICapabilityModuleInvoker
{
    /// <summary>
    /// [EN] Stable provider identifier for the built-in Skill Provider.
    /// [JA] 組み込み Skill Provider の安定 provider identifier です。
    /// </summary>
    public const string ProviderIdValue = "aikernel.skill";

    private readonly ICapabilityModuleRegistry? _registry;
    private readonly IDslPipelineCompiler? _dslCompiler;
    private readonly SkillLoader _loader;
    private readonly string _rootPath;
    private IReadOnlyDictionary<string, SkillCapabilityDescriptor> _descriptors =
        new Dictionary<string, SkillCapabilityDescriptor>(StringComparer.Ordinal);

    /// <summary>
    /// [EN] Creates a provider with the default skill root path.
    /// [JA] 既定の skill root path を使用して provider を作成します。
    /// </summary>
    public SkillProvider()
        : this(null, null, new SkillLoader(), GetDefaultRootPath())
    {
    }

    /// <summary>
    /// [EN] Creates a provider with explicit registries and root path.
    /// [JA] 明示された registry と root path を使用して provider を作成します。
    /// </summary>
    public SkillProvider(
        ICapabilityModuleRegistry? registry,
        IDslPipelineCompiler? dslCompiler,
        SkillLoader? loader = null,
        string? rootPath = null)
    {
        _registry = registry;
        _dslCompiler = dslCompiler;
        _loader = loader ?? new SkillLoader();
        _rootPath = SelectExplicitRootPath(rootPath)
            .Match(_ => GetDefaultRootPath(), value => value);
    }

    /// <summary>
    /// [EN] Executes the ProviderId operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして ProviderId 操作を実行します。
    /// </summary>
    public string ProviderId => ProviderIdValue;

    /// <summary>
    /// [EN] Executes the Name operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして Name 操作を実行します。
    /// </summary>
    public string Name => "Skill Provider";

    /// <summary>
    /// [EN] Executes the Version operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして Version 操作を実行します。
    /// </summary>
    public string Version => "1.0.0";

    /// <summary>
    /// [EN] Executes the GetCapabilities operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして GetCapabilities 操作を実行します。
    /// </summary>
    public IProviderCapabilities GetCapabilities()
    {
        return new SkillProviderCapabilities(
            _descriptors.Values
                .SelectMany(x => x.ProvidedOperations)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// [EN] Executes the IsAvailableAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして IsAvailableAsync 操作を実行します。
    /// </summary>
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// [EN] Executes the InitializeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InitializeAsync 操作を実行します。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_registry is null)
        {
            _descriptors = _loader.Load(_rootPath)
                .Select(x => x.ToDescriptor())
                .OrderBy(x => x.CapabilityId, StringComparer.Ordinal)
                .ToDictionary(x => x.CapabilityId, x => x, StringComparer.Ordinal);
            return;
        }

        var descriptors = await _loader
            .LoadAndRegisterAsync(_rootPath, _registry, _dslCompiler)
            .ConfigureAwait(false);

        _descriptors = descriptors
            .OrderBy(x => x.CapabilityId, StringComparer.Ordinal)
            .ToDictionary(x => x.CapabilityId, x => x, StringComparer.Ordinal);
    }

    /// <summary>
    /// [EN] Executes the ShutdownAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして ShutdownAsync 操作を実行します。
    /// </summary>
    public Task ShutdownAsync()
    {
        _descriptors = new Dictionary<string, SkillCapabilityDescriptor>(StringComparer.Ordinal);
        return Task.CompletedTask;
    }

    /// <summary>
    /// [EN] Executes the GetHealthAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして GetHealthAsync 操作を実行します。
    /// </summary>
    public Task<ProviderHealthStatus> GetHealthAsync()
    {
        return Task.FromResult(new ProviderHealthStatus(
            IsHealthy: true,
            Message: "Healthy",
            CheckedAt: DateTime.UnixEpoch,
            ResponseTimeMs: 0));
    }

    /// <summary>
    /// [EN] Executes the InvokeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InvokeAsync 操作を実行します。
    /// </summary>
    public ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(
            request.Metadata,
            StringComparer.Ordinal)
        {
            ["provider"] = ProviderId,
            ["operation"] = request.Operation
        };

        return ValueTask.FromResult(FindDescriptor(request.CapabilityId)
            .Match(
                () => Fail(
                    request,
                    metadata,
                    "SKILL_CAPABILITY_NOT_FOUND",
                    "Requested skill capability is not registered."),
                descriptor => RequireSupportedOperation(descriptor, request.Operation)
                    .Match(
                        error => Fail(
                            request,
                            metadata,
                            "SKILL_OPERATION_NOT_SUPPORTED",
                            error),
                        skill => CreateSuccess(request, metadata, skill))));
    }

    private Option<SkillCapabilityDescriptor> FindDescriptor(
        string capabilityId)
    {
        if (_descriptors.TryGetValue(capabilityId, out var descriptor))
        {
            return Option<SkillCapabilityDescriptor>.Some(descriptor);
        }

        return Option<SkillCapabilityDescriptor>.None();
    }

    private static Either<string, SkillCapabilityDescriptor> RequireSupportedOperation(
        SkillCapabilityDescriptor descriptor,
        string operation)
    {
        if (descriptor.ProvidedOperations.Contains(operation, StringComparer.Ordinal))
        {
            return Either<string, SkillCapabilityDescriptor>.FromRight(descriptor);
        }

        return Either<string, SkillCapabilityDescriptor>.FromLeft(
            "Requested operation is not exposed by the skill capability.");
    }

    private static CapabilityInvocationResult Fail(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata,
        string errorCode,
        string errorMessage)
    {
        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);
    }

    private static CapabilityInvocationResult CreateSuccess(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        SkillCapabilityDescriptor skill)
    {
        metadata["skill.source_path"] = skill.SourcePath;
        metadata["skill.description"] = skill.Description;

        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: true,
            OutputHash: ComputeHash(skill),
            ErrorCode: null,
            ErrorMessage: null,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static string ComputeHash(
        SkillCapabilityDescriptor descriptor)
    {
        var payload = string.Join(
            "\n",
            descriptor.CapabilityId,
            descriptor.Name,
            descriptor.Version,
            descriptor.Description,
            descriptor.SourcePath,
            string.Join(",", descriptor.ProvidedOperations));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDefaultRootPath()
    {
        return Environment.GetEnvironmentVariable("AIKERNEL_SKILL_ROOT") ?? "skills";
    }

    private static Either<string, string> SelectExplicitRootPath(
        string? rootPath)
    {
        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            return Either<string, string>.FromRight(rootPath);
        }

        return Either<string, string>.FromLeft("Skill root path was not specified.");
    }

    private sealed class SkillProviderCapabilities : IProviderCapabilities
    {
        private static readonly string[] DataTypes = ["skill", "markdown", "dsl"];
        private readonly IReadOnlyList<string> _operations;

        /// <summary>
        /// [EN] Creates provider capabilities from the operations discovered in Skill.MD files.
        /// [JA] Skill.MD file から発見された operation から provider capabilities を作成します。
        /// </summary>
        public SkillProviderCapabilities(
            IReadOnlyList<string> operations)
        {
            _operations = SelectOperations(operations)
                .Match(_ => ["skill.execute"], value => value);
        }

        private static Either<string, IReadOnlyList<string>> SelectOperations(
            IReadOnlyList<string> operations)
        {
            if (operations.Count > 0)
            {
                return Either<string, IReadOnlyList<string>>.FromRight(operations);
            }

            return Either<string, IReadOnlyList<string>>.FromLeft(
                "Skill provider has no discovered operations.");
        }

        /// <summary>
        /// [EN] Executes the SupportedOperations operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportedOperations 操作を実行します。
        /// </summary>
        public IReadOnlyList<string> SupportedOperations => _operations;

        /// <summary>
        /// [EN] Executes the SupportedDataTypes operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportedDataTypes 操作を実行します。
        /// </summary>
        public IReadOnlyList<string> SupportedDataTypes => DataTypes;

        /// <summary>
        /// [EN] Executes the MaxConcurrentConnections operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして MaxConcurrentConnections 操作を実行します。
        /// </summary>
        public int MaxConcurrentConnections => 1;

        /// <summary>
        /// [EN] Executes the RateLimit operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして RateLimit 操作を実行します。
        /// </summary>
        public RateLimitInfo? RateLimit => null;

        /// <summary>
        /// [EN] Executes the new operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして new 操作を実行します。
        /// </summary>
        public ModelCapacityVector Vector => new(
            structuralIntegrity: 1f,
            linguisticFluidity: 0f,
            reasoningDepth: 0f,
            fidelity: 1f,
            latencyPerformance: 1f);

        /// <summary>
        /// [EN] Executes the GetDynamicCapacities operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして GetDynamicCapacities 操作を実行します。
        /// </summary>
        public IDictionary<string, float>? GetDynamicCapacities(
            IExecutionConstraints constraints)
        {
            return null;
        }

        /// <summary>
        /// [EN] Executes the GetCapabilityProfile operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして GetCapabilityProfile 操作を実行します。
        /// </summary>
        public ICapabilityProfile? GetCapabilityProfile()
        {
            return null;
        }

        /// <summary>
        /// [EN] Executes the SupportsOperation operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsOperation 操作を実行します。
        /// </summary>
        public bool SupportsOperation(
            string operation)
        {
            return _operations.Contains(operation, StringComparer.Ordinal);
        }

        /// <summary>
        /// [EN] Executes the SupportsDataType operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsDataType 操作を実行します。
        /// </summary>
        public bool SupportsDataType(
            string dataType)
        {
            return DataTypes.Contains(dataType, StringComparer.Ordinal);
        }

        /// <summary>
        /// [EN] Executes the SupportsQuantization operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsQuantization 操作を実行します。
        /// </summary>
        public bool SupportsQuantization(
            string quantizationLevel)
        {
            return false;
        }

        /// <summary>
        /// [EN] Executes the SupportsQueryAugmentation operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsQueryAugmentation 操作を実行します。
        /// </summary>
        public bool SupportsQueryAugmentation => false;

        /// <summary>
        /// [EN] Executes the SupportsQueryDecomposition operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsQueryDecomposition 操作を実行します。
        /// </summary>
        public bool SupportsQueryDecomposition => false;

        /// <summary>
        /// [EN] Executes the SupportsQueryRouting operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsQueryRouting 操作を実行します。
        /// </summary>
        public bool SupportsQueryRouting => false;

        /// <summary>
        /// [EN] Executes the MaxQueryParts operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして MaxQueryParts 操作を実行します。
        /// </summary>
        public int MaxQueryParts => 0;

        /// <summary>
        /// [EN] Executes the SupportedQueryProcessingOperations operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportedQueryProcessingOperations 操作を実行します。
        /// </summary>
        public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

        /// <summary>
        /// [EN] Executes the SupportsQueryProcessingOperation operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsQueryProcessingOperation 操作を実行します。
        /// </summary>
        public bool SupportsQueryProcessingOperation(
            string operation)
        {
            return false;
        }

        /// <summary>
        /// [EN] Executes the SupportsEmbedding operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportsEmbedding 操作を実行します。
        /// </summary>
        public bool SupportsEmbedding => false;

        /// <summary>
        /// [EN] Executes the EmbeddingDimensions operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして EmbeddingDimensions 操作を実行します。
        /// </summary>
        public int? EmbeddingDimensions => null;

        /// <summary>
        /// [EN] Executes the SupportedEmbeddingModels operation as part of the AIKernel public reference surface.
        /// [JA] AIKernel の公開参照サーフェスとして SupportedEmbeddingModels 操作を実行します。
        /// </summary>
        public IReadOnlyList<string> SupportedEmbeddingModels => [];
    }
}
