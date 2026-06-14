namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Supplies immutable CTG canon references from a merged descriptor. JA: merge 済み descriptor から不変の CTG 正典参照を供給します。
/// </summary>
public sealed class CtgStaticCanonReferenceSource : ICtgCanonReferenceSource
{
    private readonly IReadOnlyList<CanonReference> _allReferences;
    private readonly IReadOnlyList<CanonReference> _decisionGateReferences;
    private readonly IReadOnlyList<CanonReference> _trajectoryGateReferences;

    /// <summary>
    /// EN: Initializes an empty source. JA: 空の source を初期化します。
    /// </summary>
    public CtgStaticCanonReferenceSource()
        : this([], [], [])
    {
    }

    /// <summary>
    /// EN: Initializes a source from a merged descriptor. JA: merge 済み descriptor から source を初期化します。
    /// </summary>
    /// <param name="descriptor">EN: The merged ROM descriptor. JA: merge 済み ROM descriptor です。</param>
    public CtgStaticCanonReferenceSource(CtgMergedRomDescriptor descriptor)
        : this(descriptor, new CtgCanonReferenceResolver())
    {
    }

    /// <summary>
    /// EN: Initializes a source from a merged descriptor and resolver. JA: merge 済み descriptor と resolver から source を初期化します。
    /// </summary>
    /// <param name="descriptor">EN: The merged ROM descriptor. JA: merge 済み ROM descriptor です。</param>
    /// <param name="resolver">EN: The canon reference resolver. JA: 正典参照 resolver です。</param>
    public CtgStaticCanonReferenceSource(
        CtgMergedRomDescriptor descriptor,
        CtgCanonReferenceResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(resolver);

        _allReferences = resolver.Resolve(descriptor);
        _decisionGateReferences = resolver.Resolve(EnumerateSingle(descriptor.DecisionGateReference));
        _trajectoryGateReferences = resolver.Resolve(EnumerateSingle(descriptor.TrajectoryGateReference));
    }

    /// <summary>
    /// EN: Initializes a source from explicit reference sets. JA: 明示された参照セットから source を初期化します。
    /// </summary>
    /// <param name="allReferences">EN: All known references. JA: 既知のすべての参照です。</param>
    /// <param name="decisionGateReferences">EN: Decision gate references. JA: 決定ゲート参照です。</param>
    /// <param name="trajectoryGateReferences">EN: Trajectory gate references. JA: 軌道ゲート参照です。</param>
    public CtgStaticCanonReferenceSource(
        IReadOnlyList<CanonReference> allReferences,
        IReadOnlyList<CanonReference> decisionGateReferences,
        IReadOnlyList<CanonReference> trajectoryGateReferences)
    {
        _allReferences = allReferences ?? throw new ArgumentNullException(nameof(allReferences));
        _decisionGateReferences = decisionGateReferences ?? throw new ArgumentNullException(nameof(decisionGateReferences));
        _trajectoryGateReferences = trajectoryGateReferences ?? throw new ArgumentNullException(nameof(trajectoryGateReferences));
    }

    /// <summary>
    /// EN: Gets references for decision gate requests. JA: 決定ゲート要求用の参照を取得します。
    /// </summary>
    /// <returns>EN: Decision gate canon references. JA: 決定ゲートの正典参照を返します。</returns>
    public IReadOnlyList<CanonReference> GetDecisionGateReferences()
    {
        return _decisionGateReferences;
    }

    /// <summary>
    /// EN: Gets references for trajectory gate requests. JA: 軌道ゲート要求用の参照を取得します。
    /// </summary>
    /// <returns>EN: Trajectory gate canon references. JA: 軌道ゲートの正典参照を返します。</returns>
    public IReadOnlyList<CanonReference> GetTrajectoryGateReferences()
    {
        return _trajectoryGateReferences;
    }

    /// <summary>
    /// EN: Gets all known CTG canon references. JA: 既知のすべての CTG 正典参照を取得します。
    /// </summary>
    /// <returns>EN: All canon references. JA: すべての正典参照を返します。</returns>
    public IReadOnlyList<CanonReference> GetAllReferences()
    {
        return _allReferences;
    }

    private static IEnumerable<CanonReference> EnumerateSingle(
        CanonReference? reference)
    {
        if (reference is not null)
        {
            yield return reference;
        }
    }
}
