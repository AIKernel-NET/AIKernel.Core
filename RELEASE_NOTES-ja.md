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
- 0.1.0.2 development patch line 向けに Core 標準 Provider surface を追加します:
  `MinimalRuntimeProvider`、`LocalExecutionProvider`、`VfsProvider`、
  `SkillProvider`、`SystemInfoProvider`。これらの組み込み Provider は、
  Tools や外部 Provider に依存せず、決定論的 boot、local DSL execution、read-only VFS、
  OpenAI 互換 `SKILL.md` registration、安全な system introspection capability を公開します。
- Provider manifest loading、dynamic capability metadata registration、任意の assembly
  loading、CLI 向け provider setting のために、Core-owned `IDynamicProviderRegistry`
  extension surface を追加します。
- Core は既定で CUDA-free です。Native/GPU execution は opt-in external Capability として扱います。
- Python binding は `aikernel-net` として公開し、`import aikernel_net` を stable Python namespace とします。
  Python surface は C# execution logic を再実装せず、Core 標準 Provider、provider manifest、
  ROM storage、VFS Git の contract descriptor を公開します。

このリリースは 0.0.x の設計実装ラインを閉じ、0.1.x の prototype validation line を開始します。
