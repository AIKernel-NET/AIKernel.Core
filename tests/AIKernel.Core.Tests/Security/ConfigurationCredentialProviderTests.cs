using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Tests.Security;

using AIKernel.Core.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class ConfigurationCredentialProviderTests
{
    [Fact]
    public async Task GetSecretAsync_ReturnsSecret_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = "sk-config-123456"
                })
            .Build();

        var provider = new ConfigurationCredentialProvider(configuration);

        var secret = await provider.GetSecretAsync(
            "OpenAI:ApiKey",
            TestContext.Current.CancellationToken);

        Assert.Equal("sk-config-123456", secret);
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenConfigurationValueIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();

        var provider = new ConfigurationCredentialProvider(configuration);

        await Assert.ThrowsAsync<SecureCredentialNotFoundException>(
            async () => await provider.GetSecretAsync(
                "OpenAI:ApiKey",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenConfigurationValueIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenAI:ApiKey"] = " invalid-secret "
                })
            .Build();

        var provider = new ConfigurationCredentialProvider(configuration);

        await Assert.ThrowsAsync<SecureCredentialInvalidException>(
            async () => await provider.GetSecretAsync(
                "OpenAI:ApiKey",
                TestContext.Current.CancellationToken));
    }
}
