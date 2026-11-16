# Core CT1・I1・B4・I4・PF1・V2 統合候補の検証記録

検証日: 2026-11-16〜17 (JST)。0.1.4 向けの統合候補。dev への取り込みや公開は行っていない。

## 対象と統合結果

- ベース: Core `dev` = `512c31bf4847ab00e028176f488c8febdf85e035`（G1・G2・V1 統合済み）。
- 候補ブランチ: `review/CORE-CT1-I1-B4-I4-PF1-V2-integration`。
- 検証した実装・テスト: `eeb926e3d1fb4d04f286eac49aca9cb04e4ae4c6`。この後のコミットは本記録のみ。
- 作業場所: `D:/AIKernel/repos/AIKernel.Core`。変更のなかった dev のチェックアウトを再利用。
- 上流 NET: 統合済み `dev` = `37a71a958857de6efea2458dfb0a49d07a4c9462`。

| 項目 | 採用した元コミット | 統合コミット | 内容 |
|---|---|---|---|
| CT1 | `287227936aa885e9ef2a8fba98cd1df9444591d7` | `2f145c73c11d` | Approve / Abstain / Reject 以外を Unknown として拒否 |
| I1 | `efbc8109eeeb1484789befa7493d924527e812f3` | `a843f70dd57a` | 未初期化・不完全な判定を含む軌跡を Halt にする |
| B4 | `edbb93fce57952410067c21f15cec4916c7ac9ff` | `d82c5cde8e55` | キャンセル例外の判定を Common の共通入口へ集約 |
| I4 | `9d89b5656e515966f4fa2420b0594ee5722c37ad` | `55c2b1565608` | OrElse 内の例外を元の失敗情報付き UNHANDLED_EXCEPTION へ変換 |
| PF1 | `2c4b48e1a9217e46a5f21f3c1f2a64806c020505` | `b07015824deb` | 不変 ReplayLog の共有構造と遅延ハッシュ、互換経路とベンチマーク |
| V2 | `fd79767b86775011d8dbcdc7572e4d47fe9c993a` | `19428db9b061` | Result / SelectMany の値と成否に関するモナド則テスト |

6 回の `--no-ff` マージは競合なし。CT1 と I1 が変更する `CtgRejectReasonClassifier` は
自動マージ後の双方の判定を確認した。B4・I4・PF1 が関わる結果・実行処理も合わせて検証した。
既存テストのコード・期待値は変更していない。元の 6 改修以外の製品コード変更はない。

CT1 は `287227936aa885e9ef2a8fba98cd1df9444591d7` の投票修正だけを採用した。
`review/CT1` の tip 全体は取り込まず、以下の CUDA Driver ABI / 仮想メモリ関連 7 コミットを除外した。
全件が候補の祖先に存在しないことを Git で照合した。

- `f504f0ecf61995739188ac8460b61442f8c34971`
- `d028b4d1c3dc2ff91dc3a40be5006c842b4ed23d`
- `75fb786bf098b652be343c1b9a9abce8a4739ae1`
- `c5d7b29db905ef6864fa9e6aaae64de2d518177f`
- `283567b7c662552d2aa05a730e91ba2550d827b2`
- `27c50c1731b5dcda7ab63ba7b51739909a89fcfd`
- `837a682c3b92eb914cb124dffc29c5d2b5fa81d7`

Kernel の csproj、Python、workflow はベース dev と同一。
Kernel.cs の変更は B4 のキャンセル判定への置換であり、新 CUDA ABI の実装ではない。
新ネイティブ DLL の同梱・配布対応を本候補に含めたとは扱わない。

## 統合時に追加した検証

`tests/AIKernel.Core.Tests/Governance/CtgIntegratedSafetyTests.cs` に 6 ケースを追加した。
Ethos の未定義値 `-1` / `4` / `int.MinValue` と、不完全な判定を持つステップを組み合わせ、
その前後順序を入れ替えて検証する。正しい Allow 判定の後に、どちらの順序でも Halt となり、
最初の失敗ステップと拒否理由が対応することを確認した。CT1 と I1 のどちらかの修正が
欠けた場合を検出する組合せ試験であり、既存テストの期待値を変更するものではない。

| 項目 | 今回の候補で合格した関連テスト数 | 確認範囲 |
|---|---:|---|
| CT1 | 316 | 正規 3 値の既存 27 通りに加え、Unknown・未定義値を含む組合せ |
| I1 | 66 | Result / ResultStep / ErrorContext / CTG の未初期化・不完全な判定 |
| B4 | 58 | OperationCanceledException 派生、TaskCanceledException、リンク済みトークン等 |
| I4 | 30 | OrElse(Func) のフォールバック例外と元の失敗情報の保持 |
| PF1 | 32 | ログ順序、親 ID、分岐、可変データの互換性、UTF-8 分割境界等 |
| V2 | 48 | Result / SelectMany の法則と非同期経路の等価性、各 1,024 入力、計 49,152 ケース |
| CT1 + I1 統合 | 6 | 未定義票と不完全ステップの前後関係、Halt と最初の失敗理由 |
| 既存 V1 | 16 | StepId / ReplayLogHash / ROM / チャット意味ハッシュの固定ベクトル |

元の Core dev の 455 テストは名前・結果とも維持。6 改修で追加された 550 件と、今回の組合せ 6 件で
Core は 1,011 件（単体 1,005 + 統合 6）となった。V2 の比較対象は値と成否であり、
例外文面・履歴メタデータまで同一とする法則を追加したものではない。

B4 の共通判定は実行境界で元の例外型を扱う。I4 の汎用 Result.OrElse は、
既存の Map / Bind と同様にフォールバック例外を UNHANDLED_EXCEPTION に変換する。
ここに投げられた OperationCanceledException もこの扱いであり、勝手に成功へ変換したり、
Result の契約を実行境界の取消伝播へ変更してはいない。両方の関連テストを統合後に再実行した。

## 全体・下流検証

Windows / .NET SDK 10.0.401 / Release で Core と下流 7 リポジトリを検証した。
下流は各 dev の `git archive` による隔離コピー。下流の CT1 / B4 等の review ブランチや
未コミット変更を混入させていない。したがって本候補だけで全リポジトリの B4 統合完了とはしない。

元の公開版キャッシュから複製した 72 パッケージを識別し、NET の 4 パッケージのみを
前回検証済み dev の成果物へ置換して Core をビルドした。Core の API 検証用 5 基準パッケージは
元の公開 0.1.3 のまま保持。Common / Core / Hosting / Kernel / TestKit の全 5 パッケージを
`EnablePackageValidation=true`、`PackageValidationBaselineVersion=0.1.3` で pack し、
生成した候補パッケージを隔離した下流フィードへ供給した。互換性の抑制や例外設定は追加していない。

実行コマンド（SOLUTION は各 `.slnx`）:

```powershell
dotnet restore SOLUTION --configfile LOCAL_FEED_CONFIG --packages ISOLATED_CACHE --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build SOLUTION -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test SOLUTION -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=REPO' --results-directory RESULTS -m:2 -nr:false
dotnet pack SOLUTION -c Release --no-build --no-restore -o OUTPUT -m:2 -nr:false
```

ローカルフィードのみを使うため NuGetAudit を無効にした。CI には無効化を追加していない。
下流の `[0.1.3]` 参照を変えずに候補 DLL を使うため、検証用パッケージも隔離フィード内では
0.1.3 を使用する。**これらは公開・配布用ではない。** 共有 NuGet キャッシュは置換していない。
NET 自体のテストは再実行せず、前回検証済み dev と成果物を照合して再利用した。

| リポジトリ | 検証ソース | restore | build | pack | 合格 | 失敗 |
|---|---|---|---|---|---:|---:|
| Core | `eeb926e3d1fb` | 成功 | 成功 | 成功 | 1011 | 0 |
| Providers | `db7e989fad73` | 成功 | 成功 | 成功 | 120 | 0 |
| Cuda13.0 | `9e3ef8268b09` | 成功 | 成功 | 成功 | 21 | 0 |
| Control | `2229477a719f` | 成功 | 成功 | 成功 | 42 | 0 |
| Tools | `05ec02165ae8` | 成功 | 成功 | 成功 | 34 | 0 |
| Wasm | `2c6b11f47c81` | 成功 | 成功 | 成功 | 64 | 0 |
| Demo | `b3fcb62035f4` | 成功 | 成功 | 成功 | 71 | 0 |
| Doom | `3df7008d990a` | 成功 | 成功 | 成功 | 133 | 2 |

合計 **1496 件合格、既存 2 件失敗、新規失敗 0**。スキップなし。
下流のテスト名・結果は前回の Core G1・G2・V1 統合候補の記録と一致した。
Doom の失敗は以下の既知 2 件で、どちらも `HUD_UNIFORM_FLOAT_COUNT = 32` の期待と実装の不一致。
期待値や実装の修正は行っていない。

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

Core のビルド DLL、nupkg 内 DLL、下流の復元キャッシュ・実出力 DLL を SHA-256 で照合した。
下流 nupkg に含まれる DLL と Tools の publish 出力も対象。NET の参照 DLL は前回検証済みのものと一致。

| Core パッケージ | DLL SHA-256 | 一致した下流出力数 |
|---|---|---:|
| AIKernel.Common | `92553c3722d5f78345444d7119db30dd48e7cb2545f19e9c09d0e3c9c16945a4` | 29 |
| AIKernel.Core | `3b2e938a12f066e0b89a7b279e5b0cdedf337cf9e9e3c873df234930566cb9ba` | 26 |
| AIKernel.Hosting | `030aa06f5a602cecc71ff5160b99c94361e0195489997902506d786e262f33aa` | 5 |
| AIKernel.Kernel | `7b82526579897000e672c7e6400ef7d2fdcf91a3a77d3c29ee67ad4bcf23a643` | 3 |
| AIKernel.TestKit | `9b7e2c4e52fbec125491b749b715518a47f8cc3628c423c5bbc3a92cb63331c1` | 0 |

出力数 0 の TestKit も build / pack と公開版との API 互換性、nupkg 内 DLL の一致を確認した。
`git diff --check dev` に問題はなかった。

## PF1 の再測定とハッシュ保持

前回検証済み Core dev `512c31bf4847` の Common DLL を固定して、今回と同じ
ベンチマーク入力生成コードを参照させた。48 ケースの状態・成否・StepId・親 ID・全エントリ・
ReplayLogHash が候補と一致した。PF1 単独検証時の変更前記録とも一致した。
1,000 / 10,000 ステップの 4 経路のハッシュも、PF1 単独検証の記録と一致。
今回のベンチマークが読み込んだ Common DLL と全体検証で pack した DLL の一致も確認した。

下流の build / test / pack 終了後に測定。Release、`DOTNET_TieredCompilation=0`、
各サイズの合計 100,000 ステップ相当を 7 試行、サイズの実行順を交互にした。
下表は 1 回の構築と最後のハッシュ取得までの中央値。測定中の GC を除外していない。
旧 dev の O(n²) 経路の大規模時間測定は今回再実行せず、48 ケースの互換性照合に用いた。
時間を CI の成否条件へ追加していない。
本作業の build / test は測定と重ねていないが、別 worktree では並行編集を観測しており、
ホスト全体の他作業・負荷は制御していない。そのため専用の静穏環境での性能保証とは扱わない。

| 経路 | 1,000 件 ms | 10,000 件 ms | 実時間比 | CPU 時間比 | 割当量比 |
|---|---:|---:|---:|---:|---:|
| append | 11.216 | 121.807 | 10.86 | 12.00 | 10.028 |
| prefix | 18.513 | 190.848 | 10.31 | 9.74 | 10.018 |
| bind | 17.177 | 174.769 | 10.17 | 10.00 | 10.025 |
| loop | 21.547 | 201.555 | 9.35 | 11.00 | 10.010 |

**PF1 の完了判定はレビューに残す。** 今回の実時間比は 9.35〜10.86 倍。
前回単独検証の約 10.45〜13.16 倍に対し、今回の中央値の比は 10 倍前後だった。
ただし append / prefix / bind は厳密な 10.0 倍を超えている。要求の「おおむね」の許容幅は
数値で定義されていないため、新たに厳密な 10.0 を合否基準として追加しない。
集計 JSON の allTimeRatiosAtMost10 は参考値であり、CI や検証スクリプトの失敗条件ではない。
この実測値を許容するかと、下記の適用範囲を合わせてレビューする。時間の変動を環境負荷だけのせいと
断定せず、本候補の機能試験成功だけで PF1 全体を完了扱いにはしない。

測定対象は不変 Metadata の経路。可変辞書・外部リストは v0.1.3 の生成時ハッシュの意味を保つため
従来経路を維持し、その全経路まで線形化してはいない。各中間プレフィックスのハッシュをすべて
要求すると、v1 のペイロード形式では引き続き二次の仕事量になる。これらの契約変更は本候補で行わない。
詳細は既存の `REVIEW_PF1-v0.1.3.md` とベンチマーク README を参照。

## 保持した状態と未検証事項

- NET の未コミット 33 ファイル、Cuda13.0 の未コミット workflow 2 ファイルを保持。
  前後の Git 状態、ファイル SHA-256、ステージ済みツリーを照合した。
- NET の統合済み dev worktree を保持。既存ブランチ・main・タグと全 10 リポジトリの dev は
  開始時のまま。新しい候補ブランチだけを追加した。
- Core の別 worktree `D:/AIKernel/artifacts/cuda-driver-native/core-context-free-pci` では、
  最初に Bonsai 関連の未コミット変更 10 ファイル、その後に 12 ファイルを観測し、
  並行編集が継続していることを確認した。開始時は変更なしで、branch / HEAD / index は開始時と同じ。
  今回の作業でその worktree のソースは操作しておらず、本候補や検証ソースにも含めていない。
  保持照合はこの相違を検出して停止したため、開始時の記録を上書きせず、2 回の観測を別々に保存した。
  最終照合では元からあった未コミット 35 ファイルと既存参照・index を保護しつつ、別 worktree の
  進行中の編集は時点ごとの観測として記録する。並行編集の内容が全期間不変だったとは主張しない。
- dev への取り込み、push、PR、タグ作成、パッケージ公開は行っていない。
- Linux / GitHub Actions 実行、オンライン脆弱性監査、NuGet.org からの新規取得は未実施。
- CUDA / WebGPU / Cosmos 実機、TheoriaCast 共同受入、下流 review 同士の統合は未実施。
- V1 の既存ベクトルを変更していない。StepId / ReplayLogHash の OS 改行依存は H1a の別項目。
- I1 単独記録にある default ResultStep の null ReplayLog と直接の WithSemanticDelta 呼出しによる
  NullReferenceException 等は別課題のまま。未初期化を成功・Allow にする変更はしていない。

次は本候補の内容と PF1 の性能・適用範囲のレビュー。既存の未解決事項を本候補で完了扱いにせず、
dev へ取り込むかは別途判断する。本検証だけで 0.1.4 の公開条件全体を満たしたとはしない。

## ローカル証跡

`D:/AIKernel/.codex_tmp/core-results-integration-20261116/`:

- `candidate.json` / `selected-commits.json` / `excluded-cuda-commits.json` / `dev-heads.json`: 出自と採用範囲。
- `prepare.py` / `run-validation.ps1` / `verify.py`: 準備、全体実行、Git・TRX・DLL の照合。
- `validation-status.json` / `verified-results.json` / `results/` / 各ログ: 終了コードと検証結果。
- `source-package-hashes.json` / `net-candidate-hashes.json`: 依存パッケージの識別。
- `package-validation-settings.json`: 全 5 パッケージの評価済み互換性検証設定。
- `preservation-before.json` / `preservation-after.json`: 未コミット変更と既存参照の保持。
- `concurrent-worktree-observation.json` / `concurrent-worktree-observation-2.json`:
  別 worktree の並行変更 10 / 12 ファイルを検出した時点の記録。最終観測は preservation-after.json。
- `prepare-benchmark.py` / `run-benchmark.ps1` / `verify-benchmark.py`: 再測定と互換性照合。
- `benchmark-baseline.json` / `benchmark-status.json` / `benchmark-candidate.csv` / `benchmark-verified.json`:
  ベンチマークの全試行と集計。
- `fingerprints-baseline.json` / `fingerprints-candidate.json`: 48 ケースの構造比較。

隔離フィード・キャッシュ・生成パッケージ・生ログは Git に追加しない。
