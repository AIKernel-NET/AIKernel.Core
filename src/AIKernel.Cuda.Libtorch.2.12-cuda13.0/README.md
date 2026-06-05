# AIKernel.Cuda.Libtorch.2.12-cuda13.0

Reference AIKernel external Capability module for LibTorch 2.12.0 with CUDA
13.0 on Windows/MSVC.

This package keeps the LibTorch/CUDA implementation outside AIKernel.Core. It
depends on the standard AIKernel.NET external Capability module contracts and
the OS-independent `AIKernel.Core.Memory` abstractions, then calls a small
native C ABI bridge by P/Invoke. LibTorch and CUDA types never cross the managed
boundary. The native bridge uses the Cdecl calling convention.

## Operations

- `load_model`
  - Arguments: `path`
  - Returns metadata containing `model_handle`.
- `unload_model`
  - Arguments: `model_handle`
  - Releases a loaded native session.
- `forward`
  - Arguments: `model_handle`, `input_ids`
  - `input_ids` is a comma-separated list of integer token ids.
  - `input_ids` accepts up to 4096 token ids.
  - Returns metadata containing `output_tokens` and `logits_count`.

## Runtime Layout

LibTorch is not bundled in this NuGet package. Download the Windows LibTorch
2.12.0 + CUDA 13.0 distribution manually and place it in one of these locations:

- Windows runtime folder: `Runtime/win-x64/libtorch/`
- Environment override: `AIKERNEL_LIBTORCH_PATH`
- System loader path: `PATH`

For this workspace, CMake also uses:

- `ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch`
- `ref/env.txt` for `VS2026_PATH` and `CUDA_PATH`

## Build Native Bridge

Install Visual Studio 2026 C++ tools, the VS CMake component, and CUDA Toolkit
13.0 before configuring the bridge. If CUDA is installed outside the default
location, set `CUDA_PATH` or `CUDAToolkit_ROOT`. In this workspace, CMake reads
`ref/env.txt`; if `CUDA_PATH` points to the `bin` directory, CMake uses its
parent as `CUDAToolkit_ROOT`.

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S Native -B Native/build/win-x64 -A x64
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build Native/build/win-x64 --config Release
```

The native build emits `libtorch_bridge.dll`. Make the library discoverable by
the application before invoking the Capability module.

## Register Descriptor

Use `LibTorchCapabilityDescriptor.Create()` and register the returned
`CapabilityModuleDescriptor` with `ICapabilityModuleRegistry`. Replace the Core
default fail-closed `ICapabilityModuleInvoker` with `LibTorchCapabilityInvoker`
only in trusted GPU hosts.

## Boundary Rules

- Reference AIKernel.Core only for OS-independent MemoryRegion/MemoryMapper
  abstractions.
- Do not reference AIKernel.Kernel or any OS-specific memory mapper from this
  package.
- Do not expose LibTorch or CUDA types to managed code.
- Keep all GPU execution inside `libtorch_bridge`.
- Treat missing native libraries as host configuration failures.
