# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.3（公開準備中）

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
