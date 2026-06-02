namespace AIKernel.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class AIKernelCoreBuilder
{
    public AIKernelCoreBuilder(
        IServiceCollection services,
        IConfiguration? configuration = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration;
    }

    public IServiceCollection Services { get; }

    public IConfiguration? Configuration { get; }
}