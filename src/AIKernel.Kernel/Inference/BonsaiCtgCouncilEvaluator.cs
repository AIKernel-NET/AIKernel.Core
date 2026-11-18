using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AIKernel.Abstractions.Governance;
using AIKernel.Core.Governance;
using AIKernel.Core.Time;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;

namespace AIKernel.Kernel.Inference;

/// <summary>Immutable council rule text verified against an operator-pinned canonical ROM digest.</summary>
public sealed class BonsaiCouncilRule
{
    internal string Text { get; }
    /// <summary>The full source and digest used by this rule.</summary>
    public CanonReference Reference { get; }
    private BonsaiCouncilRule(string text,CanonReference reference){Text=text;Reference=reference;}
    /// <summary>Accepts bytes supplied by the host's canonical VFS/ROM loader. Does not fetch arbitrary paths.</summary>
    public static BonsaiCouncilRule FromUtf8(ReadOnlySpan<byte> utf8,CanonReference expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if(utf8.Length is <1 or >32768 || string.IsNullOrWhiteSpace(expected.CanonId) || string.IsNullOrWhiteSpace(expected.Path) ||
            expected.ContentHash is not {Length:64} ||
            !string.Equals(Convert.ToHexStringLower(SHA256.HashData(utf8)),expected.ContentHash,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Council ROM identity or digest mismatch.");
        string text=new UTF8Encoding(false,true).GetString(utf8);
        if(text.Length>6500 || text.Contains("<|",StringComparison.Ordinal))throw new InvalidDataException("Council ROM exceeds the prompt contract or contains model control delimiters.");
        var ids=text.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.StartsWith("ID:",StringComparison.Ordinal)).ToArray();
        if(ids.Length!=1||ids[0]!="ID: "+expected.CanonId)throw new InvalidDataException("Council ROM header identity mismatch or duplication.");
        return new(text,expected);
    }
}

/// <summary>Evaluation evidence plus the existing deterministic CTG gate. This is not an RH cryptographic proof.</summary>
public sealed record BonsaiCtgDecision(CouncilEvaluationResult Evaluation,DecisionGateResult Gate);

/// <summary>Experimental model response contracts. Neither mode establishes authorization or RH verification.</summary>
internal enum BonsaiCtgResponseMode
{
    /// <summary>Preserves the existing reason/vote contract and its known semantic limitations.</summary>
    DirectVotes,
    /// <summary>Opt-in condition classification with a reviewed host binding; semantic regressions remain.</summary>
    CanonicalFindings,
    /// <summary>Validation-only observation-first voting; failed semantic admission.</summary>
    GroundedDirectVotes,
    /// <summary>Validation-only comparison of actual claims with observations.</summary>
    ComparativeDirectVotes,
    /// <summary>Validation-only v4 instruction preservation with observations before the proposal.</summary>
    ObservationFirstDirectVotes,
    /// <summary>Validation-only readable JSON punctuation with the unchanged v4 instructions.</summary>
    ReadableDirectVotes,
    /// <summary>Validation-only exact Pathos interaction section; Logos and Ethos retain v4.</summary>
    PathosScopeDirectVotes,
    /// <summary>Validation-only Pathos scope gated by mandatory host-owned evidence requirements.</summary>
    EvidenceScopedDirectVotes,
    /// <summary>Validation-only exact observation citations with unchanged model-selected votes.</summary>
    CitedDirectVotes,
    /// <summary>Validation-only cited findings using the reviewed canonical finding-to-vote binding.</summary>
    CitedFindings,
    /// <summary>Validation-only compact proposal packet with separate candidate and observation fields.</summary>
    PacketDirectVotes,
    /// <summary>Validation-only balanced applications of the pinned canonical criteria.</summary>
    DemonstratedPacketVotes,
    /// <summary>Validation-only draft and review; only the reviewed assessment becomes a council vote.</summary>
    ReviewedPacketVotes,
    /// <summary>Validation-only exact interaction sections for all three councils.</summary>
    CouncilScopedDirectVotes,
    /// <summary>Validation-only provenance records and explicit negation/condition reading.</summary>
    ProvenanceDirectVotes,
    /// <summary>Validation-only separate findings for every canonical criterion.</summary>
    PerCriterionFindings,
    /// <summary>Validation-only open reading aid before the three canonical assessments.</summary>
    FocusedReadingVotes,
    /// <summary>Validation-only reading with the exact core responsibility definitions restored.</summary>
    DefinedReadingVotes,
    /// <summary>Validation-only reading of the actual action and its observation-supported effects.</summary>
    ActionEffectReadingVotes,
    /// <summary>Validation-only vote generation before explanatory prose.</summary>
    VoteFirstReadingVotes,
    /// <summary>Validation-only exact observation followed by vote and reason.</summary>
    AnchoredReadingVotes,
    /// <summary>Validation-only fixed council protocols with independent Pathos reasoning about impact.</summary>
    RoutedReadingVotes,
    /// <summary>Validation-only literal comparison of the proposed report against observation text.</summary>
    ComparedReadingVotes,
    /// <summary>Validation-only canonical findings for reports with a literal comparison.</summary>
    RecordFindingVotes,
    /// <summary>Validation-only model classification using the same council's independently generated explanation.</summary>
    ReasonedFindingVotes,
    /// <summary>Validation only. Allows one bounded replacement for a token-limited council explanation.</summary>
    CompletedReasonedFindingVotes,
    /// <summary>Validation only. Assess the directed edit between two source-bound literals.</summary>
    SourceBoundRecordVotes,
    /// <summary>Validation only. Recheck a supported report with a demonstrably different field value.</summary>
    ChallengedRecordVotes,
    /// <summary>Validation only. Keep the original proposal when an action summary drops the identified field.</summary>
    FieldBoundReadingVotes,
    /// <summary>Validation only. Challenge explicit, source-bound exact reporting requests.</summary>
    ScopedReportVotes,
    /// <summary>Validation only. Request a concise new explanation if a truncated draft already exceeds the input limit.</summary>
    ConciseReportRetryVotes
}

/// <summary>Experimental Bonsai 1.7B council observations; canonical voting fidelity is not established.
/// The three roles share one model and are not independent judges. Never use as an authorization authority.</summary>
public sealed class BonsaiCtgCouncilEvaluator : ICouncilEvaluator,IDisposable
{
    private static readonly CouncilKind[] Councils=[CouncilKind.Logos,CouncilKind.Ethos,CouncilKind.Pathos];
    // Keep natural-language facts readable while still escaping HTML/model-control delimiters.
    private static readonly JsonSerializerOptions FactsJson=new(){Encoder=JavaScriptEncoder.Create(UnicodeRanges.All)};
    private readonly Func<string,string,CancellationToken,Task<CpuGeneration>> classify;
    private readonly Func<string,string,IReadOnlyList<string>,CancellationToken,Task<CpuGeneration>>? select;
    private readonly Func<string,string,CancellationToken,Task<CpuGeneration>>? retryReason;
    private readonly ImmutableDictionary<CouncilKind,BonsaiCouncilRule> rules;
    private readonly IKernelClock clock;
    private readonly string modelHash;
    private readonly Qwen3CpuModel? model;
    private readonly BonsaiCtgResponseMode responseMode;
    private readonly TimeSpan evaluationTimeout;
    private readonly SemaphoreSlim gate=new(1,1);
    private bool disposed;
    // Offline validation only. Production traces retain hashes instead of model text that may repeat private facts.
    internal Action<CouncilKind,CpuGeneration>? Observation { get; set; }
    // Offline validation only; a draft never enters the decision gate or grants permission.
    internal Action<CouncilKind,CpuGeneration>? ProvisionalObservation { get; set; }
    internal Action<CouncilKind,string,CpuGeneration>? CriterionObservation { get; set; }
    internal Action<CpuGeneration>? ReadingObservation { get; set; }
    /// <summary>Host-owned canonical prefix payloads; read between requests. No request facts are cached.</summary>
    public Qwen3PrefixCacheStatistics? PrefixCacheStatistics=>model?.PrefixCacheStatistics;
    /// <summary>Loads the operator-pinned 1.7B checkpoint. The caller supplies verified canonical council ROMs.</summary>
    public BonsaiCtgCouncilEvaluator(string modelPath,string expectedModelSha256,IReadOnlyDictionary<CouncilKind,BonsaiCouncilRule> rules,
        int cpuThreads=4,IKernelClock? clock=null,TimeSpan? evaluationTimeout=null,long maximumPrefixCacheBytes=512L<<20)
        :this(modelPath,expectedModelSha256,rules,BonsaiCtgResponseMode.DirectVotes,cpuThreads,clock,evaluationTimeout,maximumPrefixCacheBytes){}
    // Findings failed the semantic admission check. Keep the comparison path internal to the validation harness.
    internal BonsaiCtgCouncilEvaluator(string modelPath,string expectedModelSha256,IReadOnlyDictionary<CouncilKind,BonsaiCouncilRule> rules,
        BonsaiCtgResponseMode responseMode,int cpuThreads=4,IKernelClock? clock=null,TimeSpan? evaluationTimeout=null,long maximumPrefixCacheBytes=512L<<20)
    {
        this.rules=ValidateRules(rules,responseMode);this.responseMode=responseMode;this.clock=clock??KernelClock.System();
        this.evaluationTimeout=ValidateTimeout(evaluationTimeout);
        if(maximumPrefixCacheBytes<0||maximumPrefixCacheBytes>2L<<30)throw new ArgumentOutOfRangeException(nameof(maximumPrefixCacheBytes));
        using(var stream=File.OpenRead(modelPath))modelHash=Convert.ToHexStringLower(SHA256.HashData(stream));
        if(expectedModelSha256 is not {Length:64} || !string.Equals(modelHash,expectedModelSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bonsai model digest mismatch.");
        var info=GgufModelInspector.Inspect(modelPath);
        if(info.Architecture!="qwen3" || info.StoredParameters is <1_500_000_000 or >2_000_000_000)throw new NotSupportedException("This evaluator requires the pinned Bonsai 1.7B family.");
        model=new(modelPath,cpuThreads,2048,"auto");model.ConfigureTrustedPrefixCache(ReadingContract(responseMode) is null?3:4,maximumPrefixCacheBytes);
        select=model.ClassifyAsync;
        if (UsesCompletedReasons(responseMode))
            retryReason = (system, user, token) => model.GenerateAsync(system, user, 256, token);
        classify=(system,user,token)=>model.GenerateAsync(system,user,responseMode is BonsaiCtgResponseMode.CitedDirectVotes or BonsaiCtgResponseMode.CitedFindings or BonsaiCtgResponseMode.CanonicalFindings?256:192,token);
    }
    internal BonsaiCtgCouncilEvaluator(Func<string,string,CancellationToken,Task<CpuGeneration>> classify,IReadOnlyDictionary<CouncilKind,BonsaiCouncilRule> rules,IKernelClock? clock=null,
        BonsaiCtgResponseMode responseMode=BonsaiCtgResponseMode.DirectVotes,
        Func<string,string,IReadOnlyList<string>,CancellationToken,Task<CpuGeneration>>? select=null,
        Func<string,string,CancellationToken,Task<CpuGeneration>>? retryReason=null)
    {this.classify=classify;this.select=select;this.retryReason=retryReason;this.rules=ValidateRules(rules,responseMode);this.responseMode=responseMode;this.clock=clock??KernelClock.System();modelHash="test-double";evaluationTimeout=TimeSpan.FromMinutes(3);}
    private static TimeSpan ValidateTimeout(TimeSpan? value)
    {
        var timeout=value??TimeSpan.FromMinutes(3);
        if(timeout<TimeSpan.FromMilliseconds(1)||timeout>TimeSpan.FromMinutes(30))throw new ArgumentOutOfRangeException(nameof(value));
        return timeout;
    }
    internal static string? ReadingContract(BonsaiCtgResponseMode mode) => mode switch
    {
        BonsaiCtgResponseMode.FocusedReadingVotes => BonsaiCtgFocusedPrompt.Contract,
        BonsaiCtgResponseMode.DefinedReadingVotes => BonsaiCtgFocusedPrompt.DefinedContract,
        BonsaiCtgResponseMode.ActionEffectReadingVotes => BonsaiCtgFocusedPrompt.EffectsContract,
        BonsaiCtgResponseMode.VoteFirstReadingVotes => BonsaiCtgFocusedPrompt.VoteFirstContract,
        BonsaiCtgResponseMode.AnchoredReadingVotes => BonsaiCtgAnchoredVotePrompt.Contract,
        BonsaiCtgResponseMode.RoutedReadingVotes => BonsaiCtgFocusedPrompt.RoutedContract,
        BonsaiCtgResponseMode.ComparedReadingVotes => BonsaiCtgFocusedPrompt.ComparisonContract,
        BonsaiCtgResponseMode.RecordFindingVotes => BonsaiCtgRecordFindingPrompt.Contract,
        BonsaiCtgResponseMode.CompletedReasonedFindingVotes => "canonical-completed-reasoned-finding-v29",
        BonsaiCtgResponseMode.SourceBoundRecordVotes => "canonical-source-bound-record-v30",
        BonsaiCtgResponseMode.ChallengedRecordVotes => "canonical-challenged-record-v31",
        BonsaiCtgResponseMode.FieldBoundReadingVotes => "canonical-field-bound-reading-v32",
        BonsaiCtgResponseMode.ScopedReportVotes => "canonical-scoped-report-v33",
        BonsaiCtgResponseMode.ConciseReportRetryVotes => "canonical-concise-report-retry-v34",
        BonsaiCtgResponseMode.ReasonedFindingVotes => BonsaiCtgReasonedFindingPrompt.Contract,
        _ => null
    };

    internal static bool UsesReportChallenge(BonsaiCtgResponseMode mode) => mode is
        BonsaiCtgResponseMode.ChallengedRecordVotes or BonsaiCtgResponseMode.FieldBoundReadingVotes or
        BonsaiCtgResponseMode.ScopedReportVotes or BonsaiCtgResponseMode.ConciseReportRetryVotes;

    internal static bool UsesCompletedReasons(BonsaiCtgResponseMode mode) =>
        mode == BonsaiCtgResponseMode.CompletedReasonedFindingVotes || UsesReportChallenge(mode);

    private static ImmutableDictionary<CouncilKind,BonsaiCouncilRule> ValidateRules(IReadOnlyDictionary<CouncilKind,BonsaiCouncilRule> rules,BonsaiCtgResponseMode mode)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if(ReadingContract(mode) is null && mode is not (BonsaiCtgResponseMode.DirectVotes or BonsaiCtgResponseMode.CanonicalFindings or BonsaiCtgResponseMode.GroundedDirectVotes or BonsaiCtgResponseMode.ComparativeDirectVotes or BonsaiCtgResponseMode.ObservationFirstDirectVotes or BonsaiCtgResponseMode.ReadableDirectVotes or BonsaiCtgResponseMode.PathosScopeDirectVotes or BonsaiCtgResponseMode.EvidenceScopedDirectVotes or BonsaiCtgResponseMode.CitedDirectVotes or BonsaiCtgResponseMode.CitedFindings or BonsaiCtgResponseMode.PacketDirectVotes or BonsaiCtgResponseMode.DemonstratedPacketVotes or BonsaiCtgResponseMode.ReviewedPacketVotes or BonsaiCtgResponseMode.CouncilScopedDirectVotes or BonsaiCtgResponseMode.ProvenanceDirectVotes or BonsaiCtgResponseMode.PerCriterionFindings))throw new ArgumentOutOfRangeException(nameof(mode));
        if(rules.Count!=3||Councils.Any(c=>!rules.TryGetValue(c,out var rule)||rule is null||!rule.Reference.CanonId.EndsWith(".Council."+c,StringComparison.Ordinal)))throw new ArgumentException("All three canonical council rules are required.");
        if(mode is not BonsaiCtgResponseMode.DirectVotes)foreach(var council in Councils)BonsaiCtgFindingPolicy.RequireSupported(rules[council],council);
        return rules.ToImmutableDictionary();
    }
    /// <summary>Evaluates bounded facts as untrusted data. Errors produce Unknown votes; cancellation propagates.</summary>
    public ValueTask<CouncilEvaluationResult> EvaluateAsync(CouncilEvaluationRequest request,CancellationToken cancellationToken)
        =>EvaluateCoreAsync(request,null,false,cancellationToken);
    /// <summary>Evaluates an unverified proposal separately from request.InputFacts, which must contain supporting observations only.
    /// Does not extract a proposal from fact names, establish the truth of supplied facts, or authorize an operation.</summary>
    public ValueTask<CouncilEvaluationResult> EvaluateProposalAsync(CouncilEvaluationRequest request,string proposal,CancellationToken cancellationToken=default)
        =>EvaluateCoreAsync(request,proposal,true,cancellationToken);
    /// <summary>Checks host-owned required observations before evaluating a separate proposal. Failed retrieval is an
    /// evaluation failure (all Unknown), not a model vote. Availability alone never approves the proposal.</summary>
    public ValueTask<CouncilEvaluationResult> EvaluateProposalWithEvidenceAsync(CouncilEvaluationRequest request,string proposal,
        BonsaiCtgEvidenceRequirements evidence,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return EvaluateCoreAsync(request,proposal,true,cancellationToken,evidence);
    }
    private async ValueTask<CouncilEvaluationResult> EvaluateCoreAsync(CouncilEvaluationRequest request,string? proposal,bool separated,CancellationToken cancellationToken,
        BonsaiCtgEvidenceRequirements? evidence=null)
    {
        ArgumentNullException.ThrowIfNull(request);cancellationToken.ThrowIfCancellationRequested();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            bool findings=responseMode==BonsaiCtgResponseMode.CanonicalFindings;
            bool comparative=responseMode==BonsaiCtgResponseMode.ComparativeDirectVotes;
            bool ordered=responseMode==BonsaiCtgResponseMode.ObservationFirstDirectVotes;
            bool readable=responseMode==BonsaiCtgResponseMode.ReadableDirectVotes;
            bool cited=responseMode==BonsaiCtgResponseMode.CitedDirectVotes;
            bool citedFinding=responseMode==BonsaiCtgResponseMode.CitedFindings;
            bool reviewed=responseMode==BonsaiCtgResponseMode.ReviewedPacketVotes;
            bool demonstrated=responseMode==BonsaiCtgResponseMode.DemonstratedPacketVotes||reviewed;
            bool packet=responseMode==BonsaiCtgResponseMode.PacketDirectVotes||demonstrated;
            bool effects=responseMode==BonsaiCtgResponseMode.ActionEffectReadingVotes;
            bool voteFirst=responseMode==BonsaiCtgResponseMode.VoteFirstReadingVotes;
            string? readingContract=ReadingContract(responseMode);
            bool focused=readingContract is not null;
            bool defined=focused&&responseMode!=BonsaiCtgResponseMode.FocusedReadingVotes;
            bool perCriterion=responseMode==BonsaiCtgResponseMode.PerCriterionFindings;
            bool provenance=responseMode==BonsaiCtgResponseMode.ProvenanceDirectVotes;
            bool councilScoped=responseMode==BonsaiCtgResponseMode.CouncilScopedDirectVotes||provenance;
            bool requiresEvidence=focused||perCriterion||councilScoped||responseMode==BonsaiCtgResponseMode.EvidenceScopedDirectVotes||cited||citedFinding||packet;
            bool scoped=responseMode==BonsaiCtgResponseMode.PathosScopeDirectVotes||requiresEvidence;
            bool grounded=responseMode==BonsaiCtgResponseMode.GroundedDirectVotes||comparative||ordered;
            var metadata=new Dictionary<string,string>(StringComparer.Ordinal)
            { ["evaluator"]="bonsai-1.7b-provisional",["model.sha256"]=modelHash,["rh.verified"]="false",["roles.independent"]="false",
                ["prompt.contract"]=readingContract??(perCriterion?BonsaiCtgCriteriaPrompt.Contract:provenance?BonsaiCtgProvenancePrompt.Contract:councilScoped?BonsaiCtgProvenancePrompt.ScopeContract:reviewed?BonsaiCtgReviewPrompt.Contract:demonstrated?BonsaiCtgDemonstrationPrompt.Contract:packet?BonsaiCtgPacketPrompt.Contract:citedFinding?BonsaiCtgCitedFindingPrompt.Contract:cited?BonsaiCtgCitationPrompt.Contract:requiresEvidence?"canonical-evidence-scope-json-v10":scoped?"canonical-pathos-scope-json-v9":readable?BonsaiCtgReadableJson.Contract:ordered?BonsaiCtgObservationFirstPrompt.Contract:comparative?BonsaiCtgComparativePrompt.Contract:grounded?BonsaiCtgGroundedPrompt.Contract:separated?"canonical-separated-assessment-json-v4":findings?"canonical-findings-json-v3":"canonical-assessment-json-v2") };
            if(reviewed)metadata["draft.binding"]="false";
            if(findings||citedFinding)metadata["binding.policy"]=BonsaiCtgFindingPolicy.Contract;
            if(separated&&findings||(grounded||readable||scoped)&&!separated)return Failure("ctg.bonsai.unsupported_contract",metadata);
            if(requiresEvidence&&evidence is null)return Failure("ctg.bonsai.evidence_contract_required",metadata);
            if(separated&&(string.IsNullOrWhiteSpace(proposal)||proposal.Length>2048))return Failure("ctg.bonsai.invalid_proposal",metadata);
            if(request.InputFacts is null||request.InputFacts.Count is <1 or >16||request.InputFacts.Any(x=>x.Key.Length>128||x.Value is null||x.Value.Length>2048)||
                request.RequiredCouncils is null||request.RequiredCouncils.Count>0&&!request.RequiredCouncils.Order().SequenceEqual(Councils.Order()))
                return Failure("ctg.bonsai.invalid_request",metadata);
            // Prompt text and citation validation must refer to the same snapshot across awaits.
            var inputFacts=request.InputFacts.ToImmutableSortedDictionary(StringComparer.Ordinal);
            if(inputFacts.Count is <1 or >16||inputFacts.Any(x=>string.IsNullOrWhiteSpace(x.Key)||x.Key.Length>128||x.Value is null||x.Value.Length>2048))
                return Failure("ctg.bonsai.invalid_request",metadata);
            string facts=findings?JsonSerializer.Serialize(inputFacts.Select((pair,id)=>new{id,key=pair.Key,text=pair.Value}),FactsJson):
                JsonSerializer.Serialize(inputFacts,FactsJson);
            string? proposalJson=separated?JsonSerializer.Serialize(proposal,FactsJson):null;
            if(readable)
            {
                facts=BonsaiCtgReadableJson.Serialize(inputFacts);
                proposalJson=BonsaiCtgReadableJson.Serialize(proposal);
            }
            if(facts.Length+(proposalJson?.Length??0)>3000)return Failure("ctg.bonsai.input_budget",metadata);
            metadata["facts.sha256"]=Hash(facts);
            if(separated)
            {
                metadata["proposal.sha256"]=Hash(proposalJson!);
                metadata["evaluation_input.sha256"]=Hash(JsonSerializer.Serialize(new{proposal,supportingFacts=inputFacts},FactsJson));
            }
            if(evidence is not null)
            {
                metadata["evidence.requirements.contract"]="host-required-observations-v1";
                metadata["evidence.requirements.sha256"]=Hash(JsonSerializer.Serialize(evidence.Entries,FactsJson));
                metadata["evidence.bound_input.sha256"]=Hash(JsonSerializer.Serialize(new{proposal,supportingFacts=inputFacts,required=evidence.Entries},FactsJson));
                if(evidence.Check(inputFacts) is { } failure)return Failure(failure,metadata);
            }
            var votes=new List<CouncilVote>();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(evaluationTimeout);
            try
            {
                string? actionReading=null;
                bool challenge = UsesReportChallenge(responseMode);
                bool scopedReport = responseMode is BonsaiCtgResponseMode.ScopedReportVotes or BonsaiCtgResponseMode.ConciseReportRetryVotes;
                bool reasoned = challenge || responseMode is BonsaiCtgResponseMode.ReasonedFindingVotes or BonsaiCtgResponseMode.CompletedReasonedFindingVotes;
                bool sourceBound = responseMode == BonsaiCtgResponseMode.SourceBoundRecordVotes;
                bool compareLiterals = sourceBound || reasoned || responseMode is BonsaiCtgResponseMode.ComparedReadingVotes or BonsaiCtgResponseMode.RecordFindingVotes;
                var witness = compareLiterals ? BonsaiCtgLiteralComparison.TryCreate(proposal!, inputFacts,
                    includeFieldPaths: sourceBound || challenge, allowSourceCitation: scopedReport) : null;
                string? edits = sourceBound ? witness?.DescribeEdits() : null;
                if (edits is { Length: > 1024 }) edits = null;
                if (edits is not null) metadata["record.edits.sha256"] = Hash(edits);
                string? comparison = witness?.ToPromptData();
                if (comparison is not null) metadata["comparison.sha256"] = Hash(comparison);
                string? recordComparison = responseMode == BonsaiCtgResponseMode.RecordFindingVotes && witness is not null
                    ? BonsaiCtgRecordFindingPrompt.FormatComparison(witness, inputFacts.Keys)
                    : null;
                if (recordComparison is not null) metadata["binding.policy"] = BonsaiCtgFindingPolicy.Contract;
                string? reasonedComparison = reasoned ? witness?.DescribeForCouncil() : null;
                if (reasonedComparison is not null || edits is not null)
                {
                    metadata["binding.policy"] = BonsaiCtgFindingPolicy.Contract;
                    if (reasonedComparison is not null) metadata["draft.binding"] = "false";
                    if (select is null) return Failure("ctg.bonsai.selection_unavailable", metadata);
                }
                if(focused && edits is null)
                {
                    var readingPrompt=BonsaiCtgFocusedPrompt.Reading(facts,proposalJson!,effects);
                    var readingAnswer=await classify(readingPrompt.System,readingPrompt.User,timeout.Token).ConfigureAwait(false);
                    ReadingObservation?.Invoke(readingAnswer);
                    metadata["reading.prompt.sha256"]=Hash(readingPrompt.System+"\n"+readingPrompt.User);
                    metadata["reading.response.sha256"]=Hash(readingAnswer.Text);
                    metadata["reading.backend"]=readingAnswer.CpuBackend;
                    metadata["reading.activation_precision"]=readingAnswer.ActivationPrecision;
                    metadata["reading.binding"]="false";
                    try{actionReading=BonsaiCtgFocusedPrompt.ValidateReading(readingAnswer);}
                    catch(InvalidDataException){return Failure("ctg.bonsai.invalid_reading",metadata);}
                    if ((scopedReport || responseMode == BonsaiCtgResponseMode.FieldBoundReadingVotes) && witness is not null)
                    {
                        bool retains = witness.ReadingRetainsClaimedField(actionReading);
                        if (!retains) actionReading = null;
                        metadata["reading.used_source"] = retains ? "generated_reading" : "original_proposal";
                        metadata["reading.used.sha256"] = Hash(actionReading ?? proposal!);
                    }
                }
                foreach(var council in Councils)
                {
                    if(perCriterion)
                    {
                        var parts=BonsaiCtgCriteriaPrompt.Build(rules[council],council,facts,proposalJson!);
                        var assessments=new List<string>();var prompts=new List<string>();var responses=new List<string>();
                        foreach(var part in parts)
                        {
                            var response=await classify(part.System,part.User,timeout.Token).ConfigureAwait(false);
                            CriterionObservation?.Invoke(council,part.Name,response);
                            string key=council+".criterion."+assessments.Count;
                            metadata[key+".name"]=part.Name;metadata[key+".rom_text.sha256"]=Hash(part.Text);
                            prompts.Add(Hash(part.System+"\n"+part.User));responses.Add(Hash(response.Text));
                            metadata[key+".prompt.sha256"]=prompts[^1];metadata[key+".response.sha256"]=responses[^1];
                            metadata[key+".backend"]=response.CpuBackend;metadata[key+".activation_precision"]=response.ActivationPrecision;
                            string finding;
                            try{finding=BonsaiCtgCriteriaPrompt.Parse(response);}
                            catch(InvalidDataException){return Failure("ctg.bonsai.invalid_criterion",metadata);}
                            assessments.Add(finding);metadata[key+".finding"]=finding;
                        }
                        var combined=BonsaiCtgCriteriaPrompt.Resolve(council,assessments);
                        metadata[council+".prompt.sha256"]=Hash(JsonSerializer.Serialize(prompts));
                        metadata[council+".response.sha256"]=Hash(JsonSerializer.Serialize(responses));
                        metadata[council+".rom.sha256"]=rules[council].Reference.ContentHash!;
                        metadata["binding.policy"]=BonsaiCtgCriteriaPrompt.Binding;
                        votes.Add(new(){CouncilKind=council,VoteValue=combined,CanonReferences=[parts[0].Reference],ObservedAt=clock.Now,
                            Metadata=new Dictionary<string,string>{{"label",combined.ToString()},{"binding.policy",BonsaiCtgCriteriaPrompt.Binding},{"prompt.sha256",metadata[council+".prompt.sha256"]}}});
                        continue;
                    }
                    bool routed = responseMode == BonsaiCtgResponseMode.RoutedReadingVotes || compareLiterals;
                    bool anchored = responseMode == BonsaiCtgResponseMode.AnchoredReadingVotes ||
                        (routed && council is CouncilKind.Logos or CouncilKind.Ethos);
                    var rule=rules[council];var prompt=edits is not null?BonsaiCtgReasonedFindingPrompt.BuildFromRecord(rule,council,facts,proposalJson!,edits):
                        reasonedComparison is not null?BonsaiCtgReasonedFindingPrompt.Build(rule,council,facts,proposalJson!,actionReading,reasonedComparison):
                        recordComparison is not null?BonsaiCtgRecordFindingPrompt.Build(rule,council,facts,proposalJson!,actionReading!,recordComparison):
                        anchored?BonsaiCtgAnchoredVotePrompt.Build(rule,council,facts,proposalJson!,actionReading!,comparison):
                        focused?BonsaiCtgFocusedPrompt.Build(rule,council,facts,proposalJson!,actionReading!,defined,voteFirst,literalComparison:comparison):
                        provenance?BonsaiCtgProvenancePrompt.Build(rule,council,facts,proposalJson!):
                        councilScoped?BonsaiCtgDirectVotePrompt.BuildWithCouncilScopes(rule,council,facts,proposalJson!):
                        reviewed?BonsaiCtgReviewPrompt.Build(rule,council,facts,proposalJson!):
                        demonstrated?BonsaiCtgDemonstrationPrompt.Build(rule,council,facts,proposalJson!):
                        packet?BonsaiCtgPacketPrompt.Build(rule,council,facts,proposalJson!):
                        citedFinding?BonsaiCtgCitedFindingPrompt.Build(rule,council,facts,proposalJson!):
                        cited?BonsaiCtgCitationPrompt.Build(rule,council,facts,proposalJson!):
                        scoped?BonsaiCtgDirectVotePrompt.BuildWithPathosScope(rule,council,facts,proposalJson!):
                        ordered?BonsaiCtgObservationFirstPrompt.Build(rule,council,facts,proposalJson!):
                        comparative?BonsaiCtgComparativePrompt.Build(rule,council,facts,proposalJson!):
                        grounded?BonsaiCtgGroundedPrompt.Build(rule,council,facts,proposalJson!):
                        separated?BonsaiCtgDirectVotePrompt.BuildSeparated(rule,council,facts,proposalJson!):
                        findings?BonsaiCtgPrompt.Build(rule,council,facts):BonsaiCtgDirectVotePrompt.Build(rule,council,facts);
                    string system=prompt.System,user=prompt.User;
                    if (reasonedComparison is not null) user = BonsaiCtgReasonedFindingPrompt.ForReason(user);
                    metadata[council+".prompt.sha256"]=Hash(system+"\n"+user);metadata[council+".rom.sha256"]=rule.Reference.ContentHash!;
                    metadata[council+".excerpt.sha256"]=Hash(prompt.Excerpt);
                    var answer=edits is not null
                        ? await select!(system,user,BonsaiCtgReasonedFindingPrompt.Choices(council),timeout.Token).ConfigureAwait(false)
                        : await classify(system,user,timeout.Token).ConfigureAwait(false);
                    if(reviewed || reasonedComparison is not null)
                    {
                        if ((challenge || responseMode == BonsaiCtgResponseMode.CompletedReasonedFindingVotes) && reasonedComparison is not null &&
                            answer.FinishReason == "length" && retryReason is not null)
                        {
                            // Keep the incomplete attempt for replay only. It never enters the replacement or selection prompt.
                            string key = council + ".draft.initial";
                            metadata[key + ".prompt.sha256"] = metadata[council + ".prompt.sha256"];
                            metadata[key + ".response.sha256"] = Hash(answer.Text);
                            metadata[key + ".finish_reason"] = answer.FinishReason;
                            metadata[key + ".output_tokens"] = answer.OutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            CriterionObservation?.Invoke(council, "truncated_reason", answer);
                            timeout.Token.ThrowIfCancellationRequested();
                            metadata[council + ".draft.retry.max_output_tokens"] = "256";
                            if (responseMode == BonsaiCtgResponseMode.ConciseReportRetryVotes &&
                                answer.Text.AsSpan().TrimEnd().Length > BonsaiCtgReasonedFindingPrompt.MaximumReasonLength)
                            {
                                // Extending this same deterministic prefix cannot bring its text under the limit.
                                user = BonsaiCtgReasonedFindingPrompt.ForConciseReason(prompt.User);
                                metadata[council + ".prompt.sha256"] = Hash(system + "\n" + user);
                                metadata[council + ".draft.retry.format"] = "one_sentence";
                            }
                            answer = await retryReason(system, user, timeout.Token).ConfigureAwait(false);
                            metadata[council + ".draft.retry.finish_reason"] = answer.FinishReason;
                            timeout.Token.ThrowIfCancellationRequested();
                        }
                        ProvisionalObservation?.Invoke(council,answer);
                        metadata[council+".draft.prompt.sha256"]=metadata[council+".prompt.sha256"];
                        metadata[council+".draft.response.sha256"]=Hash(answer.Text);
                        metadata[council+".draft.output_tokens"]=answer.OutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        try{user=reasonedComparison is not null ? BonsaiCtgReasonedFindingPrompt.ForSelection(prompt.User,answer) : BonsaiCtgReviewPrompt.ForReview(user,answer);}
                        catch(InvalidDataException){return Failure("ctg.bonsai.invalid_draft",metadata);}
                        metadata[council+".prompt.sha256"]=Hash(system+"\n"+user);
                        answer=reasonedComparison is not null
                            ? await select!(system,user,BonsaiCtgReasonedFindingPrompt.Choices(council),timeout.Token).ConfigureAwait(false)
                            : await classify(system,user,timeout.Token).ConfigureAwait(false);
                    }
                    string? challengedFinding = null;
                    if (challenge && council == CouncilKind.Logos && witness?.HasMaterialValueDifference == true &&
                        (!scopedReport || witness.IsBoundExactReport(proposal!, inputFacts)) &&
                        BonsaiCtgReasonedFindingPrompt.Finding(council, answer) == "supported")
                    {
                        var difference = witness.DescribeEdits();
                        if (difference.Length <= 1024)
                        {
                            metadata["Logos.before_challenge.prompt.sha256"] = metadata["Logos.prompt.sha256"];
                            metadata["Logos.before_challenge.response.sha256"] = Hash(answer.Text);
                            metadata["Logos.before_challenge.excerpt.sha256"] = metadata["Logos.excerpt.sha256"];
                            var check = BonsaiCtgReportChallenge.Build(rule, facts, proposalJson!, difference);
                            var review = await select!(check.System, check.User, BonsaiCtgReportChallenge.Choices, timeout.Token).ConfigureAwait(false);
                            metadata["Logos.challenge.prompt.sha256"] = Hash(check.System + "\n" + check.User);
                            metadata["Logos.challenge.response.sha256"] = Hash(review.Text);
                            metadata["Logos.challenge.edits.sha256"] = Hash(difference);
                            metadata["Logos.challenge.excerpt.sha256"] = Hash(check.Excerpt);
                            if (review.FinishReason != "classified" || review.OutputTokens != 1 ||
                                !BonsaiCtgReportChallenge.Choices.Contains(review.Text))
                                return Failure("ctg.bonsai.invalid_assessment", metadata);
                            if (review.Text == "Error")
                            {
                                CriterionObservation?.Invoke(council, "before_challenge", answer);
                                challengedFinding = "proposal_defect";
                                metadata["Logos.prompt.sha256"] = metadata["Logos.challenge.prompt.sha256"];
                                metadata["Logos.excerpt.sha256"] = metadata["Logos.challenge.excerpt.sha256"];
                                prompt = check;
                                answer = review;
                            }
                            else
                            {
                                CriterionObservation?.Invoke(council, "challenge_deferred", review);
                            }
                        }
                    }
                    Observation?.Invoke(council,answer);
                    metadata[council+".response.sha256"]=Hash(answer.Text);
                    metadata[council+".backend"]=answer.CpuBackend;
                    metadata[council+".activation_precision"]=answer.ActivationPrecision;
                    metadata[council+".finish_reason"]=answer.FinishReason;
                    metadata[council+".output_tokens"]=answer.OutputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    metadata[council+".input_tokens"]=answer.InputTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    metadata[council+".elapsed_ms"]=answer.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    metadata[council+".prefill_forward_tokens"]=answer.PrefillForwardTokens.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    CouncilVoteValue value;var voteMetadata=new Dictionary<string,string>{["prompt.sha256"]=metadata[council+".prompt.sha256"]};
                    try
                    {
                        if (reasonedComparison is not null || edits is not null)
                        {
                            string finding = challengedFinding ?? BonsaiCtgReasonedFindingPrompt.Finding(council, answer);
                            value = BonsaiCtgFindingPolicy.Resolve(council, finding);
                            voteMetadata["finding"] = metadata[council+".finding"] = finding;
                            voteMetadata["binding.policy"] = BonsaiCtgFindingPolicy.Contract;
                            if (edits is not null) voteMetadata["record.edits.sha256"] = metadata["record.edits.sha256"];
                            else voteMetadata["draft.response.sha256"] = metadata[council+".draft.response.sha256"];
                        }
                        else if(findings||citedFinding||recordComparison is not null)
                        {
                            var assessment=citedFinding?BonsaiCtgCitedFindingPrompt.Parse(answer,council,inputFacts):BonsaiCtgPrompt.Parse(answer,council,inputFacts);value=assessment.Vote;
                            voteMetadata["finding"]=metadata[council+".finding"]=assessment.Finding;
                            voteMetadata["evidence.sha256"]=metadata[council+".evidence.sha256"]=assessment.EvidenceSha256;
                            voteMetadata["evidence.source"]=metadata[council+".evidence.source"]=assessment.SourceId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            voteMetadata["binding.policy"]=BonsaiCtgFindingPolicy.Contract;
                        }
                        else if(cited)
                        {
                            var assessment=BonsaiCtgCitationPrompt.Parse(answer,inputFacts);value=assessment.Vote;
                            voteMetadata["evidence.source"]=metadata[council+".evidence.source"]=assessment.Source.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            voteMetadata["evidence.sha256"]=metadata[council+".evidence.sha256"]=assessment.EvidenceSha256;
                        }
                        else if(anchored)value=BonsaiCtgAnchoredVotePrompt.Parse(answer,inputFacts,council);
                        else value=BonsaiCtgDirectVotePrompt.Parse(answer,requireVoteFirst:voteFirst);
                    }
                    catch(InvalidDataException){return Failure(findings||cited||citedFinding||recordComparison is not null||reasonedComparison is not null?"ctg.bonsai.invalid_assessment":"ctg.bonsai.invalid_vote",metadata);}
                    voteMetadata["label"]=value.ToString();
                    votes.Add(new(){CouncilKind=council,VoteValue=value,CanonReferences=[prompt.Reference],ObservedAt=clock.Now,Metadata=voteMetadata});
                }
            }
            catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested){return Failure("ctg.bonsai.timeout",metadata);}
            catch(Exception ex) when(ex is ArgumentException or InvalidDataException or ArithmeticException or OutOfMemoryException or IOException or InvalidOperationException)
            {return Failure("ctg.bonsai.evaluation_failed",metadata);}
            if(model is not null)
            {
                var cache=model.PrefixCacheStatistics;
                metadata["prefix_cache.bytes"]=cache.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["prefix_cache.hits"]=cache.Hits.ToString(System.Globalization.CultureInfo.InvariantCulture);
                metadata["prefix_cache.maximum_bytes"]=cache.MaximumBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return new(){OperationId=request.OperationId,Succeeded=true,Decision=new(){Votes=votes,ObservedAt=clock.Now},ObservedAt=clock.Now,
                CorrelationId=request.CorrelationId,TraceId=request.TraceId,CanonReferences=votes.SelectMany(v=>v.CanonReferences).ToArray(),Metadata=metadata};
            CouncilEvaluationResult Failure(string code,IReadOnlyDictionary<string,string> details)=>new(){OperationId=request.OperationId,Succeeded=false,ErrorCode=code,
                Decision=new(){Votes=Councils.Select(c=>new CouncilVote{CouncilKind=c,VoteValue=CouncilVoteValue.Unknown}).ToArray()},
                ObservedAt=clock.Now,CorrelationId=request.CorrelationId,TraceId=request.TraceId,
                CanonReferences=Councils.Select(c=>rules[c].Reference).ToArray(),Metadata=details};
        }
        finally{gate.Release();}
    }
    /// <summary>Runs the existing CTG discrete gate on the model's observations. No model-generated permit is trusted.</summary>
    public async Task<BonsaiCtgDecision> EvaluateGateAsync(CouncilEvaluationRequest request,CancellationToken cancellationToken=default)
    {
        var evaluation=await EvaluateAsync(request,cancellationToken).ConfigureAwait(false);
        return await DecideAsync(request,evaluation,cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Evaluates a separate proposal and supporting observations, then runs the unchanged deterministic CTG gate.</summary>
    public async Task<BonsaiCtgDecision> EvaluateProposalGateAsync(CouncilEvaluationRequest request,string proposal,CancellationToken cancellationToken=default)
    {
        var evaluation=await EvaluateProposalAsync(request,proposal,cancellationToken).ConfigureAwait(false);
        return await DecideAsync(request,evaluation,cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Checks required observation availability, evaluates the proposal, and invokes the unchanged CTG gate.</summary>
    public async Task<BonsaiCtgDecision> EvaluateProposalWithEvidenceGateAsync(CouncilEvaluationRequest request,string proposal,
        BonsaiCtgEvidenceRequirements evidence,CancellationToken cancellationToken=default)
    {
        var evaluation=await EvaluateProposalWithEvidenceAsync(request,proposal,evidence,cancellationToken).ConfigureAwait(false);
        return await DecideAsync(request,evaluation,cancellationToken).ConfigureAwait(false);
    }
    private async Task<BonsaiCtgDecision> DecideAsync(CouncilEvaluationRequest request,CouncilEvaluationResult evaluation,CancellationToken cancellationToken)
    {
        var input=new CtgCouncilVoteExtractor().Extract(evaluation);
        var decision=await new CtgDecisionGateEvaluator(clock).EvaluateAsync(new(){OperationId=request.OperationId,StepId=request.StepId,
            GateInput=input,CanonReferences=evaluation.CanonReferences,TraceId=request.TraceId,CorrelationId=request.CorrelationId},cancellationToken).ConfigureAwait(false);
        return new(evaluation,decision);
    }
    /// <summary>Requires host admission before model evaluation, then CTG allowance before invoking the operation.
    /// The host admission callback owns authority, scope and resource policy; a model cannot supply it.</summary>
    public Task<BonsaiCtgDecision> RunIfAllowedAsync(CouncilEvaluationRequest request,Func<CancellationToken,Task<bool>> hostAdmission,
        Func<CancellationToken,Task> operation,CancellationToken cancellationToken=default)
        =>RunCoreAsync(request,null,false,hostAdmission,operation,cancellationToken);
    /// <summary>Requires host admission before evaluating a separate proposal; only an accepted CTG gate invokes the operation.
    /// The host still owns authority, scope and resource policy. Model observations are not authorization.</summary>
    public Task<BonsaiCtgDecision> RunProposalIfAllowedAsync(CouncilEvaluationRequest request,string proposal,Func<CancellationToken,Task<bool>> hostAdmission,
        Func<CancellationToken,Task> operation,CancellationToken cancellationToken=default)
        =>RunCoreAsync(request,proposal,true,hostAdmission,operation,cancellationToken);
    /// <summary>Requires host admission, host-provided observation availability, successful model evaluation and an
    /// accepted CTG gate before executing. The host must bind requirements and operation to the same proposed action.</summary>
    public Task<BonsaiCtgDecision> RunProposalWithEvidenceIfAllowedAsync(CouncilEvaluationRequest request,string proposal,
        BonsaiCtgEvidenceRequirements evidence,Func<CancellationToken,Task<bool>> hostAdmission,
        Func<CancellationToken,Task> operation,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return RunCoreAsync(request,proposal,true,hostAdmission,operation,cancellationToken,evidence);
    }
    private async Task<BonsaiCtgDecision> RunCoreAsync(CouncilEvaluationRequest request,string? proposal,bool separated,Func<CancellationToken,Task<bool>> hostAdmission,
        Func<CancellationToken,Task> operation,CancellationToken cancellationToken,BonsaiCtgEvidenceRequirements? evidence=null)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(hostAdmission);ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        if(!await hostAdmission(cancellationToken).ConfigureAwait(false))
        {
            var rejected=new CouncilEvaluationResult{OperationId=request.OperationId,Succeeded=false,ErrorCode="ctg.host.admission_denied",ObservedAt=clock.Now};
            var deny=await new CtgDecisionGateEvaluator(clock).EvaluateAsync(new(){OperationId=request.OperationId,StepId=request.StepId,
                GateInput=new(),TraceId=request.TraceId,CorrelationId=request.CorrelationId},cancellationToken).ConfigureAwait(false);
            return new(rejected,deny);
        }
        var evaluation=await EvaluateCoreAsync(request,proposal,separated,cancellationToken,evidence).ConfigureAwait(false);
        var decision=await DecideAsync(request,evaluation,cancellationToken).ConfigureAwait(false);
        if(decision.Evaluation.Succeeded&&decision.Gate.Accepted){cancellationToken.ThrowIfCancellationRequested();await operation(cancellationToken).ConfigureAwait(false);}
        return decision;
    }
    private static string Hash(string text)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    /// <summary>Waits for evaluation and releases owned model state.</summary>
    public void Dispose(){gate.Wait();try{if(disposed)return;disposed=true;model?.Dispose();}finally{gate.Release();}}
}
