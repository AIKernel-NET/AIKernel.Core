namespace AIKernel.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>[EN] Documents this public package API member. [JA] AIKernelCoreBuilder を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.AIKernelCoreBuilder']/summary" />
public sealed class AIKernelCoreBuilder
{
    /// <summary>[EN] Documents this public package API member. [JA] AIKernelCoreBuilder を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.AIKernelCoreBuilder.#ctor']/summary" />
    public AIKernelCoreBuilder(
        IServiceCollection services,
        IConfiguration? configuration = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration;
    }

    /// <summary>[EN] Documents this public package API member. [JA] Services を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Services']/summary" />
    public IServiceCollection Services { get; }

    /// <summary>[EN] Documents this public package API member. [JA] Configuration を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Hosting.AIKernelCoreBuilder.Configuration']/summary" />
    public IConfiguration? Configuration { get; }
}