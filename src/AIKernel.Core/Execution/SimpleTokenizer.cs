namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Tokenization;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SimpleTokenizer']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.SimpleTokenizer']" />
public sealed class SimpleTokenizer : ITokenizer
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Execution.SimpleTokenizer.TokenizerProfileId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Execution.SimpleTokenizer.TokenizerProfileId']" />
    public string TokenizerProfileId => "aikernel.simple";

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Execution.SimpleTokenizer.Name']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Execution.SimpleTokenizer.Name']" />
    public string Name => "AIKernel Simple Tokenizer";

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.Tokenize']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.Tokenize']" />
    public IReadOnlyList<Token> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tokens = new List<Token>();
        var index = 0;

        foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var start = text.IndexOf(part, index, StringComparison.Ordinal);
            var end = start + part.Length;
            index = end;

            tokens.Add(new Token
            {
                Value = part,
                TokenId = StableTokenId(part),
                StartPosition = start,
                EndPosition = end,
                TokenType = "word"
            });
        }

        return tokens;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.CountTokens']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.CountTokens']" />
    public int CountTokens(string text)
    {
        return Tokenize(text).Count;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.Decode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.Decode']" />
    public string Decode(IReadOnlyList<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        return string.Join(" ", tokens.Select(x => x.Value));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetStatistics']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetStatistics']" />
    public TokenizerStatistics GetStatistics()
    {
        return new TokenizerStatistics
        {
            VocabularySize = 0,
            SupportedModels = ["*"],
            Version = "1",
            AverageTokenLength = 0,
            MaxTokenLength = 0
        };
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.SupportsModel']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.SupportsModel']" />
    public bool SupportsModel(string modelName)
    {
        return !string.IsNullOrWhiteSpace(modelName);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetPhysicalCardinality']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetPhysicalCardinality']" />
    public int GetPhysicalCardinality(int logicalTokenCount, string deviceType)
    {
        return Math.Max(0, logicalTokenCount);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetPaddingInfo']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.SimpleTokenizer.GetPaddingInfo']" />
    public PaddingInfo GetPaddingInfo(int logicalTokenCount, int physicalCardinality)
    {
        return new PaddingInfo
        {
            LogicalTokenCount = logicalTokenCount,
            PhysicalCardinality = physicalCardinality,
            PaddingAmount = Math.Max(0, physicalCardinality - logicalTokenCount),
            PaddingPercentage = logicalTokenCount <= 0
                ? 0
                : (float)Math.Max(0, physicalCardinality - logicalTokenCount) / logicalTokenCount,
            PaddingMethod = "none",
            Rationale = "Simple tokenizer does not apply padding."
        };
    }

    private static int StableTokenId(string value)
    {
        return StringComparer.Ordinal.GetHashCode(value);
    }
}
