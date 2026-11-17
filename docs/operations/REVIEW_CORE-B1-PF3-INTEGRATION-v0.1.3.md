# Core B1・PF3 統合候補の検証記録

検証日: 2026-11-17 (JST)。0.1.4 向けの統合候補。dev への取り込み・push・PR・公開は行っていない。

## 対象と出自

- ベース: Core dev `63f4ebe75038f007a8ebe29adbd1ed81d6c233be`。G1・G2・V1 と CT1・I1・B4・I4・PF1・V2 は統合済み。
- 候補: `review/CORE-B1-PF3-integration`。
- 検証した実装・テスト: `8f51a5a91492c34ae6d105f9e824497525532675`。以後は本記録だけを追加する。
- 上流 NET dev: `37a71a958857de6efea2458dfb0a49d07a4c9462`。
- 作業場所: `D:/AIKernel/repos/AIKernel.Core`。開始時にクリーンだった dev のチェックアウトを再利用。

| 項目 | 採用した元コミット | 統合コミット |
|---|---|---|
| B1 | `e0a3a1e721f82180253e127e67d16574566e5ad3` | `67c4570b3425f551987858f827e7f234b343a4fe` |
| PF3 | `50d5945b14d5e8e4d7cdd4dd933d53aeec439847` | `f19d58a41ded70f2bb46d62a143d45a5476c837e` |

B1 を先に `--no-ff` でマージし、続いて PF3 をマージした。
競合は `LocalExecutionInvoker.ExecutePipelineAsync` の 1 箇所。
B1 の開始時刻取得を PF3 の明示的な await / 例外伝播処理へ組み込み、両方を保持した。
他の review ブランチは追加で取り込んでいない。
前回除外した CT1 内の CUDA 関連 7 コミットも、引き続き候補の祖先に含まれない。

## 再現した統合上の不足と修正

PF3 の単独記録にあった注意点を確認した。単純なマージだけでは、同期 ROM は B1 の時計を使う一方、
新しい `DslRomCapabilityRegistry.InvokeAsync` は UnixEpoch の開始時刻を作るままだった。
LocalExecution の競合を解消した直後の `f19d58a41ded70f2bb46d62a143d45a5476c837e` で既存 B1 テストを実行すると、
**21 件中 16 件合格・5 件失敗**。失敗はすべて ROM 経路で、次の 5 件だった。

- `AIKernel.Core.Tests.Dsl.LoopUntilClockTests.EachInvocationGetsItsOwnClockOrigin(rom: True)`
- `AIKernel.Core.Tests.Dsl.LoopUntilClockTests.AdvancingClockStopsExactlyAtDeadlineAndReplays(rom: True)`
- `AIKernel.Core.Tests.Dsl.LoopUntilClockTests.SystemClockRunsThreeBodiesAndRecordedClockReplaysIdentically(rom: True)`
- `AIKernel.Core.Tests.Dsl.LoopUntilClockTests.NestedLoopsShareThePipelineOrigin(rom: True)`
- `AIKernel.Core.Tests.Dsl.LoopUntilClockTests.TimeSpentBeforeTheLoopCountsTowardsItsDeadline(rom: True)`

統合時の追加修正:

- 同期・非同期 ROM が同じ非公開 `CreateExecutionContext` を使い、呼び出した ROM 自身の
  `CompiledDslPipeline.GetExecutionStartedAtUtc()` から開始時刻を取得する。
  外側の未時限パイプラインの時計を ROM へ流用しない。
- LocalExecution は B1 の暗黙開始時刻と PF3 の取消伝播を両方保持。
- PF3 が新設した 7 箇所のキャンセル例外判定を、統合済み B4 の
  `CancellationExceptionClassifier.IsCancellation` へ接続。派生例外とリンクされたトークンを同じ方針で扱う。
- 組合せ用テスト 11 件を新しいファイルへ追加。

追加調整の製品コードは CompiledDslPipeline.Async.cs、DslRomCapabilityRegistry.cs / .Async.cs、
LocalExecutionInvoker.cs の 4 ファイルのみ。Common / Kernel / Hosting の製品ソース、
公開済みインターフェース・DTO・enum、Canon、ハッシュ形式・ID の計算式は変更していない。
Kernel の csproj、Python、CI workflow もベース dev と同一。

## テスト範囲

既存の期待値やテストコードは変更していない。B1 の 21 件と PF3 の 38 件も元ブランチから変更なし。
統合後は、再現時に失敗した 5 件を含めてすべて合格した。

| テスト群 | 合格件数 | 内容 |
|---|---:|---|
| ベース dev の全テスト | 1011 | 名前・成否を前回の統合候補 TRX と照合 |
| B1 | 21 | 実時計の 30 秒・3 反復、期限境界、再呼出し、入れ子、リプレイ等 |
| PF3 | 38 | ループ途中の取消、非同期能力の await、呼出元非占有、同期・非同期の結果と履歴の一致等 |
| 今回の組合せ | 11 | 待機時間と期限、ROM の時計、取消例外・遅延成功・明示開始時刻 |

Core は合計 **1,081 件**（単体 1,075 + 統合 6）合格。
ベース側の V1 16 件、B4 58 件、PF1 32 件、V2 48 件を含む全 1,011 件も引き続き合格した。
既存の PF1 性能ベンチマークは再実行していない。Common の実装は今回変更しておらず、
前回の性能・適用範囲に関する留保を本検証で解消したとは扱わない。

新しい `tests/AIKernel.Core.Tests/Dsl/DslIntegratedClockCancellationTests.cs` の 11 件:

| 検証 | 件数 |
|---|---:|
| 非同期能力の待機中に 30 秒経過したら次の本体を実行せず、記録した時計で出力と ReplayLogHash が一致（inline / ROM） | 2 |
| 時限実行中の TaskCanceledException / 独自派生例外が、呼出元とは異なるリンク済みトークンでも伝播（inline / ROM） | 4 |
| 取消に協調しない能力を切り離さず完了まで待ち、遅い成功を破棄して次の反復を開始しない（inline / ROM） | 2 |
| 暗黙開始時刻の読取りが取消例外を投げても一般の失敗に変換しない（inline / ROM） | 2 |
| 公開 ExecuteAsync に渡した期限超過済みの明示開始時刻を上書きせず、同期実行と同じハッシュになる | 1 |

待機・取消テストは TaskCompletionSource で境界を制御し、実際に 30 秒待つテストではない。
既存 B1 の SystemClock テストは、実時計からの読取り列を記録・再供給して比較する。
非同期化によるスケジューリング時間まで実時計で常に同一になるという保証ではない。

## 全体・下流検証

Windows / .NET SDK 10.0.401 / Release。Core と下流 7 リポジトリの restore / build / test / pack を実行。
下流は各 dev の `git archive` による隔離コピーを用い、下流 review や未コミット変更は含めなかった。
NET 自体は前回検証済み dev と成果物を再利用し、今回の出力 DLL とハッシュで照合した。

元の公開版キャッシュから複製した 72 パッケージを記録し、Core の build 用フィードでは NET の 4 パッケージだけを
検証済み成果物へ置換。Core の API 検証用 5 基準パッケージは元の公開 0.1.3 のまま保持した。
Common / Core / Hosting / Kernel / TestKit の 5 パッケージすべてで
`EnablePackageValidation=true` / `PackageValidationBaselineVersion=0.1.3` を評価して pack した。
API 互換性の抑制や除外は追加していない。

```powershell
dotnet restore SOLUTION --configfile LOCAL_FEED_CONFIG --packages ISOLATED_CACHE --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build SOLUTION -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test SOLUTION -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=REPO' --results-directory RESULTS -m:2 -nr:false
dotnet pack SOLUTION -c Release --no-build --no-restore -o OUTPUT -m:2 -nr:false
```

SOLUTION は各 `.slnx`。NuGetAudit=false はローカルフィードだけでの検証のためで、CI には追加していない。
下流の `[0.1.3]` 参照を変えず検証するため、候補パッケージも隔離フィード内では 0.1.3 を使用。
**生成パッケージは検証専用であり、公開・配布用ではない。** 共有 NuGet キャッシュは変更していない。

| リポジトリ | 検証ソース | restore | build | pack | 合格 | 失敗 |
|---|---|---|---|---|---:|---:|
| Core | `8f51a5a91492` | 成功 | 成功 | 成功 | 1081 | 0 |
| Providers | `db7e989fad73` | 成功 | 成功 | 成功 | 120 | 0 |
| Cuda13.0 | `9e3ef8268b09` | 成功 | 成功 | 成功 | 21 | 0 |
| Control | `2229477a719f` | 成功 | 成功 | 成功 | 42 | 0 |
| Tools | `05ec02165ae8` | 成功 | 成功 | 成功 | 34 | 0 |
| Wasm | `2c6b11f47c81` | 成功 | 成功 | 成功 | 64 | 0 |
| Demo | `b3fcb62035f4` | 成功 | 成功 | 成功 | 71 | 0 |
| Doom | `3df7008d990a` | 成功 | 成功 | 成功 | 133 | 2 |

合計 **1566 件合格、既存 2 件失敗、新規失敗 0**。スキップなし。
下流の全テストの名前・成否は前回の Core 統合候補と一致した。
Doom の失敗は以下の既知 2 件で、`HUD_UNIFORM_FLOAT_COUNT = 32` の期待と実装の不一致。
期待値は変更していない。

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

候補のビルド DLL、nupkg 内 DLL、下流の復元キャッシュ・実出力を SHA-256 で照合。
Tools の publish 出力と下流 nupkg 内に含まれる NET / Core DLL も対象にした。

| Core パッケージ | DLL SHA-256 | 一致した下流出力数 |
|---|---|---:|
| AIKernel.Common | `2c77642b30dc9ea2a407d805d81a5e52ce3cc387a4f9f0ee479ddf2ba23a2d77` | 29 |
| AIKernel.Core | `9be259c0f41b54fb1a0ac21a1cfdc960e036599f4d283989105a9e5d7a90a1b3` | 26 |
| AIKernel.Hosting | `9164b967a56cb242a01ca2d930a75213463afdea56f5b004629c10599fe5d00a` | 5 |
| AIKernel.Kernel | `d20fee0a2ee437ac8a53362835b76d37218ceae5240fde392a2d74fd45d5fdaa` | 3 |
| AIKernel.TestKit | `63e80831a93e2eb85d5fcf50d47886707cf58fc075d7d86c88352c35edf086f8` | 0 |

TestKit は今回の下流実出力に参照がないが、build / pack、公開版との互換性、nupkg 内 DLL 一致は確認済み。
`git diff --check dev` に問題なし。

## 変更の保持と範囲の制限

NET の未コミット 33 ファイルと Cuda13.0 の workflow 2 ファイルは、開始前後のファイル SHA-256、
Git 状態、ステージ済みツリーが一致。全 10 リポジトリの dev は開始時のまま。
本作業で既存ブランチ・main・タグは更新せず、新しい候補ブランチだけを追加した。

Core の別 worktree `D:/AIKernel/artifacts/cuda-driver-native/core-context-free-pci` では、
開始時と最終観測の間に本作業とは独立した変更を検出した。この worktree のソースは操作せず、
候補や下流検証にも混入させていない。元の記録を置き換えず、観測時の HEAD / index / 未コミット状態を
`concurrent-worktree-observations.json` と `preservation-after.json` に記録した。
その独立ブランチを除く既存参照と全 dev は開始時の値を維持している。

同期 Execute / Invoke は維持。取消は協調的で、同期コールバックや取消非対応の能力を強制終了しない。
実行済み外部作用は巻き戻さず、取消時は成功 DTO や部分成功を返さない。
timeout_reached を成功として表す既存の状態区分も維持している。C1 の状態体系変更は対象外。
外部 IKernelPipeline の暗黙開始時刻は従来どおり UnixEpoch で、独自時計を推測しない。

Linux / GitHub Actions、オンライン脆弱性監査、CUDA / WebGPU 実機、実 Cosmos、TheoriaCast 共同受入は未実施。
StepId / ReplayLogHash の OS 改行依存、PF1 の可変データ経路・各中間ハッシュ取得の制限などは別項目のまま。
本候補だけで 0.1.4 公開条件全体を完了とはしない。

次はこの候補のレビュー。dev への取り込み、push、PR、タグ作成、公開は行っていない。

## ローカル証跡

`D:/AIKernel/.codex_tmp/core-b1-pf3-integration-20261117/`:

- `candidate.json` / `selected-commits.json` / `excluded-cuda-commits.json` / `dev-heads.json`: ソースと採用範囲。
- `prepare.py` / `run-validation.ps1` / `verify.py`: 準備・全体検証・照合。
- `reproduce.ps1` / `reproduction-status.json` / `reproduction/` / 各 `reproduction-*.log`: 調整前の 5 失敗の証跡。
- `validation-status.json` / `verified-results.json` / `results/` / 各 restore/build/test/pack ログ: 最終結果。
- `package-validation-settings.json` / `source-package-hashes.json` / `net-candidate-hashes.json`: 設定と依存物の識別。
- `preservation-before.json` / `preservation-after.json` / `concurrent-worktree-observations.json`: 変更保持と別 worktree の観測。

隔離フィード・キャッシュ・生成パッケージ・生ログは Git に追加しない。
