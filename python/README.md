# AIKernel.Python

Python binding for the AIKernel Native ABI capability bridge.

This package is distributed by GitHub direct install only:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

PyPI publication is intentionally disabled for the current phase.

## Native Toolchain

`AIKernel.Python` uses `scikit-build-core` and CMake to build the existing
`libtorch_bridge` C ABI. The C ABI header is not modified.

Set `AIKERNEL_LIBTORCH_PATH` to a LibTorch 2.12.0 + CUDA 13.0 distribution before
installing when the repository-local `ref/` folder is not present.

On Windows, the repository development environment can also read `ref/env.txt`
for `CUDA_PATH`.

## API

```python
import aikernel

handle = aikernel.load_model("model.pt")
result = aikernel.forward([1, 2, 3])
aikernel.unload_model(handle)
```

The wrapper is intentionally thin:

- `load_model(path) -> int`
- `forward(input_ids, handle=None) -> ForwardResult`
- `unload_model(handle) -> None`

MemoryRegion / MemoryMapper internals are not exposed to Python.
