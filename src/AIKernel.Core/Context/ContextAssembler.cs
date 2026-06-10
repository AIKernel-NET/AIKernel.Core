namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Rom;
using AIKernel.Core.Rom;
using AIKernel.Core.Time;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssembler']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.ContextAssembler']/summary" />
public sealed class ContextAssembler : IContextAssembler
{
    private readonly IRomLoader _romLoader;
    private readonly IRomPathResolver _pathResolver;
    private readonly IContextAssemblyGovernancePolicy _governancePolicy;
    private readonly IContextCollectionFactory _contextCollectionFactory;
    private readonly IContextHashCalculator _hashCalculator;
    private readonly IKernelClock _clock;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.#ctor']/summary" />
    public ContextAssembler(
        IRomLoader romLoader,
        IRomPathResolver pathResolver,
        IContextAssemblyGovernancePolicy governancePolicy,
        IContextCollectionFactory contextCollectionFactory,
        IContextHashCalculator hashCalculator,
        IKernelClock? clock = null)
    {
        _romLoader = romLoader ?? throw new ArgumentNullException(nameof(romLoader));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _governancePolicy = governancePolicy ?? throw new ArgumentNullException(nameof(governancePolicy));
        _contextCollectionFactory = contextCollectionFactory ?? throw new ArgumentNullException(nameof(contextCollectionFactory));
        _hashCalculator = hashCalculator ?? throw new ArgumentNullException(nameof(hashCalculator));
        _clock = clock ?? KernelClock.System();
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AssembleAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AssembleAsync']/summary" />
    public async Task<IContextSnapshot> AssembleAsync(
        IVfsSession session,
        ContextAssemblyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaxDepth,
                "MaxDepth must be zero or greater.");
        }

        var state = new AssemblyState(request);

        await LoadGraphAsync(
            session,
            request.RootRomId,
            parentRomId: null,
            relationKind: "root",
            depth: 0,
            state,
            cancellationToken).ConfigureAwait(false);

        var orderedRoms = state.GetRomsInDeterministicOrder();
        var orderedEdges = state.GetEdgesInDeterministicOrder();

        var context = _contextCollectionFactory.Create(
            orderedRoms,
            orderedEdges,
            request.Scope);

        var contextHash = _hashCalculator.ComputeHash(
            request,
            orderedRoms,
            orderedEdges);

        return new AssembledContextSnapshot(
            snapshotId: $"ctx:{contextHash}",
            parentSnapshotId: request.ParentSnapshotId,
            createdAtUtc: _clock.Now,
            contextHash: contextHash,
            context: context);
    }

    private async Task LoadGraphAsync(
        IVfsSession session,
        RomId romId,
        RomId? parentRomId,
        string relationKind,
        int depth,
        AssemblyState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > state.Request.MaxDepth)
        {
            throw new ContextAssemblyException(
                $"Context assembly exceeded MaxDepth={state.Request.MaxDepth}. RomId='{romId.Value}'.");
        }

        if (parentRomId is not null)
        {
            state.AddEdge(new RomContextEdge(parentRomId, romId, relationKind));
        }

        if (state.IsLoaded(romId) || state.IsLoading(romId))
        {
            return;
        }

        state.MarkLoading(romId);

        string path;

        try
        {
            path = await _pathResolver
                .ResolvePathAsync(romId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ContextAssemblyException(
                $"ROM path could not be resolved. RomId='{romId.Value}'.",
                ex);
        }

        var rom = await _romLoader
            .LoadAsync(session, path, cancellationToken)
            .ConfigureAwait(false);

        if (rom.RomId != romId)
        {
            throw new RomIdentityMismatchException(
                requested: romId,
                actual: rom.RomId,
                path: path);
        }

        var decision = await _governancePolicy
            .EvaluateAsync(rom, state.Request.Scope, cancellationToken)
            .ConfigureAwait(false);

        if (!decision.IsAllowed)
        {
            throw new ContextAssemblyGovernanceException(rom.RomId, decision.Reason);
        }

        state.AddLoaded(rom);

        foreach (var relation in GetRelationsToFollow(rom, state.Request))
        {
            var targetRomId = RomIdFactory.Create(
                relation.TargetRomId,
                nameof(relation.TargetRomId));

            await LoadGraphAsync(
                session,
                targetRomId,
                parentRomId: rom.RomId,
                relationKind: relation.Kind,
                depth: depth + 1,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        state.MarkCompleted(romId);
    }

    private static IEnumerable<RomRelationSnapshot> GetRelationsToFollow(
        RomSnapshot rom,
        ContextAssemblyRequest request)
    {
        var relations = rom.Relations
            .OrderBy(x => x.TargetRomId, StringComparer.Ordinal)
            .ThenBy(x => x.Kind, StringComparer.Ordinal);

        if (request.RelationKindsToFollow.Count == 0)
        {
            return relations;
        }

        return relations.Where(x => request.RelationKindsToFollow.Contains(x.Kind));
    }

    private sealed class AssemblyState
    {
        private readonly Dictionary<RomId, RomSnapshot> _loaded = [];
        private readonly HashSet<RomId> _loading = [];
        private readonly List<RomContextEdge> _edges = [];

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AssemblyState']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AssemblyState']/summary" />
        public AssemblyState(ContextAssemblyRequest request)
        {
            Request = request;
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssembler.Request']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Context.ContextAssembler.Request']/summary" />
        public ContextAssemblyRequest Request { get; }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.IsLoaded']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.IsLoaded']/summary" />
        public bool IsLoaded(RomId romId) => _loaded.ContainsKey(romId);

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.IsLoading']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.IsLoading']/summary" />
        public bool IsLoading(RomId romId) => _loading.Contains(romId);

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.MarkLoading']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.MarkLoading']/summary" />
        public void MarkLoading(RomId romId)
        {
            _loading.Add(romId);
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.MarkCompleted']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.MarkCompleted']/summary" />
        public void MarkCompleted(RomId romId)
        {
            _loading.Remove(romId);
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AddLoaded']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AddLoaded']/summary" />
        public void AddLoaded(RomSnapshot rom)
        {
            _loaded.Add(rom.RomId, rom);
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AddEdge']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.AddEdge']/summary" />
        public void AddEdge(RomContextEdge edge)
        {
            if (!_edges.Contains(edge))
            {
                _edges.Add(edge);
            }
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.GetRomsInDeterministicOrder']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.GetRomsInDeterministicOrder']/summary" />
        public IReadOnlyList<RomSnapshot> GetRomsInDeterministicOrder()
        {
            return _loaded.Values
                .OrderBy(x => x.RomId.Value, StringComparer.Ordinal)
                .ToArray();
        }

        /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.GetEdgesInDeterministicOrder']/summary" />
        /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.ContextAssembler.GetEdgesInDeterministicOrder']/summary" />
        public IReadOnlyList<RomContextEdge> GetEdgesInDeterministicOrder()
        {
            return _edges
                .OrderBy(x => x.SourceRomId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.TargetRomId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.Kind, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
