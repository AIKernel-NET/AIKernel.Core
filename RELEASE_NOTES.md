# AIKernel.Core Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.3 (Unreleased)

- Align Common, Core, Kernel, Hosting, and TestKit with the 0.1.3 AIKernel.NET contracts. Core remains the managed runtime; CUDA, LibTorch, and other accelerator runtimes are outside its package boundary.
- The Python distribution target is `aikernel-net` (`import aikernel_net`). Validate the wheel's managed assemblies and CTG-ROM sample separately from the NuGet packages.
- Follow the [Core release checklist](docs/operations/release-checklist.md) before building and publishing stable artifacts.

## 0.1.2

**June 16th, 2026 - Canonical runtime line.**

AIKernel.Core 0.1.2 aligns the deterministic runtime packages with AIKernel.NET 0.1.2.

- Publish `AIKernel.Common`, `AIKernel.Core`, `AIKernel.Hosting`, and `AIKernel.Kernel` as the synchronized Core package family.
- Align CTG runtime documentation and Python wrapper guidance with the 0.1.2 public release flow.
- Keep Provider implementations outside Core and route provider-owned capabilities through AIKernel.Providers.
- Preserve deterministic CTG evaluator behavior while exposing the runtime surface needed by Control and Tools.
