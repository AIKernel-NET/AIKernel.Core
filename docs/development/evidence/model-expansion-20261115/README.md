# モデル拡張の第1段階：互換性とGGUF形式の検証

2026-11-15、`codex/model-expansion-gemma4-gptoss`。
起点は `c6f8fdf`。これは性能ベンチマークではなく、読み取り・数値・生成結果の回帰検証です。
Gemma 4 / GPT-OSS のforward、tokenizer、template、生成、RSS上限は未検証です。

## 結果

- Coreテスト：854件成功、失敗0、スキップ0。追加20件ではF16/BF16の全有限ビットパターン、
  MXFP4の全符号と指数境界、Q4/Q6の独立したパック値、CPU参照内積、expertビュー、
  2GiB超のoffset、1TiBテンソルのdescriptor、不正な入力・キャンセルを確認しました。
- Bonsai 1.7B / 4B：AVX2・FP32、各32ステップ、6チェックポイントの
  **910,014ロジット/モデルが改修前とビット完全一致**。greedy token ID列も一致しました。
  [FP32結果](fp32-compatibility.json)
- Bonsai 2モデル × CPU 2経路・CUDA 3経路：128トークン固定のハッシュを
  11月15日の既存基準と照合しました。[経路別結果](q8-compatibility.json)
  CPUは `avx2-int8-q8` / `avx2-q8`、CUDAは `dp4a` / `q8` / `tensorcore`。
  この回はGPUデバイス0での互換性確認であり、全GPU・全命令セットの再測定ではありません。
- Gemma 4 QAT / GPT-OSS：revisionを固定した実GGUFの先頭16MiBを取得し、
  総ファイル長3,349,516,256 / 12,109,566,624バイトに対して全テンソル領域を検査しました。
  [ヘッダーの検査結果](header-validation.json) · [配布元と取得部分のハッシュ](header-provenance.json)

`header-provenance.json` の `fileSha256` は配布元のメタデータの値です。
ファイル全体はダウンロードしていないため、今回こちらで検証したのは `headerSha256` です。
先頭部分に含まれない重みの内容、生成の数値一致、実行資源枠はこの検査では保証しません。

## 再現手順

リポジトリルートから実行します。生成検証には対応するモデルと、ビルド済みのCPU/CUDAネイティブDLLが必要です。
配布・モデル本体・ヘッダーのバイナリはこのディレクトリに含めていません。

```powershell
dotnet test tests/AIKernel.Core.Tests/AIKernel.Core.Tests.csproj -c Release -p:GeneratePackageOnBuild=false
dotnet build tests/AIKernel.Inference.Optimization.Validation/AIKernel.Inference.Optimization.Validation.csproj -c Release -r win-x64 -o artifacts/model-expansion-check -p:GeneratePackageOnBuild=false
python tests/AIKernel.Inference.Optimization.Validation/inspect_pinned_headers.py artifacts/model-expansion-check/AIKernel.Core.Tests.exe artifacts/model-expansion-headers
```

ヘッダー検証スクリプトは固定revisionの先頭だけを取得し、SHA-256を照合します。
既存のキャッシュがあれば再利用します。通常の単体テストからネットワーク接続は行いません。
手元に全ファイルがある場合は `AIKernel.Core.Tests.exe --inspect <path.gguf>` でも検査できます。

FP32照合は `LogitValidation.cs` の固定プロンプト、4ワーカー、文脈1024、greedy生成で実行しました。
基準DLLは変更前に退避し、Kernel DLLのSHA-256は
`0b54e70506fb8d78c75e550650ce8955ab59289619235a105c12ce5573efabc0` です。
変更前のrunnerで `--logits <model> avx2 <before>` を実行後、変更後のrunnerで
`--logits <model> avx2 <after> <before.tokens.json>` を実行します。
`.f32` の全バイトと `.tokens.json` の全IDを比較します。

Q8のハッシュ照合は `Program.cs` の同一プロンプトとwarmupを使い、各runnerを
`<model> cpu avx2-int8-q8`、`<model> cpu avx2-q8`、`<model> cuda dp4a`、
`<model> cuda q8`、`<model> cuda tensorcore` で呼び出します。
ハッシュ基準はWebSiteの `public_html/assets/benchmarks/2026-11-15-optimization/final-q8-hashes.json`。
ネイティブDLLのハッシュは結果JSONに保存しています。

| 実行に使用したモデル | 全ファイルSHA-256（今回ローカルで検証） |
| --- | --- |
| Bonsai-1.7B-Q1_0.gguf | `3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3` |
| Bonsai-4B-Q1_0.gguf | `4524b3f997f0f06444e568d1f26e2efd69effa3218c7ad3047432fb171e42168` |

## 数値契約と今後のゲート

追加形式は符号・ブロック配置とスケールを扱うCPU参照実装です。活性化をQ8へ丸めません。
Q4_K_Mはモデルファイルの量子化プリセットなので、テンソル型として登録していません。
F32/Q1_0以外を既存GPU/Q8カーネルで処理する指定は拒否します。
今回の合格結果だけで、新モデルの生成数値や新形式のGPU性能が検証済みとはしません。

次はGemma 4のtokenizer/templateと層ごとのモデル定義を実装し、公式参照との中間値・
teacher-forced logitsの照合を行います。GPT-OSSにはこれに加えMoEとHarmonyが必要です。
新モデルの実行上限は、格納重みだけでなくKV・作業領域・変換時ピークを含めて別途設計します。
