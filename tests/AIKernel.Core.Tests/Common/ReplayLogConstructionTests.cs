namespace AIKernel.Core.Tests.Common;

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AIKernel.Common.Results;
using Xunit;

public sealed class ReplayLogConstructionTests
{
    [Theory]
    [InlineData("none", 1)]
    [InlineData("immutable", 1)]
    [InlineData("sorted", 1)]
    [InlineData("none", 513)]
    [InlineData("immutable", 513)]
    [InlineData("sorted", 513)]
    public void AppendsPreserveOrderParentsAndCanonicalHash(string metadataKind, int count)
    {
        var step = Build(count, metadataKind);
        Assert.Equal(count, step.ReplayLog.Count);
        Assert.Equal(Enumerable.Range(0, count).Select(i => $"step.{i}"), step.ReplayLog.Select(e => e.SemanticDelta.Label));
        for (var i = 1; i < count; i++)
            Assert.Equal(step.ReplayLog[i - 1].StepId, step.ReplayLog[i].ParentStepId);
        Assert.Equal(step.StepId, step.ReplayLog[^1].StepId);
        AssertCanonicalHash(step);
        Assert.Equal(step.ReplayLog.ToArray(), step.ReplayLog.ToArray());
    }

    [Fact]
    public void DefaultAndEmptyRetainTheirDistinctContracts()
    {
        var uninitialized = default(ResultStep<int, int>);
        var empty = ResultStep<int, int>.Success(0, 0);
        Assert.True(uninitialized.IsFailure);
        Assert.Null(uninitialized.ReplayLog);
        Assert.Null(uninitialized.ReplayLogHash);
        Assert.Empty(empty.ReplayLog);
        AssertCanonicalHash(empty);
    }

    [Fact]
    public void BranchesAndProjectionsDoNotChangeRetainedPrefixes()
    {
        var prefix = Build(64);
        var entries = prefix.ReplayLog.ToArray();
        var expectedHash = SnapshotHash(entries);
        var left = prefix.WithSemanticDelta(new SemanticDelta("left"));
        var right = prefix.WithSemanticDelta(new SemanticDelta("right"));
        var projected = prefix.Map(value => value + 1).WithState(9).MapState((state, _) => state + 1);

        Assert.Equal(entries, prefix.ReplayLog);
        Assert.Equal(expectedHash, prefix.ReplayLogHash);
        Assert.Equal(expectedHash, projected.ReplayLogHash);
        Assert.Same(prefix.ReplayLog, projected.ReplayLog);
        Assert.Equal(entries, left.ReplayLog.Take(entries.Length));
        Assert.Equal(entries, right.ReplayLog.Take(entries.Length));
        Assert.NotEqual(left.StepId, right.StepId);
        AssertCanonicalHash(left);
        AssertCanonicalHash(right);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(73)]
    public void FailureReplacesOnlyCurrentEntryAndPreservesOriginalSnapshot(int count)
    {
        var original = Build(count);
        var entries = original.ReplayLog.ToArray();
        var expectedHash = SnapshotHash(entries);
        var failed = original.Map<int>(_ => throw new IOException("failed projection"));
        Assert.True(failed.IsFailure);
        Assert.Equal("UNHANDLED_EXCEPTION", failed.Error!.Code);
        Assert.Equal(Math.Max(1, count), failed.ReplayLog.Count);
        Assert.False(failed.ReplayLog[^1].IsSuccess);
        Assert.Equal(failed.Error.Code, failed.ReplayLog[^1].ErrorCode);
        Assert.Equal(entries.Take(Math.Max(0, count - 1)), failed.ReplayLog.Take(Math.Max(0, count - 1)));
        Assert.Equal(entries, original.ReplayLog);
        Assert.Equal(expectedHash, original.ReplayLogHash);
        AssertCanonicalHash(failed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BindRebasesCompleteSuffixWithoutChangingEitherInput(bool asynchronous)
    {
        var prefix = Build(29);
        var suffix = Build(41, "sorted");
        var prefixEntries = prefix.ReplayLog.ToArray();
        var suffixEntries = suffix.ReplayLog.ToArray();
        var bound = asynchronous ? await prefix.BindAsync(_ => Task.FromResult(suffix)) : prefix.Bind(_ => suffix);
        Assert.Equal(70, bound.ReplayLog.Count);
        Assert.Equal(prefixEntries, bound.ReplayLog.Take(29));
        var parent = prefix.StepId;
        foreach (var entry in bound.ReplayLog.Skip(29))
        {
            Assert.Equal(parent, entry.ParentStepId);
            Assert.True(entry.IsSuccess);
            Assert.Equal(ResultStep<int, int>.Success(0, 0).WithSemanticDelta(entry.SemanticDelta, parent).StepId, entry.StepId);
            parent = entry.StepId;
        }
        Assert.Equal(parent, bound.StepId);
        Assert.Equal(prefixEntries, prefix.ReplayLog);
        Assert.Equal(suffixEntries, suffix.ReplayLog);
        AssertCanonicalHash(bound);
    }

    [Theory]
    [InlineData("dictionary")]
    [InlineData("read_only_wrapper")]
    [InlineData("builder")]
    public void MutableMetadataKeepsConstructionTimeHashAndRehashesOnNewResult(string kind)
    {
        IDictionary<string, string> mutable = kind == "builder"
            ? ImmutableDictionary.CreateBuilder<string, string>()
            : new Dictionary<string, string>();
        mutable["value"] = "before";
        var metadata = kind == "read_only_wrapper"
            ? new ReadOnlyDictionary<string, string>(mutable)
            : (IReadOnlyDictionary<string, string>)mutable;
        var step = ResultStep<int, int>.Success(0, 0).WithSemanticDelta(new SemanticDelta("mutable", Metadata: metadata));
        var before = SnapshotHash(step.ReplayLog);
        mutable["value"] = "after";
        // The first property read must still return the hash taken before the mutation.
        Assert.Equal(before, step.ReplayLogHash);
        Assert.Same(metadata, step.ReplayLog[0].SemanticDelta.Metadata);
        var next = step.WithState(1);
        Assert.NotEqual(before, next.ReplayLogHash);
        AssertCanonicalHash(next);
        Assert.Equal(before, step.ReplayLogHash);
    }

    [Fact]
    public void MutableEntryAfterImmutablePrefixRetainsEagerHashSemantics()
    {
        var prefix = Build(17);
        var mutable = new Dictionary<string, string> { ["x"] = "before" };
        var step = prefix.WithSemanticDelta(new SemanticDelta("mutable", Metadata: mutable));
        var before = SnapshotHash(step.ReplayLog);
        mutable["x"] = "after";
        Assert.Equal(before, step.ReplayLogHash);
        AssertCanonicalHash(prefix);
        AssertCanonicalHash(step.WithSemanticDelta(new SemanticDelta("next")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalPrefixRetainsAliasingAndHashTiming(bool array)
    {
        var entries = Build(3).ReplayLog.ToArray();
        IReadOnlyList<ResultStepReplayLogEntry> external = array ? entries : entries.ToList();
        var step = ResultStep<int, int>.Success(0, 0).WithReplayLogPrefix(external);
        var before = SnapshotHash(external);
        var replacement = entries[0] with { ErrorCode = "changed externally" };
        if (array) entries[0] = replacement;
        else ((List<ResultStepReplayLogEntry>)external)[0] = replacement;
        Assert.Same(external, step.ReplayLog);
        Assert.Equal(before, step.ReplayLogHash);
        Assert.NotEqual(before, step.WithState(1).ReplayLogHash);
        AssertCanonicalHash(step.WithState(1));
    }

    [Fact]
    public void NonemptySuffixSnapshotsExternalPrefixMembership()
    {
        var external = Build(4).ReplayLog.ToList();
        var before = external.ToArray();
        var step = Build(3).WithReplayLogPrefix(external);
        external.Clear();
        Assert.Equal(7, step.ReplayLog.Count);
        Assert.Equal(before, step.ReplayLog.Take(4));
        AssertCanonicalHash(step);
    }

    [Fact]
    public void LongLogCanBeEnumeratedIndexedAndSerializedWithoutRecursiveTraversal()
    {
        var step = Build(20_000, "none");
        var entries = step.ReplayLog.ToArray();
        for (var i = entries.Length - 1; i >= 0; i--) Assert.Equal(entries[i], step.ReplayLog[i]);
        var json = System.Text.Json.JsonSerializer.Serialize(step.ReplayLog);
        var restored = System.Text.Json.JsonSerializer.Deserialize<ResultStepReplayLogEntry[]>(json)!;
        Assert.Equal(entries.Select(e => e.StepId), restored.Select(e => e.StepId));
        Assert.Equal(step.ReplayLogHash, SnapshotHash(restored));
    }

    [Fact]
    public async Task ConcurrentReadsAndBranchesShareNoMutableConstructionState()
    {
        var step = Build(257);
        var expected = SnapshotHash(step.ReplayLog.ToArray());
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            Assert.Equal(expected, step.ReplayLogHash);
            Assert.Equal("step.128", step.ReplayLog[128].SemanticDelta.Label);
            var branch = step.WithSemanticDelta(new SemanticDelta($"branch.{i}"));
            Assert.Equal(258, branch.ReplayLog.Count);
            AssertCanonicalHash(branch);
        })));
        Assert.Equal(257, step.ReplayLog.Count);
        Assert.Equal(expected, step.ReplayLogHash);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void InvalidIndexKeepsArrayInterfaceExceptionType(int index)
    {
        var step = Build(3);
        IReadOnlyList<ResultStepReplayLogEntry> array = step.ReplayLog.ToArray();
        var expected = Record.Exception(() => array[index]);
        var actual = Record.Exception(() => step.ReplayLog[index]);
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
    }

    [Fact]
    public void UnknownMetadataEnumerationFailureStillOccursDuringConstruction()
    {
        var metadata = new ThrowOnSecondEnumeration();
        var exception = Assert.Throws<InvalidOperationException>(() => ResultStep<int, int>.Success(0, 0)
            .WithSemanticDelta(new SemanticDelta("external", Metadata: metadata)));
        Assert.Equal("second enumeration", exception.Message);
        Assert.Equal(2, metadata.Enumerations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(7999)]
    [InlineData(8000)]
    [InlineData(8001)]
    [InlineData(16385)]
    public void Utf8ChunkBoundariesKeepLegacyPayloadIncludingInvalidSurrogates(int length)
    {
        var text = new string('界', length) + "😀\ud800X\udc00e\u0301\r\n=\n";
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add("\ud800key", text).Add("😀", "\udc00\n").Add("", "");
        var step = ResultStep<int, int>.Success(0, 0)
            .WithSemanticDelta(new SemanticDelta(text, (OriginStep)(-1), (SemanticSlot)int.MinValue, metadata, text))
            .WithSemanticDelta(new SemanticDelta("tail", Metadata: metadata))
            .Map<int>(_ => throw new IOException("failed"));
        AssertCanonicalHash(step);
    }

    private static ResultStep<int, int> Build(int count, string metadataKind = "immutable")
    {
        var step = ResultStep<int, int>.Success(0, 0);
        for (var i = 0; i < count; i++)
        {
            IReadOnlyDictionary<string, string>? metadata = metadataKind switch
            {
                "immutable" => ImmutableDictionary<string, string>.Empty.Add("b", "2").Add("a", "1"),
                "sorted" => ImmutableSortedDictionary<string, string>.Empty.Add("b", "2").Add("a", "1"),
                _ => null
            };
            step = step.WithSemanticDelta(new SemanticDelta($"step.{i}", OriginStep.KernelFacade, SemanticSlot.T, metadata));
        }
        return step;
    }

    private static string SnapshotHash(IReadOnlyList<ResultStepReplayLogEntry> entries)
        => ResultStep<int, int>.Success(0, 0).WithReplayLogPrefix(entries.ToArray()).ReplayLogHash;

    private static void AssertCanonicalHash(ResultStep<int, int> step)
    {
        var expected = LegacyHash(step.ReplayLog);
        Assert.Equal(expected, SnapshotHash(step.ReplayLog.ToArray()));
        Assert.Equal(expected, step.ReplayLogHash);
    }

    // Frozen v0.1.3 reference: the full string is encoded in one operation. This checks
    // the streamed encoder independently, including UTF-16 pairs crossing chunk boundaries.
    private static string LegacyHash(IReadOnlyList<ResultStepReplayLogEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("aikernel.result-step.replay-log.v1");
        builder.AppendLine(entries.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var entry in entries)
        {
            builder.AppendLine(entry.StepId);
            builder.AppendLine(entry.ParentStepId ?? string.Empty);
            builder.AppendLine(entry.IsSuccess ? "success" : "failure");
            builder.AppendLine(entry.ErrorCode ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.Label);
            builder.AppendLine(entry.SemanticDelta.Kind ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.OriginStep?.ToString() ?? string.Empty);
            builder.AppendLine(entry.SemanticDelta.SemanticSlot?.ToString() ?? string.Empty);
            if (entry.SemanticDelta.Metadata is not null)
            {
                foreach (var pair in entry.SemanticDelta.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    builder.Append(pair.Key);
                    builder.Append('=');
                    builder.AppendLine(pair.Value);
                }
            }
            builder.AppendLine(entry.SemanticDelta.Metadata?.Count.ToString(CultureInfo.InvariantCulture) ?? "0");
        }
        return "replay:sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private sealed class ThrowOnSecondEnumeration : IReadOnlyDictionary<string, string>
    {
        public int Enumerations { get; private set; }
        public int Count => 1;
        public IEnumerable<string> Keys => ["key"];
        public IEnumerable<string> Values => ["value"];
        public string this[string key] => key == "key" ? "value" : throw new KeyNotFoundException();
        public bool ContainsKey(string key) => key == "key";
        public bool TryGetValue(string key, out string value) { value = "value"; return ContainsKey(key); }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            if (++Enumerations == 2) throw new InvalidOperationException("second enumeration");
            return new[] { new KeyValuePair<string, string>("key", "value") }.AsEnumerable().GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
