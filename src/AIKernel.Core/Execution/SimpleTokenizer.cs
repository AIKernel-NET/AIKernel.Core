namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Tokenization;

public sealed class SimpleTokenizer : ITokenizer
{
    public string TokenizerProfileId => "aikernel.simple";

    public string Name => "AIKernel Simple Tokenizer";

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

    public int CountTokens(string text)
    {
        return Tokenize(text).Count;
    }

    public string Decode(IReadOnlyList<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        return string.Join(" ", tokens.Select(x => x.Value));
    }

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

    public bool SupportsModel(string modelName)
    {
        return !string.IsNullOrWhiteSpace(modelName);
    }

    public int GetPhysicalCardinality(int logicalTokenCount, string deviceType)
    {
        return Math.Max(0, logicalTokenCount);
    }

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
