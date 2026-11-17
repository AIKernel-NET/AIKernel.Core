# B1: LoopUntil の実行開始時刻

検証日: 2026-11-12 (JST)。v0.1.3 のレビュー指摘を0.1.4向けに修正する項目です。
対象: AIKernel.Core、dev `dd8b258` から作成した `review/B1`。

## 再現と修正

LocalExecutionInvoker は開始時刻をUnixEpochに固定し、DSL ROM呼出しも同じ既定値を使っていました。
一方、LoopUntilはコンパイル時に渡されたIKernelClockで経過時間を計算します。
KernelClock.System()を使った30秒・3反復のDSLで、inlineとROMの両方とも本体が0回のまま終了することを再現しました。

暗黙的な呼出しの開始時刻を、対象CompiledDslPipelineのIKernelClockから取得します。
実行ごとに取得し、同じpipeline内の入れ子ループには同じ開始時刻を使います。
LoopUntilに到達する前の処理時間も期限に含めます。ROMは呼び出されたROM自身の実行開始が基準です。

時刻を使わないpipeline、maxIterations=0のLoopUntil、0回Loopの中だけにあるLoopUntilには、時計読取りを追加しません。
公開ExecuteAsyncに渡された明示的なStartedAtUtcはそのまま使い、古い開始時刻のtimeoutを解除しません。
外部のIKernelPipeline実装には、従来と同じUnixEpochの文脈を渡します。外部実装の時計を推測しません。
時計取得の失敗は既存の例外境界で失敗になり、本体を実行しません。

公開インターフェース・DTO・enum、Canon、StepId/ReplayLogHash/ROM hashの計算式・形式は変更していません。
固定epochの既存実行とReplayLogHashが等しいことも確認します。
実時計のリプレイは、開始時刻の読取りを含むIKernelClock.Nowの列を記録・再供給する方法で検証します。
新しいリプレイファイル形式や時計記録APIは追加していません。

timeout_reachedが成功として返る既存ステータスは、この項目では変更しません。状態区分の変更は承認待ちC1の範囲です。
実時計で途中まで実行できるようになったケースの出力・トレースは変わりますが、ハッシュ計算式の変更ではありません。

## 回帰テスト

`tests/AIKernel.Core.Tests/Dsl/LoopUntilClockTests.cs` に21件追加しました。
修正前は10件失敗・11件成功、修正後は21件成功。既存のテストコード・期待値は変更していません。

| 検証 | 件数 |
|---|---:|
| 実時計、30秒・3反復、時計記録からの出力/ReplayLogHash一致（inline/ROM） | 2 |
| 29秒では続行、30秒ちょうどで停止、リプレイ一致 | 2 |
| 1時間後の再呼出しでも新しい開始時刻を使う | 2 |
| timeout=0では本体0回 | 2 |
| maxIterations=0で時計を読まない | 2 |
| 時刻を使わないpipelineで時計を読まない | 2 |
| 開始時刻取得失敗で本体を実行しない | 2 |
| ループより前の処理時間を期限に含む | 2 |
| 入れ子ループで開始時刻を再設定しない | 2 |
| 明示開始時刻を上書きせず、期限超過を維持 | 1 |
| 固定epoch時の既存の直接実行とReplayLogHash一致 | 2 |

## 全体・下流検証

Coreと下流7件のrestore/buildが成功しました。**945件成功、DoomのS0既知失敗2件、新規失敗なし**。

| Repository | build | 成功 | 既知失敗 |
|---|---|---:|---:|
| Core | 成功 | 460 | 0 |
| Providers | 成功 | 120 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

Coreの修正DLLはローカルnupkg・隔離cache・下流の25個の出力で一致しました。
DLL SHA-256: `ccfa5d788a8554fb9b8592d8625bef24f9e2faec91a8e4bdf066c3e5591facfc`。
Doomの失敗はHUD_UNIFORM_FLOAT_COUNTの32/36不一致による、S0と同じ2件です。

証跡: `D:\AIKernel\.codex_tmp\review-b1-20261112`。
下流は各devのgit archiveによる隔離コピーを使い、作業中ブランチと未コミット変更を保持します。
Core/Common/Hosting/Kernelをローカルフィードへpackし、下流の隔離キャッシュへ復元します。
この検証専用の0.1.3パッケージは公開物ではありません。既存のグローバルキャッシュや配布物を置き換えません。
Windowsで検証します。Linux・CUDA実機・実Cosmos・TheoriaCast共同受入は今回の対象外です。

## 0.1.4に向けた残作業

今回の完了項目はB1のみです。パッケージ版の一括変更、main/devへの取り込み、push、PR、タグ、公開は行いません。
G1/G2/V1/CT1/WS2はreviewブランチがあり、dev履歴に完了コミットがないためレビュー待ちです。
Cosmosのdev13もreview/COSMOS-P0でレビュー待ちであり、実機受入済みではありません。

元の順序に従う次の着手可能項目はB4（キャンセル判定の統一）です。
H1aはG2/V1のdev取り込み待ちです。
CT2/PV1/PV2/PV3/WS1/B2/B3など、行頭が▲の項目は元の指示どおり未実装です。
特にファイル・ネットワーク境界、応答打切り、WASM hash検証は、S0の提案に対する人間の承認・案の選択が必要です。
今回のB1成功だけで0.1.4を公開可能とは判定しません。
