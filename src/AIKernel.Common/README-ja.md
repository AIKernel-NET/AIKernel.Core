# AIKernel.Common

[English](README.md)

AIKernel.Common は、AIKernel ecosystem 全体で共有する foundational utility component
を提供します。

JSON serialization helper、file / path utility、logging primitive、common exception、
functional result primitive など、横断的に使う機能を含みます。

利用想定:

- AIKernel.Core
- AIKernel.Tools
- AIKernel.CLI
- AIKernel.Foundation（任意）

Domain logic や kernel abstraction は含みません。AIKernel module 全体で一貫した
挙動を保つための、軽量な implementation-level support module です。

## 主な機能

- `Result<T>`: fail-closed computation
- `Option<T>`: pure optional values
- `Either<L,R>`: pure left/right branching
- `ResultStep<TState,TValue>`: deterministic step identity、semantic delta、replay log
- `PipelineStep`: deterministic finite loop、timeout-style loop、suspend、resume point
- LINQ query syntax: `Select`, `SelectMany`, `Where`, `Bind`, `Map`, `Tap`

`PipelineStep` は agent-style user-land control flow を有限で観測可能な形に保ちます。
loop iteration、suspend point、resume point は `ResultStepReplayLogEntry` として
表現されます。`Map` は pure projection のままで replay node を追加しません。

## 設計思想

AIKernel.Common は Interface-Led Architecture (ILA) に従います。

- domain logic を持たない
- kernel abstraction を持たない
- AIKernel.NET contract package に依存しない
- pure implementation-level utilities
- stable, reusable, cross-cutting components

## 使用例

```csharp
var suspended = PipelineStep.Suspend<string, int>(
    "awaiting-approval",
    "Needs user approval.");

var resumed =
    from approval in PipelineStep.Resume(
        suspended.ReplayLog,
        "approved",
        1,
        "User approved.")
    from looped in PipelineStep.Loop(
        ResultStep<string, int>.Success("agent", approval),
        maxIterations: 2,
        static (iteration, value) => ResultStep<string, int>
            .Success($"agent:{iteration}", value + 1))
    select looped;
```

## ライセンス

Apache License 2.0.
