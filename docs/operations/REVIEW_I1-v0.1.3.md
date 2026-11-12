# I1: default・未初期化値の失敗維持とCTG検証

検証日: 2026-11-13 (JST)。v0.1.3のレビュー指摘を0.1.4向けに修正する項目です。
対象: AIKernel.Core、dev `dd8b258` から作成した `review/I1`。
既存のreview/B4等は保持し、このブランチへ取り込んでいません。

## 確認した挙動と修正

Result / ResultStepのdefault、引数なしnew、未初期化配列要素は、既にIsSuccess=false / IsFailure=trueでした。
同期・非同期のMap / Bind / SelectMany、Tap、Where、Match、結果の相互変換、ループを通じても
成功側の処理を実行しないことをテストで固定します。
ErrorContextは参照型なのでdefaultはnullです。null、空のJSON、空フィールド、任意項目未設定のエラーを
Failへ渡しても成功に昇格しないことを確認します。
明示的なSuccess / Okで渡す0・false・空文字は有効な値として維持します。
承認待ちI2のSuccess(null)の扱いは変更しません。

一方、CtgTrajectoryGateEvaluatorはDenyと等しい値だけで停止していました。
new StepGovernanceTraceや空JSON由来のDecisionGateResultのUnknownが、Continue / Accepted=trueへ進む問題を再現しました。
部分的に初期化されたAllow結果も、Succeeded / Acceptedが未設定のまま継続していました。

軌跡の継続条件を、各ステップのDecisionGateが次のすべてを満たすことにします。

- 結果が存在する。
- Succeeded=true（評価完了）。
- DecisionKind=Allow（明示的な許可）。
- Accepted=true（許可フラグとの整合）。

default、未設定、Unknown、未定義のDecisionKind、nullステップ／null判定はHalt / Accepted=falseになります。
同じ条件を判定と理由生成で共用し、最初に継続できない位置のindexとstep IDを残します。
不完全な結果には既存のRejectReasonKind.FailClosed / FAIL_CLOSEDを使い、ReasonIdは `ctg.trajectory.fail_closed` です。
step IDが欠ける場合は空文字とします。既存の明示的DenyはSTEP_DENIED、空の軌跡はIMPLICIT_DENYのままです。

Succeededは評価APIの処理完了を表します。Halt / Denyを正しく評価した場合もSucceeded=trueであり、
許可はAcceptedとDecisionKindで判断します。新しい判断結果を実行完了として扱うものではありません。

## 契約と正典

公開済みインターフェース・DTO・enum、rom/governance、Result / ResultStep / ErrorContextの実装、
StepId / ReplayLogHash / ROM hashの計算式・形式は変更していません。
新しい実装はCore内のprivate / internal処理だけです。

Canonが対象とする「評価済みAllow / Denyの列」では従来どおり、全AllowならContinue、DenyがあればHaltです。
今回はこの入力条件を満たさない不完全な結果を許可列へ紛れ込ませないための検証です。
評議会の再評価、連続スコア、内容判断などは追加しません。
従来誤ってContinueだった入力の結果・トレースはHaltへ変わりますが、正常な判定の意味は変更しません。
CT1の未定義投票値修正はreview/CT1でレビュー待ちのため、このdevブランチには混ぜていません。

## 追加テスト

追加66件。修正前は34件失敗・32件成功、修正後は66件成功です。
既存のテストコード・期待値は変更していません。

| テスト | 件数 |
|---|---:|
| Resultのdefault / new / 配列要素、値型・参照型、同期・非同期合成と副作用の抑止 | 6 |
| ResultStepの同条件、結果変換・状態変換・Loop / LoopUntilの抑止 | 6 |
| ErrorContextの欠損・空値が失敗を維持 | 4 |
| 成功から呼んだbinderがdefaultを返した場合、後続の処理を停止（同期・非同期） | 2 |
| 明示的成功の0・false・空文字を維持 | 1 |
| CTG DTO・enumの既定状態 | 1 |
| 欠損投票、空JSON、null入力をDenyにする | 6 |
| 未設定のCouncilEvaluationに承認票がない | 1 |
| 11種類の不完全ステップ×3位置をHaltにし、最初の失敗を記録 | 33 |
| 空・JSON・nullのステップ列をHaltにする | 3 |
| trace assembler経由でもdefault判定が許可にならない | 1 |
| 明示的Allow / Denyと最初のDeny理由の互換性 | 1 |
| null要求を判定前に例外で拒否 | 1 |

ファイル:

- `tests/AIKernel.Core.Tests/Common/DefaultValueSafetyTests.cs`
- `tests/AIKernel.Core.Tests/Governance/CtgDefaultSafetyTests.cs`

## 全体・下流検証

9リポジトリのrestore / Release buildが成功しました。SDKは10.0.401です。
**1,163件成功、DoomのS0既知失敗2件、新規失敗なし**。追加66件はすべて成功しました。

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| NET | 成功 | 173 | 0 |
| Core | 成功 | 505 | 0 |
| Providers | 成功 | 120 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

Doomの失敗はHUD_UNIFORM_FLOAT_COUNTの32/36不一致による以下の既知2件です。

- DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets
- DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping

Common / Core / Hosting / Kernelの4種のDLLは、ローカルnupkg・隔離cache・計77出力で一致しました。
Coreは30出力で一致しています。各DLLのSHA-256と対象パスは `verified-results.json` に保存しています。

証跡: `D:\AIKernel\.codex_tmp\review-i1-20261113`。
Coreはreview/I1、他8リポジトリは各devのgit archiveによる隔離コピーを使用します。
Core / Common / Hosting / Kernelを検証専用フィードへpackして下流へ供給し、DLLを照合します。
ローカルの0.1.3再packは配布物ではなく、グローバルキャッシュや公開済みパッケージは置き換えません。
restore / Release build / testのログ、TRX、pack結果、各devのHEADを保存しています。

実行コマンドの主要部分:

```powershell
dotnet restore <solution> --configfile <scratch>/NuGet.config --packages <scratch>/packages --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build <solution> -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test <solution> -c Release --no-build --no-restore --logger trx --results-directory <scratch>/results -m:2 -nr:false
dotnet pack <core-project> -c Release --no-build --no-restore -o <scratch>/feed -m:2 -nr:false
```

Windowsの.NETで検証します。Linux、実GPU、実Cosmos、TheoriaCast共同受入、DoomのEmscripten再ビルドは含みません。

## 追加で見つかった問題（未修正）

default(ResultStep<int,int>)のReplayLogはnullです。
その値へ直接WithSemanticDelta(SemanticDelta.Empty)を呼ぶとNullReferenceExceptionになります。
default(Result<int>)のErrorもnullで、失敗状態であってもエラー情報の存在は保証されていません。
今回のテストはこれらを成功と見なさず、成功側の処理を実行しないことを検証しています。
トレース追加時の正規化やエラー情報の補完は別件として報告し、本項目では実装しません。
小さな診断の結果を `additional-finding.log` に保存しています。

## 次の項目と人間の判断

完了項目はI1のみです。review/I1のレビューとdevへの取り込みは人間が行います。
push、PR、main/devへの取り込み、タグ、パッケージ公開は行いません。
既存review/B4等との統合後の検証はリリース前に必要です。

元の順序で次の着手可能項目はI4です。I2は承認待ち、H1aはG2/V1のdev取り込み待ちです。
上記の未初期化トレースの例外と、失敗時のError情報保証を修正する場合は別項目として範囲を決めてください。
I1だけで0.1.4を公開可能とは判定しません。
