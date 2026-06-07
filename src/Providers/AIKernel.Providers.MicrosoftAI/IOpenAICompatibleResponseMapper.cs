namespace AIKernel.Providers.MicrosoftAI;

using Microsoft.Extensions.AI;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.IOpenAICompatibleResponseMapper']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.IOpenAICompatibleResponseMapper']" />
public interface IOpenAICompatibleResponseMapper
{
    /// <summary>Executes the GetPrimaryText operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで GetPrimaryText 操作を実行します。</summary>
    string GetPrimaryText(ChatResponse response);

    /// <summary>Executes the CreateProjection operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで CreateProjection 操作を実行します。</summary>
    OpenAICompatibleResponseProjection CreateProjection(
        ChatResponse response,
        string fallbackModelId,
        DateTimeOffset observedAtUtc);
}
