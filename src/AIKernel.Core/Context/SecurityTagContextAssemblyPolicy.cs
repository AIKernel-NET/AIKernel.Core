namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy']" />
public sealed class SecurityTagContextAssemblyPolicy : IContextAssemblyGovernancePolicy
{
    private readonly IReadOnlySet<string> _allowedSecurityTags;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy.#ctor']" />
    public SecurityTagContextAssemblyPolicy(IEnumerable<string> allowedSecurityTags)
    {
        ArgumentNullException.ThrowIfNull(allowedSecurityTags);

        _allowedSecurityTags = allowedSecurityTags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy.EvaluateAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.SecurityTagContextAssemblyPolicy.EvaluateAsync']" />
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
                new ContextAssemblyDecision(false, "ROM has no security tags."));
        }

        var deniedTags = rom.SecurityTags
            .Where(tag => !_allowedSecurityTags.Contains(tag))
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        if (deniedTags.Length > 0)
        {
            return ValueTask.FromResult(
                new ContextAssemblyDecision(
                    false,
                    $"ROM contains denied security tags: {string.Join(", ", deniedTags)}"));
        }

        return ValueTask.FromResult(new ContextAssemblyDecision(true));
    }
}
