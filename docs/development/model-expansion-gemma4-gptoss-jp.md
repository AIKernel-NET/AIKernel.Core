# Gemma 4 と GPT-OSS 対応の実装計画

2026-11-15。レビュー対象は `codex/context-free-cuda-pci` の `9362118`。
現時点で独自エンジンが実行できるのは Qwen3 dense / F32・Q1_0 です。
この文書は次版の実装計画であり、Gemma 4 / GPT-OSS の対応完了を示しません。
0.1.4 は既存の Bonsai の数値・再現性・配布検証を維持します。

## 添付レビューから採用する方針

モデル定義、重み形式、トークナイザー、対話形式、実行資源の制約を分離します。
最初は Qwen3 の演算を変えずに分離し、既存のハッシュ基準を通してから
新しいモデルを追加します。最初の候補は Gemma 4 E2B のテキスト入出力、
次に GPT-OSS-20B とします。画像・音声エンコーダーの実装は別の対応項目です。

高速化案は区間別プロファイルで優先度を決めます。総応答時間から
1サイクルあたりの演算要素数や、AVX-512 の遅延原因を確定しません。
Server / 検証機の旧グラフはプロトタイプの値であり、開発機の改修後の値と
混ぜて最内ループの効率を推定しません。

| 項目 | 次の実装・検証 |
| --- | --- |
| 命令セット分岐 | 行列の呼び出し時に解決し、文字列比較を内側ループから外す。既存ハッシュと処理区間の時間を比較 |
| AVX-512 マスク加算 | `2*selectedSum-totalSum` は加算順序・桁落ちが変わる実験として隔離。零・相殺・大きさの偏った入力も独立した倍精度参照と照合 |
| Q8 の複数ブロック処理 | 整数積和を並列化しても、各ブロックのスケールの掛け方、最終FP32加算順、丸め、FMAの扱いを固定。整数合計・中間floatのビット列・最終ハッシュを順に照合 |
| QKV / Gate-Up のまとめ処理 | 入力再利用と発行回数を測定。重み全体を複製する結合は前提にせず、テンソルのビューで処理する |
| ワーカーとAttention | 常駐ワーカーの同期費用とAttentionの値加重和を測定。共有Serverは既存の利用枠を守り、アイドル時は休止する |
| GEMM Prefill / GPU Forward | 個別の設計変更として進める。Prefill・Decode・転送・同期の時間を分け、GPU常駐後に自動選択を校正し直す |

`AIKERNEL_INFERENCE_PROFILE` の有無を同じ入力・バイナリ・ワーカー数で比べ、
計測自体の負荷を確認します。数学的に同値でもFP32の完全一致は仮定しません。

## 公式仕様に照らした補正

### Gemma 4 E2B は Qwen3 の小変更ではない

公式E2B設定には、語彙262,144、35層、512トークンの窓、全文脈層との混在、
KV共有、層別入力埋め込み、GELU系活性化、最終ロジットのsoft capが含まれます。
さらに局所Attentionと全文脈Attentionでhead次元・RoPE設定が異なります。
したがって窓付きAttentionとKV共有は、E2Bを正しく動かす前提です。
共有元の層とKVの寿命は公式参照実装に合わせ、層数だけから推測しません。
[Google配布のE2B設定](https://huggingface.co/google/gemma-4-E2B-it/blob/main/config.json)

E2Bは有効パラメーター数の表記です。公式カードでは埋め込み込み5.1B、
有効2.3Bとされているため、2B全重みとして見積もると不足します。
E4Bの有効値も4B以下とは限りません。読み込み上限を名称で判定せず、
実テンソル・形式・KV・作業領域から計算します。
[Gemma 4モデルカード](https://ai.google.dev/gemma/docs/core/model_card_4)

Gemma 4は `system` ロールを扱い、旧世代とは対話区切りも異なります。
旧Gemma用テンプレートやQwenの `<|im_start|>` を流用しません。
トークナイザーの方式・正規化・特殊IDは対象配布物で固定し、
「GemmaだからSentencePiece」と名前だけで選びません。
[Gemma 4対話形式](https://ai.google.dev/gemma/docs/core/prompt-formatting-gemma4)

### GPT-OSS はモデル定義と数値演算を追加する

20Bの公式設定では24層、32 experts中4を選択、語彙201,088、
128トークン窓と全文脈層の交互配置です。MoE重みはMXFP4です。
[OpenAI配布の20B設定](https://huggingface.co/openai/gpt-oss-20b/blob/main/config.json)

Attention sinkに加え、線形層のbias、クリップと係数を含むSwiGLU、
ルーターの選択・正規化もQwen3と分けます。sinkは単純な定数加算ではなく、
softmaxの安定化とマスクを含めて参照実装と照合します。
[OpenAI参照実装](https://github.com/openai/gpt-oss/blob/main/gpt_oss/torch/model.py)

o200k系BPEだけでは対話形式を再現できません。Harmonyのメッセージ境界・
channel・終了と制御移譲を区別し、公開返信にはfinal側を返します。
推論強度は対応するテンプレートの入力として扱います。
[OpenAIの形式要件](https://github.com/openai/gpt-oss#harmony-format--tools)

全expertのGPU常駐は高速化の配置方針であり、モデルを実行するための唯一の方法ではありません。
CPU配置・部分オフロード・expertキャッシュも設計候補ですが、転送待ちを測る必要があります。
active parameter数は計算量の目安であり、保存・常駐させる重みの総量には使いません。

### 重み形式は型ごとに扱う

Q4_0とQ8_0は32要素、Q1_0は128要素、Q4_Kは256要素のsuper-blockです。
`Q4_K_M` は混合形式の量子化プリセットであり、単一のテンソル型として登録しません。
Q4_0の格納スケールはFP16なので、BF16として読み替えてはいけません。
QAT変換での精度懸念は、元チェックポイント・変換器・格納型を固定して検証します。
MXFP4もINT4と同じ解釈にはせず、共有指数と値のデコードを用意します。
[GGMLブロック定義](https://github.com/ggml-org/llama.cpp/blob/master/ggml/src/ggml-common.h) ·
[GGML参照量子化](https://github.com/ggml-org/llama.cpp/blob/master/ggml/src/ggml-quants.c) ·
[量子化プリセット](https://github.com/ggml-org/llama.cpp/blob/master/include/llama.h)

GoogleにはQATのGGUF配布経路がありますが、実行可否は選んだファイルに含まれる
全テンソル型で判断します。Q4_0というファイル名だけでロード可能とはしません。
[Google QAT配布案内](https://ai.google.dev/gemma/docs/core#quantization-aware_training_qat)

## 現在の制約と変更する境界

| 実装箇所 | 現在の制約 | 次版の変更 |
| --- | --- | --- |
| [GgufWeights](../../src/AIKernel.Kernel/Inference/GgufWeights.cs) | qwen3固定、rank 1/2、各次元200,000、F32/Q1_0、総数4.1B上限 | GGUFの構文・境界検査をモデルの対応判定から分離。rank 3のexpert軸を含む形状配列を導入し、オフセット・バイト数はchecked 64bitで検証 |
| [Qwen3CpuModel](../../src/AIKernel.Kernel/Inference/Qwen3CpuModel.cs) | qwen3の名前・Layer構成、同一KV形状、head次元256以下 | モデルごとの定義、層ごとのAttention/RoPE/FFN/KV参照を構成。Qwen用の既存APIは互換adapterにする |
| 同モデルの実行上限 | 1～6ワーカー、64～2048文脈、出力256、入力文字数制限 | モデル能力と運用上限を分離。Serverの7GB・CPU枠は維持し、上限解除を既定動作にしない |
| [QwenTokenizer](../../src/AIKernel.Kernel/Inference/QwenTokenizer.cs) | gpt2/qwen2、NFC、固定した対話特殊トークン | encode/decodeとchat templateを分離し、モデル別の正規化・特殊ID・複数終了条件に対応 |
| [CudaQuantizedMatrix](../../src/AIKernel.Kernel/Inference/CudaQuantizedMatrix.cs) / [VulkanQuantizedMatrix](../../src/AIKernel.Kernel/Inference/VulkanQuantizedMatrix.cs) | 既定重み枠1GiB、ファイル長int.MaxValue以下 | 64bitテンソル領域と分割upload、配置計画・割当見積もり。単に上限値を広げない |
| [Qwen3Workspace](../../src/AIKernel.Kernel/Inference/Qwen3Workspace.cs) | 全層・文脈のFP32 KV | 局所窓のリング、共有KVの所有者、段階確保、精度を明示したKV保存。返却前消去と異常時の報告を維持 |

量子化の重み型と活性化精度は別の選択です。デコード参照実装、CPU kernel、
CUDA/Vulkan kernel、対応するブロック寸法と丸め契約を型ごとに登録します。
不明なarchitecture・未対応テンソル型・未対応の画像/音声入力は実行前に拒否し、
Qwenの処理へ黙って落としません。

## 実装順と完了条件

1. **互換性を保った分離**：GGUF読み取り、モデル定義、テンソル形式、tokenizer、
   chat template、resource limitsを分離。既存2モデル×5経路のQ8ハッシュ、
   FP32ロジット基準、キャンセル時の消去を通過させる。`auto`はFP32を維持。
2. **形式の参照実装**：F16/BF16、Q8_0、Q4_0を追加。小さな既知ブロック、
   非整列・端数・切断ファイル、2GiB境界、混合形式を検証してから高速化する。
   整数積和の中間飽和限界はQ1の±254を使い回さず、形式ごとに再計算する。
3. **Gemma 4 E2Bテキスト実行**：正確なtokenizer/template、PLE、局所/全文脈
   Attention、共有KV、層別RoPE・正規化・活性化・soft capを実装。
   最初は小さい運用文脈で公式参照に対する中間テンソル・ロジット照合を行う。
4. **GPT-OSS-20B**：MXFP4、MoE、sink、bias付き線形層と専用SwiGLU、Harmonyを追加。
   expertの選択/重み付き和と窓境界を小さなfixtureで検証し、その後に実モデルへ進む。
5. **高速化と実運用枠**：形式・モデルごとにGEMM Prefill、GPU Forward、配置と
   自動選択を測定。新モデルは新しいベンチマーク系列にし、Bonsaiの図を置き換えない。

新モデルの基準は、モデルrevisionとファイルSHA-256、変換器commit、
tokenizer/template、参照実装revision、精度・kernel・ワーカー・seedを固定します。
文字列の見た目だけでなくtoken ID列を照合し、日本語、空白、Unicode、
特殊トークンを含む利用者入力、終了/制御移譲のfixtureを持ちます。
サンプリングの追加はgreedy基準を維持した後に行います。

KVは窓の直前・境界・折返し、共有層、Prefillと逐次Decodeの一致を確認します。
モデルごとにteacher forcingで誤差許容値を先に決め、違うモデル同士に
Qwenのトークンハッシュや誤差閾値を流用しません。

## Serverのメモリ条件

7GB枠では、格納重みだけでなく、常駐ページ、KV、作業領域、tokenizer、
ランタイム、変換中のピーク、同時実行数を含めて入場判定します。
同じServerで複数モデルを同時にロードできることは前提にしません。
SSD/mmapはアドレス空間を広げる手段であり、RSS上限や速度の保証にはなりません。
expert/embeddingのアクセスで生じるページフォールト、初回/定常応答時間も記録します。

E2Bは最初の検証候補で、7GB枠内の動作保証は未実施です。
GPT-OSS-20Bの完全常駐を同枠へ約束せず、十分なメモリの開発機から検証します。
対応状況は「読み取り」「tokenizer/template」「数値一致」「生成」
「資源枠」「高速化」を別々に記録し、読み取りだけでモデル対応済みとは表示しません。
