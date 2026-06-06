using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Tests.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using AIKernel.Abstractions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using AIKernel.Hosting;

public sealed class SecureCredentialResolverTests
{
    [Fact]
    public async Task ResolveAsync_InjectsSecretIntoSecureOptions_WhenSecretKeyNameIsSpecified()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISecureCredentialProvider>(
            new StubSecureCredentialProvider("sk-test-123456"));

        services
            .AddOptions<TestSecureOptions>()
            .Configure(options =>
            {
                options.SecretKeyName = "OpenAI:ApiKey";
            });

        services.AddSingleton<SecureCredentialResolver<TestSecureOptions>>();

        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<SecureCredentialResolver<TestSecureOptions>>();

        var options = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("sk-test-123456", options.ApiKey);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsOptions_WhenDirectApiKeyIsSpecified()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISecureCredentialProvider>(
            new StubSecureCredentialProvider("unused"));

        services
            .AddOptions<TestSecureOptions>()
            .Configure(options =>
            {
                options.ApiKey = "sk-direct-123456";
            });

        services.AddSingleton<SecureCredentialResolver<TestSecureOptions>>();

        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<SecureCredentialResolver<TestSecureOptions>>();

        var options = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Equal("sk-direct-123456", options.ApiKey);
    }

    [Fact]
    public async Task ResolveAsync_FailsClosed_WhenNeitherApiKeyNorSecretKeyNameIsSpecified()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISecureCredentialProvider>(
            new StubSecureCredentialProvider("unused"));

        services.AddOptions<TestSecureOptions>();
        services.AddSingleton<SecureCredentialResolver<TestSecureOptions>>();

        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<SecureCredentialResolver<TestSecureOptions>>();

        await Assert.ThrowsAsync<SecureCredentialNotFoundException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_FailsClosed_WhenBothApiKeyAndSecretKeyNameAreSpecified()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISecureCredentialProvider>(
            new StubSecureCredentialProvider("sk-secret-123456"));

        services
            .AddOptions<TestSecureOptions>()
            .Configure(options =>
            {
                options.ApiKey = "sk-direct-123456";
                options.SecretKeyName = "OpenAI:ApiKey";
            });

        services.AddSingleton<SecureCredentialResolver<TestSecureOptions>>();

        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<SecureCredentialResolver<TestSecureOptions>>();

        await Assert.ThrowsAsync<SecureCredentialAmbiguousException>(
            async () => await resolver.ResolveAsync(TestContext.Current.CancellationToken));
    }

    private sealed class TestSecureOptions : ISecureOptions
    {
        public string? SecretKeyName { get; set; }

        public string? ApiKey { get; set; }
    }

    private sealed class StubSecureCredentialProvider : ISecureCredentialProvider
    {
        private readonly string _secret;

        public StubSecureCredentialProvider(string secret)
        {
            _secret = secret;
        }

        public ValueTask<string> GetSecretAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_secret);
        }
    }
}
