# CTG Governance Integration Guide

この guide は、Canonical Triadic Governance (CTG) を AIKernel.Core 側で利用するための
実装境界を説明します。Canon、Council、Gate、RejectPolicy、ROM、DTO、enum、YAML の
意味論は contract / ROM layer が所有します。Core は、それらが定義した carrier を
決定論的に実行するだけです。

## Scope

Core は次の pure CTG kernel surface を実装します。

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

`CtgDecisionGateEvaluator` が読むのは `GateInput.Logos`、`GateInput.Ethos`、
`GateInput.Pathos` だけです。confidence、risk score、diagnostics、metadata score、
provider state、Guard、PDP、model output、external engine state は読みません。

## Hosting

CTG Core integration surface だけを登録する場合は `AddCtgGovernance()` を使います。

```csharp
services.AddCtgGovernance();
```

標準の `AddAIKernelCore()` は runtime registration の一部として `AddCtgGovernance()` を
呼び出します。contract implementation の登録には `TryAddSingleton` を使うため、host が
事前に登録した `IDecisionGate` や `ITrajectoryGate` を置き換えません。

## Adapter Boundary

`CtgCouncilDecisionToGateInputAdapter` は `CouncilDecision` または
`CouncilEvaluationResult` を `GateInput` に変換します。semantic evaluation は行いません。
Logos、Ethos、Pathos のいずれかの vote が欠けている場合は
`CouncilVoteValue.Unknown` になり、decision gate が fail-closed として扱います。

`CtgStepTraceAssembler` は既存の council / gate carrier から
`StepGovernanceTrace` を組み立てます。step を allow / deny にする判断は行いません。

`ICtgCanonReferenceSource` は、service が作成する request に不変の canon reference を
供給します。source が使われるのは、`CtgGovernanceService` が低レベル carrier から
`DecisionGateRequest` または `TrajectoryGateRequest` を作る場合だけです。gate decision
には関与しません。

## ROM Locale YAML Boundary

`CtgRomLocaleYamlAdapter` は merge 済み CTG locale YAML を
`CtgMergedRomDescriptor`、解決済み `CanonReference`、diagnostics に変換します。
抽出するのは次のような path/reference carrier だけです。

- `canon.path` / `canon.canonReference`
- `councils[].rulesPath` / `councils[].canonReference`
- `decisionGate.policyPath` / `decisionGate.canonReference`
- `trajectoryGate.policyPath` / `trajectoryGate.canonReference`
- `rejectPolicy.rulesPath` / `rejectPolicy.canonReference`

必須 path または canon reference が欠けている場合、adapter は fail-closed の
governance diagnostics を出力します。代替 rule を生成せず、canon rule text を DTO に
複製しません。

## Non-Responsibilities

Core CTG は次を実装しません。

- Provider / model call
- Council semantic evaluation provider
- Guard / PDP enforcement
- Control runtime orchestration
- Wasm ABI mirror
- Tools CLI / report
- GUI、audio、vision、input、sandbox provider

これらの package が Core を呼び出します。Core はそれらへ依存しません。

## Distribution Boundary

0.1.1.1 CTG Core 更新は NuGet packages のみで配布します。PyPI package は作成・公開しません。
次の公式 v0.1.3 正典シリーズでは、NuGet と PyPI の package family を同期公開する前提です。
外部 Provider package は、それぞれの repository が更新されるまで独自の release line に維持します。
