namespace AIKernel.IntegrationTests;

using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Core;
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

    [Fact]
    public async Task GetHealthAsync_UsesCurrentInitializationState()
    {
        OpenAICompatibleProviderHealthContext? observedContext = null;

        var provider = CreateProvider(
            context =>
            {
                observedContext = context;
                return new ProviderHealthStatus(
                    IsHealthy: context.IsInitialized,
                    Message: context.IsInitialized ? "OK" : "Not initialized",
                    CheckedAt: context.CheckedAtUtc.UtcDateTime,
                    ResponseTimeMs: 0);
            });

        await provider.InitializeAsync();

        var health = await provider.GetHealthAsync();

        Assert.True(health.IsHealthy);
        Assert.NotNull(observedContext);
        Assert.True(observedContext.IsInitialized);
        Assert.Equal("openai-compatible", observedContext.ProviderId);
        Assert.Equal("gpt-test", observedContext.ModelId);
    }

    private static OpenAICompatibleProvider CreateProvider()
    {
        return CreateProvider(healthStatusFactory: null);
    }

    private static OpenAICompatibleProvider CreateProvider(
        Func<OpenAICompatibleProviderHealthContext, ProviderHealthStatus>? healthStatusFactory)
    {
        return new OpenAICompatibleProvider(
            new StubChatClient(),
            new TestProviderCapabilities(),
            new OpenAICompatibleResponseMapper(),
            new OpenAICompatibleProviderOptions
            {
                ModelId = "gpt-test",
                ApiKey = "sk-test-123456",
                HealthStatusFactory = healthStatusFactory
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
