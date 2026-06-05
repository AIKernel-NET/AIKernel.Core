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

The native bridge currently targets Windows/MSVC only. Linux and macOS native
builds will be added after the native Linux server environment is prepared.

Set `AIKERNEL_LIBTORCH_PATH` to a LibTorch 2.12.0 + CUDA 13.0 distribution before
installing when the repository-local `ref/` folder is not present.

On Windows, the repository development environment can also read `ref/env.txt`
for `CUDA_PATH`.

By default, the package build also runs `dotnet publish` and bundles the managed
AIKernel assemblies under `aikernel/managed`:

- `AIKernel.Common.dll`
- `AIKernel.Core.dll`
- `AIKernel.Kernel.dll`
- `AIKernel.Cuda.Libtorch.Cuda13.dll`
- `AIKernel.Abstractions.dll`
- `AIKernel.Dtos.dll`
- `AIKernel.Enums.dll`

The publish output also carries required transitive runtime DLLs such as
`Microsoft.Extensions.*` and `YamlDotNet`.

Python exposes only discovery helpers for these assemblies. It does not
reimplement Kernel internals, `Win32MemoryMapper`, `PosixMemoryMapper`,
`KernelContext`, or OS memory APIs.

For wrapper-only development or CI tests without LibTorch, disable the native
and managed builds explicitly:

```bash
pip install -e . \
  --config-settings=cmake.define.AIKERNEL_PYTHON_BUILD_NATIVE=OFF \
  --config-settings=cmake.define.AIKERNEL_PYTHON_INCLUDE_MANAGED=OFF
pytest
```

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
- `load_model_result(path) -> Result[int]`
- `forward_result(input_ids, handle=None) -> Result[ForwardResult]`
- `unload_model_result(handle) -> Result[None]`
- `managed_assemblies() -> ManagedAssemblySet`
- `require_managed_assemblies() -> ManagedAssemblySet`

MemoryRegion / MemoryMapper internals are not exposed to Python.

## Monad Syntax

`AIKernel.Python` includes lightweight `Result`, `Option`, and `Try` helpers so
Python user-land pipelines can mirror the C# `AIKernel.Common` monad style
without copying Kernel or Capability internals.

Method-chain style:

```python
from aikernel import Try

result = (
    Try(lambda: load_history())
    .bind(lambda history: Try(lambda: parse_json(history)))
    .bind(lambda document: Try(lambda: validate(document)))
)
```

Decorator-based do notation:

```python
import aikernel
from aikernel import Result, Try, do

@do(Result)
def pipeline():
    handle = yield Try(lambda: aikernel.load_model("model.pt"))
    output = yield aikernel.forward_result([1, 2, 3], handle=handle)
    return output
```

`Result` captures exceptions as failures. `Option` is a pure short-circuit
container and propagates exceptions normally.
