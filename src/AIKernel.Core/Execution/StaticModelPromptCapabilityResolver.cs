namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;

public sealed class StaticModelPromptCapabilityResolver : IModelPromptCapabilityResolver
{
    private readonly IReadOnlyDictionary<string, ModelPromptCapability> _capabilities;

    public StaticModelPromptCapabilityResolver(IEnumerable<ModelPromptCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        _capabilities = BuildCapabilityMap(capabilities);
    }

    public ModelPromptCapability Resolve(
        IModelProvider provider,
        KernelExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var modelId = request.RequestedModelId;

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new UnsupportedPromptCapabilityException(
                "RequestedModelId is required for static capability resolution.");
        }

        var key = CreateKey(provider.ProviderId, modelId);

        if (!_capabilities.TryGetValue(key, out var capability))
        {
            throw new UnsupportedPromptCapabilityException(
                $"Prompt capability was not found. ProviderId='{provider.ProviderId}', ModelId='{modelId}'.");
        }

        return capability;
    }

    private static string CreateKey(string providerId, string modelId)
    {
        return providerId + "::" + modelId;
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
