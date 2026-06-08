namespace AIKernel.Hosting;

using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using Microsoft.Extensions.DependencyInjection;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.ModelProviderHostingExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.ModelProviderHostingExtensions']" />
public static class ModelProviderHostingExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        ModelPromptCapability capability)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        var validated = ValidateCapability(capability);

        AddProviderRegistration<TProvider>(
            builder.Services,
            serviceProvider => ActivatorUtilities.CreateInstance<TProvider>(
                serviceProvider));
        builder.Services.AddSingleton(validated);

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, ModelPromptCapability> capabilityFactory)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(capabilityFactory);

        AddProviderRegistration<TProvider>(
            builder.Services,
            serviceProvider => ActivatorUtilities.CreateInstance<TProvider>(
                serviceProvider));
        builder.Services.AddSingleton(serviceProvider =>
            ValidateCapability(capabilityFactory(serviceProvider)));

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        IEnumerable<ModelPromptCapability> capabilities)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(capabilities);

        var snapshot = capabilities
            .Select(ValidateCapability)
            .ToArray();

        AddProviderRegistration<TProvider>(
            builder.Services,
            serviceProvider => ActivatorUtilities.CreateInstance<TProvider>(
                serviceProvider));
        foreach (var capability in snapshot)
        {
            builder.Services.AddSingleton(capability);
        }

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, TProvider> providerFactory,
        ModelPromptCapability capability)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);

        var validated = ValidateCapability(capability);

        AddProviderRegistration(builder.Services, providerFactory);
        builder.Services.AddSingleton(validated);

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, TProvider> providerFactory,
        Func<IServiceProvider, ModelPromptCapability> capabilityFactory)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(capabilityFactory);

        AddProviderRegistration(builder.Services, providerFactory);
        builder.Services.AddSingleton(serviceProvider =>
            ValidateCapability(capabilityFactory(serviceProvider)));

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.WithModelProvider&lt;TProvider&gt;']" />
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, TProvider> providerFactory,
        IEnumerable<ModelPromptCapability> capabilities)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(capabilities);

        var snapshot = capabilities
            .Select(ValidateCapability)
            .ToArray();

        AddProviderRegistration(builder.Services, providerFactory);
        foreach (var capability in snapshot)
        {
            builder.Services.AddSingleton(capability);
        }

        return builder;
    }

    private static ModelPromptCapability ValidateCapability(
        ModelPromptCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (string.IsNullOrWhiteSpace(capability.ProviderId))
        {
            throw new ArgumentException(
                "ModelPromptCapability.ProviderId is required.",
                nameof(capability));
        }

        if (string.IsNullOrWhiteSpace(capability.ModelId))
        {
            throw new ArgumentException(
                "ModelPromptCapability.ModelId is required.",
                nameof(capability));
        }

        return capability;
    }

    private static void AddProviderRegistration<TProvider>(
        IServiceCollection services,
        Func<IServiceProvider, TProvider> providerFactory)
        where TProvider : class, IModelProvider
    {
        var registration = new ProviderRegistration<TProvider>(providerFactory);

        services.AddSingleton<IModelProvider>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProvider>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProviderIdentity>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProviderCapabilitySource>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProviderAvailabilityProbe>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProviderLifecycle>(
            serviceProvider => registration.Get(serviceProvider));
        services.AddSingleton<IProviderHealthProbe>(
            serviceProvider => registration.Get(serviceProvider));
    }

    private sealed class ProviderRegistration<TProvider>(
        Func<IServiceProvider, TProvider> providerFactory)
        where TProvider : class, IModelProvider
    {
        private readonly Lock _gate = new();
        private TProvider? _instance;

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.Get']" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.ModelProviderHostingExtensions.Get']" />
        public TProvider Get(
            IServiceProvider serviceProvider)
        {
            if (_instance is not null)
            {
                return _instance;
            }

            lock (_gate)
            {
                return _instance ??= providerFactory(serviceProvider);
            }
        }
    }
}
