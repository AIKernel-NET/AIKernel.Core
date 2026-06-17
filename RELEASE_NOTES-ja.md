# AIKernel.Core リリースノート

[English](RELEASE_NOTES.md)

## 0.1.2

**2026年6月16日 - 正典 Runtime line。**

AIKernel.Core 0.1.2 は、決定論的 runtime package を AIKernel.NET 0.1.2 に揃えます。

- `AIKernel.Common`、`AIKernel.Core`、`AIKernel.Hosting`、`AIKernel.Kernel` を同期された Core package family として公開します。
- CTG runtime documentation と Python wrapper guidance を 0.1.2 public release flow に揃えます。
- Provider 実装は Core の外に置き、provider-owned capability は AIKernel.Providers に委譲します。
- deterministic CTG evaluator behavior を維持しつつ、Control と Tools が必要とする runtime surface を公開します。