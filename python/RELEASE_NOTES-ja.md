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

## 0.0.5.1 — Python package rename and PyPI registration

この release では、AIKernel.NET project identity として PyPI に登録するため、
Python distribution package を `aikernel-net` に rename / prepare しました。

- PyPI distribution name を `aikernel` から `aikernel-net` に変更しました。
- API behavior と package scope は維持しました。
- import package を `aikernel_net` に変更しました。
- `__version__ = "0.0.5.1"` を追加しました。
- `aikernel-net` として PyPI へ正式登録できるよう準備しました。
- documentation と release checklist を更新しました。

PyPI の `aikernel` は別プロジェクトです。AIKernel.NET は project identity を守り、
user confusion を避けるために `aikernel-net` を使います。

Package は既定で CPU-only であり、CUDA、LibTorch、native ABI payload は含みません。
GPU / native execution は外部 Capability concern です。
