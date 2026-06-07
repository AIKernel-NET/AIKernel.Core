# AIKernel.Python リリースノート

[English](RELEASE_NOTES.md)

## 0.1.0 — Stable Python binding baseline

- 公式 PyPI package `aikernel-net` を `0.1.0` へ昇格しました。
- import package name は `aikernel_net` のままです。
- 無関係な PyPI `aikernel` project との混乱を避けるため、旧 in-repository
  `aikernel` import package を削除しました。
- Managed assembly discovery を AIKernel.Core 0.1.0 package family と揃えました。

Install:

```bash
pip install aikernel-net==0.1.0
```

Import:

```python
import aikernel_net
```

PyPI の `aikernel` は別プロジェクトです。AIKernel.NET は project identity を守り、
user confusion を避けるために `aikernel-net` を使います。

Package は既定で CPU-only であり、CUDA、LibTorch、native ABI payload は含みません。
GPU / native execution は外部 Capability concern です。
