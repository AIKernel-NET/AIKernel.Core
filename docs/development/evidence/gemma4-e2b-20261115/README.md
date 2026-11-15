# Gemma 4 E2B：CPU生成と参照照合

2026-11-15、`codex/model-expansion-gemma4-gptoss`、起点 `eaa6200`。
この検証は生成機能と数値・既存モデルの互換性が対象です。
複数の機能テストを並行したため、所要時間を性能ベンチマークとして比較しません。

## モデルと参照

- モデル：Google `gemma-4-E2B_q4_0-it.gguf`、revision
  `675cff42a74c774d6cb76f76d8eacb49b48c9b93`。
  全ファイルSHA-256 `fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634` を取得後に検証。
- Googleのconfig/tokenizer/template：revision `4adf15748de9e1a0097b6274cee9c15836c41c37`。
- Transformers：revision `3a134bc37f7c22c801b618e289b0255425f22647` の
  `Gemma4TextModel`、eager Attention、FP32、4 CPUスレッド。
  実際にimportしたmodeling/configurationファイルのハッシュも照合済み。
- Python 3.12、PyTorch `2.15.0+cpu`、gguf `0.19.0`、tokenizers `0.23.3`、NumPy `2.5.4`。
  [配布元・revision・ソースハッシュ](reference-provenance.json)

**同じQAT量子化重みを両実装へ渡しています。** 参照側は独立したgguf-pyでデコードし、
計算はTransformers本体のForwardを実行します。容量の大きい埋め込みだけは必要な行を
デコードするlookupに置き換え、公式の埋め込み倍率を適用します。Attention、共有KV、
PLE射影、FFN、正規化、RoPEは参照実装をそのまま使用します。
元のBF16チェックポイントとの量子化品質比較、GoogleのJAX実装との比較は行っていません。

## 合格した検証

| 項目 | 条件・結果 |
| --- | --- |
| tokenizer | 日本語、空白、改行、結合文字、絵文字、byte fallback、制御文字列、固定seedの文字列を含む112例でIDとdecodeが一致 |
| 対話テンプレート | thinking/toolsを無効にした単一system/userターン8例で、GoogleのJinjaテンプレートから得たIDと一致 |
| 数値 | 2位置 × 35層のAttention後/FFN後/層出力、埋め込み・PLE・最終Norm、12ステップの全語彙ロジット、計228組が合格 |
| ロジット | 262,144語彙 × 12ステップ、最大絶対誤差 `0.00014209747314453125` |
| 中間値を含む最大誤差 | 最大絶対誤差 `0.0006103515625` |
| トークン | 独立したgreedy生成で12ステップとも同じID。自然な停止までの返信は「東京です。」 |
| 公開API | `<turn\|>` による停止、途中キャンセル、キャンセル後の再実行、Dispose後の拒否を確認 |
| 単体テスト | 868件成功、失敗0、スキップ0。14件を追加 |

数値ゲートは全比較で最大絶対誤差 `1e-3`、RMS誤差 `1e-4` を固定しています。
AIKernelの逐次FP32加算とPyTorchのバッチ演算は加算順序が異なるため、ビット一致は要求しません。
入力は[固定ID列](generation-input.json)。参照はバッチPrefill、AIKernelは逐次Prefillで、
双方の独立生成が一致したため各Decode比較の履歴も同じです。
EOS後の追加ステップは数値検証だけで実行し、通常の生成APIでは終了時に停止します。

[数値比較の全結果](numerical-comparison.json) · [AIKernelのID列](candidate-result.json) ·
[参照のID列](reference-result.json) · [tokenizer/template結果](tokenizer-comparison.json)

KVの単体テストは窓の直前・境界・直後・2回目の折返し（511/512/513/1025トークン）を含みます。
保持した全履歴からの独立計算と、リングKVのAttention結果を比較します。
共有する読者が書き込まないこと、全文脈KVの上限超過拒否、KV・ロジット・スコア・PLE・
中間活性化の消去も確認します。実モデル全体で512超の長文生成は今回の比較に含めていません。

## 実生成と既存モデル

[ライフサイクル検証](lifecycle.json)では「日本の首都はどこですか？」への回答が
「東京です。」となり、3出力トークンで停止しました。途中キャンセル後にも同じ回答が返りました。
[問い合わせへの返信](support-reply.json)も通常の `GenerateAsync` から生成し、停止を確認しました。
モデルの返信内容の正確性や問い合わせ分類の品質を保証する評価ではありません。

Bonsai 1.7B / 4Bは、既存の2 CPU Q8経路と3 CUDA経路で128トークンのハッシュを照合し、全10経路が一致しました。
FP32は各モデル910,014ロジットと32ステップのIDが、改修前の基準とビット完全一致しました。
[Q8/CUDA結果](bonsai-regression.json) · [FP32結果](bonsai-fp32-regression.json)
基準とネイティブDLLのハッシュは[前段階の記録](../model-expansion-20261115/README.md)と同じです。

## 再現手順

モデル本体とPython参照の依存関係は検証専用です。AIKernelの実行依存には追加していません。
以下はリポジトリルートからのPowerShell例です。

```powershell
dotnet test tests/AIKernel.Core.Tests/AIKernel.Core.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
dotnet build tests/AIKernel.Inference.Optimization.Validation/AIKernel.Inference.Optimization.Validation.csproj -c Release -r win-x64 -o artifacts/gemma4-check -p:GeneratePackageOnBuild=false
python -m venv artifacts/gemma4-reference-env
artifacts/gemma4-reference-env/Scripts/python -m pip install torch==2.15.0+cpu --index-url https://download.pytorch.org/whl/cpu
artifacts/gemma4-reference-env/Scripts/python -m pip install -r tests/AIKernel.Inference.Optimization.Validation/requirements-gemma4-reference.txt
```

以後の `python` はこの検証用venvを指定します。`<model.gguf>` は前記のSHA-256に一致するファイルです。
モデル重み、32MBのtokenizer、ロジットのバイナリをGitへ含めないでください。

```text
python tests/AIKernel.Inference.Optimization.Validation/prepare_gemma4_reference.py artifacts/gemma4-assets
python tests/AIKernel.Inference.Optimization.Validation/gemma4_tokenizer_reference.py artifacts/gemma4-check/AIKernel.Core.Tests.exe <model.gguf> artifacts/gemma4-assets artifacts/gemma4-tokenizer
artifacts/gemma4-check/AIKernel.Core.Tests.exe --gemma-forward <model.gguf> docs/development/evidence/gemma4-e2b-20261115/generation-input.json artifacts/gemma4-candidate
python tests/AIKernel.Inference.Optimization.Validation/gemma4_reference.py <model.gguf> artifacts/gemma4-assets docs/development/evidence/gemma4-e2b-20261115/generation-input.json artifacts/gemma4-reference
python tests/AIKernel.Inference.Optimization.Validation/compare_gemma4.py artifacts/gemma4-candidate artifacts/gemma4-reference artifacts/gemma4-comparison.json
artifacts/gemma4-check/AIKernel.Core.Tests.exe --gemma-lifecycle <model.gguf>
```

通常のAPIを使う試験は `--gemma-generate <model.gguf> <system> <user> <maxNewTokens>` です。
既存Bonsaiの回帰は `compare_bonsai_hashes.py <runner> <models-directory> <baseline-json> <output-json>`。
FP32のコマンドは前段階と同じ `--logits` です。

## 未検証の範囲

生成が確認できたのはWindows開発機のCPU参照経路です。
GemmaのSIMD/GPU高速化、Linux/AVX-only実機、Serverの7GB枠、長時間負荷、
元BF16チェックポイントとの品質比較、画像・音声、ツールや履歴付き対話は未検証です。
GPT-OSSの生成対応もこの変更には含みません。
