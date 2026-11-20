# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.4（公開準備中）

[推論・操作評価APIと最小コード例](https://github.com/AIKernel-NET/AIKernel.NET/blob/dev/docs/overview/inference-api-0-1-4-jp.md)。

0.1.3から更新する方は、[モデルの選び方と最初の回答の確認手順](https://github.com/AIKernel-NET/AIKernel.NET/blob/dev/docs/overview/upgrade-0-1-4-jp.md)から始めてください。

- Cosmos 3 Edge／Nano の取り込み済み重みで、文章・RGB画像・時刻付き動画フレームから応答を生成できます。CPU推論は CUDA と LibTorch が不要です。対話APIは各ターンの発言者と画像を保って次の質問を処理します。
- Monolith のモデル登録で、使用するモデルと import-manifest.json の SHA-256 を固定できます。WebSite の日英チャットでは、接続状態を確認し、同じ写真について続けて質問できます。利用者のAPIキーは不要です。
- Cosmos 3 Edge／Nano を4ワーカー、モデルホストの7 GB枠で実行し、文章・画像・動画の全6条件で参照回答と生成トークン列が一致しました。標準KV設定（128 MB）での最大RSSは3.564 GBです。6件合計のSSD書き込み量は標準設定で84 KiB、KVを8 MiBに制限した試験で18.09 GBでした。 [開発機との比較と測定条件](docs/development/evidence/cosmos3-server-7gb-20261120/README.md)
- GPT-OSS-20B は Server の実メモリ7GB枠でSSDへのページングを使用し、検証用2入力で llama.cpp と生成トークン列を照合しました。
- Gemma 4 E2Bでテキストを生成できます。CPU実行と、対応するNVIDIA GPUに重み・KV・forward処理を置く実行に対応します。[対応GGUFと数値検証](docs/gemma4-e2b-inference.md)
- Bonsai 1.7B のCTGは、操作の提案とホストが取得した観測を分け、governance ROMの規則で判断します。Serverでの再生成16例と保存応答の再生60例を合わせた76例で、期待した許可30件・拒否46件と一致しました。[運用手順と評価の内訳](docs/development/bonsai-ctg-operations-jp.md)
- Cosmosの重みがキャッシュに収まらない場合、Prefillは最大32トークンずつ重みページを共有します。ホストの非公開監査で初回読み込み、入力処理、生成にかかった時間を確認できます。[メモリ設定と監査](docs/cosmos3-inference.md)
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
