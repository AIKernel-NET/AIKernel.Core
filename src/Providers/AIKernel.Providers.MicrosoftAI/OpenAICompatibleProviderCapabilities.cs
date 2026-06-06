namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

public sealed class OpenAICompatibleProviderCapabilities : IProviderCapabilities
{
    private static readonly string[] Operations =
    [
        "chat",
        "text-generation"
    ];

    private static readonly string[] DataTypes =
    [
        "text"
    ];

    public IReadOnlyList<string> SupportedOperations => Operations;

    public IReadOnlyList<string> SupportedDataTypes => DataTypes;

    public int MaxConcurrentConnections => 1;

    public RateLimitInfo? RateLimit => null;

    public ModelCapacityVector Vector => new();

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
        return Operations.Contains(operation, StringComparer.OrdinalIgnoreCase);
    }

    public bool SupportsDataType(
        string dataType)
    {
        return DataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
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
