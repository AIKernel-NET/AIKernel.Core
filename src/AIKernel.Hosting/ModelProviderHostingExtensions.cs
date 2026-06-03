namespace AIKernel.Hosting;

using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using Microsoft.Extensions.DependencyInjection;

public static class ModelProviderHostingExtensions
{
    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        ModelPromptCapability capability)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        var validated = ValidateCapability(capability);

        builder.Services.AddSingleton<IModelProvider, TProvider>();
        builder.Services.AddSingleton(validated);

        return builder;
    }

    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, ModelPromptCapability> capabilityFactory)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(capabilityFactory);

        builder.Services.AddSingleton<IModelProvider, TProvider>();
        builder.Services.AddSingleton(serviceProvider =>
            ValidateCapability(capabilityFactory(serviceProvider)));

        return builder;
    }

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

        builder.Services.AddSingleton<IModelProvider, TProvider>();
        foreach (var capability in snapshot)
        {
            builder.Services.AddSingleton(capability);
        }

        return builder;
    }

    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, TProvider> providerFactory,
        ModelPromptCapability capability)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);

        var validated = ValidateCapability(capability);

        builder.Services.AddSingleton<IModelProvider>(providerFactory);
        builder.Services.AddSingleton(validated);

        return builder;
    }

    public static AIKernelCoreBuilder WithModelProvider<TProvider>(
        this AIKernelCoreBuilder builder,
        Func<IServiceProvider, TProvider> providerFactory,
        Func<IServiceProvider, ModelPromptCapability> capabilityFactory)
        where TProvider : class, IModelProvider
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(capabilityFactory);

        builder.Services.AddSingleton<IModelProvider>(providerFactory);
        builder.Services.AddSingleton(serviceProvider =>
            ValidateCapability(capabilityFactory(serviceProvider)));

        return builder;
    }

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

        builder.Services.AddSingleton<IModelProvider>(providerFactory);
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
}
