# AIKernel.Core Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.1.1

**June 14th, 2026 - CTG core integration surface.**
**2026年6月14日--CTG コア統合面。**

AIKernel.Core 0.1.1.1 aligns with the AIKernel.NET 0.1.1.1 contract packages
and adds the implementation-side Canonical Triadic Governance (CTG) core
surface.

- Add pure CTG governance evaluators for decision gates and trajectory gates.
- Add council-vote extraction, council-decision-to-gate-input adaptation,
  rejection reason classification, canon reference resolution, and governance
  trace assembly.
- Add `CtgStepTraceAssembler`, `ICtgGovernanceService`,
  `CtgGovernanceService`, `ICtgCanonReferenceSource`, and
  `CtgStaticCanonReferenceSource` as the Core integration surface that later
  Control packages can call without reimplementing gate logic.
- Add `CtgRomLocaleYamlAdapter` to adapt merged CTG locale YAML into
  `CanonReference` carriers and fail-closed diagnostics without copying canon
  rule text into DTOs.
- Add `AddCtgGovernance()` and wire it into `AddAIKernelCore()` using
  non-replacing DI registrations.
- Add CTG truth table, fail-closed unknown-vote, reject reason serialization,
  locale parity, VFS/ROM merge adapter, service, and hosting tests.

Distribution note: the 0.1.1.1 line is NuGet-only. No PyPI package is created
for this update.

## 0.1.1

**June 10th, 2026 - Cohering the core runtime.**
**2026年6月10日--コアランタイムを一貫化する。**

Cohering the core runtime: execution, context, and semantic state form a
governed kernel circuit. コアランタイムの一貫化--Execution・Context・Semantic
State が統治されたカーネル回路を形成する。

AIKernel.Core 0.1.1 is the synchronized executable runtime baseline for the
AIKernel Semantic OS package family.

- Align with AIKernel.NET 0.1.1 contracts for Abstractions, DTOs, Enums,
  Control, routing, memory, DSL, History ROM, Capability ROM, and governance.
- Provide the runtime family: `AIKernel.Common`, `AIKernel.Core`,
  `AIKernel.Kernel`, `AIKernel.Hosting`, `AIKernel.Providers.MicrosoftAI`, and
  `AIKernel.TestKit`.
- Stabilize Result, Option, Either, ResultStep, LINQ composition, ReplayLog,
  SemanticDelta, DSL execution, ROM registration, and fail-closed Kernel
  boundaries.
- Add Core standard provider surfaces for the 0.1.1 release line:
  `MinimalRuntimeProvider`, `LocalExecutionProvider`, `VfsProvider`,
  `SkillProvider`, and `SystemInfoProvider`. These built-in providers expose
  deterministic boot, local DSL execution, read-only VFS, OpenAI-compatible
  `SKILL.md` registration, and safe system introspection capabilities without
  depending on Tools or external providers.
- Add the Core-owned `IDynamicProviderRegistry` extension surface for provider
  manifest loading, dynamic capability metadata registration, optional assembly
  loading, and CLI-facing provider settings.
- Keep Core CUDA-free by default. Native/GPU execution remains an opt-in
  external Capability.
- Publish the Python binding as `aikernel-net`; `import aikernel_net` is the
  stable Python namespace. The Python surface exposes contract descriptors for
  Core standard providers, provider manifests, ROM storage, and VFS Git without
  reimplementing C# execution logic.

This release synchronizes the 0.1.1 semantic circuit across Core runtime,
contracts, standard provider surfaces, and Python binding metadata.
