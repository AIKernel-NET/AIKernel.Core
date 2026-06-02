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

        _capabilities = capabilities.ToDictionary(
            x => CreateKey(x.ProviderId, x.ModelId),
            StringComparer.Ordinal);
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
}
