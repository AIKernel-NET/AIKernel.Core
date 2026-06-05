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

At runtime, the wrapper adds the native bridge directory, `AIKERNEL_LIBTORCH_PATH`
(`lib` when present), and `CUDA_PATH/bin` to the Windows DLL search path before
loading the bridge. This keeps dependent LibTorch and CUDA DLL resolution local
to the package process without copying those runtimes into the Python wrapper.

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
`managed_assemblies()` resolves bundled assemblies first, then paths from
`AIKERNEL_MANAGED_ASSEMBLY_PATH`, then matching packages from the NuGet
global-packages cache (`NUGET_PACKAGES` or `~/.nuget/packages`).

## Implementation Status

The current Python surface is a native ABI language binding:

```text
Python
  -> ctypes
  -> C++ Native ABI (libtorch_bridge)
  -> LibTorch
```

The package also bundles and discovers the managed AIKernel assemblies so hosts
can locate the .NET Core / Kernel / Capability payloads from Python packaging.
The managed Capability execution path exists on the .NET side:

```text
C# LibTorchCapabilityInvoker
  -> Core IMemoryMapper
  -> Kernel Win32MemoryMapper / PosixMemoryMapper
  -> C++ Native ABI (libtorch_bridge)
```

Python does not yet host the .NET runtime or invoke
`LibTorchCapabilityInvoker` directly. When that managed bridge is added, it
should reuse the bundled assemblies and keep Python as an outer API layer
rather than copying Kernel or OS-specific mapper internals into Python.

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
- `runtime_layout() -> RuntimeLayout`

The package includes inline type hints and a `py.typed` marker for PEP 561
compatible type checkers.

MemoryRegion / MemoryMapper internals are not exposed to Python.
`runtime_layout()` reports package file locations only; it does not expose
KernelContext or OS-specific mapper internals.

## Monad Syntax

`AIKernel.Python` includes lightweight `Result`, `Option`, `Either`, and `Try`
helpers so Python user-land pipelines can mirror the C# `AIKernel.Common`
monad style without copying Kernel or Capability internals.

Method-chain style:

```python
from aikernel import Try

result = (
    Try.run(lambda: load_history())
    .bind(lambda history: Try.run(lambda: parse_json(history)))
    .bind(lambda document: Try.run(lambda: validate(document)))
)
```

`Try(lambda: ...)` is also supported as a shorthand for `Try.run(lambda: ...)`.

Decorator-based do notation:

```python
import aikernel
from aikernel import Result, Try, do

@do(Result)
def pipeline():
    handle = yield Try.run(lambda: aikernel.load_model("model.pt"))
    output = yield aikernel.forward_result([1, 2, 3], handle=handle)
    return output
```

`Result` captures exceptions as failures. `Option` is a pure short-circuit
container and propagates exceptions normally. `Either` is also pure:
`Right(value)` flows through `bind` / `map`, while `Left(value)` short-circuits
without capturing exceptions. `do(Result)`, `do(Option)`, and `do(Either)` are
supported; only the `Result` form converts exceptions into failures.

Native wrapper result APIs attach capability lifecycle feedback to
`Result.metadata`. With the current stable C ABI, asynchronous page-in,
page-out, and defragmentation events are reported as
`not_observable_from_current_abi`; future managed Capability feedback can be
carried through the same metadata channel without changing the wrapper API.
