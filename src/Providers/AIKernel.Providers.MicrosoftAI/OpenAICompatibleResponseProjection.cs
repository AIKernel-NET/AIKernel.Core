namespace AIKernel.Providers.MicrosoftAI;

using System.Collections.Immutable;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection']" />
public sealed record OpenAICompatibleResponseProjection
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ModelId']" />
    public required string ModelId { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.RawResponse']" />
    public required string RawResponse { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.PrimaryText']" />
    public required string PrimaryText { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.IsTruncated']" />
    public required bool IsTruncated { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.ObservedAtUtc']" />
    public required DateTimeOffset ObservedAtUtc { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleResponseProjection.string']" />
    public required ImmutableDictionary<string, string> Metadata { get; init; }
}
