# AIKernel.Python Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.1 — Core release alignment

- Promotes the Python binding metadata to the AIKernel.Core 0.1.1 release line.
- Aligns managed assembly discovery with AIKernel.NET contract packages and AIKernel.Core packages version 0.1.1.
- Keeps the standard provider, provider manifest, monadic result, and managed assembly discovery surfaces stable for the 0.1.1 package registration flow.

## 0.1.0.2.dev36 — Core standard provider contract preview

- Added Python descriptors for Core standard providers:
  `MinimalRuntimeProvider`, `LocalExecutionProvider`, `VfsProvider`,
  `SkillProvider`, and `SystemInfoProvider`.
- Exposed standard provider and capability lookup helpers without
  reimplementing C# provider execution logic.
- Aligned managed assembly discovery with the AIKernel.Core `0.1.0-dev39`
  local development package while keeping shared contract assemblies on the
  stable `0.1.0` line.
- Added provider-level managed invoker metadata for `SkillProvider` so the
  Python descriptors match the C# dynamic skill invocation surface.
- Aligned Python ROM storage and VFS Git contract operation ordering with the
  C# Core-owned capability contracts.
- Preserved provider manifest capability ordering in Python to match Core
  dynamic manifest loading semantics.
- Added bilingual C# record parameter documentation for Core standard provider,
  Skill, ROM storage, and VFS Git descriptors.
- Completed required public surface comment coverage for C# library members and
  Python public helpers.
- Added the Core-owned dynamic provider registry surface used by CLI/provider
  manifest loading.
- Added Python descriptors for provider manifests plus Core-owned ROM storage
  and VFS Git capability contracts.
- Switched local execution pipeline JSON parsing to monadic `Result` / LINQ
  composition.
- Switched DSL compiler validation and DSL ROM snapshot creation to monadic
  `Result` / LINQ composition.
- Switched DSL ROM capability invocation resolution/execution boundaries to
  monadic `Result` / LINQ composition while preserving ROM replay metadata.
- Switched DSL document node-array parsing and DSL ROM resolution to monadic
  `Result` / LINQ composition.
- Switched compiled DSL capability execution value validation to monadic
  `Result` / LINQ composition and folded sequential node execution through a
  short-circuiting aggregate.
- Switched DSL ROM store save/load I/O orchestration to async monadic
  `Task<Result<T>>` / LINQ composition.
- Switched DSL ROM registration and metadata canonical identity validation to
  monadic `Result` / LINQ composition while keeping dictionary registration as
  the explicit side-effect boundary.
- Switched DSL argument parsing and compiler capability-argument validation to
  short-circuiting monadic `Result` aggregates.
- Switched DSL ROM capability snapshot validation to monadic `Result` / LINQ
  composition while preserving ROM metadata on failures.
- Switched SkillProvider descriptor lookup, operation validation, and contract
  metadata reads to `Option` / `Either` pure branches.
- Switched Core-owned ROM storage/VFS Git contract metadata and SystemInfo
  provider metadata extraction to `Option`-based presence checks.
- Switched VFS invocation provider/path/credential selection and static
  model prompt capability resolution to `Option` / `Either` branches.
- Switched provider manifest endpoint extraction, candidate ROM metadata, and
  ROM frontmatter optional/required field reads to `Option` / `Either` branches.
- Routed DSL ROM registry presence checks through the existing `Option`
  snapshot lookup helpers.
- Routed VFS credential parameter presence through `Option` helpers.
- Registered Core standard invokers through DI and the dynamic provider
  registry so CLI/provider loading can enumerate the built-in invocation
  surface.
- Added direct constructor coverage for dynamic provider registry snapshots
  containing both providers and invokers.
- Aligned LocalExecutionProvider DSL node/argument parsing with the Core DSL
  parser using short-circuiting monadic `Result` aggregates.
- Composed LocalExecutionProvider parse, compile, and execute through
  `Task<Result<T>>` LINQ and converted compiler/runtime exceptions into
  fail-closed invocation results.
- Replaced pure pipeline/Skill.MD branching with `Either<string,T>` where no
  exception boundary is involved, including Skill header selection, fallback
  steps, slug normalization, local JSON value selection, and ROM metadata
  equality checks.
- Replaced local pipeline presence checks with `Option<string>` and converted
  JSON parse, DSL compile, and DSL execute exception boundaries through
  `Try.Run` / `Try.RunAsync` before mapping them back to structured results.
- Moved ResultStep loop trace branching to monadic primitives: optional loop
  timestamps and replay-log parents now use `Option<T>`, while loop transition
  decisions use `Either<string,T>`.
- Moved DSL document parser property presence checks to `Option<JsonElement>`
  and pure property validation/value selection to `Either<string,T>`.
- Aligned LocalExecutionProvider's inline DSL parser with the Core parser by
  moving local property presence checks to `Option<JsonElement>` and pure
  property validation to `Either<string,T>`.
- Moved compiler pure validation, ROM registry presence checks, ROM store I/O
  exception capture, and ROM path parsing into the updated monad split:
  `Either` for pure validation, `Option` for dictionary presence, and `Try`
  for exception-to-`Result` conversion.

## 0.1.0 — Stable Python binding baseline

- Promoted the official PyPI package `aikernel-net` to `0.1.0`.
- Kept the import package name as `aikernel_net`.
- Removed the legacy in-repository `aikernel` import package to avoid
  confusion with the unrelated PyPI `aikernel` project.
- Aligned managed assembly discovery with the AIKernel.Core 0.1.0 package
  family.

Install:

```bash
pip install aikernel-net==0.1.0
```

Import:

```python
import aikernel_net
```

The PyPI package named `aikernel` is a different project. AIKernel.NET uses
`aikernel-net` to protect the project identity and avoid user confusion.

The package remains CPU-only by default and does not include CUDA, LibTorch, or
native ABI payloads. GPU and native execution remain external Capability
concerns.
