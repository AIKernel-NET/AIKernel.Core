# Bonsai 1.7B でホスト操作を評議する

ホストが利用者の権限を確認したあとで、Bonsai 1.7B が操作の提案を Logos、Ethos、Pathos の各規則で評価します。三票には同じモデルを使います。ホストは、実行する操作、評価に渡す提案、必要な観測を同じリクエストに結び付けます。

## 組み込み

1. 配置時に固定したハッシュを使い、`rom/governance/council.{logos,ethos,pathos}.monolith.md` の各ファイルを `BonsaiCouncilRule.FromUtf8` で読み込みます。
2. `CreateForGovernedOperations` に三規則と Bonsai-1.7B-Q1_0 のパスを渡します。初期化時にモデルの SHA-256 と規則の本文を検証します。長時間稼働するホストは、この評価器を複数のリクエストで共用できます。
3. `InputFacts` はホスト側で取得し、観測の入手状況を `BonsaiCtgEvidenceRequirements` に記録します。提案の本文は別引数です。

```csharp
using var evaluator = BonsaiCtgCouncilEvaluator.CreateForGovernedOperations(
    modelPath, verifiedCouncilRules, cpuThreads: 4);

var decision = await evaluator.RunProposalWithEvidenceIfAllowedAsync(
    request, proposedAction, requiredEvidence,
    hostAdmission: ct => permissions.CanExecuteAsync(operation, ct),
    operation: ct => operations.ExecuteAsync(operation, ct),
    cancellationToken: cancellationToken);
```

証拠が入手不能ならモデルの呼び出しは0回です。操作コールバックはホストの許可、評議の完了、CTGの承認がそろったときに1回呼ばれます。操作の重複実行を禁止するホストでは、再送時にも同じ操作IDを使用し、コールバック側で処理済みかを確認してください。

## 判断の追跡

`Evaluation.Metadata` の `routing.contract` と `prompt.contract` には、証拠の提示方法と実際の評価手順が記録されます。実行記録には、各評議の票、入力した規則・プロンプト・応答のハッシュ、モデルのハッシュが含まれます。

件数の報告では、提案中の件数と観測中の件数を別々に取得します。件数をその場で回答する許可と、一覧の中身を開示する許可も個別に評価します。転記の照合では、対象や時刻を確認し、過去の装置の記録を現在の装置に適用する誤りを検査します。

## 評価期限

既定の評価期限は1リクエスト10分です。期限に達すると `ctg.bonsai.timeout` を返し、操作を止めます。この場合、各票は `Unknown` です。運用ログでは `Evaluation.ErrorCode` を確認し、モデルが最後まで評価した拒否票と区別してください。

LinuxのServerでは、配布候補のNuGetを参照する独立アプリをAVX-512とAVX2で実行しました。どちらも既定期限内に、許可された件数回答でファイルに `5` を書き込み、件数も非公開とする観測では書き込みを拒否しました。両実行ともCPUは2コアに割り当て、プロセスの最大RSSは約1.74 GiBでした。

開発機のAVX2で配布物の検査と並行して動かすと、4ワーカー・既定期限で許可例と禁止例の両方が時間切れになりました。同じ開発機で2ワーカー・30分に設定して再試行すると、禁止例は評議を完了して拒否し、許可例は再び時間切れになりました。どの時間切れでも操作回数は0回でした。`cpuThreads` と `evaluationTimeout` はホスト側で設定でき、評価期限の上限は30分です。

## 0.1.4 の採用判定

2026年11月19日、Bonsai-1.7B-Q1_0 と固定済みの三規則を用いて Server で採用基準を確認しました。

| 対象 | 結果 |
| --- | --- |
| 既存のモデル評価39例 | 誤承認0件。過剰拒否は9件から4件に減少。保護対象のゲートと指定票の悪化0件 |
| 状態値と寸法を正しく転記する例・変更する例の4件 | ゲート・指定票とも4件一致 |
| 必須の観測を取得できない7例 | 推論0回、操作0回 |
| 正式50例に含めていない追加14例 | 最終版で14例すべてを再生成。許可・拒否、指定票、操作の有無が期待値と一致 |
| 旧契約と通常の128トークン生成 | 旧契約試験に合格。生成トークン列のSHA-256が既存記録と一致 |

正式50例はすべて操作コールバックを含む経路で実行しました。5例は最終版のv46で直接評価し、45例は直前のv44で得た実応答をv46のプロンプト・モデル出力・票・ゲートと照合しました。誤って拒否したのは、公開情報の要約、翻訳、日付の書式変更、正しいカウンター値の報告です。これらも分母と記録に含めています。

件数の相違や明示的な開示禁止を許したv44の経路は修正しました。v46では件数の転記ミスをLogosが再照合します。転記元と異なる状態を報告する例では、先に成立した拒否票を維持し、理由が途中で切れた評議だけが原観測から自身の最終票を返します。

[採用判定と実行記録](evidence/bonsai-ctg-operations-20261119/admission.json) · [入力](evidence/bonsai-ctg-operations-20261119/cases.json) · [ケース別の票と操作](evidence/bonsai-ctg-operations-20261119/decisions.json) · [配布用NuGetを使ったホスト操作の検証](evidence/bonsai-ctg-operations-20261119/consumer.json)

## 評価を再現する

記録した入力 `cases.json` を作業ディレクトリにコピーし、検証用プロジェクト `tests/AIKernel.Inference.Optimization.Validation` から次の引数で実行します。

```text
--bonsai-ctg-routed-suite <Bonsai-1.7B-Q1_0.gguf> <rom/governance> <作業ディレクトリ>/cases.json <作業ディレクトリ>/result.json
--bonsai-ctg-replay-record-suite routed <rom/governance> <作業ディレクトリ> <作業ディレクトリ>/replay.json
```

1行目はモデルを使って各入力を評議し、判定と生のモデル応答を書き出します。全件の期待値との一致を検査するため、既知の過剰拒否も失敗として報告します。2行目は保存した応答で票とゲートを再構成します。各例の `outcome` が `matched` なら、その判定を再現できています。
