namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Supplies CTG canon references to Core governance service requests. JA: Core 統治サービス要求へ CTG 正典参照を供給します。
/// </summary>
public interface ICtgCanonReferenceSource
{
    /// <summary>
    /// EN: Gets references for decision gate requests. JA: 決定ゲート要求用の参照を取得します。
    /// </summary>
    /// <returns>EN: Decision gate canon references. JA: 決定ゲートの正典参照を返します。</returns>
    IReadOnlyList<CanonReference> GetDecisionGateReferences();

    /// <summary>
    /// EN: Gets references for trajectory gate requests. JA: 軌道ゲート要求用の参照を取得します。
    /// </summary>
    /// <returns>EN: Trajectory gate canon references. JA: 軌道ゲートの正典参照を返します。</returns>
    IReadOnlyList<CanonReference> GetTrajectoryGateReferences();

    /// <summary>
    /// EN: Gets all known CTG canon references. JA: 既知のすべての CTG 正典参照を取得します。
    /// </summary>
    /// <returns>EN: All canon references. JA: すべての正典参照を返します。</returns>
    IReadOnlyList<CanonReference> GetAllReferences();
}
