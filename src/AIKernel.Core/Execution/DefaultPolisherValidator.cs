namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Execution;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultPolisherValidator']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultPolisherValidator']" />
public sealed class DefaultPolisherValidator : IPolisherValidator
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.ValidateLogicPreservationAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.ValidateLogicPreservationAsync']" />
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
            Message = isValid
                ? "Logic preserved."
                : "Polished output diverges from original logic.",
            Violations = isValid ? [] : ["logic_divergence"],
            LogicIntegrityScore = isValid ? 1 : 0
        });
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.AnalyzeDivergenceAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultPolisherValidator.AnalyzeDivergenceAsync']" />
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
            DivergenceType = hasDiverged ? "logic_divergence" : string.Empty,
            Description = hasDiverged
                ? "Polished output does not match the original logic representation."
                : "No divergence detected.",
            Severity = hasDiverged ? "fail_closed" : "none",
            AlteredSegments = hasDiverged ? ["serialized_representation"] : []
        });
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
