namespace AIKernel.Core.Concepts;

/// <summary>
/// [Intent layer - Telos / テロス]
/// [EN] Concept facade for goal, intent, and objective responsibility.
/// [JA] goal / intent / objective の責務を表す概念 facade です。
/// Old technical name: Goal / Intent / Objective.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or Provider implementation names.
/// </summary>
public sealed class TelosObjective
{
    /// <summary>
    /// [EN] Initializes a stable objective concept.
    /// [JA] 安定した objective concept を初期化します。
    /// </summary>
    public TelosObjective(string objectiveId, string description)
    {
        ObjectiveId = string.IsNullOrWhiteSpace(objectiveId)
            ? throw new ArgumentException("Objective id is required.", nameof(objectiveId))
            : objectiveId;
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("Objective description is required.", nameof(description))
            : description;
    }

    /// <summary>[EN] Stable objective identifier. [JA] 安定した objective 識別子です。</summary>
    public string ObjectiveId { get; }

    /// <summary>[EN] Human-readable objective description. [JA] 人間可読な objective description です。</summary>
    public string Description { get; }

    /// <summary>
    /// [EN] Returns whether this objective semantically matches the supplied id.
    /// [JA] 指定 id がこの objective と意味的に一致するかを返します。
    /// </summary>
    public bool Matches(string objectiveId)
        => string.Equals(ObjectiveId, objectiveId, StringComparison.Ordinal);
}

/// <summary>
/// [Canonical layer - Nomos / ノモス]
/// [EN] Concept facade for ROM, Canon, and rule-set responsibility.
/// [JA] ROM / Canon / rule-set の責務を表す概念 facade です。
/// Old technical name: Policy / RuleSet / Canon.
/// Do not use this term for DTO, Request, Result, Serializer, or Provider implementation names.
/// </summary>
public sealed class NomosCanon
{
    /// <summary>
    /// [EN] Initializes a canon concept with deterministic rule references.
    /// [JA] 決定論的な rule reference を持つ canon concept を初期化します。
    /// </summary>
    public NomosCanon(string canonId, IEnumerable<string> ruleReferences)
    {
        CanonId = string.IsNullOrWhiteSpace(canonId)
            ? throw new ArgumentException("Canon id is required.", nameof(canonId))
            : canonId;
        RuleReferences = ruleReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>[EN] Stable canon identifier. [JA] 安定した canon 識別子です。</summary>
    public string CanonId { get; }

    /// <summary>[EN] Deterministically ordered rule references. [JA] 決定論的に整列された rule reference です。</summary>
    public IReadOnlyList<string> RuleReferences { get; }

    /// <summary>
    /// [EN] Returns whether a rule reference is contained in this canon concept.
    /// [JA] rule reference がこの canon concept に含まれるかを返します。
    /// </summary>
    public bool ContainsRule(string ruleReference)
        => RuleReferences.Contains(ruleReference, StringComparer.Ordinal);
}

/// <summary>
/// [Governance layer - Dike / ディケー]
/// [EN] Concept facade for justice, order, and safety-boundary responsibility.
/// [JA] justice / order / safety boundary の責務を表す概念 facade です。
/// Old technical name: SafetyGate / PolicyBoundary.
/// Do not use this term for DTO, Request, Result, Mapper, or Provider implementation names.
/// </summary>
public sealed class DikeSafetyBoundary
{
    /// <summary>
    /// [EN] Initializes a safety boundary concept.
    /// [JA] safety boundary concept を初期化します。
    /// </summary>
    public DikeSafetyBoundary(string boundaryId, IEnumerable<string> requirements)
    {
        BoundaryId = string.IsNullOrWhiteSpace(boundaryId)
            ? throw new ArgumentException("Boundary id is required.", nameof(boundaryId))
            : boundaryId;
        Requirements = requirements
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>[EN] Stable safety boundary identifier. [JA] 安定した safety boundary 識別子です。</summary>
    public string BoundaryId { get; }

    /// <summary>[EN] Safety requirements owned by this boundary. [JA] この boundary が所有する safety requirement です。</summary>
    public IReadOnlyList<string> Requirements { get; }

    /// <summary>
    /// [EN] Returns whether this boundary requires the supplied safety condition.
    /// [JA] 指定された safety condition がこの boundary で要求されるかを返します。
    /// </summary>
    public bool Requires(string requirement)
        => Requirements.Contains(requirement, StringComparer.Ordinal);
}

/// <summary>
/// [Governance layer - Ethos / エトス]
/// [EN] Concept facade for ethical, safety, and normative council responsibility.
/// [JA] ethical / safety / normative council の責務を表す概念 facade です。
/// Old technical name: EthicalPolicyCouncil.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or Provider implementation names.
/// </summary>
public sealed class EthosCouncil
{
    /// <summary>[EN] Stable council responsibility. [JA] 安定した council responsibility です。</summary>
    public string Responsibility => "Ethics, safety, and norms";
}

/// <summary>
/// [Governance layer - Pathos / パトス]
/// [EN] Concept facade for risk, anomaly, and intuitive danger-signal responsibility.
/// [JA] risk / anomaly / intuitive danger signal の責務を表す概念 facade です。
/// Old technical name: RiskSignalCouncil.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or Provider implementation names.
/// </summary>
public sealed class PathosCouncil
{
    /// <summary>[EN] Stable council responsibility. [JA] 安定した council responsibility です。</summary>
    public string Responsibility => "Risk, anomaly, and danger signals";
}

/// <summary>
/// [Governance layer - Logos / ロゴス]
/// [EN] Concept facade for logic, consistency, and verification responsibility.
/// [JA] logic / consistency / verification の責務を表す概念 facade です。
/// Old technical name: LogicalConsistencyCouncil.
/// Do not use this term for DTO, Mapper, Adapter, Serializer, or Provider implementation names.
/// </summary>
public sealed class LogosCouncil
{
    /// <summary>[EN] Stable council responsibility. [JA] 安定した council responsibility です。</summary>
    public string Responsibility => "Logic, consistency, and verification";
}
