# V2: ResultとSelectManyのモナド則テスト

対象はCoreのV2のみです。`dev` (`dd8b258`) 起点の `review/V2` で作業しています。
本体実装、公開契約、ハッシュ計算、既存テストの期待値は変更していません。
I1、I4、PF1、PF3等のレビュー待ちブランチも取り込んでいません。

## 比較する法則と範囲

`return`を`Result<T>.Success`、`bind`を`Result<T>.Bind`として、次を比較します。
同じ法則を`SelectMany(f, (_, value) => value)`による連結にも適用します。

- 左単位元則: `bind(return(x), f)`と`f(x)`。
- 右単位元則: `bind(m, return)`と`m`。
- 結合則: `bind(bind(m, f), g)`と`bind(m, x => bind(f(x), g))`。

等価性は`IsSuccess`、`IsFailure`、`Value`で判定します。値は`EqualityComparer<T>.Default`で
比較し、失敗時も格納された既定値の一致を確認します。ErrorContextの内容・参照同一性や診断メタデータ、
ResultStepのトレースの同一性をモナド則の条件には含めません。

関数は副作用がなく、例外を投げず、すべての生成入力にResultを返す関数です。
例外を投げる関数やnullデリゲート、キャンセル・Faulted Taskの扱いは、この法則の検証範囲とは別です。
例外を捕捉する既存実装を変更したり、例外時の既存テストを弱めたりしていません。

## 追加テスト

既存のxUnit基盤で、外部パッケージを追加せずに入力と関数を生成するプロパティテストを追加しました。
固定シードは`1`、`20261113`、`0x9e3779b9`、各テストケースの生成回数は1,024回です。
xorshift32と明示的な整数演算を使い、時計、System.Random、実行時依存の文字列ハッシュには依存しません。

| 対象 | ケース数 | 内容 |
| --- | ---: | --- |
| 整数のResult | 18 | 3シード × Bind/SelectMany × 3法則 |
| nullを含む文字列のResult | 18 | 3シード × Bind/SelectMany × 3法則 |
| 異なる型のLINQ射影 | 3 | int → string → intの結合順序と元の値の保持 |
| 非同期SelectMany | 9 | 3シード × Result＋非同期関数、Task＋非同期関数、Task＋同期関数 |
| 合計 | 48 | 49,152回の生成評価 |

初期Resultは成功、明示的な失敗、`default(Result<T>)`を含みます。関数は成功、明示的な失敗、
default、入力に応じた成功／失敗、`Success(default)`の5種類です。初期状態3種類と関数2つの
5×5種類の組合せは順番に選び、各シードで75通りすべてを通します。整数は端値と32ビット範囲から生成した値、
文字列はnull、空文字、改行、NUL、日本語、結合文字、サロゲートを含みます。
生成した関数のパラメーターも固定シードから決まります。

LINQの射影は結合順序の一致に加え、外側・内側の値が保持されることを独立に検証します。
非同期版は`Task.Yield()`を経由し、3つの公開SelectManyオーバーロードの値と成否が
同期版と一致することを確認します。外側・内側の値は組で別々に保持し、入れ替わりも検出します。
任意のTaskや副作用についてモナド則を保証する試験ではありません。

失敗メッセージにはシード、サンプル番号、入力、初期Result、生成した関数の条件、両辺の結果を出します。
自動縮小は行わず、同じMemberDataとサンプル番号で再現します。
有限個の生成試験であり、すべての型と関数に関する形式的証明ではありません。

現行devでは`Success(null)`も成功なので生成対象に含めています。I2の採否は変更していません。
I2を統合する際は、nullをreturnする関数をモナドの対象に含めるかも含めて判断し、
このテストの期待を単に緩めずに再確認してください。

## 実行方法

Coreで通常の復元後、追加テストだけを実行する場合:

```powershell
dotnet test tests/AIKernel.Core.Tests/AIKernel.Core.Tests.csproj -c Release --filter FullyQualifiedName~ResultMonadLawProperties
```

## 検証結果

検証完了日: 2026-11-14（Windows）。追加48ケースはCore全体試験でもすべて成功し、
生成した範囲ではモナド則の破れは見つかりませんでした。
Coreと下流7リポジトリのrestore/buildも成功し、全体テストは972件成功、
Doomの既知の2件のみ失敗しました。Coreのbuildは警告・エラーともに0件です。

| リポジトリ | 成功 | 失敗 |
| --- | ---: | ---: |
| Core | 487 | 0 |
| Providers | 120 | 0 |
| Cuda13.0 | 21 | 0 |
| Control | 42 | 0 |
| Tools | 34 | 0 |
| Wasm | 64 | 0 |
| Demo | 71 | 0 |
| Doom | 133 | 2（既知） |

既知の失敗は以下の2件です。PF3実施時の記録と、テスト名・成否・エラーメッセージが一致しました。

- `DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping`
- `DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets`

どちらも`const HUD_UNIFORM_FLOAT_COUNT = 32`を探す既存Assert.Containsの失敗です。
期待値は変更していません。前回のPF3専用テスト38件を除いた既存テストと、
今回のV2専用テスト48件を除いたテストの名前・成否が一致し、既存の失敗は増えていません。

AIKernel.Commonの生成元・候補nupkg・隔離NuGetキャッシュ・今回のソリューション内の
34か所のDLLについて、SHA-256が一致することを確認しました。
機械可読の集計とDLLの照合結果は`verified-results.json`、下流試験は`results/*.trx`、
Coreの最終試験は`core-recheck/*.trx`、初回の追加テスト単独の試験は`properties/properties.trx`、
48ケースの一覧は`new-test-cases.txt`にあります。非同期射影の比較を外側・内側の値の組に強めた後、
Core全体を再build/testしました。集計では初回と再実行のCoreを二重計上していません。
Linuxでのbuild/test、GitHub CI、実GPUでの実行は今回の検証には含みません。
既存のローカルブランチ参照と、他リポジトリのチェックアウト・未コミット変更の状態も照合しました。
検証中に別作業のCore `codex/context-free-cuda-pci`が`ebecb1b`から`079cea5`へ進んでいました。
旧コミットが新コミットの祖先であることを確認し、その更新は保持しています。
dev/mainとその他の既存レビュー用ブランチは開始時から変わっていません。

証跡保存先: `D:\AIKernel\.codex_tmp\review-v2-20261113`。
Coreは今回のブランチ、下流7リポジトリは各devの隔離コピーで検証します。
Resultを含むAIKernel.Commonは今回のCoreから隔離フィードにpackし、下流の隔離キャッシュで使用します。
候補パッケージは現行の0.1.3設定で生成する検証専用のもので、公開・本番導入用ではありません。
他のブランチ、dev/main、ユーザーの未コミット変更を維持し、push、PR、devへの取り込み、
タグとパッケージの公開は行いません。


## 次の項目と人間の判断

承認待ち・依存待ちの項目を除くと、次の候補はDM2（DemoのREADMEと実装の整合）です。
V2はレビュー後のdevへの取り込み待ちです。今回、実装変更を必要とする法則の破れや、
新しい承認事項は見つかっていません。I2の将来の統合時の注意は前述のとおりです。
