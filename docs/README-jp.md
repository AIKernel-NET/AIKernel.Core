# AIKernel.Core Documentation

[English](README.md)

この folder は AIKernel.Core の実装側 development guide を含みます。
Canonical papers は AIKernel.NET repository で管理します。この folder は runtime
implementation と package usage guidance のためのものです。

この docs は、AIOS SDK における kernel runtime layer として Core を説明します。
Core は provider、control、WASM、GPU backend、tools、example と組み合わせて
AIOS distribution を構築するための安定した基盤です。

公式 AIOS ディストリビューション **AIKernel.Monolith** の開発も開始されています。
Monolith は 0.1.x 系の安定化後に SDK layer を統合する reference system として
位置づけられます。

## リポジトリ横断整合

共有の repository boundary、v0.1.3 development versioning、依存関係順、
PyPI Trusted Publishing、Python wrapper scope は
[AIKernel GPU rev3 Migration v0.1.3](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/migration/v0.1.3-gpu-rev3-migration.md)
で定義します。履歴としての v0.1.1.1 validation rule は
[AIKernel Repository Alignment v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/repository-alignment-v0.1.1.1-ja.md)
に残します。
複数 repository をまたぐ変更を行う場合は、まず
[リポジトリ横断開発者ガイド v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/cross-repository-developer-guide-v0.1.1.1-ja.md)
を読んでください。

Core は deterministic kernel runtime と CTG evaluator implementation を所有します。
Provider endpoint behavior、browser/WASM execution、scenario-specific mapping を
Core に取り込みません。

## Development Guides

- [User Guide](user-guide/index-ja.md)
- [CTG Governance Integration Guide](development/ctg-governance-integration.md)
- [CTG Governance Integration Guide 日本語](development/ctg-governance-integration-jp.md)
- [Concept Elevation Notes / 概念昇格ノート](development/concept-elevation.md)
- [CUDA Capability Development Guide](development/cuda-capability-development-guide.md)
- [CUDA Capability 開発ガイド](development/cuda-capability-development-guide-jp.md)
- [AIKernel.Core Release Checklist](operations/release-checklist.md)
- [AIKernel.Core リリースチェックリスト](operations/release-checklist-jp.md)
- [AIKernel.Python README](../python/README.md)
- [AIKernel.Python README 日本語](../python/README-ja.md)

## どのページを読むべきか

- Core package を install する場合や standard provider boot surface を確認する場合は
  User Guide を読んでください。
- CUDA Capability guide は、明示的に外部 GPU / native package を追加する場合だけ
  読んでください。既定の Core / Python install は CPU-only です。
- package 公開準備を行う場合は Release Checklist を読んでください。
- Python から `aikernel-net` 経由で Core を利用する場合は Python README を読んでください。

## 最初の検証

外部 Provider や native capability module を追加する前に、まず CPU/default package
family を検証してください。

```powershell
dotnet build AIKernel.Core.slnx -c Release
dotnet test AIKernel.Core.slnx -c Release --no-build
```

## Package Boundaries

AIKernel.Core は CPU/default package family を公開します。

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.TestKit`

`AIKernel.Providers.MicrosoftAI` は外部 Provider package として利用し、Provider
repository が更新されるまでは provider package line のまま扱います。

CUDA support は optional で、この repository の外側にあります。既定の
AIKernel.Core と AIKernel.Python install は CUDA、LibTorch、native bridge を要求しません。
GPU host は `AIKernel.Cuda13.0.Libtorch2.12.win-x64` のような外部 CUDA Capability
module を明示的に install / register します。

対応する distribution path:

- Windows/Linux C# application は `AIKernel.*` NuGet packages を install します。
- Python consumer は `aikernel-net` を install します。この package は薄い managed
  assembly loading helper、generated managed API catalog、example 向けの CTG-ROM
  sample asset を提供します。
- GPU/native execution は明示的な Capability package でのみ追加します。

v0.1.3 development では、NuGet は `0.1.3-dev{buildNumber}`、Python は
`0.1.3.dev{buildNumber}` のような local development version を使います。公開手順が
stable release step を明示するまで、stable `0.1.3` artifact は作成しません。

`AIKernel.Vfs` は Core implementation namespace であり、独立 NuGet package ではありません。
VFS contract は AIKernel.NET contract packages にあり、in-process VFS provider は
`AIKernel.Core` 内にあります。

## Standard Providers

AIKernel.Core は、外部 Provider の読み込み前に利用できる OS-level 標準 Provider を含みます。

- `MinimalRuntimeProvider`: 決定論的な `runtime.ping` boot capability。
- `LocalExecutionProvider`: Core DSL runtime を使った inline DSL pipeline execution。
- `VfsProvider`: read/list/exists/metadata operation の read-only VFS capability。
- `SkillProvider`: OpenAI 互換 `SKILL.md` の読み込みと capability registration。
- `SystemInfoProvider`: provider、capability、VFS state、runtime version の安全な system introspection。

これらの Provider は AIKernel.Tools、外部 Provider、native ABI bridge、HTTP、model inference に依存しません。

Core は、外部 Provider manifest を読み込む CLI / host scenario のために
`IDynamicProviderRegistry` も公開します。この dynamic surface は、stable な
`IProviderRegistry` contract package を変更せずに、provider metadata、capability
descriptor、任意の provider assembly、CLI 向け manifest setting を登録します。
標準 Provider invoker も同じ dynamic registry から参照できます。`SkillProvider` は
`SKILL.md` から runtime に capability set を発見するため、provider-level invoker
として表現します。

## Release Verification

Core package family 公開前に実行します。

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
```

v0.1.3 integration では、`python/README-ja.md` の Python package checks も実行します。
stable package artifact は、AIKernel.NET contract packages が利用可能になった後、
依存関係順に作成します。
