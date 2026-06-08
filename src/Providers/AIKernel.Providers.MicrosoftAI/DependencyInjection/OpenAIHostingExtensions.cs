namespace AIKernel.Providers.MicrosoftAI.DependencyInjection;

using System.Collections.Immutable;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions']" />
public static class OpenAIHostingExtensions
{
    private const string UserRole = "user";
    private const string SystemRole = "system";
    private const string AssistantRole = "assistant";
    private const string ToolRole = "tool";

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions.WithOpenAI&lt;TBuilder&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions.WithOpenAI&lt;TBuilder&gt;']" />
    public static TBuilder WithOpenAI<TBuilder>(
        this TBuilder builder,
        Action<OpenAICompatibleProviderOptions> configure,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
        where TBuilder : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        var services = GetServices(builder);

        services
            .AddOptions<OpenAICompatibleProviderOptions>()
            .Configure(configure);

        RegisterOpenAIProvider(services, chatClientFactory);

        return builder;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions.WithOpenAI&lt;TBuilder&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.DependencyInjection.OpenAIHostingExtensions.WithOpenAI&lt;TBuilder&gt;']" />
    public static TBuilder WithOpenAI<TBuilder>(
        this TBuilder builder,
        IConfiguration configurationSection,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
        where TBuilder : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurationSection);
        ArgumentNullException.ThrowIfNull(chatClientFactory);

        var services = GetServices(builder);

        services
            .AddOptions<OpenAICompatibleProviderOptions>()
            .Bind(configurationSection);

        RegisterOpenAIProvider(services, chatClientFactory);

        return builder;
    }

    private static void RegisterOpenAIProvider(
        IServiceCollection services,
        Func<IServiceProvider, OpenAICompatibleProviderOptions, IChatClient> chatClientFactory)
    {
        services.AddLogging();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<OpenAICompatibleProviderOptions>,
                OpenAICompatibleProviderOptionsValidator>());

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IHostedService,
                OpenAICompatibleProviderStartupValidator>());

        services.TryAddSingleton<IOpenAICompatibleResponseMapper, OpenAICompatibleResponseMapper>();
        services.TryAddSingleton<IProviderCapabilities, OpenAICompatibleProviderCapabilities>();

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<OpenAICompatibleProviderOptions>>()
                .Value;

            EnsureApiKeyResolved(options);

            return chatClientFactory(serviceProvider, options);
        });

        services.AddSingleton(CreateModelPromptCapability);
        services.AddSingleton<OpenAICompatibleProvider>();
        services.AddSingleton<IModelProvider>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProvider>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProviderIdentity>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProviderCapabilitySource>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProviderAvailabilityProbe>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProviderLifecycle>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IProviderHealthProbe>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<ITextGenerationProvider>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IStreamingGenerationProvider>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
        services.AddSingleton<IQuestionAnsweringProvider>(
            serviceProvider => serviceProvider.GetRequiredService<OpenAICompatibleProvider>());
    }

    private static ModelPromptCapability CreateModelPromptCapability(
        IServiceProvider serviceProvider)
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<OpenAICompatibleProviderOptions>>()
            .Value;

        var supportedRoles = new List<string>
        {
            UserRole
        };

        if (options.SupportsSystemRole)
        {
            supportedRoles.Add(SystemRole);
        }

        if (options.SupportsAssistantRole)
        {
            supportedRoles.Add(AssistantRole);
        }

        if (options.SupportsToolRole)
        {
            supportedRoles.Add(ToolRole);
        }

        return new ModelPromptCapability
        {
            ProviderId = options.ProviderId,
            ModelId = options.ModelId,
            MessageFormat = PromptMessageFormat.ChatMessages,
            MaxInputTokens = options.MaxInputTokens,
            MaxOutputTokens = options.MaxOutputTokens ?? 1024,
            SupportedRoles = supportedRoles.ToImmutableArray(),
            SystemInstructionRole = options.SupportsSystemRole
                ? SystemRole
                : UserRole
        };
    }

    private static IServiceCollection GetServices<TBuilder>(
        TBuilder builder)
        where TBuilder : notnull
    {
        var servicesProperty = builder
            .GetType()
            .GetProperty("Services", typeof(IServiceCollection));

        if (servicesProperty?.GetValue(builder) is IServiceCollection services)
        {
            return services;
        }

        var builderType = builder.GetType().FullName ?? builder.GetType().Name;

        throw new InvalidOperationException(
            $"Builder type '{builderType}' does not expose an IServiceCollection Services property.");
    }

    private static void EnsureApiKeyResolved(
        OpenAICompatibleProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAICompatibleProviderOptions.ApiKey has not been resolved. Ensure host startup has completed and SecretKeyName is valid.");
        }
    }
}
