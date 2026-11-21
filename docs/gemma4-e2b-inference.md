# Gemma 4 E2BのCPU／GPUテキスト生成

`Gemma4E2BCpuModel` はGemma 4 E2Bの専用tokenizer、対話テンプレート、生成グラフを
AIKernel内で実行します。CPUではAVX-512／AVX2のFP32 SIMDを使用し、非対応CPUでは参照経路へ戻ります。
CPU実行にはCUDA・LibTorch・Pythonは不要です。GPUは明示的に選択でき、NVIDIAドライバーと
AIKernelのネイティブDLLを使います。CUDA Toolkit・LibTorchは実行時に必要ありません。
既定のCPU経路とCUDAはFP32の活性化を維持します。VNNIの残差Q8経路は明示指定専用です。

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

forward全体をGPUに置く場合は次のように指定します。従来のクラス名を維持しており、
選択結果は `Backend`、GPUの符号化重みは `GpuWeightBytes`、作業領域等を含む割り当て量は
`GpuResidentBytes` で確認できます。

```csharp
using var model = new Gemma4E2BCpuModel(
    "gemma-4-E2B_q4_0-it.gguf", 4, 1024, 4L << 30,
    new Gemma4ExecutionOptions {
        Backend = Gemma4Backend.CudaResident,
        CudaDeviceOrdinal = 0,
        MaximumResidentGpuBytes = 4L << 30
    });
```

`Auto` はCPU上でAVX-512、AVX2、参照経路の順に可否を判定します。
CPUとGPUの速度を自動計測して選ぶ機能ではありません。
比較用の `Reference` は改修前のスカラー計算を保ちます。CUDA指定時の未対応形式、VRAM不足、
ドライバーエラーは呼び出し元へ返し、CPUへ黙って切り替えません。

`CudaResident` はF16／Q4_0／Q6_Kの行列、PLE検索表、共有KV、中間活性化をGPUに保持します。
正規化、Attention、RoPE、GELU、残差、soft cap、argmaxまで同じストリームへ発行し、
通常のforwardは最後に1回同期します。生成時の戻り値はtoken IDとエラー状態の8バイトです。
位置ごとのRoPE定数だけは読み込み時にCPUで計算してGPUへ置きます。
文脈2048で明示的GPU割り当ては約3.379GB。予算には重み・KV・作業領域・定数表を含め、
ドライバー固有のメモリと診断用転送領域は含めません。ロード後はCPUの重みマップを解放します。

既存の `Cuda` は行列だけをGPUへ置く比較用経路として維持しています。この経路では
`MaximumGpuWeightBytes`（既定2GiB）が有効で、PLE検索・Attention・KVはCPU側です。
両経路とも全重みのFP32展開コピーは作りません。バッチPrefillは今後の対象です。
このPTX経路はCompute Capability 7.5以上を対象にしています。

## CPUの命令セット指定

Q6_Kは32要素ずつレジスター内で復号し、スカラー復号用の一時タイルを介さず内積へ渡します。
Q4_0／Q6_K／F16のHalf変換は、プロセス内で共有する65,536要素の表（約256KiB）を使います。
全重みの展開コピーは追加しません。Q6_Kの `(blockScale * localScale) * quant`、
活性化との乗算、各アキュムレーターへの加算順序は維持し、FMAへの置換は行いません。

`Gemma4ExecutionOptions.InstructionSet` に `Avx2`／`Avx512` を指定するとFP32経路を固定できます。
`AvxVnniResidual`／`Avx512VnniResidual` は、32要素の入力を3段の残差Q8へ分け、Q4_0行列に
整数積和を使う実験経路です。F16／Q6_KにはFP32内積を使います。対応する最新の
`aikernel_cpu_accel` ライブラリとCPU・OSの対応が必要で、強制指定が使えなければ例外になります。

残差Q8は近似計算です。検証した入力では既存の数値ゲートを維持しましたが、ビット完全一致を
一般に保証するものではありません。今回のServer実測ではAVX2より遅く、`Auto` の候補には
加えていません。生成結果の `ActivationPrecision` は `q8-residual-3`、`Backend` は実際の経路名です。

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
`InitialWeightCacheBytes` を指定すると、モデルのページ数とプロセスの空き予算内で
重み用バッファを先に確保します。Gemma 4 E2BとGPT-OSS 20Bの `WeightRamUsage` から、
確保済み・使用中・再利用可能なバッファと確保量のピークをバイト単位で取得できます。
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

重みは符号化されたまま保持し、CPU SIMD経路はレジスター内で必要な要素を復号します。
非SIMDの参照経路は従来のブロック復号を維持します。
CPUのKV、ロジット、Attentionスコア、PLEと中間活性化はリクエストごとに確保します。
GPU常駐経路はモデル専用の作業領域を再利用し、CPU側のKVコピーは確保しません。
いずれも成功・例外・キャンセル時にモデルの同時実行枠を返す前に利用者データを消去します。
現段階では利用者のprefixをキャッシュしません。
公開APIはモデルごとに生成を直列化します。`Dispose` は実行中の生成の完了を待ちます。

マップされたファイルのサイズはRSSの保証ではありません。CPU／GPUの応答時間と
Serverの7GB枠の検証は[最適化の検証記録](development/gemma4-optimization-20261115-jp.md)で
条件を区別して記録します。Bonsaiとはモデル・入力・量子化形式が異なるため、速度を直接比較しません。
AVX-512／VNNIとGPU全forwardの追加結果は[2026-11-16の比較記録](development/gemma4-resident-20261116-jp.md)にまとめています。
Q6_K復号・Half変換の改修前後は[CPUカーネルの追加検証](development/gemma4-q6-20261116-jp.md)に記録しています。
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
