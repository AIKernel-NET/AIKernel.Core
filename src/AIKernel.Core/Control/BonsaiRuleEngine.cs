namespace AIKernel.Core.Control;

using AIKernel.Abstractions.Events;
using AIKernel.Common.Results;

/// <summary>
/// [EN] Rule contract for the Core Bonsai rule engine.
/// [JA] Core Bonsai rule engine 向けの rule contract です。
/// </summary>
public interface IBonsaiRule
{
    /// <summary>[EN] Rule identifier. [JA] rule identifier です。</summary>
    string RuleId { get; }

    /// <summary>[EN] Evaluates whether the rule matches an input. [JA] rule が input に一致するか評価します。</summary>
    bool Matches(string input);

    /// <summary>[EN] Produces a deterministic rule output. [JA] 決定論的な rule output を生成します。</summary>
    string Execute(string input);
}

/// <summary>
/// [EN] Core Bonsai rule engine contract used as an AI OS daemon boundary.
/// [JA] AI OS daemon 境界として使用する Core Bonsai rule engine contract です。
/// </summary>
public interface IBonsaiEngine
{
    /// <summary>[EN] Registers a Bonsai rule. [JA] Bonsai rule を登録します。</summary>
    void RegisterRule(IBonsaiRule rule);

    /// <summary>[EN] Evaluates input against registered rules. [JA] 登録済み rule に対して input を評価します。</summary>
    Task<BonsaiRuleResult> EvaluateAsync(string input, CancellationToken cancellationToken = default);
}

/// <summary>
/// [EN] Result produced by the Core Bonsai rule engine.
/// [JA] Core Bonsai rule engine が生成する result です。
/// </summary>
public sealed record BonsaiRuleResult(
    bool Matched,
    string? RuleId,
    string Output);

/// <summary>
/// [EN] Deterministic evaluator for one Bonsai rule.
/// [JA] 1 つの Bonsai rule を評価する決定論的 evaluator です。
/// </summary>
public sealed class RuleEvaluator
{
    /// <summary>[EN] Evaluates one rule against input. [JA] 1 つの rule を input に対して評価します。</summary>
    public BonsaiRuleResult Evaluate(IBonsaiRule rule, string input)
        => RequireSuccess(TryEvaluate(rule, input));

    /// <summary>[EN] Evaluates one rule as a Result pipeline. [JA] 1 つの rule を Result pipeline として評価します。</summary>
    public Result<BonsaiRuleResult> TryEvaluate(IBonsaiRule? rule, string? input)
        =>
            from validRule in ValidateRule(rule)
            from safeInput in Result<string>.Ok(input ?? string.Empty)
            from matched in Try.Run(() => validRule.Matches(safeInput))
            from result in RuleMatchResult(validRule, safeInput, matched)
            select result;

    private static Result<BonsaiRuleResult> RuleMatchResult(
        IBonsaiRule rule,
        string input,
        bool matched)
        => RuleMatchDecision(matched).Match(
            _ => Result<BonsaiRuleResult>.Ok(new BonsaiRuleResult(false, null, input)),
            _ => Try.Run(() => new BonsaiRuleResult(true, rule.RuleId, rule.Execute(input))));

    private static Either<string, string> RuleMatchDecision(bool matched)
        => matched
            ? Either<string, string>.FromRight("matched")
            : Either<string, string>.FromLeft("missed");

    private static Result<IBonsaiRule> ValidateRule(IBonsaiRule? rule)
        => rule is null
            ? Result<IBonsaiRule>.Fail("Bonsai rule is required. ErrorCode=BONSAI_RULE_REQUIRED")
            : Result<IBonsaiRule>.Ok(rule);

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}

/// <summary>
/// [EN] Core Bonsai rule engine implementation.
/// [JA] Core Bonsai rule engine implementation です。
/// </summary>
public sealed class BonsaiEngine : IBonsaiEngine
{
    private readonly List<IBonsaiRule> _rules = [];
    private readonly RuleEvaluator _evaluator;
    private readonly IEventBus? _eventBus;

    /// <summary>[EN] Initializes the Bonsai engine. [JA] Bonsai engine を初期化します。</summary>
    public BonsaiEngine()
        : this(new RuleEvaluator(), null)
    {
    }

    /// <summary>[EN] Initializes the Bonsai engine with an evaluator. [JA] evaluator で Bonsai engine を初期化します。</summary>
    public BonsaiEngine(RuleEvaluator evaluator)
        : this(evaluator, null)
    {
    }

    /// <summary>[EN] Initializes the Bonsai engine with an evaluator and optional EventBus. [JA] evaluator と任意の EventBus で Bonsai engine を初期化します。</summary>
    public BonsaiEngine(RuleEvaluator evaluator, IEventBus? eventBus)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _eventBus = eventBus;
    }

    /// <summary>[EN] Documents this public package API member. [JA] RegisterRule を実行します。</summary>
    /// <inheritdoc />
    public void RegisterRule(IBonsaiRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    /// <summary>[EN] Documents this public package API member. [JA] EvaluateAsync を実行します。</summary>
    /// <inheritdoc />
    public async Task<BonsaiRuleResult> EvaluateAsync(string input, CancellationToken cancellationToken = default)
        => RequireSuccess(await TryEvaluateAsync(input, cancellationToken).ConfigureAwait(false));

    /// <summary>[EN] Evaluates input against registered rules as a Result pipeline. [JA] 登録済み rule に対して input を Result pipeline として評価します。</summary>
    public Task<Result<BonsaiRuleResult>> TryEvaluateAsync(string? input, CancellationToken cancellationToken = default)
    {
        return
            from result in EvaluateFirstRule(input, cancellationToken).AsTask()
            from published in PublishResultAsync(result, cancellationToken)
            select result;
    }

    private Result<BonsaiRuleResult> EvaluateFirstRule(string? input, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<BonsaiRuleResult>.Fail("Bonsai evaluation was cancelled. ErrorCode=BONSAI_EVALUATION_CANCELLED");
        }

        var safeInput = input ?? string.Empty;
        foreach (var rule in _rules)
        {
            var result = _evaluator.TryEvaluate(rule, safeInput);
            var decision = StopWhenFailedOrMatched(result)
                .Match<Result<BonsaiRuleResult>?>(() => null, stopped => stopped);
            if (decision is { } stopped)
            {
                return stopped;
            }
        }

        return Result<BonsaiRuleResult>.Ok(new BonsaiRuleResult(false, null, safeInput));
    }

    private static Option<Result<BonsaiRuleResult>> StopWhenFailedOrMatched(
        Result<BonsaiRuleResult> result)
        => result.Match(
            error => Option<Result<BonsaiRuleResult>>.Some(Result<BonsaiRuleResult>.Fail(error)),
            value => value.Matched
                ? Option<Result<BonsaiRuleResult>>.Some(result)
                : Option<Result<BonsaiRuleResult>>.None());

    private Task<Result<bool>> PublishResultAsync(BonsaiRuleResult result, CancellationToken cancellationToken)
    {
        if (_eventBus is null)
        {
            return Result<bool>.Ok(true).AsTask();
        }

        var eventName = EventName(result.Matched);
        return Try.RunAsync(async () =>
        {
            await _eventBus.PublishAsync(
                eventName,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["matched"] = result.Matched.ToString(),
                    ["ruleId"] = result.RuleId ?? string.Empty,
                    ["output"] = result.Output
                },
                cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    private static Either<string, string> RuleMatchDecision(bool matched)
        => matched
            ? Either<string, string>.FromRight("matched")
            : Either<string, string>.FromLeft("missed");

    private static string EventName(bool matched)
        => RuleMatchDecision(matched).Match(_ => "BonsaiRuleMissed", _ => "BonsaiRuleMatched");

    private static T RequireSuccess<T>(Result<T> result)
        => result.Match(
            error => throw new InvalidOperationException(error.Message),
            value => value);
}
