namespace AIKernel.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']" />
public sealed class AIKernelCoreBuilder
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']" />
    public AIKernelCoreBuilder(
        IServiceCollection services,
        IConfiguration? configuration = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']" />
    public IServiceCollection Services { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']" />
    public IConfiguration? Configuration { get; }
}