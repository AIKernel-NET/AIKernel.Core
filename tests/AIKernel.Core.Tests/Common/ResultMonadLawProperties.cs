namespace AIKernel.Core.Tests.Common;

using AIKernel.Common.Results;
using System.Globalization;
using System.Text.Json;

public sealed class ResultMonadLawProperties
{
    private const int Samples = 1024;
    private static readonly int[] Seeds = [1, 20261113, unchecked((int)0x9e3779b9)];
    private static readonly JsonSerializerOptions DiagnosticJson = new() { IncludeFields = true };

    public static IEnumerable<object[]> LawCases()
    {
        foreach (var seed in Seeds)
        foreach (var selectMany in new[] { false, true })
        foreach (var law in new[] { "left-identity", "right-identity", "associativity" })
            yield return [seed, selectMany, law];
    }

    public static IEnumerable<object[]> ProjectionCases()
        => Seeds.Select(seed => new object[] { seed });

    public static IEnumerable<object[]> AsyncCases()
    {
        foreach (var seed in Seeds)
        foreach (var form in new[] { "result-async", "task-async", "task-sync" })
            yield return [seed, form];
    }

    [Theory]
    [MemberData(nameof(LawCases))]
    public void IntegerResultsObeyMonadLaws(int seed, bool selectMany, string law)
    {
        var generator = new Generator(seed);
        for (var sample = 0; sample < Samples; sample++)
        {
            var value = generator.Integer(sample);
            var initial = Initial(sample, value);
            var f = generator.Function(sample % 5);
            var g = generator.Function(sample / 5 % 5);
            CheckLaw(law, selectMany, value, initial, f.Integer, g.Integer,
                Case(seed, sample, initial, f, g));
        }
    }

    [Theory]
    [MemberData(nameof(LawCases))]
    public void NullableTextResultsObeyMonadLaws(int seed, bool selectMany, string law)
    {
        var generator = new Generator(seed);
        for (var sample = 0; sample < Samples; sample++)
        {
            var value = generator.Text(sample);
            var initial = Initial(sample, value);
            var f = generator.Function(sample % 5);
            var g = generator.Function(sample / 5 % 5);
            CheckLaw(law, selectMany, value, initial, f.Text, g.Text,
                Case(seed, sample, initial, f, g));
        }
    }

    [Theory]
    [MemberData(nameof(ProjectionCases))]
    public void LinqAssociativityPreservesAllProjectedValuesAcrossTypes(int seed)
    {
        var generator = new Generator(seed);
        for (var sample = 0; sample < Samples; sample++)
        {
            var initial = Initial(sample, generator.Integer(sample));
            var first = generator.Function(sample % 5);
            var second = generator.Function(sample / 5 % 5);
            Result<string?> F(int value) => first.Text(value.ToString(CultureInfo.InvariantCulture));
            Result<int> G(string? value) => second.Integer(value?.Length ?? -1);

            var left =
                from pair in (from x in initial from y in F(x) select (X: x, Y: y))
                from z in G(pair.Y)
                select (pair.X, pair.Y, Z: z);
            var right =
                from x in initial
                from pair in (from y in F(x) from z in G(y) select (Y: y, Z: z))
                select (X: x, pair.Y, pair.Z);
            var context = Case(seed, sample, initial, first, second);
            SameOutcome(left, right, context);

            // Independently verify the retained outer and inner values, not just agreement
            // between two queries which both use SelectMany's projector overload.
            if (initial.IsFailure) { Assert.True(left.IsFailure, context); continue; }
            var middle = F(initial.Value);
            if (middle.IsFailure) { Assert.True(left.IsFailure, context); continue; }
            var last = G(middle.Value);
            if (last.IsFailure) { Assert.True(left.IsFailure, context); continue; }
            SameOutcome(Result<(int, string?, int)>.Success((initial.Value, middle.Value, last.Value)), left, context);
        }
    }

    [Theory]
    [MemberData(nameof(AsyncCases))]
    public async Task AsyncSelectManyPreservesTheSynchronousResultObservation(int seed, string form)
    {
        var generator = new Generator(seed);
        for (var sample = 0; sample < Samples; sample++)
        {
            var initial = Initial(sample, generator.Integer(sample));
            var f = generator.Function(sample % 5);
            var salt = generator.NextInteger();
            (int Outer, int Inner, int Salt) Project(int outer, int inner) => (outer, inner, salt);
            Task<Result<int>> BindAsync(int value) => Yield(f.Integer(value));
            var expected = from x in initial from y in f.Integer(x) select Project(x, y);
            var actual = form switch
            {
                "result-async" => await initial.SelectMany(BindAsync, Project),
                "task-async" => await Yield(initial).SelectMany(BindAsync, Project),
                "task-sync" => await Yield(initial).SelectMany(f.Integer, Project),
                _ => throw new ArgumentOutOfRangeException(nameof(form))
            };
            SameOutcome(expected, actual, $"seed={seed}; sample={sample}; form={form}; initial={Describe(initial)}; f={f}; salt={salt}");
        }
    }

    private static void CheckLaw<T>(string law, bool selectMany, T value, Result<T> initial,
        Func<T, Result<T>> f, Func<T, Result<T>> g, string context)
    {
        Result<T> Bind(Result<T> result, Func<T, Result<T>> next)
            => selectMany ? result.SelectMany(next, static (_, bound) => bound) : result.Bind(next);

        var pair = law switch
        {
            "left-identity" => (Bind(Result<T>.Success(value), f), f(value)),
            "right-identity" => (Bind(initial, Result<T>.Success), initial),
            "associativity" => (Bind(Bind(initial, f), g), Bind(initial, x => Bind(f(x), g))),
            _ => throw new ArgumentOutOfRangeException(nameof(law))
        };
        SameOutcome(pair.Item1, pair.Item2, $"law={law}; selectMany={selectMany}; value={JsonSerializer.Serialize(value, DiagnosticJson)}; {context}");
    }

    private static void SameOutcome<T>(Result<T> left, Result<T> right, string context)
    {
        var equal = left.IsSuccess == right.IsSuccess && left.IsFailure == right.IsFailure
            && EqualityComparer<T>.Default.Equals(left.Value!, right.Value!);
        Assert.True(equal, $"{context}; left={Describe(left)}; right={Describe(right)}");
    }

    private static string Describe<T>(Result<T> result)
        => $"(success={result.IsSuccess}, failure={result.IsFailure}, value={JsonSerializer.Serialize(result.Value, DiagnosticJson)})";

    private static string Case<T>(int seed, int sample, Result<T> initial, PureFunction f, PureFunction g)
        => $"seed={seed}; sample={sample}; initial={Describe(initial)}; f={f}; g={g}";

    private static Result<T> Initial<T>(int sample, T value) => (sample / 25 % 3) switch
    {
        0 => Result<T>.Success(value),
        1 => Result<T>.Fail(new ErrorContext("initial failure", "V2_INITIAL", (sample & 1) == 0)),
        _ => default
    };

    private static async Task<Result<T>> Yield<T>(Result<T> result)
    {
        await Task.Yield();
        return result;
    }

    // Every function is total and deterministic. Failure/default are returned values;
    // throwing delegates and side effects are not functions in this monad-law domain.
    private readonly record struct PureFunction(int Mode, int Salt, uint Divisor)
    {
        public Result<int> Integer(int value)
            => Apply(value, unchecked(value * (Salt | 1) + Salt));

        public Result<string?> Text(string? value)
        {
            var key = 0;
            foreach (var character in value ?? "") key = unchecked(key * 31 + character);
            return Apply<string?>(key, Salt.ToString("x8", CultureInfo.InvariantCulture) + ":" + value);
        }

        private Result<T> Apply<T>(int key, T mapped) => Mode switch
        {
            0 => Result<T>.Success(mapped),
            1 => Fail<T>(),
            2 => default,
            3 => unchecked((uint)key) % Divisor == 0 ? Fail<T>() : Result<T>.Success(mapped),
            4 => Result<T>.Success(default!),
            _ => throw new ArgumentOutOfRangeException(nameof(Mode))
        };

        private Result<T> Fail<T>()
            => Result<T>.Fail(new ErrorContext("generated failure", $"V2_{Salt:x8}", (Salt & 1) == 0));
    }

    // Fixed xorshift32 rather than a clock seed or runtime-dependent string hash.
    private sealed class Generator(int seed)
    {
        private uint _state = unchecked((uint)seed);
        private static readonly int[] IntegerEdges = [0, 1, -1, int.MinValue, int.MaxValue, int.MinValue + 1, int.MaxValue - 1];
        private static readonly string?[] TextEdges = [null, "", "a", "\0", "\n", "日本語", "é", "e\u0301", "🙂", "\ud800"];

        private uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        public int NextInteger() => unchecked((int)Next());
        public int Integer(int sample) => sample < IntegerEdges.Length ? IntegerEdges[sample] : NextInteger();
        public PureFunction Function(int mode) => new(mode, NextInteger(), 2 + Next() % 31);

        public string? Text(int sample)
        {
            if (sample < TextEdges.Length) return TextEdges[sample];
            var characters = new char[Next() % 25];
            for (var i = 0; i < characters.Length; i++) characters[i] = (char)Next();
            return new string(characters);
        }
    }
}
