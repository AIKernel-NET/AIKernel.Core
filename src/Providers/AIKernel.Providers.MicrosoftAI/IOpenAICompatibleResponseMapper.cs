namespace AIKernel.Providers.MicrosoftAI;

using Microsoft.Extensions.AI;

public interface IOpenAICompatibleResponseMapper
{
    string GetPrimaryText(ChatResponse response);

    OpenAICompatibleResponseProjection CreateProjection(
        ChatResponse response,
        string fallbackModelId,
        DateTimeOffset observedAtUtc);
}
