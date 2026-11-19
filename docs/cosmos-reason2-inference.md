# Cosmos-Reason2-2B 推論プレビュー

2026-11-16追記：利用者の指定により、開発対象を **Cosmos3 Edge／Nano** に変更した。
新しい利用手順と実重みの検証結果は [Cosmos3推論](cosmos3-inference.md) を参照。
以下はReason2の合成重み検証時点の記録であり、Reason2のアクセス申請を進める必要はない。

対象は NVIDIA の [Cosmos-Reason2-2B](https://huggingface.co/nvidia/Cosmos-Reason2-2B)。
テキスト・画像・動画を扱う Qwen3-VL の CPU 経路を AIKernel.Kernel 内に実装した。
**実際のCosmos重みでの生成検証は未完了**。2026-11-16時点では、保存済み認証情報を使った公式checkpointの取得が401となり、アクセス許可待ちである。
公開されているベースモデルのメタデータと、小型の合成重みを使った公式参照比較まで完了している。
この状態をCosmos本体の品質・性能・リリース適合性の確認済みとは扱わない。

## 実装内容

- safetensors の F32／BF16／F16。重みは読み取り専用メモリーマップで保持し、モデル全体のFP32コピーを作らない。
- 画像の16×16パッチ、時間方向2フレーム、2×2の空間マージ、画像の位置埋め込み補間、2次元RoPE、非因果アテンション。
- DeepStackの中間視覚特徴を言語モデルの初期層に加える処理。
- Q/K正規化、GQA、3軸のinterleaved MRoPE、KVキャッシュ、greedy生成。
- NFC byte-level BPE、対話テンプレート、動画のフレーム対の平均時刻。奇数フレームの末尾は複製する。
- 入力サイズ・フレーム数・総パッチ数・文脈長の事前検査、キャンセル、リクエスト終了時の内部画素特徴・KV・作業配列の消去。

推論でPython／LibTorch／外部APIは使用しない。FFmpegは任意のファイルデコード用アダプターに限る。
RGB24の画像や動画フレームを直接渡す場合、FFmpegも不要。

## C#で使う

利用条件への同意とアクセス権を持つアカウントで取得した、次の公式ファイルを同じローカルフォルダーに置く。
SDKはモデルや認証情報を自動取得しない。

```text
config.json
tokenizer.json
preprocessor_config.json
video_preprocessor_config.json
chat_template.json
model.safetensors
```

分割されたモデルの場合は `model.safetensors.index.json` と、そのインデックス内のローカルshardも読める。
プロセッサー定義とテンプレートが検証済みの仕様と異なる場合は、黙って別の前処理を適用せず読み込みを拒否する。
実Cosmos checkpointのテンプレートとの照合も、アクセス許可後の検証に含む。

```csharp
using AIKernel.Kernel.Inference;

using var model = new CosmosReason2Model(modelDirectory,
    new CosmosExecutionOptions { CpuThreads = 4, ContextTokens = 2048 });
var media = new CosmosMediaDecoder(ffmpegExecutable);
var image = await media.ReadImageAsync(imagePath, width: 256, height: 256);
var reply = await model.GenerateAsync(
    "観察できる内容と推測を区別して説明してください。",
    [image, new CosmosContent.Text("画像には何が写っていますか。")],
    maxNewTokens: 64);
Console.WriteLine(reply.Text);

var video = await media.ReadVideoAsync(videoPath,
    width: 256, height: 256, intervalSeconds: .5,
    durationSeconds: 4, maximumFrames: 8);
var videoReply = await model.GenerateAsync(
    "動画の出来事を時系列で説明してください。",
    [video, new CosmosContent.Text("何が変化しましたか。")],
    maxNewTokens: 64);
```

テキストのみなら `CosmosContent.Text` だけを渡す。
直接入力は `CosmosContent.Image(CosmosRgbFrame)` または `CosmosContent.Video(frames)` を使う。
RGBは密に並んだ3チャンネル、幅・高さは32の倍数。動画は同じ寸法・単調非減少の秒単位タイムスタンプが必要。
フレームのメモリーはリクエスト完了まで変更しない。SDKは呼び出し元が所有するRGBメモリーを消去しない。

アダプターのリサイズは指定寸法へのbicubicであり、公式プロセッサーの自動解像度選択とは異なる。
同じ結果を比較するときは、両方へ同じデコード済みRGBと寸法を渡し、公式側の自動リサイズ・再サンプリングを無効にする。
動画アダプターは選択した元フレームの時刻を取得する。仮のFPSで時刻を作らない。

## 検証済みの範囲

[検証記録](development/evidence/cosmos-reason2-20261116/README.md)に結果と再現方法を保存した。

| 検証 | 結果 |
| --- | --- |
| 小型の公式Qwen3-VLグラフ、F32／BF16／F16、テキスト・画像・動画 | 各形式16チェック合格。最大絶対誤差5.113×10⁻⁷（ゲート1×10⁻⁵） |
| 各形式・各入力の8トークン生成 | 公式参照のtoken IDと一致 |
| 画像・動画パッチ | 公式Fastプロセッサーとビット一致 |
| テキスト／画像／動画／混在の対話トークン・3軸位置 | 公式参照と一致 |
| AVX2を無効にしたBF16経路 | 同じ参照ゲートと生成token ID一致。AVX-only実機試験ではない |
| PNG、可逆圧縮動画 | 画素と選択フレームの時刻が一致、キャンセル成功 |
| 単体テスト全体 | 900件成功、失敗・スキップ0件 |

合成モデルは言語幅32、4層、視覚幅32、3層、64×96画像、奇数3フレーム動画を使用する。
実際の2B checkpointとは規模も学習済み重みも異なるため、これらの誤差や出力一致を実モデルへ外挿しない。
BF16／F16試験も活性化はFP32。同じ保存重みを独立に復号した公式FP32グラフとの比較である。

参照のプロセッサー・対話処理はTransformers **4.57.1** に固定した。
検討中の5.24開発版では動画の外側のvision制御トークンの扱いが異なったため、版を混ぜない。

## 残る検証

実Cosmosモデルの取得・メタデータ照合、テキスト／画像／動画の実生成と公式ロジット比較が必要。
CosmosのGPU経路、量子化GGUF、バッチprefill、Serverの7GB枠はまだ検証していない。
今回のメモリーマップはOSによるページングであり、Gemmaで検証したAIKernelの5GBキャッシュ／7GB RSS予算をこのモデルへ適用したものではない。
Gemma側の7GB枠とBonsaiの既存実装はそのまま維持する。
