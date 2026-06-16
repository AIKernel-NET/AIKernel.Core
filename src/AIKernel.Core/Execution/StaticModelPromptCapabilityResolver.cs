namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;

/// <summary>[EN] Documents this public package API member. [JA] StaticModelPromptCapabilityResolver を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver']/summary" />
public sealed class StaticModelPromptCapabilityResolver : IModelPromptCapabilityResolver
{
    private readonly IReadOnlyDictionary<string, ModelPromptCapability> _capabilities;

    /// <summary>[EN] Documents this public package API member. [JA] StaticModelPromptCapabilityResolver を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver.#ctor']/summary" />
    public StaticModelPromptCapabilityResolver(IEnumerable<ModelPromptCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        _capabilities = BuildCapabilityMap(capabilities);
    }

    /// <summary>[EN] Documents this public package API member. [JA] Resolve を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver.Resolve']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.StaticModelPromptCapabilityResolver.Resolve']/summary" />
    public ModelPromptCapability Resolve(
        IModelProvider provider,
        KernelExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        return RequireModelId(request.RequestedModelId)
            .Match(
                message => throw new UnsupportedPromptCapabilityException(message),
                modelId => FindCapability(provider.ProviderId, modelId)
                    .Match(
                        () => throw new UnsupportedPromptCapabilityException(
                            $"Prompt capability was not found. ProviderId='{provider.ProviderId}', ModelId='{modelId}'."),
                        capability => capability));
    }

    private static string CreateKey(string providerId, string modelId)
    {
        return providerId + "::" + modelId;
    }

    private Option<ModelPromptCapability> FindCapability(
        string providerId,
        string modelId)
    {
        if (_capabilities.TryGetValue(CreateKey(providerId, modelId), out var capability))
        {
            return Option<ModelPromptCapability>.Some(capability);
        }

        return Option<ModelPromptCapability>.None();
    }

    private static Either<string, string> RequireModelId(
        string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            return Either<string, string>.FromRight(modelId);
        }

        return Either<string, string>.FromLeft(
            "RequestedModelId is required for static capability resolution.");
    }

    private static IReadOnlyDictionary<string, ModelPromptCapability> BuildCapabilityMap(
        IEnumerable<ModelPromptCapability> capabilities)
    {
        var map = new Dictionary<string, ModelPromptCapability>(StringComparer.Ordinal);

        foreach (var capability in capabilities)
        {
            ValidateCapability(capability);

            var key = CreateKey(capability.ProviderId, capability.ModelId);
            if (!map.TryAdd(key, capability))
            {
                throw new ArgumentException(
                    $"Duplicate prompt capability registration. ProviderId='{capability.ProviderId}', ModelId='{capability.ModelId}'.",
                    nameof(capabilities));
            }
        }

        return map;
    }

    private static void ValidateCapability(ModelPromptCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (string.IsNullOrWhiteSpace(capability.ProviderId))
        {
            throw new ArgumentException(
                "ModelPromptCapability.ProviderId is required.",
                nameof(capability));
        }

        if (string.IsNullOrWhiteSpace(capability.ModelId))
        {
            throw new ArgumentException(
                "ModelPromptCapability.ModelId is required.",
                nameof(capability));
        }
    }
}
