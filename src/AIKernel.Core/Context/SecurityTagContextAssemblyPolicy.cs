namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Rom;

public sealed class SecurityTagContextAssemblyPolicy : IContextAssemblyGovernancePolicy
{
    private readonly IReadOnlySet<string> _allowedSecurityTags;

    public SecurityTagContextAssemblyPolicy(IEnumerable<string> allowedSecurityTags)
    {
        ArgumentNullException.ThrowIfNull(allowedSecurityTags);

        _allowedSecurityTags = allowedSecurityTags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    public ValueTask<ContextAssemblyDecision> EvaluateAsync(
        RomSnapshot rom,
        ContextAssemblyScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(scope);

        cancellationToken.ThrowIfCancellationRequested();

        if (rom.SecurityTags.Length == 0)
        {
            return ValueTask.FromResult(
                ContextAssemblyDecision.Deny("ROM has no security tags."));
        }

        var deniedTags = rom.SecurityTags
            .Where(tag => !_allowedSecurityTags.Contains(tag))
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        if (deniedTags.Length > 0)
        {
            return ValueTask.FromResult(
                ContextAssemblyDecision.Deny(
                    $"ROM contains denied security tags: {string.Join(", ", deniedTags)}"));
        }

        return ValueTask.FromResult(ContextAssemblyDecision.Allow());
    }
}
