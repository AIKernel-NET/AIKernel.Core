# PF1: ReplayLog構築の線形化

検証日: 2026-11-13 (JST)。v0.1.3のレビュー指摘を0.1.4向けに修正する項目です。
対象: AIKernel.Core、dev `dd8b258`から作成した`review/PF1`。
元の`review/I4` (`9d89b56`)と他のレビュー待ちブランチは保持しています。

## 再現

ResultStepは追記・結合のたびに全エントリを配列にコピーし、コンストラクターで全ログを
CreateReplayLogHashへ渡していました。一定サイズの記録をn回追加すると、コピーとハッシュの
仕事量がともにO(n²)になります。DSLの新規ResultStepへのプレフィックス接続でも再ハッシュしていました。

変更前のappendベンチマークは1,000件で3,671.4102ms / 773,470,176 bytes、
10,000件で456,081.4458ms / 76,515,335,960 bytesでした。
時間比は約124倍、割当量比は約99倍です。変更前は各サイズ1試行です。

## 実装と計算量

- 内部のPersistentReplayLogがImmutableStackを共有します。追記と末尾の失敗記録への置換はO(1)です。
- Count、先頭・末尾参照はO(1)です。その他の添字参照は初回にO(n)で非公開配列を生成し、以後O(1)です。
- 列挙は再帰を使わずO(n)です。各履歴に全配列を保持させずに列挙・ハッシュでき、長いログでもスタックを消費しません。
- 接続する側のログがk件なら、親IDの再計算と接続はO(k)です。既存のプレフィックスは走査しません。
- 不変ログのReplayLogHashは最初の要求時に計算して保持します。キャッシュは並行読み取りでも安全に公開します。
- 最終ハッシュはStringBuilderのチャンクを状態を持つUTF-8 Encoderで変換し、同じSHA-256へ順に渡します。大きな一時文字列とUTF-8配列を作りません。
- 一定サイズの不変Metadataによるn件構築と最後のハッシュ取得1回は、時間・保持メモリともにO(n)です。

公開プロパティは従来どおりIReadOnlyList<ResultStepReplayLogEntry> / stringです。
StepIdの計算と、ReplayLogHashの正規ペイロード・版・改行・SHA-256という計算仕様は変更していません。
ReplayLogHashのUTF-8変換とSHA-256への入力を小分けにした実装変更は、旧v0.1.3の一括エンコードと照合します。
失敗時に最後の記録を書き換える既存の意味も維持しています（I3は別の承認待ち項目）。
default ResultStepのReplayLog / ReplayLogHashがnullである挙動も維持します。

## 互換性のために維持する経路と限界

遅延ハッシュが安全なのは、Metadataがnull、ImmutableDictionary、ImmutableSortedDictionaryの
エントリを内部で保持する場合です。実際のDSLとPipelineStepのループ記録はImmutableDictionaryを使います。

可変Dictionary、ReadOnlyDictionaryによるラップ、ImmutableDictionary.Builder、独自の辞書、
外部のIReadOnlyListは、従来の配列・生成時ハッシュの経路を維持します。
これにより、生成後に呼出元が内容を変更しても元のResultStepのハッシュは生成時の値のまま、
次のResultStepを生成すると変更後の内容を再ハッシュする、という既存動作を維持します。
独自辞書の列挙例外が発生するタイミングも遅らせません。

したがって、可変データを含む全経路の線形化を達成したとは扱いません。
それらも線形化するには、外部データをいつ不変のスナップショットにするかという契約判断が必要です。
承認なしにMetadataを凍結したり、変化した内容を古いハッシュで扱う変更は行っていません。

また、各中間プレフィックスの異なるハッシュをすべて要求した場合の仕事量は二次のままです。
v1では先頭にログ件数を含む全ペイロードをハッシュするため、形式を変えずに前のダイジェストへ追記することはできません。
本項目の計測は構築と最終ハッシュ取得までを含みます。

## 回帰テストと変更前後の照合

追加32件すべて成功しました。既存テストへの変更はありません。
変更前devのバイナリと変更後について、48ケースの状態・成否・StepId・親ID・ReplayLogHash・全エントリが
JSONの構造比較で一致しました。1,000 / 10,000ステップの実測時のハッシュも変更前と同じです。
新しいログのハッシュは、公開APIに外部配列を渡す従来の即時計算経路とも比較しています。
UTF-8の分割処理については、テスト内に固定したv0.1.3の一括エンコード実装とも照合しています。

追加テストの対象:

- null / 不変 / ソート済み不変Metadataの順序、親ID、正規ハッシュ
- 未初期化と空のログ、分岐、Map / WithState / MapStateの履歴保持
- 末尾の失敗記録、同期・非同期Bindによる複数エントリの親ID付け替え
- 可変Metadata、外部配列・リスト、非空ログとの接続の既存の評価タイミング
- 20,000件の列挙・添字参照・JSON往復、並行読み取りと分岐
- 範囲外添字、独自Metadataの列挙例外の発生タイミング
- UTF-8の分割境界、サロゲートペア、不正な単独サロゲート、改行・空文字を含むメタデータとv0.1.3のハッシュ一致

既存テストのコード・期待値は変更していません。
性能の時間比は手動ベンチマークで測り、CIの成功条件には入れません。

## ベンチマーク

各サイズを合計100,000ステップ相当反復する7試行を実施しました。各時間は1回の構築当たりの中央値です。

| 経路 | 1,000件 ms | 10,000件 ms | 実時間比 | CPU時間比 | 割当量比 |
|---|---:|---:|---:|---:|---:|
| append | 10.595 | 138.189 | 13.04 | 11.18 | 10.028 |
| prefix | 18.006 | 229.231 | 12.73 | 10.73 | 10.018 |
| bind | 21.773 | 227.507 | 10.45 | 10.85 | 10.025 |
| loop | 23.922 | 314.933 | 13.16 | 10.98 | 10.010 |

UTF-8一括エンコードの段階の1構築ずつの7試行では実時間比がappend 12.52 / prefix 10.96 / bind 13.16 / loop 12.99でした。
1,000件の短い試行が数十msから数百msへ振れたため、反復数を増やして平均化しました。
元のCSVと集計はbenchmark-after-unbatched.csv / benchmark-unbatched-verified.jsonに保持しています。
一括エンコードのまま反復を増やした段階の実時間比はappend 26.78 / prefix 13.91 / bind 9.95 / loop 15.09でした。
その記録はbenchmark-before-streaming.csv / benchmark-before-streaming-verified.jsonに保持しています。
上表の最終版の全試行はbenchmark-after.csv、集計とハッシュ照合はbenchmark-verified.jsonです。
再計測ではCPU時間とGC回数もCSVに記録しています。GCは測定の前に実行し、測定中のGCは除外していません。
ベンチマークで使用したデータはすべて不変Metadataです。可変データの互換経路の性能を示すものではありません。

プロジェクト: `benchmarks/AIKernel.Common.ReplayLog.Benchmarks`。
再現コマンドと測定範囲は同ディレクトリのREADME.mdに記載しています。
.NET 10.0.12 / Windows 10.0.26300 / 8論理CPU、Release、DOTNET_TieredCompilation=0。
測定時のマシン負荷により時間は変動するため、割当量の増加率も併記します。

## 全体・下流検証

全9リポジトリのrestore / Release buildが成功しました。テストは合計1,129件成功、Doomの既知失敗2件のみです。
Coreは既存439件と追加32件の合計471件です。

| リポジトリ | restore / build | 成功テスト | 既知失敗 |
|---|---|---:|---:|
| NET | 成功 | 173 | 0 |
| Core | 成功 | 471 | 0 |
| Providers | 成功 | 120 | 0 |
| Cuda13.0 | 成功 | 21 | 0 |
| Control | 成功 | 42 | 0 |
| Tools | 成功 | 34 | 0 |
| Wasm | 成功 | 64 | 0 |
| Demo | 成功 | 71 | 0 |
| Doom | 成功 | 133 | 2 |

Doomの失敗名とメッセージはI4の記録と完全一致しました。どちらもHUD_UNIFORM_FLOAT_COUNTの期待値32に関する既知失敗です。

- DoomWebGpuProvider_SeparatesRawFramebufferAndHudCompositeTargets
- DoomPrompt_UsesCanonicalAisthesisNoesisKrisisKinesisPanelMapping

Common / Core / Hosting / Kernelの4 DLLは、ビルド元、検証用nupkg、隔離キャッシュ、全78出力でSHA-256が一致しました。
各devと元のreview/I4の参照、既存テスト、StepId計算とReplayLogの正規ペイロードも保持されています。
集計と照合結果はverified-results.json、ベンチマーク照合はbenchmark-verified.jsonに記録しています。

証跡: `D:\AIKernel\.codex_tmp\review-pf1-20261113`。
Coreはreview/PF1、他8リポジトリは各devのgit archiveによる隔離コピーを使用しました。
Common / Core / Hosting / Kernelを検証専用フィードへpackして下流へ供給しました。
ローカル0.1.3再packは検証専用で、公開済みパッケージやグローバルキャッシュを置き換えません。
Windowsの.NET検証です。Linux、実GPU、実Cosmos、TheoriaCast共同受入、Emscripten再ビルドは含みません。

## 次の項目と人間の判断

PF1の完了判定は保留です。不変ログの構築コストと割当量は線形化しましたが、
現環境の最終実時間比は約10.4〜13.2倍で、全経路の「おおむね10倍以内」を確認できていません。
時間比の差を環境負荷だけに起因すると断定せず、負荷を制御した環境での確認と必要に応じた追加分析を残します。

git上の状態はレビュー待ちです。review/PF1のレビューとdev取り込みは人間が行います。
push、PR、main/devへの取り込み、タグ、公開は行いません。
外部の可変データまで線形化するには上記の不変スナップショット契約の判断が必要です。
次の承認不要の項目はPF3です。PF2は承認待ちです。
I4等との統合後の検証はリリース前に必要であり、本項目だけで0.1.4を公開可能とは判定しません。
