# AIKernel.Python Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.0 — Stable Python binding baseline

- Promoted the official PyPI package `aikernel-net` to `0.1.0`.
- Kept the import package name as `aikernel_net`.
- Removed the legacy in-repository `aikernel` import package to avoid
  confusion with the unrelated PyPI `aikernel` project.
- Aligned managed assembly discovery with the AIKernel.Core 0.1.0 package
  family.

Install:

```bash
pip install aikernel-net==0.1.0
```

Import:

```python
import aikernel_net
```

The PyPI package named `aikernel` is a different project. AIKernel.NET uses
`aikernel-net` to protect the project identity and avoid user confusion.

The package remains CPU-only by default and does not include CUDA, LibTorch, or
native ABI payloads. GPU and native execution remain external Capability
concerns.
