# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.1.1

**2026年6月14日 - CTG コア統合面。**

AIKernel.Core 0.1.1.1 は AIKernel.NET 0.1.1.1 contract packages と整合し、
Canonical Triadic Governance (CTG) の Core 実装面を追加します。

- decision gate / trajectory gate の pure CTG governance evaluator を追加。
- council vote extraction、CouncilDecision → GateInput adapter、reject reason
  classification、canon reference resolution、governance trace assembly を追加。
- 後続の Control package が gate logic を再実装せず呼び出せる Core integration
  surface として、`CtgStepTraceAssembler`、`ICtgGovernanceService`、
  `CtgGovernanceService`、`ICtgCanonReferenceSource`、
  `CtgStaticCanonReferenceSource` を追加。
- merge 済み CTG locale YAML を `CanonReference` carrier と fail-closed diagnostics
  に変換する `CtgRomLocaleYamlAdapter` を追加。canon rule text は DTO に複製しません。
- `AddCtgGovernance()` を追加し、`AddAIKernelCore()` から非置換 DI 登録として呼び出すように整理。
- CTG truth table、unknown vote fail-closed、reject reason serialization、
  locale parity、VFS/ROM merge adapter、service、hosting のテストを追加。

配布メモ: 0.1.1.1 系は NuGet-only です。この更新では PyPI package を作成しません。

## 0.1.1

**June 10th, 2026 - Cohering the core runtime.**
**2026年6月10日--コアランタイムを一貫化する。**

Cohering the core runtime: execution, context, and semantic state form a
governed kernel circuit. コアランタイムの一貫化--Execution・Context・Semantic
State が統治されたカーネル回路を形成する。

AIKernel.Core 0.1.1 は、AIKernel Semantic OS package family の同期された実行可能
runtime baseline です。

- AIKernel.NET 0.1.1 の Abstractions、DTO、Enum、Control、routing、memory、DSL、History ROM、Capability ROM、governance contract と整合します。
- `AIKernel.Common`、`AIKernel.Core`、`AIKernel.Kernel`、`AIKernel.Hosting`、`AIKernel.Providers.MicrosoftAI`、`AIKernel.TestKit` の runtime family を提供します。
- Result、Option、Either、ResultStep、LINQ composition、ReplayLog、SemanticDelta、DSL execution、ROM registration、fail-closed Kernel boundary を安定化します。
- 0.1.1 release line 向けに Core 標準 Provider surface を追加します:
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

この release は Core runtime、contract、standard provider surface、Python binding
metadata を横断して 0.1.1 の semantic circuit を同期します。
