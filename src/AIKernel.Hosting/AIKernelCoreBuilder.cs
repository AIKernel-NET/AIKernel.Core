namespace AIKernel.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']/summary" />
public sealed class AIKernelCoreBuilder
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']/summary" />
    public AIKernelCoreBuilder(
        IServiceCollection services,
        IConfiguration? configuration = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']/summary" />
    public IServiceCollection Services { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']/summary" />
    public IConfiguration? Configuration { get; }
}