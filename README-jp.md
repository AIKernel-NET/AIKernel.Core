# AIKernel.NET Core

![AIKernel.NET Logo](assets/aikernel-logo.png)

**AIKernel.NET Core** は、LLM アプリケーションにおける **文脈の暴走・再現性の欠如・ガバナンス不能** を解決するためのランタイムです。

.NET エコシステムのために設計された、決定論的かつ不変な **知能の OS（Knowledge OS）** の実装です。

単なる LLM のラッパーではありません。

AIKernel.NET Core は、知識資産（**ROM**）の管理、文脈（**Context**）の統治、そして推論実行を制御されたトランザクションとして扱うための、堅牢な実行基盤を提供します。

---

## AIOS SDK における位置づけ

AIKernel.Core は AIOS SDK の kernel runtime layer です。決定論的 runtime、
monad、DSL、VFS / ROM、hosting、標準 Core Provider を提供し、他の layer と
組み合わせて AI Operating System distribution を構築する基盤になります。

AIKernel には、公式 AIOS ディストリビューションである **AIKernel.Monolith** もあります。
Monolith は 0.1.x 系 SDK の安定化後に、semantic runtime、capability graph、
governance、providers、WASM、GPU backend、tools を統合する標準 AIOS として
開発が開始されています。

---

## アーキテクチャの規律

AIKernel.NET Core は、以下の 3 つの正典原則に従って動作します。

### 1. Fail-Closed

署名の不一致、Governance による拒否、トークン予算の超過が発生した場合、AIKernel は不完全な成功を許しません。

実行は即座に停止します。

### 2. Deterministic Replay

すべての推論文脈は `ContextHash` と `PromptHash` によって固定されます。

同一の入力と実行環境からは、同一の実行文脈を再現できることを目指します。

> 目的は LLM そのものを完全に決定論的にすることではありません。  
> 目的は、LLM の前後にある実行プロセスを再現可能・検査可能・統治可能にすることです。

### 3. Immutability

読み込まれた知識資産（**ROM**）や生成された実行結果（`IExecutionResult`）は、システムを流れる間、不変の DTO として扱われます。

一度具体化されたオブジェクトは、その場で改ざんされません。

---

## ランタイムアーキテクチャ

AIKernel.NET Core は、OS 的な多層ランタイムとして設計されています。

```text
+-----------------------------+
|        Hosting Layer        |  .NET アプリケーション統合
+-----------------------------+
|         Kernel Layer        |  統治・オーケストレーション
+-----------------------------+
|          Core Layer         |  純粋論理: VFS / ROM / Context / Execution
+-----------------------------+
|        Provider Layer       |  外部モデル・外部サービス境界
+-----------------------------+
```

---

## OS 抽象

Core は AIKernel OS の syscall surface を保持します。Compute、Process、
Network、Logging、SemanticRouter、VFS alias、Bonsai rule engine は Core 側の
抽象または決定論的 runtime 境界として扱います。

Provider 実装はこれらの契約へ依存し、実装そのものは Providers または Wasm
runtime 側へ分離します。Control layer は EventBus を通じて
`ProcessStarted`、`ProcessStopped`、`ProcessCrashed`、`GpuKernelExecuted`、
`FileAccessed`、`NetworkRequest` などの OS event を消費できます。

---

## CTG Governance Core

AIKernel.Core 0.1.2 は、Canonical Triadic Governance (CTG) の Core 実装面を
含みます。CTG は AIKernel.NET contract DTO / enum を消費する決定論的 Core service
として実装され、ROM、Canon、Council、Gate、RejectPolicy、YAML、DTO の意味論は変更しません。

Core CTG surface は次を含みます。

- vote extraction と `CouncilDecision` から `GateInput` への adapter
- pure Decision Gate evaluation
- pure Trajectory Gate aggregation
- reject reason serialization / classification
- canon reference resolution と locale YAML adaptation
- step / trajectory governance trace assembly
- Core 側 integration facade としての `ICtgGovernanceService`

CTG service だけを登録する場合:

```csharp
services.AddCtgGovernance();
```

`AddAIKernelCore()` も CTG surface を登録します。既存の `IDecisionGate` と
`ITrajectoryGate` 登録は置き換えません。

実装境界は [CTG Governance Integration Guide](docs/development/ctg-governance-integration-jp.md)
を参照してください。

配布メモ: 0.1.2 更新では NuGet package と同期 Python wrapper を公開します。

---

## ソリューション構造

本リポジトリは、ランタイム、Provider integration、検証レイヤーで構成されています。

```text
src/
  AIKernel.Common
  AIKernel.Core
  AIKernel.Kernel
  AIKernel.Hosting
  Providers/
    (external provider integration workspace)

tests/
  AIKernel.TestKit
  AIKernel.Core.Tests
  AIKernel.IntegrationTests

python/
  AIKernel.Python
```

### `src/` — Runtime Implementation

#### `AIKernel.Common`

ランタイムファミリーで共有する関数型プリミティブです。

純粋な Result / Option / Either helper を含み、AIKernel の runtime DTO、
Provider、Hosting、Kernel 実装には依存しません。

#### `AIKernel.Core`

知識の相転移を担う純粋論理エンジンです。

```text
VFS → ROM → Context → Execution
```

このレイヤーは Core ランタイムロジックを担当し、外部 Hosting や Provider 境界から実装上の関心事を分離します。

Core は、外部サービスを呼び出さない OS-level 標準 Provider も所有します。

- `MinimalRuntimeProvider` は、決定論的な boot / capability graph 検証用の `aikernel.runtime.ping` を公開します。
- `LocalExecutionProvider` は、既存 Core DSL runtime を使った inline DSL pipeline execution 用の `aikernel.local.execute` を公開します。
- `VfsProvider` は、file read、directory list、exists、metadata summary のための read-only VFS capability module `aikernel.vfs` を公開します。
- `SkillProvider` は OpenAI 互換 `SKILL.md` を読み込み、DSL pipeline descriptor に変換して capability module として登録します。
- `SystemInfoProvider` は provider、capability、VFS mount state、runtime version の安全な read-only introspection を提供する `aikernel.system.info` を公開します。

これらの Provider は provider boundary で決定論的かつ side-effect free であり、AIKernel.Tools や外部 Provider package には依存しません。

Native Capability module 向けの OS 非依存な MemoryRegion / MemoryMapper
runtime surface は、0.1.0 prototype validation baseline では AIKernel.Core
が所有します。Core は Result-based runtime adapter を公開し、具体的な
Win32 / POSIX の mapping 実装は Kernel に置きます。

#### `AIKernel.Kernel`

OS の統治・オーケストレーション層です。

`IKernel` Facade を公開し、全ランタイムレイヤーを統合します。
信頼済みホストで使用する既定の OS 固有 `IMemoryMapper` 実装もここで提供します。

#### `AIKernel.Hosting`

.NET アプリケーションにおける点火スイッチです。

ASP.NET Core / Generic Host 向けに、`IServiceCollection` 拡張と既定の配線を提供します。

#### `Providers/`

AIKernel と外部モデル・外部サービスを接続する Provider package の integration
workspace です。0.1.2 Core package line では、この repository から Provider
package は公開しません。

##### `AIKernel.Providers.MicrosoftAI`

`Microsoft.Extensions.AI`（MEAI）を利用した外部 OpenAI 互換 Provider package です。

Microsoft の AI 抽象レイヤーを活用して開発速度を高めつつ、AIKernel の Capability ベース実行モデルを維持します。

---

### `tests/` — Verification

#### `AIKernel.TestKit`

ABI と振る舞いの規律を検証するための Contract Test Framework です。

下流実装が AIKernel の契約に従っているかを検証するための基盤を提供します。

#### `AIKernel.Core.Tests`

内部ランタイムロジックのユニットテストです。

#### `AIKernel.IntegrationTests`

複数レイヤーを貫通する結合テストです。

### `python/` — Language Binding

#### `AIKernel.Python`

AIKernel.Core の関数型プリミティブと managed assembly discovery を提供する薄い
Python binding です。Python package release が明示的に予定された場合にのみ公開します。

公開済みの Python release は `aikernel-net` package として install でき、既定では
CPU-only / CUDA-free です。Python monad helper と managed assembly discovery を公開し、
CUDA、LibTorch、native `libtorch_bridge` ABI は同梱しません。Python package は OS 固有
memory mapping、Kernel 内部構造、Capability 内部実装を再実装しません。0.1.2 CTG Core
更新では PyPI package を公開しません。

---

## クイックスタート

パッケージ利用のまとまった手順は
[AIKernel.Core User Guide](docs/user-guide/index-ja.md) を参照してください。

### 1. パッケージのインストール

```bash
dotnet add package AIKernel.Core --version 0.1.2
dotnet add package AIKernel.Hosting --version 0.1.2
dotnet add package AIKernel.Kernel --version 0.1.2
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.1.2
```

`AIKernel.Providers.MicrosoftAI` は Provider repository の更新まで 0.1.2
provider package line のまま扱います。0.1.2 Core 更新は Core、Hosting、
Kernel、Common、TestKit、AIKernel.NET contract packages を中心とした更新です。

関数型プリミティブや Contract Test helper を直接利用する場合:

```bash
dotnet add package AIKernel.Common --version 0.1.2
dotnet add package AIKernel.TestKit --version 0.1.2
```

CUDA は任意機能であり、このリポジトリの外側に分離されています。GPU ホストでは、
`AIKernel.Cuda13.0.Libtorch2.12.win-x64` などの外部 Capability package を
明示的に追加してください。CUDA Capability package は split distribution を採用する場合があります。
NuGet.org には小さな metadata package のみを置き、LibTorch / CUDA / native payload を含む
full runtime package は対応する Capability GitHub Release に添付します。CUDA 実行には
full `.nupkg` を取得し、そのフォルダを local NuGet source として追加してから install します。

```bash
dotnet nuget add source <folder-containing-full-cuda-nupkg> --name AIKernel-CUDA
dotnet add package AIKernel.Cuda13.0.Libtorch2.12.win-x64 --version 0.1.2
```

CUDA を直接利用したい LLM / SLM 開発者向けの仮想メモリレイヤー、仕様、
モナド Pipeline、module 作成方針は
[docs/development/cuda-capability-development-guide-jp.md](docs/development/cuda-capability-development-guide-jp.md)
を参照してください。他の CUDA version、model runtime、Linux CUDA host では、
CUDA Capability repository を fork して新しい Capability module を作成します。

既存の Python language binding は CPU-only 既定で install できます。GPU integration は、
対応する外部 Capability package を明示的に追加します。

```bash
pip install aikernel-net
```

base Python package は Windows / Linux 向けの CPU-only universal
`py3-none-any` wheel として公開されています。0.1.2 CTG Core 更新では PyPI package を
公開しません。既存の Python ホストは、Python release が別途予定されるまで、最新の
公開済み `aikernel-net` distribution を利用してください。import package は
`aikernel_net` です。PyPI の `aikernel` は別プロジェクトです。GPU / native runtime は
明示的に追加する Capability install として扱います。

安定版 Python release は、Python release が明示的に予定された場合のみ PyPI に
`aikernel-net` として公開します。開発 build は CI/CD 検証向けに使う場合がありますが、
利用者向け release note は公開 package のみを単位とし、開発中の変更は次の公開
release entry に統合して記載します。

source-based local validation では repository subdirectory から install できます。

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

native toolchain の前提条件と wrapper-only 開発手順は
[python/README.md](python/README.md) を参照してください。

v0.1.2 Core package family は、AIKernel.NET の契約パッケージ
`AIKernel.Abstractions`、`AIKernel.Dtos`、`AIKernel.Enums` v0.1.2 と同期しています。
`AIKernel.Vfs` は独立した NuGet 依存ではなくなり、VFS 契約は
`AIKernel.Abstractions` から提供されます。`AIKernel.Vfs` namespace は、
in-process VFS provider / store の Core 実装 namespace として残りますが、
独立 NuGet package ではありません。

### 2. API ホスト向けに Core を登録する

OpenAI 互換 Provider の実行とモデル認証情報の保持は Server/API ホスト側で行います。
browser/WASM クライアントは独自 API 境界の背後に置き、モデル API キーを
WebAssembly クライアントへ配置しないでください。

```csharp
builder.Services
    .AddAIKernelCore(builder.Configuration)
    .WithOpenAI(
        builder.Configuration.GetSection("AIKernel:Providers:OpenAI"),
        (sp, options) =>
        {
            // Microsoft.Extensions.AI の IChatClient を返します。
            // Provider パッケージは、設定された ProviderId / ModelId に対応する
            // 既定 capabilities と prompt capability metadata を登録します。
            return CreateChatClient(options);
        });

builder.Services.AddAIKernelKernel();
```

設定例:

```json
{
  "AIKernel": {
    "Providers": {
      "OpenAI": {
        "ProviderId": "openai-compatible",
        "ModelId": "gpt-4.1-mini",
        "SecretKeyName": "OpenAI:ApiKey",
        "MaxInputTokens": 8192,
        "MaxOutputTokens": 1024
      }
    }
  },
  "OpenAI": {
    "ApiKey": "<user-secrets、Key Vault、または環境設定へ保存してください>"
  }
}
```

browser/WASM 向けクライアントでは、クライアント側 service collection に
browser-safe な VFS Provider だけを登録します。

```csharp
services.AddAIKernelBrowserVfsProviders();
```

`AddAIKernelCoreVfsProviders` は、ローカルファイルシステムアクセスが想定される
信頼済み Server / Desktop ホストでのみ使用してください。

外部 Capability モジュールや Model Provider をホストへ登録する場合は、
`KernelFacadeMetadataKeys.ProviderId` を使って request metadata から Provider を選択します。
これにより、AIKernel.Tools、AIKernel.RH、その他 Provider パッケージ間で
metadata 文字列を直書きせずに統合できます。

外部 Provider パッケージは、Assembly 参照された Provider とプロセス実行を包む
Adapter Provider のどちらも `WithModelProvider<TProvider>` で接続できます。
この拡張は `IModelProvider` 実装と複数の `ModelPromptCapability` を同時に登録できるため、
Core に Provider 固有の配線を追加せずに、ProviderId / ModelId を解決できます。

契約レベルの外部 Capability モジュールについては、Core は既定で in-memory の
`ICapabilityModuleRegistry` と fail-closed の `ICapabilityModuleInvoker` を登録します。
CLI、Assembly 参照、Native、DSL ROM、Remote module の descriptor は登録できますが、
実行権限は暗黙に付与されません。実際の module 実行は、信頼済みの Tools / Provider /
Host パッケージが既定 invoker を差し替えて提供します。
Core 標準 Provider は初期化時に安全な inline capability を登録します。
これにより、外部 Provider を読み込む前に `runtime.ping`、local DSL execution、read-only VFS access、
SKILL.md registration、system introspection の boot baseline が利用できます。
これらの invoker は dynamic provider registry にも登録されます。`SkillProvider` は
`SKILL.md` から runtime に Capability を発見するため、provider-level invoker として追跡されます。
Dynamic provider loading については、Core は stable provider registry contract の
Core-owned extension として `IDynamicProviderRegistry` を公開します。Host と CLI tool は、
Core に外部 Provider 依存を追加せずに、provider manifest の読み込み、capability metadata
登録、任意の provider assembly loading を行えます。
GPU と Native ABI の実装は外部 Capability package に分離します。たとえば
`AIKernel.Cuda13.0.Libtorch2.12.win-x64` が native bridge、runtime version metadata、CUDA 固有実装を
所有し、AIKernel Capability 契約に従います。
信頼済みホストでは、`AddAIKernelKernel()` が Core の memory abstraction の背後に
OS 固有の `IMemoryMapper`（Windows では `Win32MemoryMapper`、それ以外では
`PosixMemoryMapper`）を登録します。Native Capability package は Core 抽象のみを利用し、
Kernel を直接参照しません。

ユーザランド側の routing pipeline は、`ResultStep` / LINQ チェーンから
`AIKernel.Kernel.KernelProviderRoutingDecision` を返し、`AIKernel.Kernel`
の extension helper 経由で `KernelRequest` 本体と request metadata に適用できます。
これにより、低レベル / 高レベル LLM の切替や、`aik...` で始まるコンテキストを
CLI に紐づく Capability Adapter へ流す方針を、同じ ProviderId / ModelId 契約で扱えます。
Core が提供する構築時の guard は `KernelProviderRoutingDecisionFactory` が担い、
decision carrier 自体は Kernel facade package 内で behavior-free に維持します。

AIKernel.Core には、AI が生成した計画を扱う標準 JSON DSL pipeline runtime も含まれます。
DSL は決定論的な `ResultStep` pipeline に compile され、有限 `Loop` / `LoopUntil` /
`Suspend` node をサポートします。また、`rom/dsl/{namespace}/{name}.json` に DSL ROM
として保存し、`dsl://{namespace}/{name}` で再利用可能 capability として呼び出せます。
schema と運用規則は、AIKernel.NET の
`docs/architecture/18.DSL_PIPELINE_AND_ROM_SPEC-jp.md` にある正典文書で定義されています。

compile 済み DSL pipeline は C# の LINQ query 構文でも合成できます。`Select` と
通過した `where` predicate は純粋な投影で ReplayLog node を追加しません。
`SelectMany` は前段の出力を次の DSL pipeline の入力として渡し、`ResultStep` の
ReplayLog を連結します。失敗した `where` predicate は決定論的な reject node として記録します。

```csharp
IKernelPipeline observe = compiler.Compile(observeDocument).Value!;
IKernelPipeline decide = compiler.Compile(decideDocument).Value!;

IKernelPipeline agent =
    from first in observe
    where first.Data["last_capability"] == "Observe"
    from second in decide
    select second.With(
        "route",
        $"{first.Data["last_capability"]}->{second.Data["last_capability"]}");

var result = agent.Execute(DslPipelineExecutionContext.Create());
```

チャット履歴も immutable な HistoryROM asset として固定できます。
`HistoryRomStore.SaveHistoryAsRomAsync` は、順序付き chat record を署名済み Markdown ROM
へ変換し、VFS の `rom/history/{namespace}/{name}.md` に保存して、
`history://{namespace}/{name}` として登録します。HistoryROM の読み込みは他の Core ROM
asset と同じ ROM signature verification path を使い、hash mismatch や既存 history path
への異なる内容での上書きを拒否します。

---

## 目標とする起動体験

```text
[KERNEL] Initializing AIKernel.NET Core v0.1.2...
[KERNEL] Loading VFS Provider: local... [OK]
[KERNEL] Mounting ROM root... [OK]
[KERNEL] Building ContextSnapshot... [OK]
[KERNEL] Computing ContextHash... [OK]
[KERNEL] Resolving Provider: microsoft-ai.openai-compatible... [OK]
[KERNEL] Executing governed inference... [OK]

> Hello Intelligence.
> The Semantic Context is stable.
> Execution is reproducible. Governance is active.
```

---

## ランタイムフロー

AIKernel.NET Core は、最小実装では以下の実行経路を持ちます。

```text
KernelRequest
→ VFS Mount
→ ROM Load
→ Context Build
→ Governance Check
→ Prompt Composition
→ Provider Execution
→ IExecutionResult
```

初期実装フェーズでは、プロンプト合成は簡略化され、静的な構成になる可能性があります。

高度な Governance、署名強制、Semantic Cache、Deterministic Replay は段階的に拡張します。

---

## 開発ロードマップ

### v0.0.0 — Initial Repository Setup

AIKernel.Core リポジトリの初期セットアップを行うフェーズです。

AIKernel.NET 本体で確立された Contracts / DTO / Enum を前提に、Core 実装リポジトリとしての基本構造を準備します。

- リポジトリ構造の作成
- 基本プロジェクトテンプレートのセットアップ
- `src/` と `tests/` の初期配置
- Core / Kernel / Hosting / Provider / Tests のプロジェクト骨格を定義
- AIKernel.NET 契約パッケージへの依存関係を整理

この段階では、実行可能な Kernel を完成させることではなく、今後の Core 実装が迷わず進められる土台を作ることを目的とします。

---

### v0.0.x — Development of Core Runtime Components

v0.0.x では、v0.1.0 に向けて Core ランタイムコンポーネントを段階的に実装します。

v0.0.1 で固定された Canonical Contracts を前提にしつつ、実装上必要な命名・API 境界・テスト構成は、このフェーズで軽微にブラッシュアップします。

- Core ランタイムコンポーネントの段階的実装
- VFS / ROM / Context / Provider 連携の基礎整備
- ローカルファイルを VFS としてマウントする最小実装
- ROM ファイルの読み込みと Context への変換
- 静的 Prompt Composition による最小実行経路
- MicrosoftAI Provider ラッパーの初期実装
- Hosting / DI 統合の初期配線
- Unit Test / Integration Test の雛形整備
- v0.1.0 に向けた API・命名・契約境界の調整

このフェーズの目的は、完全な Governance や Deterministic Replay を実装することではありません。

まずは、AIKernel.Core が以下の最小実行経路を持つことを確認します。

`VFS → ROM → Context → Static Prompt → Provider → IExecutionResult`

---

### v0.1.0 — Synthesis: First Executable Runtime

v0.1.0 は、AIKernel.Core における最初の実行可能ランタイムです。

AIKernel.NET v0.0.1 で確立された Canonical Contracts を、Core 実装・Provider・テストによって実行可能な形へ統合します。

これは Issue #6 で定義した **Synthesis: Executable Contracts** の Core 側対応フェーズです。

- 最小 Core ランタイム
- VFS ベースの ROM 読み込み
- ContextSnapshot の基礎
- MicrosoftAI Provider ラッパー
- 初期 Hosting 統合
- ROM から推論までの最初の実行可能経路
- 基本的な `IExecutionResult` の生成
- ContextHash の出力
- Contract Test 雛形との接続

このリリースの目的は、AIKernel.NET の思想をすべて実装し切ることではありません。

目的は、AIKernel が **「知識を読み込み、文脈を構成し、Provider を通じて推論を実行できる」** ことを、最小構成で証明することです。

---

## Roadmap Note

AIKernel.Core の開発は、AIKernel.NET の Canonical Contracts を前提として進みます。

AIKernel.NET は契約を定義する。  
AIKernel.Core はそれを実装によって証明する。

v0.0.x では Core ランタイムの構成要素を段階的に実装し、v0.1.0 で最初の実行可能ランタイムとして統合します。

このロードマップは、既存の AIKernel.NET リリースノートおよび Issue #6 で示された流れ――  
**Init → Fix → Synthesis**  
を壊さず、Core 実装リポジトリ側で具体化するためのものです。

---

## リポジトリの関係

AIKernel.NET Core は、メインの AIKernel.NET 契約リポジトリで定義された正典契約パッケージに依存します。

```text
AIKernel.NET
= contracts, DTOs, enums, documentation, contract-test skeletons

        ↓

AIKernel.Core
= concrete runtime implementation and standard providers

        ↓

AIKernel.Providers.*
= external model and service integrations
```

AIKernel.NET は契約を定義する。  
AIKernel.Core はそれを実装によって証明する。

---

## コントリビュータ向けガイドライン

Core の変更は、AIKernel 共通の開発規律に従ってください。

- [AIKernel 開発ガイドライン](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES-jp.md)
- [AIKernel Development Guidelines](../AIKernel.NET/docs/guidelines/AIKERNEL_DEVELOPMENT_GUIDELINES.md)

このガイドラインは、package code に求められる monadic LINQ style、
fail-closed behavior、DRY/DGA rules、public API の bilingual comment、
test、release check を定義します。

---

## ライセンス

Apache License 2.0.
詳細は `LICENSE` ファイルを参照してください。
