namespace AIKernel.Core.Providers;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

internal sealed class StandardProviderCapabilities : IProviderCapabilities
{
    private readonly IReadOnlyList<string> _operations;
    private readonly IReadOnlyList<string> _dataTypes;
    /// <summary>
    /// EN: Gets StandardProviderCapabilities.
    /// [EN] Documents this public package API member. [JA] StandardProviderCapabilities を取得します。
    /// </summary>

    public StandardProviderCapabilities(
        IReadOnlyList<string> operations,
        IReadOnlyList<string> dataTypes)
    {
        _operations = operations
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        _dataTypes = dataTypes
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }
    /// <summary>
    /// EN: Gets SupportedOperations.
    /// [EN] Documents this public package API member. [JA] SupportedOperations を取得します。
    /// </summary>

    public IReadOnlyList<string> SupportedOperations => _operations;
    /// <summary>
    /// EN: Gets SupportedDataTypes.
    /// [EN] Documents this public package API member. [JA] SupportedDataTypes を取得します。
    /// </summary>

    public IReadOnlyList<string> SupportedDataTypes => _dataTypes;
    /// <summary>
    /// EN: Gets MaxConcurrentConnections.
    /// [EN] Documents this public package API member. [JA] MaxConcurrentConnections を取得します。
    /// </summary>

    public int MaxConcurrentConnections => 1;
    /// <summary>
    /// EN: Gets RateLimit.
    /// [EN] Documents this public package API member. [JA] RateLimit を取得します。
    /// </summary>

    public RateLimitInfo? RateLimit => null;
    /// <summary>
    /// EN: Gets Vector.
    /// [EN] Documents this public package API member. [JA] Vector を取得します。
    /// </summary>

    public ModelCapacityVector Vector => new(
        structuralIntegrity: 1f,
        linguisticFluidity: 0f,
        reasoningDepth: 0f,
        fidelity: 1f,
        latencyPerformance: 1f);
    /// <summary>
    /// EN: Gets GetDynamicCapacities.
    /// [EN] Documents this public package API member. [JA] GetDynamicCapacities を取得します。
    /// </summary>

    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
    {
        return null;
    }
    /// <summary>
    /// EN: Executes GetCapabilityProfile.
    /// [EN] Documents this public package API member. [JA] GetCapabilityProfile を実行します。
    /// </summary>

    public ICapabilityProfile? GetCapabilityProfile()
    {
        return null;
    }
    /// <summary>
    /// EN: Gets SupportsOperation.
    /// [EN] Documents this public package API member. [JA] SupportsOperation を取得します。
    /// </summary>

    public bool SupportsOperation(
        string operation)
    {
        return _operations.Contains(operation, StringComparer.Ordinal);
    }
    /// <summary>
    /// EN: Gets SupportsDataType.
    /// [EN] Documents this public package API member. [JA] SupportsDataType を取得します。
    /// </summary>

    public bool SupportsDataType(
        string dataType)
    {
        return _dataTypes.Contains(dataType, StringComparer.Ordinal);
    }
    /// <summary>
    /// EN: Gets SupportsQuantization.
    /// [EN] Documents this public package API member. [JA] SupportsQuantization を取得します。
    /// </summary>

    public bool SupportsQuantization(
        string quantizationLevel)
    {
        return false;
    }
    /// <summary>
    /// EN: Gets SupportsQueryAugmentation.
    /// [EN] Documents this public package API member. [JA] SupportsQueryAugmentation を取得します。
    /// </summary>

    public bool SupportsQueryAugmentation => false;
    /// <summary>
    /// EN: Gets SupportsQueryDecomposition.
    /// [EN] Documents this public package API member. [JA] SupportsQueryDecomposition を取得します。
    /// </summary>

    public bool SupportsQueryDecomposition => false;
    /// <summary>
    /// EN: Gets SupportsQueryRouting.
    /// [EN] Documents this public package API member. [JA] SupportsQueryRouting を取得します。
    /// </summary>

    public bool SupportsQueryRouting => false;
    /// <summary>
    /// EN: Gets MaxQueryParts.
    /// [EN] Documents this public package API member. [JA] MaxQueryParts を取得します。
    /// </summary>

    public int MaxQueryParts => 0;
    /// <summary>
    /// EN: Gets SupportedQueryProcessingOperations.
    /// [EN] Documents this public package API member. [JA] SupportedQueryProcessingOperations を取得します。
    /// </summary>

    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];
    /// <summary>
    /// EN: Gets SupportsQueryProcessingOperation.
    /// [EN] Documents this public package API member. [JA] SupportsQueryProcessingOperation を取得します。
    /// </summary>

    public bool SupportsQueryProcessingOperation(
        string operation)
    {
        return false;
    }
    /// <summary>
    /// EN: Gets SupportsEmbedding.
    /// [EN] Documents this public package API member. [JA] SupportsEmbedding を取得します。
    /// </summary>

    public bool SupportsEmbedding => false;
    /// <summary>
    /// EN: Gets EmbeddingDimensions.
    /// [EN] Documents this public package API member. [JA] EmbeddingDimensions を取得します。
    /// </summary>

    public int? EmbeddingDimensions => null;
    /// <summary>
    /// EN: Gets SupportedEmbeddingModels.
    /// [EN] Documents this public package API member. [JA] SupportedEmbeddingModels を取得します。
    /// </summary>

    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
