namespace AIKernel.IntegrationTests;

using AIKernel.Abstractions.Providers;
using AIKernel.Providers.MicrosoftAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class OpenAICompatibleProviderTests
{
    [Fact]
    public async Task GenerateAsync_FailsClosed_WhenProviderIsNotInitialized()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ProviderApiException>(
            () => provider.GenerateAsync(
            [
                new TestModelMessage("user", "hello")
            ],
            TestContext.Current.CancellationToken));

        Assert.Contains(
            "Provider has not been initialized.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHealthAsync_FailsClosed_WhenHealthFactoryIsMissing()
    {
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<ProviderApiException>(
            () => provider.GetHealthAsync());

        Assert.Contains(
            "Provider health status factory is not configured.",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static OpenAICompatibleProvider CreateProvider()
    {
        return new OpenAICompatibleProvider(
            new StubChatClient(),
            new TestProviderCapabilities(),
            new OpenAICompatibleResponseMapper(),
            new OpenAICompatibleProviderOptions
            {
                ModelId = "gpt-test",
                ApiKey = "sk-test-123456"
            },
            NullLogger<OpenAICompatibleProvider>.Instance);
    }

    private sealed record TestModelMessage(
        string Role,
        string Content) : IModelMessage;

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "ok"))
                {
                    ModelId = options?.ModelId
                });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
