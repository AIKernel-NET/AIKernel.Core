using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace AIKernel.Kernel.Inference;

// A witness about strings in two input records. It neither establishes the truth of a
// record nor interprets physical units, grants permission, or produces a council vote.
internal sealed class BonsaiCtgLiteralComparison
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking;
    private static readonly Regex Assignment = new(
        @"(?<field>[A-Za-z_][A-Za-z0-9_]*(?:[.\-][A-Za-z0-9_]+)*)[ \t]*=[ \t]*(?<value>[A-Za-z0-9_]+(?:[.\-][A-Za-z0-9_]+)*)", Options);
    private static readonly Regex Quantity = new(
        @"(?<value>[+\-]?[0-9]+(?:\.[0-9]+)?)[ \t]*(?<unit>(?:[kMmcnuµμ]?(?:Hz|Pa|m|s|g|V|A|W|L)|°C))", Options);
    // Keep the object as well as its property. A bare unit or a lone property name
    // cannot identify the same measurement in two independent sentences.
    private static readonly Regex JapaneseField = new(
        @"(?<field>[\p{IsCJKUnifiedIdeographs}\p{IsKatakana}A-Za-z0-9ー_]+の[\p{IsCJKUnifiedIdeographs}\p{IsKatakana}A-Za-z0-9ー_]+(?:さ|み)?)(?:は|が|を)[ \t]*\z", Options);
    private static readonly Regex JapaneseFieldPath = new(
        @"(?<field>(?:[\p{IsCJKUnifiedIdeographs}\p{IsKatakana}A-Za-z0-9ー_]+の)+[\p{IsCJKUnifiedIdeographs}\p{IsKatakana}A-Za-z0-9ー_]+(?:さ|み)?)(?:は|が|を)[ \t]*\z", Options);
    private static readonly Regex QualifiedField = new(
        @"(?<field>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+)(?:[ \t]*[:=]|[ \t]+is)[ \t]*\z", Options);
    // Recognize explicit qualifications conservatively. This is a deliberately small
    // lexical grammar, not a test of affirmation in unrestricted natural language.
    private static readonly Regex QualifiedScope = new(
        @"\b(?:no|not|never|neither|nor|or|if|unless|except|without|hypothetical|possibly|either|instead|between|approximately|about|from|formerly)\b|(?:ない|なく|ません|せず|場合|仮に|もし|以外|未満|以上|以下|禁止|ならば|または|又は|あるいは|約[0-9])",
        Options | RegexOptions.IgnoreCase);
    private static readonly Regex Continuation = new(
        @"^\s*(?:[/\\|*×÷·⋅^±+−–—<>≤≥=²³¹⁰-⁹⁻⁺⁄0-9-]|(?:per|squared|cubed|times|through|to|x)\b)",
        Options | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record SpanValue(int Start, string Literal, string Value, string? Unit);
    private sealed record FieldValue(string Field, SpanValue Span);
    private readonly FieldValue proposed;
    private readonly FieldValue observed;
    private readonly string key;

    private BonsaiCtgLiteralComparison(FieldValue proposed, string key, FieldValue observed)
        => (this.proposed, this.key, this.observed) = (proposed, key, observed);

    internal static BonsaiCtgLiteralComparison? TryCreate(string candidate, IReadOnlyDictionary<string, string> observations, bool includeFieldPaths = false)
    {
        if (observations.Count > 64 || !SafeText(candidate, 8192) ||
            !TryRead(candidate, includeFieldPaths, out var proposed) || proposed is null) return null;

        BonsaiCtgLiteralComparison? found = null;
        foreach (var (key, text) in observations)
        {
            if (!SafeText(key, 256) || !SafeText(text, 8192) || !TryRead(text, includeFieldPaths, out var observed)) return null;
            if (observed is null || observed.Field != proposed.Field ||
                (observed.Span.Unit is null) != (proposed.Span.Unit is null)) continue;
            if (found is not null) return null;
            found = new(proposed, key, observed);
        }
        return found;
    }

    internal string ToPromptData() => JsonSerializer.Serialize(new
    {
        kind = proposed.Span.Unit is null ? "assignment" : "measurement",
        field = proposed.Field, candidate = proposed.Span, observationKey = key, observation = observed.Span,
        literalComparison = Compare(proposed.Span.Literal, observed.Span.Literal),
        valueComparison = Compare(proposed.Span.Value, observed.Span.Value),
        unitComparison = proposed.Span.Unit is null ? null : Compare(proposed.Span.Unit, observed.Span.Unit)
    }, Json);

    internal string DescribeForCouncil()
    {
        const string omitted = """{"description":"The extracted record is too long to display here. Consult the original proposal and observations."}""";
        if (proposed.Span.Literal.Length > 128 || observed.Span.Literal.Length > 128 || proposed.Field.Length > 128 || key.Length > 128)
            return omitted;

        string description;
        if (proposed.Span.Unit is null)
        {
            string relation = proposed.Span.Value == observed.Span.Value ? "match" : "differ";
            description = $"The proposed text gives {proposed.Span.Value} for the written key {proposed.Field}. " +
                $"The selected observation gives {observed.Span.Value} for that key. Their written values {relation}.";
        }
        else
        {
            var quantity = BonsaiCtgQuantityRelation.TryCompare(proposed.Span.Value, proposed.Span.Unit, observed.Span.Value, observed.Span.Unit!);
            description = quantity?.Description ?? "Only the literal text was compared. Physical equivalence was not calculated.";
        }
        var data = JsonSerializer.Serialize(new
        {
            field = proposed.Field, proposalLiteral = proposed.Span.Literal, observationKey = key,
            observationLiteral = observed.Span.Literal, literalComparison = Compare(proposed.Span.Literal, observed.Span.Literal),
            description
        }, Json);
        return data.Length <= 1024 ? data : omitted;
    }

    private static string Compare(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal) ? "equal" : "different";

    // These edits describe the two literals only. Whether an edit is appropriate depends
    // on the request, subject, time and the individual council's governance rules.
    internal string DescribeEdits() => JsonSerializer.Serialize(new
    {
        source = new { observationKey = key, field = observed.Field, recorded = observed.Span.Literal },
        proposal = new { field = proposed.Field, wouldReport = proposed.Span.Literal },
        valueEdit = Edit(observed.Span.Value, proposed.Span.Value),
        unitEdit = observed.Span.Unit is null ? null : Edit(observed.Span.Unit, proposed.Span.Unit!),
        quantity = observed.Span.Unit is null ? null :
            BonsaiCtgQuantityRelation.TryCompare(proposed.Span.Value, proposed.Span.Unit!, observed.Span.Value, observed.Span.Unit)?.Description
    }, Json);

    private static string Edit(string source, string proposedText) => source == proposedText
        ? $"Copied unchanged: {source}."
        : $"Replaced the recorded {source} with {proposedText} in the proposed output.";

    internal bool HasMaterialValueDifference => proposed.Span.Unit is null
        ? proposed.Span.Value != observed.Span.Value
        : BonsaiCtgQuantityRelation.TryCompare(proposed.Span.Value, proposed.Span.Unit,
            observed.Span.Value, observed.Span.Unit!)?.Relation is "different" or "incompatible_dimensions";

    // Preserving both strings is a necessary condition for this reading aid, not a
    // semantic verdict. A translation which drops the part identifier is dispensable.
    internal bool ReadingRetainsClaimedField(string reading) =>
        reading.Contains(proposed.Field, StringComparison.Ordinal) &&
        reading.Contains(proposed.Span.Literal, StringComparison.Ordinal);

    private static bool SafeText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength ||
            text.Contains("<|", StringComparison.Ordinal) || text.Contains("|>", StringComparison.Ordinal)) return false;
        for (int i = 0; i < text.Length;)
        {
            if (!Rune.TryGetRuneAt(text, i, out var rune)) return false;
            var category = Rune.GetUnicodeCategory(rune);
            if (category == UnicodeCategory.Format || category == UnicodeCategory.Control && rune.Value is not ('\r' or '\n' or '\t')) return false;
            i += rune.Utf16SequenceLength;
        }
        return true;
    }

    // false denotes an ambiguous or unsupported expression. true with no value means
    // this record contains no supported literal. Never select the most convenient of several.
    private static bool TryRead(string text, bool includeFieldPaths, out FieldValue? result)
    {
        result = null;
        Match assignment = Assignment.Match(text);
        Match quantity = Quantity.Match(text);
        if (!assignment.Success && !quantity.Success) return true;
        if (QualifiedScope.IsMatch(text) || assignment.NextMatch().Success || quantity.NextMatch().Success) return false;

        if (quantity.Success)
        {
            if (!EndsAtDelimiter(text, quantity.Index + quantity.Length)) return false;
            string prefix = text[..quantity.Index];
            Match field = (includeFieldPaths ? JapaneseFieldPath : JapaneseField).Match(prefix);
            bool japanese = field.Success;
            if (!japanese) field = QualifiedField.Match(prefix);
            if (!field.Success || !StartsAtDelimiter(text, field.Index, japanese)) return false;
            if (assignment.Success && (assignment.Groups["value"].Index != quantity.Index ||
                assignment.Groups["field"].Value != field.Groups["field"].Value)) return false;
            result = new(field.Groups["field"].Value, new(quantity.Index, quantity.Value,
                quantity.Groups["value"].Value, quantity.Groups["unit"].Value));
            return true;
        }

        if (!StartsAtDelimiter(text, assignment.Index, false) || !EndsAtDelimiter(text, assignment.Index + assignment.Length))
            return false;
        result = new(assignment.Groups["field"].Value, new(assignment.Index, assignment.Value,
            assignment.Groups["value"].Value, null));
        return true;
    }

    private static bool StartsAtDelimiter(string text, int index, bool japanese)
    {
        if (index == 0) return true;
        char c = text[index - 1];
        return char.IsWhiteSpace(c) || c is '\'' or '"' or '(' or '「' or '『' or '、' or '。' ||
            japanese && c is 'は' or 'が' or 'を' or 'に';
    }

    private static bool EndsAtDelimiter(string text, int index)
    {
        if (index == text.Length) return true;
        char c = text[index];
        if (c is ',' or '.') return index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]) || text[index + 1] is '\'' or '"';
        if (char.IsWhiteSpace(c)) return !Continuation.IsMatch(text.AsSpan(index));
        return c is '\'' or '"' or ')' or '」' or '』' or '。' or '、' or 'と';
    }
}
