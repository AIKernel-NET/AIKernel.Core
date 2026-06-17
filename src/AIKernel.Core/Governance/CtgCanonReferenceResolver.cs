namespace AIKernel.Core.Governance;

using AIKernel.Dtos.Governance;

/// <summary>
/// EN: Carries canon references from a merged CTG ROM descriptor. JA: merge 済み CTG ROM descriptor からの正典参照を運びます。
/// </summary>
public sealed record CtgMergedRomDescriptor
{
    /// <summary>EN: Gets the root canon reference. JA: root canon reference を取得します。</summary>
    public CanonReference? CanonReference { get; init; }

    /// <summary>EN: Gets council canon references. JA: council canon reference を取得します。</summary>
    public IReadOnlyList<CanonReference> CouncilReferences { get; init; } = [];

    /// <summary>EN: Gets the decision gate canon reference. JA: decision gate canon reference を取得します。</summary>
    public CanonReference? DecisionGateReference { get; init; }

    /// <summary>EN: Gets the trajectory gate canon reference. JA: trajectory gate canon reference を取得します。</summary>
    public CanonReference? TrajectoryGateReference { get; init; }

    /// <summary>EN: Gets the reject policy canon reference. JA: reject policy canon reference を取得します。</summary>
    public CanonReference? RejectPolicyReference { get; init; }

    /// <summary>EN: Gets descriptor metadata. JA: descriptor metadata を取得します。</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// EN: Resolves and normalizes CTG canon references without creating missing rules. JA: 不足したルールを作らず CTG 正典参照を解決および正規化します。
/// </summary>
public sealed class CtgCanonReferenceResolver
{
    /// <summary>
    /// EN: Resolves canon references from a merged ROM descriptor. JA: merge 済み ROM descriptor から正典参照を解決します。
    /// </summary>
    /// <param name="descriptor">EN: The merged ROM descriptor. JA: merge 済み ROM descriptor です。</param>
    /// <returns>EN: The resolved canon references. JA: 解決された正典参照を返します。</returns>
    public IReadOnlyList<CanonReference> Resolve(CtgMergedRomDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return Resolve(
            EnumerateDescriptorReferences(descriptor));
    }

    /// <summary>
    /// EN: Resolves canon references from supplied carrier values. JA: 指定された carrier 値から正典参照を解決します。
    /// </summary>
    /// <param name="references">EN: The candidate references. JA: 候補参照です。</param>
    /// <returns>EN: The resolved canon references. JA: 解決された正典参照を返します。</returns>
    public IReadOnlyList<CanonReference> Resolve(IEnumerable<CanonReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var resolved = new List<CanonReference>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in references)
        {
            if (!TryNormalize(reference, out var normalized))
            {
                continue;
            }

            var key = CreateKey(normalized);

            if (seen.Add(key))
            {
                resolved.Add(normalized);
            }
        }

        return resolved;
    }

    /// <summary>
    /// EN: Attempts to normalize one canon reference. JA: 1 件の正典参照の正規化を試みます。
    /// </summary>
    /// <param name="reference">EN: The reference to normalize. JA: 正規化対象の参照です。</param>
    /// <param name="normalized">EN: The normalized reference. JA: 正規化された参照です。</param>
    /// <returns>EN: True when the reference has required identity and path fields. JA: 必須の識別子とパスを持つ場合は true を返します。</returns>
    public bool TryNormalize(
        CanonReference? reference,
        out CanonReference normalized)
    {
        normalized = new CanonReference();

        if (reference is null ||
            string.IsNullOrWhiteSpace(reference.CanonId) ||
            string.IsNullOrWhiteSpace(reference.Path))
        {
            return false;
        }

        normalized = new CanonReference
        {
            CanonId = reference.CanonId.Trim(),
            Path = reference.Path.Trim(),
            Section = reference.Section.Trim(),
            Anchor = string.IsNullOrWhiteSpace(reference.Anchor) ? null : reference.Anchor.Trim(),
            ContentHash = string.IsNullOrWhiteSpace(reference.ContentHash) ? null : reference.ContentHash.Trim()
        };

        return true;
    }

    private static IEnumerable<CanonReference> EnumerateDescriptorReferences(
        CtgMergedRomDescriptor descriptor)
    {
        if (descriptor.CanonReference is not null)
        {
            yield return descriptor.CanonReference;
        }

        foreach (var councilReference in descriptor.CouncilReferences ?? [])
        {
            yield return councilReference;
        }

        if (descriptor.DecisionGateReference is not null)
        {
            yield return descriptor.DecisionGateReference;
        }

        if (descriptor.TrajectoryGateReference is not null)
        {
            yield return descriptor.TrajectoryGateReference;
        }

        if (descriptor.RejectPolicyReference is not null)
        {
            yield return descriptor.RejectPolicyReference;
        }
    }

    private static string CreateKey(CanonReference reference)
    {
        return string.Join(
            "\u001f",
            reference.CanonId,
            reference.Path,
            reference.Section,
            reference.Anchor ?? string.Empty,
            reference.ContentHash ?? string.Empty);
    }
}
