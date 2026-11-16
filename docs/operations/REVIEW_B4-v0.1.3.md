# B4: キャンセル判定の統一

検証日: 2026-11-12〜13 (JST)。v0.1.3 のレビュー指摘を0.1.4向けに修正する項目です。
Core (`dd8b258`) / Providers (`db7e989`) / Control (`2229477`) / Wasm (`2c6b11f`) の各devから作成した `review/B4` が対象です。

## 再現と修正

KernelExecutionStepRunnerはErrorContextへ変換した後の `exception_type` を
OperationCanceledExceptionの型名と完全一致で比較していました。
TaskCanceledExceptionと独自の派生例外を通常の `execution_failed` に分類する問題を再現しました。
修正前の54件は26件失敗・28件成功、修正後は54件成功です。

AIKernel.Common.Results.CancellationExceptionClassifier.IsCancellation(Exception?) を共通入口として追加します。
判定は元の例外の継承関係だけを使います。
Capability解決、Prompt生成、Provider実行、Tokenizerの4境界では、ErrorContextへの投影前に判定します。

- OperationCanceledException、TaskCanceledException、任意の派生型を取消とします。
- リンク済みトークン、内部・別のトークン、トークンなしでも同じ判定です。
  呼出元と例外のトークンの同一性を要求しません。
- 呼出元トークンが取消済みでも、通常例外・TimeoutException・取消を内包するラッパー・AggregateExceptionを取消へ読み替えません。
- 型名・メッセージ・Metadataから取消を推測しません。nullも取消ではありません。
- 元からCanceled結果を返すAPIはその形式を維持し、取消例外を伝播するAPIは同じ例外を伝播します。

汎用Try / Result / ErrorContextの例外投影は変更しません。
公開済みインターフェース・DTO・enum、Canon、ハッシュの計算式と形式は変更していません。
新しい共通helperは追加APIです。既存のOperationCanceledExceptionによる実行と、派生型・リンク済み取消の
Metadata / ReplayLogHashが一致することを検証します。
従来誤分類していたケースはFailedからCanceledへ変わりますが、成功・許可にはなりません。
そのケースのトレース内容は修正されるため、従来の誤分類時とのハッシュ一致は求めません。
既存の正しい取消と同じトレースになることを確認し、計算方式は維持します。

## 全.NETリポジトリの確認範囲

| Repository | 対応 |
|---|---|
| NET | 独自の例外分類なし。契約・DTO・enumを変更しない |
| Core | 共通helper追加、StepRunner・ContextAssembler・Kernelの判定を集約 |
| Providers | OpenAICompatibleProviderを共通helperへ。ManifestLoaderの不要なcatch/rethrowを削除 |
| Cuda13.0 | 独自の取消例外分類なし |
| Control | CtgControlCoordinator・ControlEmulatorEngineを共通helperへ |
| Tools | 独自の取消例外分類なし |
| Wasm | WebGpuResidentModelProviderを共通helperへ |
| Demo | 独自の取消例外分類なし |
| Doom | 独自の取消例外分類なし |

SubstrateはIO等の例外だけを捕捉しており、取消は自然に伝播できます。
不要なcatch/rethrowを取り除き、依存のないSubstrateへCommon参照を追加しません。
各APIの開始時のThrowIfCancellationRequested / IsCancellationRequestedや、例外を分類しない汎用処理は維持します。
これは取消例外の分類統一です。同期処理を途中で中断可能にするPF3や、停止済みを保証するAPIの追加ではありません。

## 追加した回帰テスト

既存のテストコード・期待値は変更していません。追加は88件です。

| 対象 | 件数 | 主な内容 |
|---|---:|---|
| Core execution | 54 | 4境界×9取消、4境界×4通常エラー、実際に取消済みのTaskをawaitする2経路 |
| Core compatibility | 4 | null判定、汎用Tryの既存の投影とMetadataを維持 |
| MicrosoftAIProvider | 6 | 元の取消例外・トークンを維持、通常エラー・Timeoutのラップを維持 |
| ManifestLoader | 6 | 派生型・linked取消の伝播、取消済みファイル読取、IOの構造化失敗を維持 |
| Control | 12 | coordinatorの伝播とemulatorのCanceled結果、通常失敗を維持 |
| Wasm model | 6 | GPU割当て前の取消伝播、通常エラーの構造化失敗を維持 |

Wasmテストは既定のNullWebGpuBackendを使い、bindings列挙で例外を注入します。
GPUバッファ作成・カーネル実行・モデルロードは行いません。

## 全体・下流検証

9リポジトリのrestore / Release buildが成功しました。SDKは10.0.401です。
**1,185件成功、DoomのS0既知失敗2件、新規失敗なし**。追加88件はすべて成功しました。

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| NET | 成功 | 173 | 0 |
| Core | 成功 | 497 | 0 |
| Providers | 成功 | 132 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 54 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 70 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

Doomの失敗はHUD_UNIFORM_FLOAT_COUNTの32/36不一致による以下の既知2件です。

- DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets
- DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping

9種の修正パッケージのDLLを、ローカルnupkg・8種の復元済み隔離cache・全107出力で照合しました。
Commonは34出力で一致。SHA-256は `dc52e9a5f5367148f32cf9e0254d192560080d3a2716de47b86d3aff23ea2ada` です。
Wasm.Modelsはこのdevの下流から参照されないため、cache再復元ではなくnupkgと自身・テストの2出力を照合しています。
各DLLの値と対象パスは `verified-results.json` に保存しています。

証跡: `D:\AIKernel\.codex_tmp\review-b4-20261112`。
未変更の5リポジトリはdevのgit archiveによる隔離コピーを使用します。
4つの変更リポジトリはreview/B4を使い、変更したCommon/Core/Hosting/Kernel、MicrosoftAI/Substrate、
Control.Core/Emulator、Wasm.Modelsを順にローカルフィードへpackして下流へ供給します。
この検証専用の0.1.3パッケージは公開物ではなく、グローバルキャッシュや配布物は置き換えません。
各リポジトリのrestore / Release build / test、TRX、pack結果を保存しています。
Controlの追加テストで生じたnull警告は解消後にbuild / testを再実行し、最終TRXは `control-final` に保存しました。
最終buildは全9リポジトリで警告0・エラー0です。

実行コマンドの主要部分:

```powershell
dotnet restore <solution> --configfile <scratch>/NuGet.config --packages <scratch>/packages --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build <solution> -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test <solution> -c Release --no-build --no-restore --logger trx --results-directory <scratch>/results -m:2 -nr:false
dotnet pack <changed-project> -c Release --no-build --no-restore -o <scratch>/feed -m:2 -nr:false
```

Windowsで検証します。Linux、CUDA実機、実Cosmos、TheoriaCast共同受入は今回の検証結果に含みません。

## 取り込みと0.1.4への残作業

4リポジトリのreview/B4は組としてレビューしてください。
消費側の新DLLはCancellationExceptionClassifierを含む新Commonが必要です。
0.1.4候補を作る際はCommonと消費側のパッケージ版・依存下限を揃え、公開済み0.1.3 Commonだけで
新しい消費側DLLを実行しないでください。本項目では版番号の一括変更・公開は行いません。

既存のreview/B1、review/CT1、review/COSMOS-P0などは保持し、混ぜていません。
Cosmosの未取り込み実装は今回のdev対象には含まれません。
それらをdevへ取り込んだ後、統合した状態で取消のAPI境界を再確認する必要があります。

完了項目はB4のみです。dev/mainへの取り込み、push、PR、タグ、公開は行いません。
元の順序で次に着手可能なのはI1です。H1aはG2/V1のdev取り込み待ちです。
▲のCT2/PV1/PV2/PV3/WS1/B2/B3等は承認・案の選択待ちであり、B4では実装しません。
B4の成功だけで0.1.4を公開可能とは判定しません。
