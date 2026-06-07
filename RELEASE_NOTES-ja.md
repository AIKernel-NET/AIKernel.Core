# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.0

> [EN] Core 0.1.0 stabilizes the canonical boundaries: Context, Execution, VFS, and Semantic State now form a governed circuit.
>
> [JA] Core 0.1.0 は正準境界を確立──Context・Execution・VFS・Semantic State が統治回路として結線される。

AIKernel.Core 0.1.0 は、AIKernel Semantic Runtime の最初の実行可能 runtime baseline です。

- AIKernel.NET 0.1.0 の Abstractions、DTO、Enum、Control、routing、memory、DSL、History ROM、Capability ROM、governance contract と整合します。
- `AIKernel.Common`、`AIKernel.Core`、`AIKernel.Kernel`、`AIKernel.Hosting`、`AIKernel.Providers.MicrosoftAI`、`AIKernel.TestKit` の runtime family を提供します。
- Result、Option、Either、ResultStep、LINQ composition、ReplayLog、SemanticDelta、DSL execution、ROM registration、fail-closed Kernel boundary を安定化します。
- Core は既定で CUDA-free です。Native/GPU execution は opt-in external Capability として扱います。
- Python binding は `aikernel-net` として公開し、`import aikernel_net` を stable Python namespace とします。

このリリースは 0.0.x の設計実装ラインを閉じ、0.1.x の prototype validation line を開始します。
