# Cosmos3 Edge／Nano のローカル推論

対象を旧Cosmos-Reason2-2Bから、NVIDIAの [Cosmos3 Edge](https://huggingface.co/nvidia/Cosmos3-Edge) と
[Cosmos3 Nano](https://huggingface.co/nvidia/Cosmos3-Nano) に変更した。
テキスト・画像・動画フレームを入力し、テキストを生成するAIKernel標準CPU経路である。
Cosmosの拡散モデルによる映像生成、音声生成、アクション生成はこのAPIの対象ではない。

推論時にPython、LibTorch、CUDA、外部APIは使わない。重みは読み取り専用のsafetensorsマップで保持し、
BF16／FP16を必要な演算時にFP32へ変換する。モデル全体のFP32コピーは持たない。
BF16の内積はFP64で累積して最後にFP32へ丸める。視覚LayerNormの平均・分散もFP64で集計する。
活性化とKVはFP32のまま。これは実モデルの数値基準を満たすための実装で、速度優位を主張するものではない。
ファイル入力に限り、呼び出し元が用意したFFmpegを使う。RGB24を直接渡す場合はFFmpegも不要。

## モデルの違い

| 項目 | Edge | Nano |
|---|---|---|
| 言語モデル | 28層、幅2048、GQA、ReLU²、Q/K正規化なし | 36層、幅4096、GQA、SwiGLU、Q/K正規化あり |
| 視覚モデル | SigLIP2、アンチエイリアス付き位置埋め込み補間、独立projector | Qwen3-VL、視覚RoPE、DeepStack |
| パッチ内の並び | HWC、時間方向1フレーム | CTHW、時間方向2フレーム |
| トークナイザー | 正規化なしの専用BPE | NFCのQwen BPE |
| 取り込む推論パラメーター数 | 2,435,616,496 | 8,767,123,696 |
| BF16推論重みのデータ量 | 4,871,232,992 bytes | 17,534,247,392 bytes |

公開モデルの4B／16Bという呼称には生成側も含まれる。このAPIは公式Transformersのreasonerが必要とする
テンソルだけを取り込む。再量子化はしない。KV・視覚特徴・OSのページキャッシュなどは上の重み量に含まれない。
**Cosmos3のGPU実行、Serverの7GB上限、AIKernelのSSD退避による上限保証は未検証・未実装**。
Gemmaで確認済みの7GBページングの結果を、この経路に適用できるとは扱わない。

## 重みの準備

2026-11-16時点の対象スナップショットは公開取得できるため、Chromeのログイン情報やHFトークンは使わない。
準備ツールはHFの暗黙の認証も無効にする。

```powershell
python tools/models/import_cosmos3.py Edge D:\models\Cosmos3-Edge
python tools/models/import_cosmos3.py Nano D:\models\Cosmos3-Nano
```

この**オフライン準備ツールだけ**はTorch、huggingface_hub、Cosmos3対応のTransformersを必要とする。
検証環境はTransformers `3a134bc37f7c22c801b618e289b0255425f22647`（5.24.0.dev0）、Torch 2.15.0+cpu。
別版のTransformersでモデル構造やテンプレートの挙動が変わった場合は、参照比較を再実行する。

- Edge固定revision：`6afc1256321c3a8e623d89be3bc5117a1e90f96b`
- Nano固定revision：`45666784803b3a7ad577e0def405f9afcc3bddb2`
- ソースshard全体のSHA-256を配布元のLFSメタデータと照合してから取り込む。
- 公式の重み名称変換を適用し、必要な全テンソルの形状・dtype・バイト範囲を検査する。
- 重みのバイト列を変えず、ルート直下の `reasoner-*.safetensors` と索引に保存する。
- 生成側の不要なテンソルは含めない。ソースと出力のハッシュは `import-manifest.json` に保存する。
- `import-sources` は再試行用の検証済みソースを保持する。準備中は元チェックポイントと取り込み先の両方のディスク容量が必要。

## C#から使う

```csharp
using AIKernel.Kernel.Inference;

using var model = new Cosmos3Model(modelDirectory, Cosmos3Variant.Edge,
    new CosmosExecutionOptions {
        CpuThreads = 4,
        ContextTokens = 2048,
        MaximumVisionPatches = 1024,
        EnableThinking = false // Edge公式テンプレートの空think指定。既定値はtrue。
    });

var text = await model.GenerateAsync("You are helpful.",
    [new CosmosContent.Text("What is 2 + 2? Answer briefly.")], maxNewTokens: 32);

var media = new CosmosMediaDecoder(ffmpegExecutable);
var image = await media.ReadImageAsync(imagePath, width: 256, height: 256);
var answer = await model.GenerateAsync("観察できる内容を説明してください。",
    [image, new CosmosContent.Text("画像に何が写っていますか。")], maxNewTokens: 64);

var video = await media.ReadVideoAsync(videoPath, width: 256, height: 256,
    intervalSeconds: .5, durationSeconds: 4, maximumFrames: 8);
var sequence = await model.GenerateAsync("動画の内容を時系列で説明してください。",
    [video, new CosmosContent.Text("何が変化しましたか。")], maxNewTokens: 64);
```

Nanoはモデルディレクトリーと `Cosmos3Variant.Nano` を指定する。Nanoのテンプレートは `EnableThinking` を使わない。
直接入力する場合は `CosmosContent.Image(CosmosRgbFrame)` または `CosmosContent.Video(frames)` を渡す。
幅・高さは32の倍数、画素は密に並んだRGB24、動画は同じ寸法かつ単調非減少の元フレーム時刻が必要。
リクエスト中は呼び出し元の画素メモリーを変更しない。

ファイルアダプターは明示された寸法へbicubicでリサイズする。公式側の自動解像度選択を再現するAPIではない。
数値比較では両実装へ同じRGBを渡し、公式側のリサイズと再サンプリングを止める。
入力上限は生成前に検査し、同一モデルのリクエストは直列化する。終了・キャンセル時には内部の画素特徴、KV、作業配列を消去する。
ユーザー本文に制御トークンの文字列が含まれても、テンプレートの制御IDとして有効化しない。

## 検証記録

検証結果と制限は [2026-11-16の記録](development/cosmos3-20261116-jp.md) を参照。
実行時間は動作確認の記録であり、CPU使用率10%以下を待った性能ベンチマークではない。
128トークン固定の性能比較や、GPU／Serverの比較グラフには流用しない。

旧Reason2のAPIと [検証記録](cosmos-reason2-inference.md) は残している。Edge／Nanoを旧Reason2として読み込むことはできない。
