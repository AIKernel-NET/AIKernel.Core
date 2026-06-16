# User Guide

[English](index.md)

この guide は AIKernel.Core を .NET / Python host から利用する方法を説明します。

Core は AIOS SDK の kernel runtime layer です。決定論的 runtime、VFS / ROM、
hosting、Kernel、標準 Core capability を提供し、provider、control、WASM、
GPU、tools layer を追加する前の AIOS distribution の基盤になります。

公式 AIOS ディストリビューション **AIKernel.Monolith** の開発も開始されています。
Monolith は 0.1.x 系の安定化後に SDK layer を統合する標準 reference distribution
として位置づけられます。

## Install Packages

```bash
dotnet add package AIKernel.Common --version 0.1.1.1
dotnet add package AIKernel.Core --version 0.1.1.1
dotnet add package AIKernel.Hosting --version 0.1.1.1
dotnet add package AIKernel.Kernel --version 0.1.1.1
```

0.1.1.1 validation line では Python binding / PyPI package は公開しません。既存の
Python 利用者は、次の公式 v0.1.2 正典シリーズで PyPI package family が更新されるまで、
公開済みの `aikernel-net` package を利用してください。

既存 Python package:

```bash
pip install aikernel-net
```

## Register Core Services

```csharp
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAIKernelCore();

using var provider = services.BuildServiceProvider();
```

Core registration には standard provider baseline が含まれます。

- MinimalRuntimeProvider
- LocalExecutionProvider
- VfsProvider
- SkillProvider
- SystemInfoProvider

Core registration には CTG governance integration surface も含まれます。
CTG service だけが必要な host は `AddCtgGovernance()` を直接使えます。

```csharp
using AIKernel.Hosting;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCtgGovernance();
```

`AddCtgGovernance()` は CTG evaluator / adapter を登録しますが、すでに登録済みの
`IDecisionGate` や `ITrajectoryGate` は置き換えません。

## Use CTG Governance

Core は deterministic CTG kernel surface を提供します。

- `CtgCouncilVoteExtractor`
- `CtgCouncilDecisionToGateInputAdapter`
- `CtgDecisionGateEvaluator`
- `CtgTrajectoryGateEvaluator`
- `CtgRejectReasonClassifier`
- `CtgGovernanceTraceBuilder`
- `CtgStepTraceAssembler`
- `CtgRomLocaleYamlAdapter`
- `ICtgGovernanceService`

Decision Gate が読むのは `GateInput.Logos`、`GateInput.Ethos`、
`GateInput.Pathos` だけです。confidence、risk score、diagnostics、metadata score
のような continuous carrier は trace material であり、gate input ではありません。

```csharp
using AIKernel.Core.Governance;
using AIKernel.Dtos.Governance;
using AIKernel.Enums.Governance;
using Microsoft.Extensions.DependencyInjection;

var ctg = provider.GetRequiredService<ICtgGovernanceService>();

var decision = await ctg.EvaluateDecisionGateAsync(
    new GateInput
    {
        Logos = CouncilVoteValue.Approve,
        Ethos = CouncilVoteValue.Approve,
        Pathos = CouncilVoteValue.Abstain
    },
    cancellationToken);
```

## Use Standard Capabilities

standard capability は external provider load 前から利用できます。

- `aikernel.runtime.ping`
- `aikernel.local.execute`
- `aikernel.vfs`
- `aikernel.system.info`
- `SKILL.md` 由来の dynamic `skill.*` capability

host は capability registry から descriptor を inspect し、registered invoker
経由で invoke できます。

## Use VFS

Core は local、memory、web-get VFS implementation を提供します。standard VFS
capability は read-only で、次を support します。

- `vfs.exists`
- `vfs.list`
- `vfs.metadata`
- `vfs.read_file`

host file を mutate する用途ではなく、deterministic inspection に使います。

## Use SKILL.md

`SkillProvider` は `SKILL.md` を再帰的に discover し、各 skill を capability module
として登録します。public な主表記は `SKILL.md` です。

Skill は structured manifest へ parse され、local execution 用 DSL pipeline
descriptor へ compile できます。

## Use Monad Primitives

`AIKernel.Common` は fail-closed な functional primitive を含みます。

- `Result<T>`: execution outcome
- `Either<L,R>`: pure two-way validation
- `Option<T>`: presence / absence
- `Try` / `TryAsync`: exception capture
- `Async<T>`: LINQ-composable asynchronous result pipeline
- `ResultStep<TState,T>`: replayable pipeline step

これらを使い、parsing、validation、execution、replay semantics を明示します。

Core code では phase boundary を見える形に保ちます。

- 例外を投げる API を含まない parsing / validation は `Either<L,R>`
- optional metadata、optional provider、lookup miss は `Option<T>`
- file、network、provider、DSL、replay execution は `Result<T>` または `Try.RunAsync`
- replay 可能な pipeline execution は `ResultStep<TState,T>`

既存互換の public adapter が例外を投げる member を公開する場合でも、内部実装は
先に fail-closed な monadic path へ委譲します。

## Use Python Wrapper

```python
from aikernel_net import standard_provider_contracts, standard_capability

for provider in standard_provider_contracts():
    print(provider.provider_id, provider.name)

vfs = standard_capability("aikernel.vfs")
print(vfs.operations)
```

Python package は contract descriptor と managed assembly loading helper を公開します。
C# runtime の置き換えではありません。

## Failure Behavior

Core は fail-closed を前提に設計されています。

- invalid ROM / DSL / Skill metadata は明示的 error を返す
- unknown ID の capability lookup は明示的に失敗する
- VFS credential / path validation は bounded
- external provider は manifest boundary 経由で load する

## Next Steps

- native compute package 追加前に CUDA guide を確認してください。
- patch package 公開前に release checklist を確認してください。
- external provider implementation は AIKernel.Providers を使ってください。
- operator command / diagnostics には AIKernel.Tools を使ってください。
