namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Core;

public sealed record OpenAICompatibleProviderOptions : ISecureOptions
{
    public string ProviderId { get; init; } = "openai-compatible";

    public string Name { get; init; } = "OpenAI Compatible Provider";

    public string Version { get; init; } = "0.0.4";

    public string ModelId { get; init; } = "";

    public Uri? Endpoint { get; init; }

    public int MaxInputTokens { get; init; } = 8192;

    public int? MaxOutputTokens { get; init; }

    public float? Temperature { get; init; }

    public float? TopP { get; init; }

    public bool SupportsSystemRole { get; init; } = true;

    public bool SupportsAssistantRole { get; init; } = true;

    public bool SupportsToolRole { get; init; }

    public bool SupportsStreaming { get; init; }

    public IReadOnlyList<string> StopSequences { get; init; } = [];

    public string? SecretKeyName { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>
    /// ProviderHealthStatus を明示的に生成する factory です。
    ///
    /// default(ProviderHealthStatus) を返す実装は、ヘルス状態が未定義のまま
    /// 上位レイヤーへ伝播するため禁止します。
    ///
    /// ProviderHealthStatus の具体形が class / record / enum / struct のいずれに変わっても、
    /// Provider 本体は default に依存せず、この factory を通じて明示的に生成された値だけを返します。
    /// </summary>
    public Func<OpenAICompatibleProviderHealthContext, ProviderHealthStatus>? HealthStatusFactory { get; init; }

    public override string ToString()
    {
        return
            $"OpenAICompatibleProviderOptions {{ ProviderId = {ProviderId}, Name = {Name}, Version = {Version}, ModelId = {ModelId}, SecretKeyName = {SecretKeyName}, ApiKey = ***REDACTED*** }}";
    }
}
