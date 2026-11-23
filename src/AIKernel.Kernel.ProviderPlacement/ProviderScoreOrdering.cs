using System.Collections.Immutable;
using AIKernel.Abstractions.Providers;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Providers;

namespace AIKernel.Kernel.Providers;

/// <summary>[EN] Validates and ranks model scores without normalization. [JA] 正規化せずにスコアを検証して順位付けします。</summary>
public static class ProviderScoreOrdering
{
    /// <summary>[EN] Orders valid scores best-first with ties by input position. [JA] スコアを良い順に並べ、同点は入力順にします。</summary>
    public static ScoredResult Create(
        ScoreSemantics semantics,
        IEnumerable<ScoredItem> items,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(semantics);
        ArgumentNullException.ThrowIfNull(items);
        if (!Enum.IsDefined(semantics.Kind) ||
            semantics.Kind == ScoreKind.Distance && semantics.HigherIsBetter ||
            semantics.Calibrated && semantics.Kind != ScoreKind.Probability ||
            semantics.Range is null || semantics.Range.Min > semantics.Range.Max ||
            semantics.Range.Min is double min && !double.IsFinite(min) ||
            semantics.Range.Max is double max && !double.IsFinite(max))
            throw InvalidOutput();

        var seen = new HashSet<int>();
        var valid = new List<ScoredItem>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null || item.Index < 0 || !seen.Add(item.Index) ||
                !double.IsFinite(item.Score.Value) ||
                item.Score.Value < semantics.Range.Min || item.Score.Value > semantics.Range.Max ||
                semantics.Kind == ScoreKind.Probability && item.Score.Value is < 0 or > 1)
                throw InvalidOutput();
            valid.Add(item);
        }

        var ordered = semantics.HigherIsBetter
            ? valid.OrderByDescending(item => item.Score.Value)
            : valid.OrderBy(item => item.Score.Value);
        return new ScoredResult
        {
            Semantics = semantics,
            Items = ordered.ThenBy(item => item.Index).ToImmutableArray()
        };
    }

    private static ProviderOperationException InvalidOutput() => new(
        new ExecutionError(ProviderErrorCodes.InvalidOutput, "The provider produced invalid model scores."));
}
