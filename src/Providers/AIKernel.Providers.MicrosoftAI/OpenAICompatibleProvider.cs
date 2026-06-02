namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Providers;
using AIKernel.Core.Time;
using AIKernel.Dtos.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;

public sealed class OpenAICompatibleProvider : IModelProvider
{
    private static readonly Action<ILogger, string, string?, Exception?> LogCompleted =
        LoggerMessage.Define<string, string?>(
            LogLevel.Information,
            new EventId(2001, nameof(LogCompleted)),
            "OpenAICompatibleProvider completed. ProviderId={ProviderId}, ModelId={ModelId}");

    private static readonly Action<ILogger, string, string?, bool, Exception?> LogCompletedDebug =
        LoggerMessage.Define<string, string?, bool>(
            LogLevel.Debug,
            new EventId(2002, nameof(LogCompletedDebug)),
            "OpenAICompatibleProvider debug completion. ProviderId={ProviderId}, ModelId={ModelId}, IsTruncated={IsTruncated}");

    private static readonly Action<ILogger, string, string, Exception?> LogProviderFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2003, nameof(LogProviderFailed)),
            "OpenAICompatibleProvider failed. ProviderId={ProviderId}, ErrorCode={ErrorCode}");

    private readonly IChatClient _chatClient;
    private readonly IProviderCapabilities _capabilities;
    private readonly IOpenAICompatibleResponseMapper _responseMapper;
    private readonly OpenAICompatibleProviderOptions _options;
    private readonly ILogger<OpenAICompatibleProvider> _logger;
    private readonly IKernelClock _clock;

    private volatile bool _isInitialized;

    public OpenAICompatibleProvider(
        IChatClient chatClient,
        IProviderCapabilities capabilities,
        IOpenAICompatibleResponseMapper responseMapper,
        IOptions<OpenAICompatibleProviderOptions> options,
        ILogger<OpenAICompatibleProvider> logger,
        IKernelClock? clock = null)
        : this(
            chatClient,
            capabilities,
            responseMapper,
            options?.Value ?? throw new ArgumentNullException(nameof(options)),
            logger,
            clock)
    {
    }

    public OpenAICompatibleProvider(
        IChatClient chatClient,
        IProviderCapabilities capabilities,
        IOpenAICompatibleResponseMapper responseMapper,
        OpenAICompatibleProviderOptions options,
        ILogger<OpenAICompatibleProvider> logger,
        IKernelClock? clock = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _responseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? KernelClock.System();

        ProviderId = RequireNonEmpty(_options.ProviderId, nameof(_options.ProviderId));
        Name = RequireNonEmpty(_options.Name, nameof(_options.Name));
        Version = RequireNonEmpty(_options.Version, nameof(_options.Version));
    }

    public string ProviderId { get; }

    public string Name { get; }

    public string Version { get; }

    public IProviderCapabilities GetCapabilities()
    {
        return _capabilities;
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(_isInitialized);
    }

    public Task InitializeAsync()
    {
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _isInitialized = false;
        return Task.CompletedTask;
    }

    public async Task<ProviderHealthStatus> GetHealthAsync()
    {
        ProviderHealthStatus healthStatus;

        try
        {
            var factory = _options.HealthStatusFactory;

            if (factory is null)
            {
                var exception = new InvalidOperationException(
                    "OpenAICompatibleProviderOptions.HealthStatusFactory is not configured.");

                // Fail-Closed:
                // ヘルス状態を生成する方法が未定義なら、状態不明のまま上位に制御を渡さない。
                // default(ProviderHealthStatus) は「正常・異常・未初期化」の区別ができず、
                // Kernel / Router が誤った判断をする可能性があるため禁止する。
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    LogProviderFailed(
                        _logger,
                        ProviderId,
                        "provider_health_status_factory_missing",
                        exception);
                }

                throw new ProviderApiException(
                    "Provider health status factory is not configured.",
                    exception);
            }

            var context = new OpenAICompatibleProviderHealthContext
            {
                ProviderId = ProviderId,
                Name = Name,
                Version = Version,
                ModelId = _options.ModelId,
                IsInitialized = _isInitialized,
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
            // ProviderHealthStatus の生成失敗は、推論ロジックの失敗ではなく、
            // Provider 基盤そのものの状態定義不全として扱う。
            // そのため ProviderApiException に包み、Fail-Closed で停止させる。
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    ProviderId,
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

            // Explicit Null Check:
            // ProviderHealthStatus が null ということは、Provider の健全性を判定できない状態。
            // 不確定な状態で上位レイヤーへ制御を渡すと、
            // Router / Kernel が「利用可能」と誤判定する可能性がある。
            //
            // 将来的に ProviderHealthStatus が struct / enum になった場合、
            // この null チェックは常に false になるが、default 値を生成しない設計は維持される。
            // つまり「null を拒否する」だけでなく、
            // 「そもそも default に依存しない」ことがこの実装の本質である。
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(
                    _logger,
                    ProviderId,
                    "provider_health_status_null",
                    exception);
            }

            throw new ProviderApiException(
                "Provider health status was null.",
                exception);
        }

        // 非同期 API としての一貫性を保つため Task.FromResult を使うが、
        // await + ConfigureAwait(false) に揃え、呼び出し側の SynchronizationContext に依存しない。
        return await Task.FromResult(healthStatus)
            .ConfigureAwait(false);
    }

    public async Task<string> GenerateAsync(
        IReadOnlyList<IModelMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (!_isInitialized)
        {
            throw new ProviderApiException(
                "Provider has not been initialized.",
                new InvalidOperationException("InitializeAsync must be called before GenerateAsync."));
        }

        ValidateMessages(messages);

        var chatMessages = ConvertMessages(messages);
        var chatOptions = CreateChatOptions();

        try
        {
            var response = await _chatClient
    .GetResponseAsync(chatMessages, chatOptions, cancellationToken)
    .ConfigureAwait(false);

            // 通常実行で必要なのは PrimaryText のみ。
            // RawResponse / Metadata / Usage 展開は Debug ログが有効な場合だけ作る。
            var primaryText = _responseMapper.GetPrimaryText(response);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogCompleted(
                    _logger,
                    ProviderId,
                    response.ModelId ?? _options.ModelId,
                    null);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var projection = _responseMapper.CreateProjection(
                    response,
                    _options.ModelId,
                    _clock.Now);

                LogCompletedDebug(
                    _logger,
                    ProviderId,
                    projection.ModelId,
                    projection.IsTruncated,
                    null);
            }

            return primaryText;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(_logger, ProviderId, "provider_timeout", ex);
            }

            throw new ProviderExecutionTimeoutException(
                "OpenAI-compatible provider timed out.",
                ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(_logger, ProviderId, "rate_limited", ex);
            }

            throw new ProviderRateLimitException(
                "OpenAI-compatible endpoint returned a rate limit response.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(_logger, ProviderId, "provider_api_error", ex);
            }

            throw new ProviderApiException(
                $"OpenAI-compatible endpoint request failed. StatusCode={ex.StatusCode?.ToString() ?? "unknown"}.",
                ex);
        }
        catch (ProviderExecutionException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(_logger, ProviderId, ex.ErrorCode, ex);
            }

            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogProviderFailed(_logger, ProviderId, "provider_api_error", ex);
            }

            throw new ProviderApiException(
                "OpenAI-compatible provider execution failed.",
                ex);
        }
    }

    public async Task StreamGenerateAsync(
        IReadOnlyList<IModelMessage> messages,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onChunk);

        if (!_options.SupportsStreaming)
        {
            throw new ProviderCapabilityMismatchException(
                "Streaming is not enabled for this provider configuration.");
        }

        var text = await GenerateAsync(messages, cancellationToken)
            .ConfigureAwait(false);

        await onChunk(text).ConfigureAwait(false);
    }

    public Task<string> AnswerAsync(
        string question,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required.", nameof(question));
        }

        var messages = new List<IModelMessage>();

        if (!string.IsNullOrWhiteSpace(context))
        {
            messages.Add(new OpenAICompatibleModelMessage("system", context));
        }

        messages.Add(new OpenAICompatibleModelMessage("user", question));

        return GenerateAsync(messages, cancellationToken);
    }

    private static bool IsNullHealthStatus(
        [NotNullWhen(false)] ProviderHealthStatus? healthStatus)
    {
        return healthStatus is null;
    }

    private void ValidateMessages(IReadOnlyList<IModelMessage> messages)
    {
        if (messages.Count == 0)
        {
            throw new ProviderCapabilityMismatchException(
                "At least one model message is required.");
        }

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Role))
            {
                throw new ProviderCapabilityMismatchException(
                    "Message role is required.");
            }

            if (message.Content is null)
            {
                throw new ProviderCapabilityMismatchException(
                    "Message content must not be null.");
            }

            var role = message.Role.Trim().ToLowerInvariant();

            if (role == "system" && !_options.SupportsSystemRole)
            {
                throw new ProviderCapabilityMismatchException(
                    "The current provider configuration does not support system role messages.");
            }

            if (role == "assistant" && !_options.SupportsAssistantRole)
            {
                throw new ProviderCapabilityMismatchException(
                    "The current provider configuration does not support assistant role messages.");
            }

            if (role == "tool" && !_options.SupportsToolRole)
            {
                throw new ProviderCapabilityMismatchException(
                    "The current provider configuration does not support tool role messages.");
            }
        }
    }

    private static ChatMessage[] ConvertMessages(
        IReadOnlyList<IModelMessage> messages)
    {
        var chatMessages = new ChatMessage[messages.Count];

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];

            chatMessages[index] = new ChatMessage(
                ConvertRole(message.Role),
                message.Content);
        }

        return chatMessages;
    }

    private static ChatRole ConvertRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "system" => ChatRole.System,
            "user" => ChatRole.User,
            "assistant" => ChatRole.Assistant,
            "tool" => ChatRole.Tool,
            _ => throw new ProviderCapabilityMismatchException(
                $"Unsupported model message role: {role}.")
        };
    }

    private ChatOptions CreateChatOptions()
    {
        return new ChatOptions
        {
            ModelId = string.IsNullOrWhiteSpace(_options.ModelId) ? null : _options.ModelId,
            MaxOutputTokens = _options.MaxOutputTokens,
            Temperature = _options.Temperature,
            TopP = _options.TopP,
            StopSequences = _options.StopSequences.Count == 0
                ? null
                : _options.StopSequences.ToList()
        };
    }

    private static string RequireNonEmpty(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return value;
    }

    private sealed record OpenAICompatibleModelMessage(
        string Role,
        string Content) : IModelMessage;
}
