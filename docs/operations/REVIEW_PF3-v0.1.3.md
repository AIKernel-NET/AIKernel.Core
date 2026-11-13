# PF3: DSLの非同期実行とループ途中のキャンセル

検証日: 2026-11-13。対象はCoreのPF3のみです。
`dev` (`dd8b258`) 起点の `review/PF3` で作業しています。
CUDA、B1、B4、PF1等のレビュー待ちブランチは取り込んでいません。

## 再現

公開 `IKernelPipeline.ExecuteAsync` は受付時のCancellationTokenだけを検査し、
同期 `Execute` を呼んでいました。Loop/LoopUntilの本体で最初の反復後にキャンセルしても
後続を実行して正常終了することを、改修前の追加テスト2件で再現しました。
ROMの公開InvokeAsyncも同期経路を呼び、LocalExecutionのTry/非同期Result連結は
キャンセル例外を一般のエラーへ変換していました。

## 変更

- 内部のパイプラインと能力レジストリに非同期経路を追加し、公開済みのExecuteAsync/InvokeAsyncから接続しました。
  NETの公開インターフェース、DTO、enumの追加・変更はありません。
- 各ノード、各反復、能力呼出しの前後でトークンを確認します。空のループでも確認し、
  入れ子のROMと非同期能力にも同じトークンを渡してawaitします。
- 実行開始と反復境界では呼出元のSynchronizationContextを捕捉せずに処理を譲ります。
  同期能力だけのDSLでも、ループ全体が終了するまでExecuteAsyncの呼出しを同期的にブロックしません。
- 同期Execute/Invokeは残しました。同期実装しかない内部の能力・パイプラインは既定の非同期アダプターで呼べます。
- OperationCanceledExceptionとその派生は呼出元へ伝播します。LocalExecutionの呼出し経路でも
  通常エラーに変換しません。一般の能力エラー、失敗、Suspend、タイムアウトの既存の意味は維持します。
  C1の状態体系や、共通Resultの例外処理方針は変更していません。
- 同期実行と同じ記録生成関数を使い、同期経路のStepId、ReplayLog、ReplayLogHashの計算は変更していません。

## 制限と互換性

キャンセルは協調的です。同期コールバックやトークンを無視する非同期能力を強制終了しません。
それらが終了するまでは実行Taskを保ち、戻った時点でキャンセルを確認して値を破棄します。
キャンセルによって裏で動く処理を切り離したり、次の反復を起動したりしません。
キャンセル時は成功DTOや部分的な成功結果を返さず、Taskがキャンセルで終了します。
すでに実行済みの外部作用の巻き戻しは行いません。

再現性の比較は同じ入力・能力結果・時計を前提とします。実時計を使う実行の所要時間や
観測タイムスタンプまで同期実行と同一であるという保証ではありません。
開始時刻のB1、実行コスト上限のPF2、ReplayLogの構築コストのPF1は今回の変更に含めません。

B1との統合時は、`DslRomCapabilityRegistry.Async.cs`のROM実行文脈にも
B1の`GetExecutionStartedAtUtc()`による開始時刻を反映してください。現時点ではdevの同期経路と同じ
UnixEpochを使っています。LocalExecutionの実行ヘルパーは両ブランチで変更されるため、
統合時はB1の開始時刻とPF3のキャンセル伝播を両方保持して再検証する必要があります。

現行devのLoopUntilは`timeout_reached`でも成功の結果を返し、既存の
`DslPipelineCompilerTests`でも`Assert.True(result.IsSuccess)`が期待値です。
レビュー文面C1の「タイムアウトをFailedで返す」とは異なります。PF3ではこの意味や期待値を変えず、
非同期経路も同じ結果と記録を返すことを確認しました。状態を見直す場合はC1で別途判断してください。

## 検証結果

Windowsで9リポジトリのrestore/buildが成功しました。テストは合計1,135件成功、
Doomの既知の2件のみ失敗しました。Coreのbuildは警告・エラーともに0件です。

| リポジトリ | 成功 | 失敗 |
| --- | ---: | ---: |
| NET | 173 | 0 |
| Core | 477 | 0 |
| Providers | 120 | 0 |
| Cuda13.0 | 21 | 0 |
| Control | 42 | 0 |
| Tools | 34 | 0 |
| Wasm | 64 | 0 |
| Demo | 71 | 0 |
| Doom | 133 | 2（既知） |

既知の失敗は以下の2件です。前回PF1の記録とテスト名・成否・エラーメッセージが一致しました。

- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`
- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`

どちらも`const HUD_UNIFORM_FLOAT_COUNT = 32`を探す既存Assert.Containsの失敗です。
期待値は変更していません。PF1で追加された専用テストを除く既存テストの名前と成否を照合し、
削除・失敗の増加がないことを確認しました。

生成元・候補nupkg・隔離NuGetキャッシュ・今回ビルドしたソリューション出力のDLLをSHA-256で照合しました。

| DLL | 一致した出力数 |
| --- | ---: |
| AIKernel.Common | 34 |
| AIKernel.Core | 30 |
| AIKernel.Hosting | 7 |
| AIKernel.Kernel | 6 |

機械可読の集計は`verified-results.json`、全体試験は`results/*.trx`、
改修前の再現は`reproduction/reproduction.trx`、追加38件の一覧は`new-test-cases.txt`にあります。
Linuxでのbuild/testとGitHub CIは今回実行していません。検証用パッケージは現行の0.1.3設定で
隔離フィードへ作成したもので、公開・本番導入用ではありません。

追加テスト38件は、Loop/LoopUntil・空の本体・入れ子・受付前の取消、待機中の能力へのトークン伝播、
遅延成功の破棄、同期能力による呼出元スレッドの占有防止、リンクされたトークンと取消例外の派生型、
ROMの公開InvokeAsyncとパイプライン経由の取消、LocalExecutionの取消と一般エラーを検証します。
同期／非同期の比較では、成功・失敗・Suspend・タイムアウト・不正入力を含む17ケースと
入れ子のROMの成功／失敗2ケースについて、値・エラー・状態・全ReplayLogエントリー・StepId・ReplayLogHashを比較します。

証跡: `D:\AIKernel\.codex_tmp\review-pf3-20261113`。
Coreはこのレビュー系列、他のリポジトリはdevの隔離コピーを使います。
検証専用フィードとキャッシュに候補パッケージを置き、実際に下流で読み込んだDLLを照合します。
既存のテスト、dev/main、他のreviewブランチ、ユーザーの未コミット変更は保持します。
push、PR、devへの統合、タグ、パッケージ公開は行いません。
