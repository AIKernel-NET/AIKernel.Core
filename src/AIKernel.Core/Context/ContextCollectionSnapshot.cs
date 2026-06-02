namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Enums;

public sealed class ContextCollectionSnapshot : IContextCollection
{
    private readonly IReadOnlyList<ContextFragment> _fragments;

    public ContextCollectionSnapshot(IEnumerable<ContextFragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        _fragments = fragments
            .OrderBy(x => x.Category)
            .ThenBy(x => x.FragmentId, StringComparer.Ordinal)
            .ToArray();
    }

    public IEnumerable<ContextFragment> GetAll()
    {
        return _fragments;
    }

    public IEnumerable<ContextFragment> GetByCategory(ContextCategory category)
    {
        return _fragments.Where(x => x.Category == category);
    }

    public OrchestrationBuffer GetOrchestrationBuffer()
    {
        return new OrchestrationBuffer(GetByCategory(ContextCategory.Orchestration));
    }

    public ExpressionBuffer GetExpressionBuffer()
    {
        return new ExpressionBuffer(GetByCategory(ContextCategory.Expression).OfType<ExpressionFragment>());
    }

    public MaterialBuffer GetMaterialBuffer()
    {
        return new MaterialBuffer(GetByCategory(ContextCategory.Material));
    }

    public HistoryBuffer GetHistoryBuffer()
    {
        return new HistoryBuffer(GetByCategory(ContextCategory.History));
    }
}
