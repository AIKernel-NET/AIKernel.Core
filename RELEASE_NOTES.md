# AIKernel.Core Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.0

> [EN] Core 0.1.0 stabilizes the canonical boundaries: Context, Execution, VFS, and Semantic State now form a governed circuit.
>
> [JA] Core 0.1.0 は正準境界を確立──Context・Execution・VFS・Semantic State が統治回路として結線される。

AIKernel.Core 0.1.0 is the first executable runtime baseline for the AIKernel
semantic runtime.

- Align with AIKernel.NET 0.1.0 contracts for Abstractions, DTOs, Enums,
  Control, routing, memory, DSL, History ROM, Capability ROM, and governance.
- Provide the runtime family: `AIKernel.Common`, `AIKernel.Core`,
  `AIKernel.Kernel`, `AIKernel.Hosting`, `AIKernel.Providers.MicrosoftAI`, and
  `AIKernel.TestKit`.
- Stabilize Result, Option, Either, ResultStep, LINQ composition, ReplayLog,
  SemanticDelta, DSL execution, ROM registration, and fail-closed Kernel
  boundaries.
- Keep Core CUDA-free by default. Native/GPU execution remains an opt-in
  external Capability.
- Publish the Python binding as `aikernel-net`; `import aikernel_net` is the
  stable Python namespace.

This release closes the 0.0.x design-implementation line and opens the 0.1.x
prototype validation line.
