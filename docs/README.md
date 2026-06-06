# AIKernel.Core Documentation

This folder contains implementation-side development guides for AIKernel.Core.
Canonical papers are managed in the AIKernel.NET repository; this folder is for
runtime implementation and package usage guidance.

## Development Guides

- [CUDA Capability Development Guide](development/cuda-capability-development-guide.md)
- [CUDA Capability 開発ガイド](development/cuda-capability-development-guide-jp.md)
- [AIKernel.Python README](../python/README.md)

## Package Boundaries

AIKernel.Core publishes the CPU/default package family:

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.Providers.MicrosoftAI`
- `AIKernel.TestKit`
- `aikernel` Python binding

CUDA support is optional and lives outside this repository. Default
AIKernel.Core and AIKernel.Python installs do not require CUDA, LibTorch, or a
native bridge. GPU hosts should opt in by installing and registering an external
CUDA Capability module such as `AIKernel.Cuda13.0.Libtorch2.12.win-x64`.

`AIKernel.Vfs` is a Core implementation namespace, not a separate NuGet package.
VFS contracts come from the AIKernel.NET contract packages and in-process VFS
providers live inside `AIKernel.Core`.

## Release Verification

Before publishing the Core package family, run:

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
cd python
py -m pytest
py -m pip wheel . -w dist --no-deps
```

The Python wheel should include `aikernel/managed/*.dll`, `py.typed`, and
`dist-info/licenses/LICENSE`, and should not include CUDA, LibTorch, or native
runtime assets.
