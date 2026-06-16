namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

/// <summary>[EN] Documents this public package API member. [JA] ContextCollectionSnapshot を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextCollectionSnapshot']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextCollectionSnapshot']/summary" />
public sealed class ContextCollectionSnapshot : IContextCollection
{
    private readonly IReadOnlyList<ContextFragment> _fragments;

    /// <summary>[EN] Documents this public package API member. [JA] ContextCollectionSnapshot を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.#ctor']/summary" />
    public ContextCollectionSnapshot(IEnumerable<ContextFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        _fragments = fragments
            .OrderBy(x => x.Category)
            .ThenBy(x => x.FragmentId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetAll を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetAll']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetAll']/summary" />
    public IEnumerable<ContextFragment> GetAll()
    {
        return _fragments;
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetByCategory を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetByCategory']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetByCategory']/summary" />
    public IEnumerable<ContextFragment> GetByCategory(ContextCategory category)
    {
        return _fragments.Where(x => x.Category == category);
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetOrchestrationBuffer を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetOrchestrationBuffer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetOrchestrationBuffer']/summary" />
    public OrchestrationBuffer GetOrchestrationBuffer()
    {
        return new OrchestrationBuffer(GetByCategory(ContextCategory.Orchestration));
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetExpressionBuffer を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetExpressionBuffer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetExpressionBuffer']/summary" />
    public ExpressionBuffer GetExpressionBuffer()
    {
        return new ExpressionBuffer(GetByCategory(ContextCategory.Expression).OfType<ExpressionFragment>());
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetMaterialBuffer を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetMaterialBuffer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetMaterialBuffer']/summary" />
    public MaterialBuffer GetMaterialBuffer()
    {
        return new MaterialBuffer(GetByCategory(ContextCategory.Material));
    }

    /// <summary>[EN] Documents this public package API member. [JA] GetHistoryBuffer を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetHistoryBuffer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextCollectionSnapshot.GetHistoryBuffer']/summary" />
    public HistoryBuffer GetHistoryBuffer()
    {
        return new HistoryBuffer(GetByCategory(ContextCategory.History));
    }
}
