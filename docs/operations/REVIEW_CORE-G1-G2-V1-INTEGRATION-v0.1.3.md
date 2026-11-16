# Core G1・G2・V1 統合候補の検証記録

検証日: 2026-11-16。0.1.4 に向けた基盤整備の統合候補であり、リリース版の変更や公開は行っていない。

## 対象と統合結果

- ベース: Core `dev` = `dd8b258eba1d3380bfaff89c95697bfd9ad3c9f6`
- 候補: `review/CORE-G1-G2-V1-integration`
- 検証した実装・設定: `a969b2f11ef465f1a208f0bdf7a5864084a10989`。この後のコミットは本記録のみ。
- 作業場所: `D:/AIKernel/repos/AIKernel.Core`。開始時に変更のなかった `review/V2` のチェックアウトを再利用。
- 上流 NET: 統合済み `dev` = `37a71a958857de6efea2458dfb0a49d07a4c9462`。

| 採用範囲 | 元コミット | 統合コミット | 内容 |
|---|---|---|---|
| `review/G1` | `c18b51efc54ec770cd40bd70f2ab7e12a40ded08` | `d3fd4a7` | dev の push / PR を対象とする Windows・Ubuntu の restore / build / test / pack |
| G2 本体のみ | `5e686d1b14079b8dcfca0acb63068843101cd8bf` | `dd89bdd` | LF 属性、SDK 固定、公開 0.1.3 とのパッケージ互換性検証 |
| `review/V1` | `0981d81d69579ecfba8648788209ecedd7a73a0a` | `d875d63` | 4 種のハッシュに対する 16 件の固定ベクトルと説明文書 |

3 回のマージは競合なし。候補は Core dev から作成し、`review/V2` の内容は含めていない。
G2 ブランチの後続 `3aae565d71`（ReplayDump 検証）と `3674151fe9`（モデル実行・Hosting 接続）は
前回の統合順序案に従って除外した。`review/G2` の tip 全体を取り込んだ候補ではない。
これら 2 件と `review/V2` が候補の祖先にないことを Git で確認した。

統合時の調整は `a969b2f` の CI 設定のみ。
`setup-dotnet` の `dotnet-version: '10.0.x'` を `global-json-file: global.json` に変更し、
統合済み NET と同じく G2 の SDK `10.0.401`（`latestPatch`、preview 不可）を基準にした。

`git diff dev -- src` は空。製品コード、公開契約、ハッシュ計算は変更していない。
既存テストの期待値は変更せず、V1 の固定ベクトルも元コミットのまま保持した。
一括の `git add --renormalize` は実行していない。

## 検証方法

Windows / .NET SDK `10.0.401` / Release で、Core と下流 7 リポジトリの
restore / build / test / pack を直列実行した。下流は各 dev の `git archive` から作成した隔離コピー。
他の review ブランチや元の未コミット変更を検証ソースに混入させていない。

NuGet は元の公開版キャッシュから複製した **72 パッケージ**を使用し、各ファイルの SHA-256 を記録した。
NET は前回検証済みの 4 パッケージを再利用し、現在の dev と検証記録のコミット一致を確認した。
NET 自体のテストは再実行せず、今回の Core / 下流出力に含まれる NET DLL の一致を検証した。

- `original-feed`: 元の公開版をそのまま保持。
- `core-feed`: NET の 4 パッケージだけを検証済み成果物へ置換。Core の 5 基準パッケージは元の公開版。
- `downstream-feed`: 上記に今回生成した Core の 5 パッケージを追加。
- Core 用・下流用で復元キャッシュを分離。共有キャッシュは変更していない。

Core の既定 `UseLocalPackageVersion=false` を使い、Common / Core / Hosting / Kernel / TestKit の
**5 パッケージすべて**を `EnablePackageValidation=true`、`PackageValidationBaselineVersion=0.1.3`
のまま build / pack した。基準キャッシュの nupkg が元の公開版と同じこともハッシュで確認。
API 互換性の抑制や除外は追加していない。

下流の `[0.1.3]` 参照を変更せず試験するため、候補も隔離フィード内では `0.1.3` を使用する。
**今回生成したパッケージは検証専用で、公開・配布用ではない。**
Core の新しい CUDA Driver / native 同梱対応はこの候補に含めていない。

実行コマンド（`SOLUTION` は各リポジトリ直下の `.slnx`）:

```powershell
dotnet restore SOLUTION --configfile LOCAL_FEED_CONFIG --packages ISOLATED_CACHE --force --no-http-cache -p:NuGetAudit=false -m:2 -nr:false
dotnet build SOLUTION -c Release --no-restore -m:2 -nr:false -p:UseSharedCompilation=false
dotnet test SOLUTION -c Release --no-build --no-restore --logger 'trx;LogFilePrefix=REPO' --results-directory RESULTS -m:2 -nr:false
dotnet pack SOLUTION -c Release --no-build --no-restore -o OUTPUT -m:2 -nr:false
```

ローカルフィードのみの検証のため `NuGetAudit=false` とした。CI にこの無効化設定は追加していない。
下流の pack 成功はコマンドの成功を表し、ネイティブ資産や実機動作の受入完了を意味しない。

## 結果

| リポジトリ | 検証ソース | restore | build | pack | テスト合格 | テスト失敗 |
|---|---|---|---|---|---:|---:|
| Core | `a969b2f11ef4` | 成功 | 成功 | 成功 | 455 | 0 |
| Providers | `db7e989fad73` | 成功 | 成功 | 成功 | 120 | 0 |
| Cuda13.0 | `9e3ef8268b09` | 成功 | 成功 | 成功 | 21 | 0 |
| Control | `2229477a719f` | 成功 | 成功 | 成功 | 42 | 0 |
| Tools | `05ec02165ae8` | 成功 | 成功 | 成功 | 34 | 0 |
| Wasm | `2c6b11f47c81` | 成功 | 成功 | 成功 | 64 | 0 |
| Demo | `b3fcb62035f4` | 成功 | 成功 | 成功 | 71 | 0 |
| Doom | `3df7008d990a` | 成功 | 成功 | 成功 | 133 | 2 |

今回実行した合計は **940 合格、既存 2 失敗、新規失敗 0**。スキップなし。
Core は 449 単体テスト（V1 の 16 件を含む）と 6 統合テストの合計 455 件が合格した。
候補の製品 DLL と nupkg 内 DLL、下流の復元キャッシュと実出力 DLL の SHA-256 を照合した。
NET の参照 DLL も前回検証済み成果物と一致した。
CLI の publish 出力、および下流 nupkg に同梱される NET / Core DLL も照合対象に含めた。

V1 の対象は `ResultStepIdentity.Create`、`CreateReplayLogHash`、`DslRomHasher.ComputeHash`、
`Sha256ChatTurnSemanticHasher`。空入力、成功・失敗・複数ステップ、Unicode、LF / CRLF 入力を固定値で検証。
**StepId / ReplayLogHash の Windows と Linux の値の違いは残る。** V1 は現行値の記録であり、
Linux の値へ統一する H1a は別項目。今回 Linux ベクトルの期待値や Windows の期待値を変更していない。

Doom の失敗は S0 と同じ以下 2 件で、テスト名と失敗内容を照合した:

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

いずれも `HUD_UNIFORM_FLOAT_COUNT = 32` の期待と実装の `36` の不一致。修正や期待値変更は行っていない。

追加確認:

- `actionlint 1.7.12 -shellcheck= -pyflakes= .github/workflows/ci.yml`: 成功。
- `git diff --check dev`: 成功。
- `git check-attr`: テストのテキストは `text=auto eol=lf`、PNG / GGUF / DLL は `text` 無効。
- 採用 3 コミットの包含、G2 の混在 2 コミットの除外、製品コードの無変更を確認。

### Core 候補の DLL 照合

| パッケージ | DLL SHA-256 | 一致した下流出力数 |
|---|---|---:|
| AIKernel.Common | `99120e86754be1ff62d331f49cec9be66f15bcc34f182d38f08fe9e735bd4d72` | 29 |
| AIKernel.Core | `17492296a6a097cf048e376d4e5c5d4cd2bfebdb6267920f63f0ec06fa303f75` | 26 |
| AIKernel.Hosting | `d9f237487ceef1780804220b59760ce4abdb957d0360341f58bd91df7b199a51` | 5 |
| AIKernel.Kernel | `d50072113f75b7470f7fb1b925794d5bbeb5426c68024a851448db0c9a188ec3` | 3 |
| AIKernel.TestKit | `a441764c09a16287b1e91dcae21330b543d01b5f0de7d2d53375d7089b0aaf2e` | 0 |

下流出力数が 0 のパッケージは今回の下流実行出力に参照がない。
その場合も build / pack、公開版との互換性、nupkg 内 DLL の一致は確認している。

## 作業状態の保持

Core の開始時のチェックアウトは変更なし。候補ブランチへ切り替えて作業し、
既存の `review/V2`、G1・G2・V1 を含む各ブランチ、dev、main、タグは更新していない。
Core の別 worktree と NET の統合済み dev worktree も保持した。

NET の未コミット 33 ファイルと Cuda13.0 の未コミット workflow 2 ファイルは、
前後の Git 状態・ファイル SHA-256・ステージ済みツリーが一致。
NET / Core / Providers / Cuda13.0 / Control / Tools / Wasm / Demo / Doom / RH の dev 参照はすべて開始時の値を維持した。
新規候補ブランチ以外の既存参照を変更していない。

## 未検証事項と次の判断

- Linux 実行と GitHub Actions 実行は未検証。既存 Docker の Linux engine が起動しておらず、
  今回は Windows 実行と CI 構文検査まで。V1 文書にある過去の Linux 検証を、この統合候補の実測として扱わない。
- NuGet.org からの新規取得とオンライン脆弱性監査は未実施。
- CUDA / WebGPU / Cosmos 実機、TheoriaCast 共同受入、下流 review 同士の統合は対象外。
- dev への取り込み、push、PR、タグ作成、公開は行っていない。

次はこの候補のレビュー。G2 に混在していた追加機能は別単位で採否を決める。
本候補の成功だけで、他の未統合修正や 0.1.4 公開条件全体を完了とはしない。

## ローカル証跡

`D:/AIKernel/.codex_tmp/core-g1-g2-v1-integration-20261116/`:

- `prepare.py` / `run-validation.ps1` / `verify.py`: 準備・実行・照合スクリプト。
- `candidate.json` / `dev-heads.json` / `source-package-hashes.json` / `net-candidate-hashes.json`: ソースと依存物の識別。
- `validation-status.json` / `verified-results.json` / `results/` / 各 `*-restore/build/test/pack.log`: 終了コード・TRX・詳細ログ。
- `package-validation-settings.json`: Core で評価された SDK のパッケージ検証設定。
- `preservation-before.json` / `preservation-after.json`: Git 状態と未コミット変更の保持証跡。

隔離フィード・キャッシュ・生成パッケージは Git に追加しない。
