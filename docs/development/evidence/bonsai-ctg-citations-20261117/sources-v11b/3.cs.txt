using System.Collections.Immutable;

namespace AIKernel.Kernel.Inference;

/// <summary>A host reader's result for a required observation. Default values never establish availability.</summary>
public enum BonsaiCtgEvidenceAvailability
{
    /// <summary>Retrieval has not established whether this required observation is available.</summary>
    Unknown=0,
    /// <summary>The host reader obtained the observation supplied in InputFacts.</summary>
    Available=1,
    /// <summary>The host reader could not obtain this required observation.</summary>
    Unavailable=2
}

/// <summary>Immutable host-declared prerequisites for one proposed action. The host owns both the required-key list
/// and retrieval results; never deserialize these from model output or an untrusted client request.
/// This is an availability check, not proof that a supplied observation is true, current or authorized.</summary>
public sealed class BonsaiCtgEvidenceRequirements
{
    internal ImmutableSortedDictionary<string,BonsaiCtgEvidenceAvailability> Entries { get; }
    /// <summary>Copies 1..16 required fact keys and their host retrieval states. Unrelated observations need not be listed.</summary>
    public BonsaiCtgEvidenceRequirements(IReadOnlyDictionary<string,BonsaiCtgEvidenceAvailability> required)
    {
        ArgumentNullException.ThrowIfNull(required);
        var snapshot=required.ToImmutableSortedDictionary(StringComparer.Ordinal);
        if(snapshot.Count is <1 or >16||snapshot.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Key.Length>128||
            p.Value is not (BonsaiCtgEvidenceAvailability.Unknown or BonsaiCtgEvidenceAvailability.Available or BonsaiCtgEvidenceAvailability.Unavailable)))
            throw new ArgumentException("Bounded required observation keys and valid retrieval states are required.",nameof(required));
        Entries=snapshot;
    }
    internal string? Check(IReadOnlyDictionary<string,string> facts)
    {
        if(Entries.Any(p=>p.Value!=BonsaiCtgEvidenceAvailability.Available))return "ctg.bonsai.required_evidence_unavailable";
        if(Entries.Keys.Any(key=>!facts.TryGetValue(key,out var value)||string.IsNullOrWhiteSpace(value)))return "ctg.bonsai.required_evidence_missing";
        return null;
    }
}
