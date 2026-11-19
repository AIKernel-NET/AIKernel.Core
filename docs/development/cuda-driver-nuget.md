# CUDA Driver ABI in the Kernel NuGet package

This is the 0.1.4 preparation path. The repository version is still 0.1.3;
packages built for review must stay in a private, isolated feed until the
release version and the other review branches have been integrated.

`AIKernel.Kernel` contains `runtimes/win-x64/native/aikernel_cuda_driver.dll`
and `runtimes/linux-x64/native/libaikernel_cuda_driver.so`. The managed
`NativeCompute` imports resolve through the application's NuGet runtime assets.
Python's `aikernel-cuda-driver` wheel is a separate distribution of the same C
source; it is not a prerequisite for a C# application.

The native library requires a C11 compiler and CMake 3.24 or later to build.
It embeds PTX and opens the installed NVIDIA Driver only when a CUDA device is
requested. It does not link LibTorch, PyTorch, CUDA Toolkit, or a CUDA runtime
redistribution. The native source and bundled license are Apache-2.0.
Supported package architectures are Windows x64 and Linux x64. Linux binaries
inherit the libc baseline of their build environment; the CI job logs record
the runner image. Other architectures and CUDA device execution require
separate validation and must not silently fall back to another GPU.

## Build and pack

On each target OS, run from the repository root:

```text
cmake -S native/cuda -B artifacts/native-build -DCMAKE_BUILD_TYPE=Release
cmake --build artifacts/native-build --config Release
cmake --install artifacts/native-build --config Release --prefix artifacts/native-stage
```

Stage the resulting library under `artifacts/cuda-native/win-x64/` or
`artifacts/cuda-native/linux-x64/`, respectively. Both must be present on the
machine that packs the NuGet package. `CudaNativeArtifactsRoot` can point to
an alternative staging directory with the same layout.
For a Visual Studio generator on Windows, add `-A x64` to the configure command.

```text
dotnet build AIKernel.Core.slnx -c Release
dotnet test AIKernel.Core.slnx -c Release --no-build
dotnet pack src/AIKernel.Common/AIKernel.Common.csproj -c Release --no-build -o artifacts/packages
dotnet pack src/AIKernel.Core/AIKernel.Core.csproj -c Release --no-build -o artifacts/packages
dotnet pack src/AIKernel.Hosting/AIKernel.Hosting.csproj -c Release --no-build -o artifacts/packages
dotnet pack src/AIKernel.Kernel/AIKernel.Kernel.csproj -c Release --no-build -o artifacts/packages
python native/cuda/tests/verify_nupkg.py artifacts/packages/AIKernel.Kernel.<version>.nupkg
```

Managed `dotnet build` no longer implicitly packs `AIKernel.Kernel`. Explicit
`dotnet pack` fails with `CUDA_NATIVE_ASSET_MISSING` if either platform asset is
absent. The package verification script additionally checks nonempty x64 PE/ELF
images, the managed assembly, license, duplicate entries, and Torch dependencies.
Never use an empty file or a test fixture as a release runtime asset.
Consume the newly built Common/Core/Hosting/Kernel family together. During
review, mixing the new Kernel with the publicly released Core 0.1.3 would omit
the new virtual-memory contracts; an isolated feed and cache are required.

`.github/workflows/native-cuda-nuget.yml` builds the two real libraries, packs
the Common/Core/Hosting/Kernel family, and runs a consumer on both operating
systems. It uploads artifacts and does not publish packages or create tags.
`tests/packaging/CudaDriverConsumer/NuGet.config` maps those four package IDs
exclusively to the newly built feed, avoiding a same-version download from
nuget.org. Its consumer uses PackageReference only, publishes for an explicit
RID, and executes `TransformerCompute(null)` softmax and matrix-vector product.
No manual DLL copying to the consumer output or PATH setting is used.

## Verify without opening a GPU

```text
python native/cuda/tests/host_abi_smoke.py <absolute-path-to-native-library>
aik gpu verify-native --provider cuda-driver --package AIKernel.Kernel.<version>.nupkg --library <native-library>
```

The new Tools selector checks all 21 `ak_*` exports, ABI version 1, host softmax,
and invalid arguments. A null context explicitly selects host execution;
device discovery and CUDA context creation are not performed. Package-only
checking is structural validation, not ABI execution or GPU readiness.

Providers defaults identify module `aikernel_cuda_driver`, entry `ak_create`,
ABI `1`, package `AIKernel.Kernel`, and logical reference
`aikernel-cuda://cuda13.0/modules/aikernel_cuda_driver`. The reference is an
identifier, not a download URL or invocation instruction. The generic
CudaComputeProvider remains descriptor-only and fails closed when unbound;
the native C signatures are implemented by Kernel, not by a generic JSON
dispatcher. In particular the new ABI does not implement the old
`aikernel_cuda13_dispatch` or promise all operations in the legacy catalog.

`--provider cuda13`, the old `AIKernel.Cuda13.0` repository, and NET's
`CudaLibTorchPath*` keys remain compatibility surfaces. They are not used by
the new verifier or required by the Driver package. Their removal/deprecation,
global version updates, broader document migration, and final 0.1.4 release
scope remain separate decisions. No uncommitted `AIKernel.LibTorch` work is
deleted by this change.
