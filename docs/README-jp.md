# AIKernel.Core Documentation

[English](README.md)

この folder は AIKernel.Core の実装側 development guide を含みます。
Canonical papers は AIKernel.NET repository で管理します。この folder は runtime
implementation と package usage guidance のためのものです。

## Development Guides

- [CUDA Capability Development Guide](development/cuda-capability-development-guide.md)
- [CUDA Capability 開発ガイド](development/cuda-capability-development-guide-jp.md)
- [AIKernel.Core Release Checklist](operations/release-checklist.md)
- [AIKernel.Core リリースチェックリスト](operations/release-checklist-jp.md)
- [AIKernel.Python README](../python/README.md)
- [AIKernel.Python README 日本語](../python/README-ja.md)

## Package Boundaries

AIKernel.Core は CPU/default package family を公開します。

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.Providers.MicrosoftAI`
- `AIKernel.TestKit`
- `aikernel-net` Python binding（`py3-none-any`, CPU-only, import `aikernel_net`）

CUDA support は optional で、この repository の外側にあります。既定の
AIKernel.Core と AIKernel.Python install は CUDA、LibTorch、native bridge を要求しません。
GPU host は `AIKernel.Cuda13.0.Libtorch2.12.win-x64` のような外部 CUDA Capability
module を明示的に install / register します。

対応する distribution path:

- Windows/Linux C# application は `AIKernel.*` NuGet packages を install します。
- Windows/Linux Python application は universal CPU-only `aikernel-net` wheel を pip から install します。
- GPU/native execution は明示的な Capability package でのみ追加します。

`AIKernel.Vfs` は Core implementation namespace であり、独立 NuGet package ではありません。
VFS contract は AIKernel.NET contract packages にあり、in-process VFS provider は
`AIKernel.Core` 内にあります。

## Release Verification

Core package family 公開前に実行します。

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
cd python
py -m pytest
py -m pip wheel . -w dist --no-deps
```

Python wheel は `aikernel_net/managed/*.dll`、`py.typed`、
`dist-info/licenses/LICENSE` を含み、`py3-none-any` tag である必要があります。
CUDA、LibTorch、native runtime asset は含めません。
