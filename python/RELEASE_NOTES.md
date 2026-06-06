# AIKernel.Python Release Notes

## 0.0.5.1 — Python package rename

This release renames the Python distribution package to avoid a PyPI name
collision.

- Changed the PyPI distribution name from `aikernel` to `aikernel-net`.
- Kept API behavior and package scope intact.
- Changed the import package to `aikernel_net`.
- Added `__version__ = "0.0.5.1"`.
- Updated documentation and release checklists to use:

```bash
pip install aikernel-net==0.0.5.1
```

```python
import aikernel_net
```

The PyPI package named `aikernel` is a different project. AIKernel.NET uses
`aikernel-net` to protect the project identity and avoid user confusion.

The package remains CPU-only by default and does not include CUDA, LibTorch, or
native ABI payloads. GPU and native execution remain external Capability
concerns.
