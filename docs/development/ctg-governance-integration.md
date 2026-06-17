# CTG Governance Integration Guide

This guide describes the AIKernel.Core implementation-side integration surface
for Canonical Triadic Governance (CTG). Canon, Council, Gate, RejectPolicy,
ROM, DTO, enum, and YAML semantics are owned by the contract and ROM layers;
Core only executes deterministic carriers that those layers already define.

## Scope

Core implements the pure CTG kernel surface:

- `CtgCouncilVoteExtractor`
- `CtgCouncilDecisionToGateInputAdapter`
- `CtgDecisionGateEvaluator`
- `CtgRejectReasonClassifier`
- `CtgGovernanceTraceBuilder`
- `CtgStepTraceAssembler`
- `CtgTrajectoryGateEvaluator`
- `CtgCanonReferenceResolver`
- `CtgRomLocaleYamlAdapter`
- `ICtgCanonReferenceSource` / `CtgStaticCanonReferenceSource`
- `ICtgGovernanceService` / `CtgGovernanceService`

`CtgDecisionGateEvaluator` reads only `GateInput.Logos`, `GateInput.Ethos`,
and `GateInput.Pathos`. It does not read confidence, risk score, diagnostics,
metadata scores, provider state, Guard, PDP, model output, or external engine
state.

## Hosting

Use `AddCtgGovernance()` to register only the CTG Core integration surface:

```csharp
services.AddCtgGovernance();
```

`AddAIKernelCore()` calls `AddCtgGovernance()` as part of the standard Core
runtime registration. Registrations use `TryAddSingleton` for contract
implementations, so existing host-provided `IDecisionGate` or `ITrajectoryGate`
registrations are not replaced.

## Adapter Boundary

`CtgCouncilDecisionToGateInputAdapter` converts `CouncilDecision` or
`CouncilEvaluationResult` into `GateInput`. It performs no semantic evaluation.
Missing Logos, Ethos, or Pathos votes become `CouncilVoteValue.Unknown`, and the
decision gate handles unknown votes by failing closed.

`CtgStepTraceAssembler` turns existing council and gate carriers into
`StepGovernanceTrace`. It does not decide whether a step is allowed or denied.

`ICtgCanonReferenceSource` supplies immutable canon references for service-created
requests. The source is used only when `CtgGovernanceService` creates a
`DecisionGateRequest` or `TrajectoryGateRequest` from lower-level carriers. It
does not participate in gate decisions.

## ROM Locale YAML Boundary

`CtgRomLocaleYamlAdapter` adapts merged CTG locale YAML into
`CtgMergedRomDescriptor`, resolved `CanonReference` values, and diagnostics.
It extracts only path/reference carriers such as:

- `canon.path` / `canon.canonReference`
- `councils[].rulesPath` / `councils[].canonReference`
- `decisionGate.policyPath` / `decisionGate.canonReference`
- `trajectoryGate.policyPath` / `trajectoryGate.canonReference`
- `rejectPolicy.rulesPath` / `rejectPolicy.canonReference`

When a required path or canon reference is missing, the adapter emits
fail-closed governance diagnostics. It never creates replacement rules and does
not copy canon rule text into DTOs.

## Non-Responsibilities

Core CTG does not implement:

- Provider or model calls
- Council semantic evaluation providers
- Guard or PDP enforcement
- Control runtime orchestration
- Wasm ABI mirrors
- Tools CLI or reports
- GUI, audio, vision, input, or sandbox providers

Those packages call Core; Core does not depend on them.

## Distribution Boundary

The 0.1.1.1 CTG Core update ships through NuGet packages only. It does not
create or publish a PyPI package. External provider packages remain on their
own release line until their repositories are updated.
