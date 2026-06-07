namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Abstractions.Providers;
using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Core;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions']" />
public sealed record OpenAICompatibleProviderOptions : ISecureOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderId']" />
    public string ProviderId { get; init; } = "openai-compatible";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Name']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Name']" />
    public string Name { get; init; } = "OpenAI Compatible Provider";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Version']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Version']" />
    public string Version { get; init; } = "0.1.0";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ModelId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ModelId']" />
    public string ModelId { get; init; } = "";

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Endpoint']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Endpoint']" />
    public Uri? Endpoint { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxInputTokens']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxInputTokens']" />
    public int MaxInputTokens { get; init; } = 8192;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxOutputTokens']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.MaxOutputTokens']" />
    public int? MaxOutputTokens { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Temperature']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.Temperature']" />
    public float? Temperature { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.TopP']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.TopP']" />
    public float? TopP { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsSystemRole']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsSystemRole']" />
    public bool SupportsSystemRole { get; init; } = true;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsAssistantRole']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsAssistantRole']" />
    public bool SupportsAssistantRole { get; init; } = true;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsToolRole']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsToolRole']" />
    public bool SupportsToolRole { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsStreaming']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SupportsStreaming']" />
    public bool SupportsStreaming { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.StopSequences']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.StopSequences']" />
    public IReadOnlyList<string> StopSequences { get; init; } = [];

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SecretKeyName']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.SecretKeyName']" />
    public string? SecretKeyName { get; set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ApiKey']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ApiKey']" />
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
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderHealthStatus']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ProviderHealthStatus']" />
    public Func<OpenAICompatibleProviderHealthContext, ProviderHealthStatus>? HealthStatusFactory { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptions.ToString']" />
    public override string ToString()
    {
        return
            $"OpenAICompatibleProviderOptions {{ ProviderId = {ProviderId}, Name = {Name}, Version = {Version}, ModelId = {ModelId}, SecretKeyName = {SecretKeyName}, ApiKey = ***REDACTED*** }}";
    }
}
