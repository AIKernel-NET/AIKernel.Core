# AIKernel.Common

**English | 日本語**

---

## Overview / 概要

**AIKernel.Common** provides foundational utility components shared across the entire AIKernel ecosystem.  
This library contains cross-cutting features such as JSON serialization helpers, file and path utilities, logging primitives, common exception types, and functional result primitives.

AIKernel.Common is designed as a lightweight, implementation-level support module used by:

- AIKernel.Core  
- AIKernel.Tools  
- AIKernel.CLI  
- AIKernel.Foundation (optional)

It does **not** include domain logic or kernel abstractions.  
Instead, it offers standardized behaviors and reusable helpers that ensure consistency across all AIKernel modules.

---

## Features / 主な機能

### Functional Results
- `Result<T>` for fail-closed computation
- `Option<T>` for pure optional values
- `Either<L,R>` for pure left/right branching
- `ResultStep<TState,TValue>` for deterministic step identity, semantic deltas, and replay logs
- `PipelineStep` for deterministic finite loops, timeout-style loops, suspend, and resume points
- LINQ query syntax support through `Select`, `SelectMany`, `Bind`, `Map`, and `Tap`

`PipelineStep` keeps agent-style user-land control flow finite and observable.
Each loop iteration, suspend point, and resume point is represented as a
`ResultStepReplayLogEntry`; `Map` remains a pure projection and does not add a
replay node. Use `PipelineStepMetadataKeys` when reading loop, suspend, and
resume metadata from external capability modules.

###  JSON Utilities  
- Unified `JsonSerializerOptions`  
- Helper methods for serialization/deserialization  
- JSON file load/save utilities  

###  File & Path Utilities  
- Safe file read/write  
- Path normalization  
- Directory helpers  

###  Logging Primitives  
- Lightweight logging helpers  
- Common log formatting utilities  

###  Common Exceptions  
- Shared exception types  
- Error handling helpers  

###  Shared Helpers  
- Try helpers for exception-to-result conversion
- Cross-module reusable utilities  

---

## Design Philosophy / 設計思想

AIKernel.Common follows the **Interface-Led Architecture (ILA)** principles:

- No domain logic  
- No kernel abstractions  
- No dependency on AIKernel.NET (abstractions)  
- Pure implementation-level utilities  
- Stable, reusable, cross-cutting components  

This ensures that all AIKernel modules behave consistently while keeping the architecture clean and layered.

---

## Repository Structure / リポジトリ構成

```text
AIKernel.Common/ 
├─ Results/
│ ├─ Result.cs
│ ├─ Option.cs
│ ├─ Either.cs
│ ├─ ResultStep.cs
│ ├─ PipelineStep.cs
│ └─ ErrorContext.cs
├─ Json/ 
│ ├─ JsonOptions.cs 
│ ├─ JsonUtil.cs 
│ └─ JsonFile.cs 
├─ IO/ 
│ ├─ FileUtil.cs 
│ └─ PathUtil.cs 
├─ Logging/ 
│ └─ LogUtil.cs 
├─ Exceptions/ 
│ └─ CommonException.cs 
└─ README.md
```

---

## Usage Examples / 使用例

### JSON Serialization

```csharp
var json = JsonUtil.ToJson(obj);
var obj2 = JsonUtil.FromJson<MyType>(json);
```
### JSON File I/O

```csharp
await JsonFile.SaveAsync("data.json", obj);
var loaded = await JsonFile.LoadAsync<MyType>("data.json");
```

### Path Utilities

```csharp
var full = PathUtil.Normalize("~/data/output.txt");
```

### Deterministic Pipeline Control

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

## License / ライセンス

MIT License

## Contributing / コントリビュート

Contributions are welcome. Please follow the AIKernel ecosystem’s coding style and architectural guidelines.

AIKernel 全体のアーキテクチャガイドラインに従ってください。
## Maintainer / メンテナー

AIKernel Project Maintained by **Takuya.S**
