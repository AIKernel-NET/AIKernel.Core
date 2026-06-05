# AIKernel.NET Core

![AIKernel.NET Logo](assets/aikernel-logo.png)

**AIKernel.NET Core** は、LLM アプリケーションにおける **文脈の暴走・再現性の欠如・ガバナンス不能** を解決するためのランタイムです。

.NET エコシステムのために設計された、決定論的かつ不変な **知能の OS（Knowledge OS）** の実装です。

単なる LLM のラッパーではありません。

AIKernel.NET Core は、知識資産（**ROM**）の管理、文脈（**Context**）の統治、そして推論実行を制御されたトランザクションとして扱うための、堅牢な実行基盤を提供します。

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

## ソリューション構造

本リポジトリは、ランタイム、Provider、検証レイヤーで構成されています。

```text
src/
  AIKernel.Core
  AIKernel.Kernel
  AIKernel.Hosting
  Providers/
    AIKernel.Providers.MicrosoftAI

tests/
  AIKernel.TestKit
  AIKernel.Core.Tests
  AIKernel.IntegrationTests
```

### `src/` — Runtime Implementation

#### `AIKernel.Core`

知識の相転移を担う純粋論理エンジンです。

```text
VFS → ROM → Context → Execution
```

このレイヤーは Core ランタイムロジックを担当し、外部 Hosting や Provider 境界から実装上の関心事を分離します。

#### `AIKernel.Kernel`

OS の統治・オーケストレーション層です。

`IKernel` Facade を公開し、全ランタイムレイヤーを統合します。

#### `AIKernel.Hosting`

.NET アプリケーションにおける点火スイッチです。

ASP.NET Core / Generic Host 向けに、`IServiceCollection` 拡張と既定の配線を提供します。

#### `Providers/`

AIKernel と外部モデル・外部サービスを接続する Provider 実装群です。

##### `AIKernel.Providers.MicrosoftAI`

`Microsoft.Extensions.AI`（MEAI）を利用した OpenAI 互換 Provider 実装です。

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

---

## クイックスタート

### 1. パッケージのインストール

```bash
dotnet add package AIKernel.Core --version 0.0.5
dotnet add package AIKernel.Hosting --version 0.0.5
dotnet add package AIKernel.Kernel --version 0.0.5
dotnet add package AIKernel.Providers.MicrosoftAI --version 0.0.5
```

関数型プリミティブや Contract Test helper を直接利用する場合:

```bash
dotnet add package AIKernel.Common --version 0.0.5
dotnet add package AIKernel.TestKit --version 0.0.5
```

v0.0.5 package family は、AIKernel.NET の契約パッケージ
`AIKernel.Abstractions`、`AIKernel.Dtos`、`AIKernel.Enums` v0.0.5 と同期しています。
`AIKernel.Vfs` は独立した NuGet 依存ではなくなり、VFS 契約は
`AIKernel.Abstractions` から提供されます。

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

ユーザランド側の routing pipeline は、`ResultStep` / LINQ チェーンから
`KernelProviderRoutingDecision` を返し、それを `KernelRequest` 本体と
request metadata に適用できます。
これにより、低レベル / 高レベル LLM の切替や、`aik...` で始まるコンテキストを
CLI に紐づく Capability Adapter へ流す方針を、同じ ProviderId / ModelId 契約で扱えます。

AIKernel.Core には、AI が生成した計画を扱う標準 JSON DSL pipeline runtime も含まれます。
DSL は決定論的な `ResultStep` pipeline に compile され、有限 `Loop` / `LoopUntil` /
`Suspend` node をサポートします。また、`rom/dsl/{namespace}/{name}.json` に DSL ROM
として保存し、`dsl://{namespace}/{name}` で再利用可能 capability として呼び出せます。
schema と運用規則は、AIKernel.NET の
`docs/architecture/18.DSL_PIPELINE_AND_ROM_SPEC-jp.md` にある正典文書で定義されています。

チャット履歴も immutable な HistoryROM asset として固定できます。
`HistoryRomStore.SaveHistoryAsRomAsync` は、順序付き chat record を署名済み Markdown ROM
へ変換し、VFS の `rom/history/{namespace}/{name}.md` に保存して、
`history://{namespace}/{name}` として登録します。HistoryROM の読み込みは他の Core ROM
asset と同じ ROM signature verification path を使い、hash mismatch や既存 history path
への異なる内容での上書きを拒否します。

---

## 目標とする起動体験

```text
[KERNEL] Initializing AIKernel.NET Core v0.1.0...
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

## ライセンス

MIT License.  
詳細は `LICENSE` ファイルを参照してください。
