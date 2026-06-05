# AIKernel.Cuda.Libtorch.2.12-cuda13.0

Reference AIKernel external Capability module for LibTorch 2.12.0 with CUDA 13.0.

This package keeps AIKernel.Core clean. It depends only on the standard AIKernel.NET
external Capability module contracts and calls a small native C ABI bridge by
P/Invoke. LibTorch and CUDA types never cross the managed boundary.

## Operations

- `load_model`
  - Arguments: `path`
  - Returns metadata containing `model_handle`.
- `forward`
  - Arguments: `model_handle`, `input_ids`
  - `input_ids` is a comma-separated list of integer token ids.
  - Returns metadata containing `output_tokens` and `logits_count`.

## Runtime Layout

LibTorch is not bundled in this NuGet package. Download LibTorch 2.12.0 + CUDA
13.0 manually and place it in one of these locations:

- Windows: `Runtime/win-x64/libtorch/`
- Linux: `Runtime/linux-x64/libtorch/`
- Environment override: `AIKERNEL_LIBTORCH_PATH`
- System loader path: `PATH` on Windows, `LD_LIBRARY_PATH` on Linux

For this workspace, the CMake defaults also look for:

- `ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch`
- `ref/libtorch-shared-with-deps-2.12.0+cu130/libtorch`

## Build Native Bridge

Install Visual Studio C++ tools or an equivalent C++17 compiler, CMake, and
CUDA Toolkit 13.0 before configuring the bridge. If CUDA is installed outside
the default location, set `CUDA_PATH` or `CUDAToolkit_ROOT`.

Windows:

```powershell
cmake -S Native -B Native/build/win-x64 -A x64
cmake --build Native/build/win-x64 --config Release
```

Linux:

```bash
cmake -S Native -B Native/build/linux-x64 -DCMAKE_BUILD_TYPE=Release
cmake --build Native/build/linux-x64
```

The native build emits `libtorch_bridge.dll` on Windows and `libtorch_bridge.so`
on Linux. Make the library discoverable by the application before invoking the
Capability module.

## Register Descriptor

Use `LibTorchCapabilityDescriptor.Create()` and register the returned
`CapabilityModuleDescriptor` with `ICapabilityModuleRegistry`. Replace the Core
default fail-closed `ICapabilityModuleInvoker` with `LibTorchCapabilityInvoker`
only in trusted GPU hosts.

## Boundary Rules

- Do not reference AIKernel.Core from this package.
- Do not expose LibTorch or CUDA types to managed code.
- Keep all GPU execution inside `libtorch_bridge`.
- Treat missing native libraries as host configuration failures.
