namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;

/// <summary>[EN] Documents this public package API member. [JA] DefaultPolisherValidator を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultPolisherValidator']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultPolisherValidator']/summary" />
public sealed class DefaultPolisherValidator : IPolisherValidator
{
    /// <summary>[EN] Documents this public package API member. [JA] ValidateLogicPreservationAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.ValidateLogicPreservationAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.ValidateLogicPreservationAsync']/summary" />
    public Task<PolisherValidationResult> ValidateLogicPreservationAsync(
        RawLogic originalLogic,
        string polishedOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(originalLogic);

        var original = Normalize(originalLogic.SerializedRepresentation);
        var polished = Normalize(polishedOutput);
        var isValid = string.Equals(original, polished, StringComparison.Ordinal);

        return Task.FromResult(new PolisherValidationResult
        {
            IsValid = isValid,
            Message = LogicPreservationMessage(isValid),
            Violations = LogicViolations(isValid),
            LogicIntegrityScore = LogicIntegrityScore(isValid)
        });
    }

    /// <summary>[EN] Documents this public package API member. [JA] AnalyzeDivergenceAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.AnalyzeDivergenceAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.AnalyzeDivergenceAsync']/summary" />
    public Task<LogicDivergenceAnalysis> AnalyzeDivergenceAsync(
        RawLogic originalLogic,
        string polishedOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(originalLogic);

        var original = Normalize(originalLogic.SerializedRepresentation);
        var polished = Normalize(polishedOutput);
        var hasDiverged = !string.Equals(original, polished, StringComparison.Ordinal);

        return Task.FromResult(new LogicDivergenceAnalysis
        {
            DivergenceDetected = hasDiverged,
            DivergenceType = DivergenceType(hasDiverged),
            Description = DivergenceDescription(hasDiverged),
            Severity = DivergenceSeverity(hasDiverged),
            AlteredSegments = DivergenceSegments(hasDiverged)
        });
    }

    private static string LogicPreservationMessage(bool isValid)
        => LogicPreservationDecision(isValid).Match(message => message, message => message);

    private static IReadOnlyList<string> LogicViolations(bool isValid)
        => LogicPreservationDecision(isValid).Match<IReadOnlyList<string>>(_ => ["logic_divergence"], _ => []);

    private static int LogicIntegrityScore(bool isValid)
        => LogicPreservationDecision(isValid).Match(_ => 0, _ => 1);

    private static Either<string, string> LogicPreservationDecision(bool isValid)
        => isValid
            ? Either<string, string>.FromRight("Logic preserved.")
            : Either<string, string>.FromLeft("Polished output diverges from original logic.");

    private static string DivergenceType(bool hasDiverged)
        => DivergenceDecision(hasDiverged).Match(_ => "logic_divergence", _ => string.Empty);

    private static string DivergenceDescription(bool hasDiverged)
        => DivergenceDecision(hasDiverged).Match(
            _ => "Polished output does not match the original logic representation.",
            _ => "No divergence detected.");

    private static string DivergenceSeverity(bool hasDiverged)
        => DivergenceDecision(hasDiverged).Match(_ => "fail_closed", _ => "none");

    private static IReadOnlyList<string> DivergenceSegments(bool hasDiverged)
        => DivergenceDecision(hasDiverged).Match<IReadOnlyList<string>>(_ => ["serialized_representation"], _ => []);

    private static Either<string, string> DivergenceDecision(bool hasDiverged)
        => hasDiverged
            ? Either<string, string>.FromLeft("diverged")
            : Either<string, string>.FromRight("preserved");

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
