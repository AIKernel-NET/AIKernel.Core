namespace AIKernel.IntegrationTests;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

internal sealed class TestProviderCapabilities : IProviderCapabilities
{
    public IReadOnlyList<string> SupportedOperations => [];

    public IReadOnlyList<string> SupportedDataTypes => [];

    public int MaxConcurrentConnections => 1;

    public RateLimitInfo? RateLimit => null;

    public ModelCapacityVector Vector => new();

    public IDictionary<string, float>? GetDynamicCapacities(IExecutionConstraints constraints)
    {
        return null;
    }

    public ICapabilityProfile? GetCapabilityProfile()
    {
        return null;
    }

    public bool SupportsOperation(string operation)
    {
        return false;
    }

    public bool SupportsDataType(string dataType)
    {
        return false;
    }

    public bool SupportsQuantization(string quantizationLevel)
    {
        return false;
    }

    public bool SupportsQueryAugmentation => false;

    public bool SupportsQueryDecomposition => false;

    public bool SupportsQueryRouting => false;

    public int MaxQueryParts => 0;

    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    public bool SupportsQueryProcessingOperation(string operation)
    {
        return false;
    }

    public bool SupportsEmbedding => false;

    public int? EmbeddingDimensions => null;

    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
