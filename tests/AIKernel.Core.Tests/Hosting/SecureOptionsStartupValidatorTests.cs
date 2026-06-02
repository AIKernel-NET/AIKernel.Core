using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Tests.Hosting;

using AIKernel.Abstractions.Hosting;
using AIKernel.Hosting;
using Xunit;

public sealed class SecureHostingDependencyTests
{
    [Fact]
    public void AIKernelHosting_DoesNotReferenceMicrosoftAIProvider()
    {
        var hostingAssembly = typeof(SecureHostingExtensions).Assembly;

        var referencedAssemblies = hostingAssembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(
            "AIKernel.Providers.MicrosoftAI",
            referencedAssemblies);
    }

    [Fact]
    public void AIKernelHosting_DoesNotReferenceKernelFacade()
    {
        var hostingAssembly = typeof(SecureHostingExtensions).Assembly;

        var referencedAssemblies = hostingAssembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(
            "AIKernel.Kernel",
            referencedAssemblies);
    }

    [Fact]
    public void AIKernelHosting_DoesNotContainOpenAICompatibleCredentialCache()
    {
        var hostingAssembly = typeof(SecureHostingExtensions).Assembly;

        var typeNames = hostingAssembly
            .GetTypes()
            .Select(x => x.FullName)
            .ToArray();

        Assert.DoesNotContain(
            "AIKernel.Hosting.OpenAICompatibleCredentialCache",
            typeNames);

        Assert.DoesNotContain(
            "AIKernel.Hosting.OpenAICompatibleCredentialResolver",
            typeNames);

        Assert.DoesNotContain(
            "AIKernel.Hosting.OpenAICompatibleProviderStartupValidator",
            typeNames);
    }
}
