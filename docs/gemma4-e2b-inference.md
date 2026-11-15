# Gemma 4 E2BのCPU／GPUテキスト生成

`Gemma4E2BCpuModel` はGemma 4 E2Bの専用tokenizer、対話テンプレート、生成グラフを
AIKernel内で実行します。CPUではAVX2 SIMDを使用し、非対応CPUでは参照経路へ戻ります。
CPU実行にはCUDA・LibTorch・Pythonは不要です。GPUは明示的に選択でき、NVIDIAドライバーと
AIKernelのネイティブDLLを使います。CUDA Toolkit・LibTorchは実行時に必要ありません。
どちらもFP32の活性化を維持し、追加のINT8量子化は行いません。

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

GPUの行列演算を使う場合は次のように指定します。従来のクラス名を維持しており、
選択結果は `Backend`、GPUに置いた重みのバイト数は `GpuWeightBytes` で確認できます。

```csharp
using var model = new Gemma4E2BCpuModel(
    "gemma-4-E2B_q4_0-it.gguf", 4, 1024, 4L << 30,
    new Gemma4ExecutionOptions {
        Backend = Gemma4Backend.Cuda,
        CudaDeviceOrdinal = 0,
        MaximumGpuWeightBytes = 2L << 30
    });
```

`Auto` はCPU上でAVX2の可否を判定します。CPUとGPUの速度を自動計測して選ぶ機能ではありません。
比較用の `Reference` は改修前のスカラー計算を保ちます。CUDA指定時の未対応形式、VRAM不足、
ドライバーエラーは呼び出し元へ返し、CPUへ黙って切り替えません。

GPU経路はF16／Q4_0／Q6_Kの行列を符号化されたまま保持します。巨大なPLEトークン検索表は
CPU上のマップに残し、FP32重みの全展開コピーを作りません。4行／128スレッドで各行を1warpが
協調して計算します。Attention、RoPE、GELU、共有KVはCPU側です。行列ごとの同期は残っており、
forward全体のGPU常駐化やバッチPrefillは今後の最適化項目です。
このPTX経路はCompute Capability 7.5以上を対象にしています。

## Serverの7GB枠とSSDバックエンド

Serverでは次のようにCPU用のメモリ予算を指定します。単位は十進バイトです。

```csharp
using var model = new Gemma4E2BCpuModel(path, 6, 2048, 4L << 30,
    new Gemma4ExecutionOptions {
        Memory = new Gemma4MemoryOptions {
            MaximumResidentBytes = 7_000_000_000,
            WeightCacheBytes = 5_000_000_000,
            RuntimeReserveBytes = 1_000_000_000,
            SwapDirectory = privateSwapDirectory
        }
    });
```

このモードは無制限のGGUFマップを解放し、すべての重みアクセスをAIKernelの
`VirtualMemorySpace` に接続します。行をまたがない4MiBページを上限付きのRAMキャッシュへ
読み込み、入り切らないページはSSD上のGGUFをbackingとして再取得します。重みは不変なので、
同じデータを別のswapファイルへ重複して書きません。これはOSのmmap任せのページングとは別の経路です。
OSのファイルキャッシュは利用されるため、backing読み込み回数は物理SSDのI/O回数と同一ではありません。

KV・活性化・tokenizer・ランタイム用の余裕を残し、ページ境界とforward開始時にRSSを確認します。
圧力が高ければRAMキャッシュを退避し、予備バッファも解放します。退避できないメモリだけで
余裕を確保できない場合は例外で停止します。`RuntimeReserveBytes` は予算上の余裕であり、
OSへのメモリ予約ではありません。この制御はcgroupのようなOS強制上限ではないため、
Server試験では外側にもRSS監視を置きます。CPU用ページングとGPU常駐重みの同時指定は未対応です。

`MemoryStatistics` からRAMページ数と `BackingReads` を確認できます。
32MiBのキャッシュで同じモデルを生成し、退避・再読み込み後も公式参照との数値契約を維持する
試験を用意しています。生成時のKVなどの利用者データはRAMで消去し、今回の重みページングには含めません。

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

マップされたファイルのサイズはRSSの保証ではありません。CPU／GPUの応答時間と
Serverの7GB枠の検証は[最適化の検証記録](development/gemma4-optimization-20261115-jp.md)で
条件を区別して記録します。Bonsaiとはモデル・入力・量子化形式が異なるため、速度を直接比較しません。
GPU経路ではリクエストの終了時に転送用のホスト／デバイスscratchも消去します。
消去に失敗した場合は例外を返します。CUDAコンテキストの破棄だけを「消去の成功」とは扱いません。

## 数値検証

Google配布のconfig/tokenizerと、revisionを固定したTransformersの
`Gemma4TextModel` を参照にしました。量子化差を混ぜないため、**両者に同じQAT GGUF重み**を使い、
参照側の重みデコードは独立した `gguf-py` で行っています。
元のBF16チェックポイントとの品質・量子化誤差の評価とは別の検証です。

FP32の加算順序が違うためビット完全一致は要求せず、最大絶対誤差 `1e-3`、
RMS誤差 `1e-4` をゲートにします。token ID列と終了処理も確認します。
詳細は[検証記録と再現手順](development/evidence/gemma4-e2b-20261115/README.md)を参照してください。
