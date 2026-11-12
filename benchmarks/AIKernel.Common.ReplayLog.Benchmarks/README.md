# ReplayLog construction benchmark

PF1の手動ベンチマークです。CIのテストに時間比の閾値を入れません。
.NET 10のReleaseで、同じマシン・設定を使い、他のビルドが終わってから実行します。

```powershell
dotnet build benchmarks/AIKernel.Common.ReplayLog.Benchmarks -c Release
$env:DOTNET_TieredCompilation = '0'
dotnet run --project benchmarks/AIKernel.Common.ReplayLog.Benchmarks -c Release --no-build --no-restore -- --samples 7 --batch-steps 100000
```

`--scenario append|prefix|bind|loop`、`--sizes 1000,10000`、`--samples 7`で範囲を選べます。
CSVには各試行の所要時間、当該スレッドの割当バイト数、ログ件数とReplayLogHashを記録します。
CPU時間とGC世代ごとの回数も記録します。`--batch-steps 100000`は1試行に合計100,000ステップ相当を
構築し、時間と割当量を1回の構築当たりに換算します。iterations列が構築回数です。
GC回数は試行全体の値です。短い計測に対するスケジューラーの影響を減らすために使います。
省略時は1試行につき1回の構築で、変更前の重い処理の計測にも使用できます。
各シナリオを200ステップでウォームアップし、サイズの実行順を交互に変えます。
GCは計測の外で実行します。中央値の10,000 / 1,000の比と、割当量の比を比較してください。

| シナリオ | 構築経路 |
|---|---|
| append | WithSemanticDeltaの連続追記 |
| prefix | DSLと同じ、新しいResultStepへの既存ログ接続と追記 |
| bind | 単一ステップの接続と親IDの付け替え |
| loop | PipelineStep.Loopによる反復とループ記録 |

計測はログ構築から**最終ReplayLogHashの取得まで**を含みます。
Metadataは実際のDSLと同じImmutableDictionaryを使います。
過去の各段階のハッシュをすべて要求する計測ではありません。v1のハッシュには先頭に件数が
含まれるため、異なる全プレフィックスをハッシュする仕事量は引き続き二次になります。
可変Metadataや外部リストの互換経路も、生成時のハッシュ確定という既存動作を維持するため
この線形化の対象外です。不変性の保証がないデータを自動的に凍結して意味を変えません。

```powershell
dotnet run --project benchmarks/AIKernel.Common.ReplayLog.Benchmarks -c Release --no-build --no-restore -- --fingerprints > fingerprints.json
```

`--fingerprints`は各経路の空・1・7・31ステップ、失敗、再開の48例について、状態、
StepId、ReplayLogHash、全エントリを出力します。変更前後でJSONを構造比較できます。
Metadataのオブジェクト内のキー順は比較から除外し、ログ配列の順序は比較してください。
v1の改行仕様を変えていないため、変更前後は同じOSで比較します。
