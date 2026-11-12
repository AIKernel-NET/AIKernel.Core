namespace AIKernel.Common.Results;

using System.Collections;
using System.Collections.Immutable;

// Only entries with immutable metadata enter this representation. External collections and
// mutable metadata keep the existing eager snapshot/hash path in ResultStep.
internal sealed class PersistentReplayLog : IReadOnlyList<ResultStepReplayLogEntry>
{
    internal static PersistentReplayLog Empty { get; } = new(ImmutableStack<ResultStepReplayLogEntry>.Empty, 0, null);

    private readonly ImmutableStack<ResultStepReplayLogEntry> _reversed;
    private readonly ResultStepReplayLogEntry? _first;
    private string? _hash;
    private ResultStepReplayLogEntry[]? _indexed;

    private PersistentReplayLog(ImmutableStack<ResultStepReplayLogEntry> reversed, int count, ResultStepReplayLogEntry? first)
    {
        _reversed = reversed;
        Count = count;
        _first = first;
    }

    public int Count { get; }

    public ResultStepReplayLogEntry this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index == Count - 1) return _reversed.Peek();
            if (index == 0) return _first!;
            var entries = Volatile.Read(ref _indexed);
            if (entries is null)
            {
                entries = ToArray();
                entries = Interlocked.CompareExchange(ref _indexed, entries, null) ?? entries;
            }
            return entries[index];
        }
    }

    internal string Hash
    {
        get
        {
            var hash = Volatile.Read(ref _hash);
            if (hash is not null) return hash;
            hash = ResultStepIdentity.CreateReplayLogHash(this);
            return Interlocked.CompareExchange(ref _hash, hash, null) ?? hash;
        }
    }

    internal static bool HasImmutableMetadata(ResultStepReplayLogEntry entry)
        => entry.SemanticDelta is { } delta &&
           delta.Metadata is null or ImmutableDictionary<string, string> or ImmutableSortedDictionary<string, string>;

    internal PersistentReplayLog Append(ResultStepReplayLogEntry entry)
        => new(_reversed.Push(entry), checked(Count + 1), _first ?? entry);

    internal PersistentReplayLog AppendRange(PersistentReplayLog suffix)
    {
        var reversed = _reversed;
        foreach (var entry in suffix) reversed = reversed.Push(entry);
        return new(reversed, checked(Count + suffix.Count), _first ?? suffix._first);
    }

    internal PersistentReplayLog ReplaceLast(ResultStepReplayLogEntry entry)
        => new(_reversed.Pop().Push(entry), Count, Count == 1 ? entry : _first);

    internal PersistentReplayLog Rebase(string parentStepId)
    {
        var reversed = ImmutableStack<ResultStepReplayLogEntry>.Empty;
        ResultStepReplayLogEntry? first = null;
        foreach (var entry in this)
        {
            var stepId = ResultStepIdentity.Create(parentStepId, entry.SemanticDelta, entry.IsSuccess, entry.ErrorCode);
            var rebased = entry with { StepId = stepId, ParentStepId = parentStepId };
            first ??= rebased;
            reversed = reversed.Push(rebased);
            parentStepId = stepId;
        }
        return new(reversed, Count, first);
    }

    private ResultStepReplayLogEntry[] ToArray()
    {
        if (Count == 0) return Array.Empty<ResultStepReplayLogEntry>();
        var entries = new ResultStepReplayLogEntry[Count];
        var index = Count;
        foreach (var entry in _reversed) entries[--index] = entry;
        return entries;
    }

    // Iterative traversal avoids stack overflow and does not retain a full array on every
    // historical log merely because its hash was read. Random indexing caches one private array.
    public IEnumerator<ResultStepReplayLogEntry> GetEnumerator()
        => ((IEnumerable<ResultStepReplayLogEntry>)(Volatile.Read(ref _indexed) ?? ToArray())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
