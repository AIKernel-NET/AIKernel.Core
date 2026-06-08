namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Models;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
using AIKernel.Dtos.Routing;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedOperations']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedOperations']" />
    public IReadOnlyList<string> SupportedOperations => Operations;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedDataTypes']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedDataTypes']" />
    public IReadOnlyList<string> SupportedDataTypes => DataTypes;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxConcurrentConnections']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxConcurrentConnections']" />
    public int MaxConcurrentConnections => 1;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.RateLimit']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.RateLimit']" />
    public RateLimitInfo? RateLimit => null;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.new']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.new']" />
    public ModelCapacityVector Vector => new();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetDynamicCapacities']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetDynamicCapacities']" />
    public IDictionary<string, float>? GetDynamicCapacities(
        IExecutionConstraints constraints)
    {
        return null;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetCapabilityProfile']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.GetCapabilityProfile']" />
    public ICapabilityProfile? GetCapabilityProfile()
    {
        return null;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsOperation']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsOperation']" />
    public bool SupportsOperation(
        string operation)
    {
        return Operations.Contains(operation, StringComparer.OrdinalIgnoreCase);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsDataType']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsDataType']" />
    public bool SupportsDataType(
        string dataType)
    {
        return DataTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQuantization']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQuantization']" />
    public bool SupportsQuantization(
        string quantizationLevel)
    {
        return false;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryAugmentation']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryAugmentation']" />
    public bool SupportsQueryAugmentation => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryDecomposition']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryDecomposition']" />
    public bool SupportsQueryDecomposition => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryRouting']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryRouting']" />
    public bool SupportsQueryRouting => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxQueryParts']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.MaxQueryParts']" />
    public int MaxQueryParts => 0;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedQueryProcessingOperations']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedQueryProcessingOperations']" />
    public IReadOnlyList<string> SupportedQueryProcessingOperations => [];

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryProcessingOperation']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsQueryProcessingOperation']" />
    public bool SupportsQueryProcessingOperation(
        string operation)
    {
        return false;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsEmbedding']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportsEmbedding']" />
    public bool SupportsEmbedding => false;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.EmbeddingDimensions']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.EmbeddingDimensions']" />
    public int? EmbeddingDimensions => null;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedEmbeddingModels']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderCapabilities.SupportedEmbeddingModels']" />
    public IReadOnlyList<string> SupportedEmbeddingModels => [];
}
