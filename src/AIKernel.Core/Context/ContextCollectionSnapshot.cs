namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextCollectionSnapshot']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextCollectionSnapshot']" />
public sealed class ContextCollectionSnapshot : IContextCollection
{
    private readonly IReadOnlyList<ContextFragment> _fragments;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.#ctor']" />
    public ContextCollectionSnapshot(IEnumerable<ContextFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        _fragments = fragments
            .OrderBy(x => x.Category)
            .ThenBy(x => x.FragmentId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetAll']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetAll']" />
    public IEnumerable<ContextFragment> GetAll()
    {
        return _fragments;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetByCategory']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetByCategory']" />
    public IEnumerable<ContextFragment> GetByCategory(ContextCategory category)
    {
        return _fragments.Where(x => x.Category == category);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetOrchestrationBuffer']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetOrchestrationBuffer']" />
    public OrchestrationBuffer GetOrchestrationBuffer()
    {
        return new OrchestrationBuffer(GetByCategory(ContextCategory.Orchestration));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetExpressionBuffer']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetExpressionBuffer']" />
    public ExpressionBuffer GetExpressionBuffer()
    {
        return new ExpressionBuffer(GetByCategory(ContextCategory.Expression).OfType<ExpressionFragment>());
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetMaterialBuffer']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetMaterialBuffer']" />
    public MaterialBuffer GetMaterialBuffer()
    {
        return new MaterialBuffer(GetByCategory(ContextCategory.Material));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetHistoryBuffer']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetHistoryBuffer']" />
    public HistoryBuffer GetHistoryBuffer()
    {
        return new HistoryBuffer(GetByCategory(ContextCategory.History));
    }
}
