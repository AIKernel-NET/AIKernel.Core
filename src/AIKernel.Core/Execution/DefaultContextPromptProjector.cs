namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultContextPromptProjector']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.DefaultContextPromptProjector']/summary" />
public sealed class DefaultContextPromptProjector : IContextPromptProjector
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultContextPromptProjector.Project']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.DefaultContextPromptProjector.Project']/summary" />
    public IReadOnlyList<ContextPromptBlock> Project(
        IContextSnapshot snapshot,
        PromptProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Context.GetAll()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.FragmentId, StringComparer.Ordinal)
            .Select(fragment => new ContextPromptBlock(
                Id: fragment.FragmentId,
                Category: fragment.Category.ToString(),
                Content: fragment.Content,
                Priority: ResolvePriority(fragment.Category),
                Metadata: options.IncludeSourceMetadata
                    ? fragment.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)))
            .ToArray();
    }

    private static int ResolvePriority(ContextCategory category)
    {
        return category switch
        {
            ContextCategory.Governance => 1000,
            ContextCategory.Orchestration => 900,
            ContextCategory.Material => 700,
            ContextCategory.History => 500,
            ContextCategory.Expression => 100,
            _ => 300
        };
    }
}
