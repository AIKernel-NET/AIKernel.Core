# AIKernel.Core Documentation

This folder contains implementation-side development guides for AIKernel.Core.
Canonical papers are managed in the AIKernel.NET repository; this folder is for
runtime implementation and package usage guidance.

## Development Guides

- [CUDA Capability Development Guide](development/cuda-capability-development-guide.md)
- [CUDA Capability 開発ガイド](development/cuda-capability-development-guide-jp.md)

CUDA support is optional and lives outside this repository. Default
AIKernel.Core and AIKernel.Python installs do not require CUDA, LibTorch, or a
native bridge. GPU hosts should opt in by installing and registering an external
CUDA Capability module such as `AIKernel.Cuda13.0.Libtorch2.12.win-x64`.
