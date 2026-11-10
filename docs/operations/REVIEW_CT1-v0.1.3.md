# CT1: 未定義の評議会投票値を拒否する

Core `dd8b258`、Control `2229477` の各 `dev` から `review/CT1` を作成した。
G1・G2・V1 は別のレビュー待ちブランチであり、この変更には含めていない。

## 再現と修正

修正前に、Logos と Pathos が Approve、Ethos が `-1`・`4`・`int.MinValue`
の各入力で Core の判定が Allow になることを回帰テストで再現した。
Control の ProviderVoteAdapter でも、未定義値がそのまま CouncilVote に残った。

- Core の `CtgRejectReasonClassifier.HasUnknownVote` は Approve・Abstain・Reject
  だけを既知値とする。決定ゲート、拒否理由、診断が既存の同じ判定入口を使うため、
  未定義値も Deny と `ctg.unknown_vote_value` 診断の対象になる。
- Control の `ProviderVoteAdapter` は同じ3値以外を Unknown に変換する。
  VoteId、評議会、観測時刻、通常のメタデータを維持し、元の入力を変更しない。

Core へ直接渡された数値は診断のメタデータに残る。判定上は Unknown と同じ扱いに
なる。Ethos が明示的に Reject の場合は、既存の `ETHOS_VETO` 優先順位を維持する。
それ以外の未知票による拒否には既存の `FAIL_CLOSED` を使う。

公開 API・DTO・enum、Canon、ハッシュ計算は変更していない。既存テストも変更していない。

## 追加テスト

- Core の `CtgUnknownVoteTests`: 7値（Approve、Abstain、Reject、Unknown、
  -1、4、int.MinValue）の直積から既存の有効27通りを除いた316通り。
  静的判定と非同期ゲートの Deny、Accepted=false、Unknown 診断、拒否理由を検証する。
  既存27通りの真理値表テストはそのまま実行する。
- Control の `ProviderVoteNormalizationTests`: 3評議会 × 7値の21通りで
  正常化と識別情報の維持を検証する。さらに未定義 Ethos の3通りについて、
  ほかの2票が Approve でも実行が Denied となり、スナップショットが生成されないことを確認する。
  既存の27通りのエミュレーターテストもそのまま実行する。

修正前の実行では、Core の新規テスト189件、Control の新規テスト12件が失敗した。
両リポジトリの既存27通りテストは成功した。

## 検証方法

Windows、.NET SDK 10.0.401。Core・Control は作業ブランチ、それ以外の下流は
各ローカル `dev` のクリーンなアーカイブを使用する。

修正済み `AIKernel.Core` と `AIKernel.Control.Core` をローカルで pack し、
必要な既存パッケージと合わせて検証専用フィードを用意する。公開版と同じ0.1.3の
パッケージ識別子を検証内だけで使用し、専用の空の NuGet キャッシュに restore する。
設定ファイルでほかのソースとフォールバックを除外する。共有キャッシュへの上書きや
パッケージ公開は行わない。その後、各ソリューションを build/test する。

```text
dotnet restore <solution> --configfile <isolated-config> --packages <isolated-cache> --force --no-http-cache -p:NuGetAudit=false
dotnet build <solution> -c Release --no-restore
dotnet test <solution> -c Release --no-build --no-restore
```

## 検証結果（2026-11-11）

| リポジトリ | build | 成功 | 失敗 |
|---|---|---:|---:|
| Core | 成功 | 755 | 0 |
| Providers | 成功 | 120 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 66 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

合計1,264件成功、スキップ0件。追加340件と両リポジトリの既存27通りのテストは
すべて成功した。Core は単体749件と統合6件を含む。

ローカルパッケージ内の DLL を基準に、専用キャッシュと下流の実際のビルド出力を
SHA-256 で照合した。修正済み Core は24出力、Control.Core は3出力で一致した。

Doom の失敗は、S0 と同じ以下の2件だけだった。どちらも
`HUD_UNIFORM_FLOAT_COUNT = 32` を期待する既存のソース検査であり、実装は36要素。
既存の期待値は変更していない。

- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`
- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`

新しい失敗は増えていない。Core と Control はそれぞれ `review/CT1` でレビューと
`dev` への取り込みを待つ。push、PR、タグ作成、パッケージ公開は行っていない。
