namespace AIKernel.Core.Providers;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

internal sealed class StandardProviderCapabilities : IProviderCapabilities
{
    private readonly IReadOnlyList<string> _operations;
    private readonly IReadOnlyList<string> _dataTypes;

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

    public IReadOnlyList<string> SupportedOperations => _operations;

    public IReadOnlyList<string> SupportedDataTypes => _dataTypes;

    public int MaxConcurrentConnections => 1;

    public RateLimitInfo? RateLimit => null;

    public ModelCapacityVector Vector => new(
        structuralIntegrity: 1f,
        linguisticFluidity: 0f,
        reasoningDepth: 0f,
        fidelity: 1f,
        latencyPerformance: 1f);

    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
    {
        return null;
    }

    public ICapabilityProfile? GetCapabilityProfile()
    {
        return null;
    }

    public bool SupportsOperation(
        string operation)
    {
        return _operations.Contains(operation, StringComparer.Ordinal);
    }

    public bool SupportsDataType(
        string dataType)
    {
        return _dataTypes.Contains(dataType, StringComparer.Ordinal);
    }

    public bool SupportsQuantization(
        string quantizationLevel)
    {
        return false;
    }

    public bool SupportsQueryAugmentation => false;

    public bool SupportsQueryDecomposition => false;

    public bool SupportsQueryRouting => false;

    public int MaxQueryParts => 0;

    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    public bool SupportsQueryProcessingOperation(
        string operation)
    {
        return false;
    }

    public bool SupportsEmbedding => false;

    public int? EmbeddingDimensions => null;

    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
