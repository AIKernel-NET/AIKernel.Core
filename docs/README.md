# AIKernel.Core Documentation

[日本語](README-jp.md)

This folder contains implementation-side development guides for AIKernel.Core.
Canonical papers are managed in the AIKernel.NET repository; this folder is for
runtime implementation and package usage guidance.

These docs describe Core as the kernel runtime layer of the AIOS SDK. Core is
the stable base that AIOS distributions combine with providers, control,
WASM, GPU backends, tools, and examples.

AIKernel.Monolith is the official AIOS distribution now in development. It is
planned as the reference system that integrates the SDK layers after the 0.1.x
line stabilizes.

## Cross-Repository Alignment

Shared repository boundaries, 0.1.1.1 local NuGet versioning, the
NuGet-only / no-PyPI rule for this validation line, and the v0.1.2
NuGet + PyPI release assumption are defined by
[AIKernel Repository Alignment v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/repository-alignment-v0.1.1.1.md).
When a change crosses repositories, start with the
[Cross-Repository Developer Guide v0.1.1.1](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/development/cross-repository-developer-guide-v0.1.1.1.md).

Core owns deterministic kernel runtime and CTG evaluator implementation. It
must not absorb provider endpoint behavior, browser/WASM execution, or
scenario-specific mapping.

## Development Guides

- [User Guide](user-guide/index.md)
- [CTG Governance Integration Guide](development/ctg-governance-integration.md)
- [CTG Governance Integration Guide 日本語](development/ctg-governance-integration-jp.md)
- [Concept Elevation Notes / 概念昇格ノート](development/concept-elevation.md)
- [CUDA Capability Development Guide](development/cuda-capability-development-guide.md)
- [CUDA Capability 開発ガイド](development/cuda-capability-development-guide-jp.md)
- [AIKernel.Core Release Checklist](operations/release-checklist.md)
- [AIKernel.Core リリースチェックリスト](operations/release-checklist-jp.md)
- [AIKernel.Python README](../python/README.md)

## Which Page Should I Read?

- Read the User Guide when installing Core packages or confirming the standard
  provider boot surface.
- Read the CUDA Capability guide only when you are adding an explicit external
  GPU/native package; default Core and Python installs are CPU-only.
- Read the Release Checklist when preparing packages for publication.
- Read the Python README when consuming Core from Python through
  `aikernel-net`.

## First Validation

Core users should first validate the CPU/default package family before adding
external providers or native capability modules:

```powershell
dotnet build AIKernel.Core.slnx -c Release
dotnet test AIKernel.Core.slnx -c Release --no-build
```

## Package Boundaries

AIKernel.Core publishes the CPU/default package family:

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.TestKit`

`AIKernel.Providers.MicrosoftAI` is consumed as an external provider package and
remains on the provider package line until the provider repository is updated.

CUDA support is optional and lives outside this repository. Default
AIKernel.Core and AIKernel.Python installs do not require CUDA, LibTorch, or a
native bridge. GPU hosts should opt in by installing and registering an external
CUDA Capability module such as `AIKernel.Cuda13.0.Libtorch2.12.win-x64`.
CUDA Capability repositories may publish a small NuGet.org metadata package and
place the full runtime `.nupkg` on GitHub Releases to avoid NuGet.org package
size limits.

The supported distribution paths are:

- Windows/Linux C# applications install the `AIKernel.*` NuGet packages.
- Python bindings stay on the previously published line during 0.1.1.1
  validation and are expected to refresh with the next official v0.1.2
  canonical series.
- GPU/native execution is added only through explicit Capability packages.

For the 0.1.1.1 CTG Core update, publish NuGet packages only. Do not create a
PyPI package for this validation line. The next official v0.1.2 canonical
series is expected to publish synchronized NuGet and PyPI package families.

`AIKernel.Vfs` is a Core implementation namespace, not a separate NuGet package.
VFS contracts come from the AIKernel.NET contract packages and in-process VFS
providers live inside `AIKernel.Core`.

## Standard Providers

AIKernel.Core includes OS-level standard providers that are available before
external providers are loaded:

- `MinimalRuntimeProvider`: deterministic `runtime.ping` boot capability.
- `LocalExecutionProvider`: inline DSL pipeline execution using the Core DSL runtime.
- `VfsProvider`: read-only VFS capability for read/list/exists/metadata operations.
- `SkillProvider`: OpenAI-compatible `SKILL.md` loading and capability registration.
- `SystemInfoProvider`: safe system introspection for providers, capabilities,
  VFS state, and runtime versions.

These providers do not depend on AIKernel.Tools, external providers, native ABI
bridges, HTTP, or model inference.

Core also exposes `IDynamicProviderRegistry` for CLI and host scenarios that
load external provider manifests. The dynamic surface registers provider
metadata, capability descriptors, optional provider assemblies, and CLI-facing
manifest settings without changing the stable `IProviderRegistry` contract
package. Standard provider invokers are available from the same dynamic
registry, and `SkillProvider` is represented as a provider-level invoker because
its capability set is discovered from `SKILL.md` at runtime.

## Release Verification

Before publishing the Core package family, run:

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
```

For 0.1.1.1, stop after NuGet package verification. Python/PyPI packaging is
out of scope until the next official v0.1.2 canonical release line.
