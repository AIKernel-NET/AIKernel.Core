namespace AIKernel.Providers.MicrosoftAI;

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.AI;

public sealed class OpenAICompatibleResponseMapper : IOpenAICompatibleResponseMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public string GetPrimaryText(ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new ProviderInvalidResponseException(
                "The model response did not contain primary text.");
        }

        return response.Text;
    }

    public OpenAICompatibleResponseProjection CreateProjection(
        ChatResponse response,
        string fallbackModelId,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        // 通常パスでは GetPrimaryText だけを呼び出す。
        // RawResponse の JSON 化や Metadata 構築は Debug などで明示的に必要な場合のみ行う。
        var primaryText = GetPrimaryText(response);
        var rawResponse = SerializeRawResponse(response);

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new ProviderInvalidResponseException(
                "The model response could not be serialized as a raw response.");
        }

        var finishReason = response.FinishReason?.ToString() ?? string.Empty;

        var metadata = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        metadata["finish_reason"] = finishReason;
        metadata["model_id"] = response.ModelId ?? fallbackModelId;
        metadata["observed_at_utc"] = observedAtUtc.ToString("O");

        if (!string.IsNullOrWhiteSpace(response.ResponseId))
        {
            metadata["response_id"] = response.ResponseId!;
        }

        if (!string.IsNullOrWhiteSpace(response.ConversationId))
        {
            metadata["conversation_id"] = response.ConversationId!;
        }

        if (response.Usage is not null)
        {
            metadata["usage"] = JsonSerializer.Serialize(response.Usage, JsonOptions);
        }

        if (response.AdditionalProperties is not null)
        {
            foreach (var pair in response.AdditionalProperties)
            {
                metadata[$"meai.additional.{pair.Key}"] =
                    Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture)
                    ?? string.Empty;
            }
        }

        return new OpenAICompatibleResponseProjection
        {
            ModelId = response.ModelId ?? fallbackModelId,
            RawResponse = rawResponse,
            PrimaryText = primaryText,
            IsTruncated = IsTruncated(finishReason),
            ObservedAtUtc = observedAtUtc,
            Metadata = metadata.ToImmutable()
        };
    }

    private static string SerializeRawResponse(ChatResponse response)
    {
        if (response.RawRepresentation is not null)
        {
            try
            {
                return JsonSerializer.Serialize(
                    response.RawRepresentation,
                    response.RawRepresentation.GetType(),
                    JsonOptions);
            }
            catch (Exception ex)
            {
                throw new ProviderInvalidResponseException(
                    "ChatResponse.RawRepresentation could not be serialized.",
                    ex);
            }
        }

        var fallback = new
        {
            response.ModelId,
            response.ResponseId,
            response.ConversationId,
            response.CreatedAt,
            FinishReason = response.FinishReason?.ToString(),
            response.Text,
            response.Usage,
            response.AdditionalProperties
        };

        return JsonSerializer.Serialize(fallback, JsonOptions);
    }

    private static bool IsTruncated(string finishReason)
    {
        return finishReason.Equals("length", StringComparison.OrdinalIgnoreCase)
            || finishReason.Equals("max_tokens", StringComparison.OrdinalIgnoreCase)
            || finishReason.Contains("length", StringComparison.OrdinalIgnoreCase);
    }
}
