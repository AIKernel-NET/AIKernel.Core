namespace AIKernel.Providers.MicrosoftAI;

using System.Diagnostics.CodeAnalysis;
using AIKernel.Core.Time;
using AIKernel.Dtos.Core;
using Microsoft.Extensions.Logging;

internal sealed class OpenAICompatibleProviderHealthCheck
{
    private static readonly Action<ILogger, string, string, Exception?> LogProviderFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2101, nameof(LogProviderFailed)),
            "OpenAICompatibleProvider health check failed. ProviderId={ProviderId}, ErrorCode={ErrorCode}");

    private readonly OpenAICompatibleProviderOptions _options;
    private readonly ILogger _logger;
    private readonly IKernelClock _clock;

    public OpenAICompatibleProviderHealthCheck(
        OpenAICompatibleProviderOptions options,
        ILogger logger,
        IKernelClock clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ProviderHealthStatus> GetHealthAsync(
        string providerId,
        string name,
        string version,
        bool isInitialized)
    {
        ProviderHealthStatus healthStatus;

        try
        {
            var factory = _options.HealthStatusFactory;

            if (factory is null)
            {
                var exception = new InvalidOperationException(
                    "OpenAICompatibleProviderOptions.HealthStatusFactory is not configured.");

                if (_logger.IsEnabled(LogLevel.Error))
                {
                    LogProviderFailed(
                        _logger,
                        providerId,
                        "provider_health_status_factory_missing",
                        exception);
                }

                throw new ProviderApiException(
                    "Provider health status factory is not configured.",
                    exception);
            }

            var context = new OpenAICompatibleProviderHealthContext
            {
                ProviderId = providerId,
                Name = name,
                Version = version,
                ModelId = _options.ModelId,
                IsInitialized = isInitialized,
                CheckedAtUtc = _clock.Now
            };

            healthStatus = factory(context);
        }
        catch (ProviderApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    providerId,
                    "provider_health_status_creation_failed",
                    ex);
            }

            throw new ProviderApiException(
                "Provider health status could not be created.",
                ex);
        }

        if (IsNullHealthStatus(healthStatus))
        {
            var exception = new InvalidOperationException(
                "Provider health status factory returned null.");

            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    providerId,
                    "provider_health_status_null",
                    exception);
            }

            throw new ProviderApiException(
                "Provider health status was null.",
                exception);
        }

        return await Task.FromResult(healthStatus)
            .ConfigureAwait(false);
    }

    private static bool IsNullHealthStatus(
        [NotNullWhen(false)] ProviderHealthStatus? healthStatus)
    {
        return healthStatus is null;
    }
}
