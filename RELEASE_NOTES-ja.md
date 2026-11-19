# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.4（公開準備中）

- Cosmos 3 Edge／Nano の取り込み済み重みで、文章・RGB画像・時刻付き動画フレームから応答を生成できます。CPU推論は CUDA と LibTorch が不要です。対話APIは各ターンの発言者と画像を保って次の質問を処理します。
- Monolith のモデル登録で、使用するモデルと import-manifest.json の SHA-256 を固定できます。WebSite の日英チャットでは、接続状態を確認し、同じ写真について続けて質問できます。利用者のAPIキーは不要です。
- GPT-OSS-20B は Server の実メモリ7GB枠でSSDへのページングを使用し、検証用2入力で llama.cpp と生成トークン列を照合しました。Cosmos の必要メモリはモデルと入力条件で変わります。
- Bonsai 1.7B を用いる CTG 評価では、審査対象の提案と裏付けとなる観測を分け、governance ROM の規則に従って判定します。対象例の合格は任意の提案に対する精度を保証するものではありません。
- CPU版を標準とし、対応GPUには CUDA Driver 経路を用意します。旧 LibTorch 追加パッケージの配布版は 0.1.3 です。

## 0.1.3

- Common・Core・Kernel・Hosting・TestKit を AIKernel.NET の 0.1.3 契約に揃えます。Core はマネージドランタイムであり、CUDA・LibTorch などのアクセラレーターランタイムは含めません。
- Python の配布名は `aikernel-net`、インポート名は `aikernel_net` です。wheel の同梱 DLL と CTG-ROM サンプルは NuGet と別に検証します。
- 安定版の生成・公開前は [Core リリースチェックリスト](docs/operations/release-checklist-jp.md) を参照してください。

## 0.1.2

**2026年6月16日 - 正典 Runtime line。**

AIKernel.Core 0.1.2 は、決定論的 runtime package を AIKernel.NET 0.1.2 に揃えます。

- `AIKernel.Common`、`AIKernel.Core`、`AIKernel.Hosting`、`AIKernel.Kernel` を同期された Core package family として公開します。
- CTG runtime documentation と Python wrapper guidance を 0.1.2 public release flow に揃えます。
- Provider 実装は Core の外に置き、provider-owned capability は AIKernel.Providers に委譲します。
- deterministic CTG evaluator behavior を維持しつつ、Control と Tools が必要とする runtime surface を公開します。
