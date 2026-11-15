# Gemma 4 E2BのCPUテキスト生成

`Gemma4E2BCpuModel` はGemma 4 E2Bの専用tokenizer、対話テンプレート、生成グラフを
AIKernel内で実行します。CUDA・LibTorch・Pythonのインストールは実行時に不要です。
現段階はFP32のCPU参照経路です。応答速度の最適化やGPUオフロードは未実装です。

## 使用例

```csharp
using AIKernel.Kernel.Inference;

using var model = new Gemma4E2BCpuModel(
    "gemma-4-E2B_q4_0-it.gguf", cpuThreads: 4, contextTokens: 1024);
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var result = await model.GenerateAsync(
    "日本語で短く回答してください。",
    "モデルが見つからず、サンプルが起動しません。確認点を一つ教えてください。",
    maxNewTokens: 64, cancellationToken: timeout.Token);
Console.WriteLine(result.Text);
```

検証に使用した配布物は
[GoogleのGemma 4 E2B QAT Q4_0 GGUF](https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/tree/675cff42a74c774d6cb76f76d8eacb49b48c9b93) です。
ファイル名は `gemma-4-E2B_q4_0-it.gguf`、SHA-256は
`fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634`。
Q4_0の線形層だけでなく、F16の層別射影とQ6_Kの埋め込みを含みます。
モデル本体はNuGetやリポジトリには同梱しません。

## 対応する処理

- Unicode BPE、空白の置換、byte fallback。NFC正規化やQwen用の分割規則は適用しません。
- Googleの単一system/userターンの対話形式。制御IDはテンプレートが挿入し、
  利用者が本文に書いた `<|turn>` などを制御トークンにはしません。
- 35層のE2Bグラフ、層別埋め込み（PLE）、GELU、RMSNorm、出力のsoft cap。
- 512トークンの局所Attentionと全文脈Attention。ヘッド幅256/512と層別RoPE。
- KVの所有層は先頭15層。後半20層は同じAttention種別の層13/14を参照します。
  局所KVはリングで保持し、共有する層からの二重書き込みを行いません。
- Greedy生成、`<turn|>` / `<eos>` による終了。toolへの制御移譲は未対応として例外にします。
  完結した思考channelは公開返信から取り除き、不完全なchannelは返信として返しません。

対象はE2Bの**テキスト入力・テキスト出力**です。E4B・MoEモデル・画像・音声・動画・
ツール呼び出し・思考モードの指定・複数ターンの履歴APIは今回の対象外です。
未知の形状や追加の出力headをQwen/E2Bとして黙って処理しません。

## 運用上限とデータの寿命

初期値は4ワーカー、文脈1024、生成128トークン、マップするファイルの上限4GiBです。
指定できる範囲は1〜6ワーカー、文脈64〜2048、生成1〜256トークン。
system/userは各8192文字以内で、トークン化後に「入力＋出力枠」が文脈に収まるか検査します。
超過した入力を切り捨てて生成しません。

重みは符号化されたままマップし、最大256要素のブロックでデコードします。
KV、ロジット、Attentionスコア、PLEと中間活性化はリクエストごとに確保し、
成功・例外・キャンセルのいずれでもモデルの同時実行枠を返す前に消去します。
現段階では利用者のprefixをキャッシュしません。
公開APIはモデルごとに生成を直列化します。`Dispose` は実行中の生成の完了を待ちます。

マップされたファイルのサイズはRSSの保証ではありません。Server上の7GB制限、
長時間負荷、Linux・AVX-only実機でのGemma生成はまだ検証していません。
この参照経路をBonsaiの性能グラフへ混在させないでください。

## 数値検証

Google配布のconfig/tokenizerと、revisionを固定したTransformersの
`Gemma4TextModel` を参照にしました。量子化差を混ぜないため、**両者に同じQAT GGUF重み**を使い、
参照側の重みデコードは独立した `gguf-py` で行っています。
元のBF16チェックポイントとの品質・量子化誤差の評価とは別の検証です。

FP32の加算順序が違うためビット完全一致は要求せず、最大絶対誤差 `1e-3`、
RMS誤差 `1e-4` をゲートにします。token ID列と終了処理も確認します。
詳細は[検証記録と再現手順](development/evidence/gemma4-e2b-20261115/README.md)を参照してください。
