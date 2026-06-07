# AIKernel.Python

[English](README.md)

AIKernel.Core の functional primitives と managed assembly discovery のための
Python binding です。

既定 package は CPU-only で、Windows / Linux 向け universal `py3-none-any` wheel
として公開します。

```bash
pip install aikernel-net
```

PyPI の `aikernel` は別プロジェクトです。AIKernel.NET は衝突を避けるため、
distribution name として `aikernel-net` を使います。import name は
`aikernel_net` です。

`0.1.0` stable baseline の詳細は [RELEASE_NOTES-ja.md](RELEASE_NOTES-ja.md) を
参照してください。

## Release Channels

Stable user release は PyPI に公開します。

- distribution: `aikernel-net`
- versions: `0.1.0` 以降の stable release
- policy: stable release のみ

Development release は CI/CD と developer validation のために分離します。

- distribution: `aikernel-net-dev`
- versions: `0.1.0-dev.1` 形式
- policy: breaking changes allowed

ユーザー向け documentation は PyPI stable package を基準にします。development
package は CI/CD または integration testing のみに使います。

## Scope

`AIKernel.Python` は既定で CUDA-free です。native ABI bridge、CUDA Capability DLL、
LibTorch binary、GPU runtime は含みません。GPU integration は
`AIKernel.Cuda13.0.Libtorch2.12.win-x64` のような外部 Capability repository に属します。

想定 install path:

- Windows/Linux C# application: `AIKernel.*` NuGet packages
- Windows/Linux Python application: `aikernel-net` pip package
- GPU host: matching external Capability package を明示追加

提供するもの:

- `Result`, `Option`, `Either`, `Try`
- sync / async monad composition helpers
- `do(...)` と `async_do(...)` notation
- managed AIKernel assembly discovery helpers
- package / runtime layout discovery

提供しないもの:

- `load_model`
- `forward`
- `unload_model`
- `libtorch_bridge`
- Win32 / POSIX memory mapping implementation
- Kernel internal
- CUDA / LibTorch runtime code

## API

```python
import aikernel_net

assemblies = aikernel_net.managed_assemblies()
layout = aikernel_net.runtime_layout()
```

Wrapper surface は意図的に小さく保ちます。

- `managed_assemblies() -> ManagedAssemblySet`
- `require_managed_assemblies() -> ManagedAssemblySet`
- `runtime_layout() -> RuntimeLayout`

## Monad Syntax

Python user-land pipeline は、C# `AIKernel.Common` の monad style に近い形で
`Result`, `Option`, `Either`, `Try` を利用できます。

```python
from aikernel_net import Try

result = (
    Try.run(lambda: load_history())
    .bind(lambda history: Try.run(lambda: parse_json(history)))
    .bind(lambda document: Try.run(lambda: validate(document)))
)
```

LINQ-style alias も利用できます。

- `select(...)` mirrors `Select`
- `select_many(...)` mirrors `SelectMany`
- `tap(...)` observes success / Some / Right values
- `where(...)` filters in the matching monad style
- `match(...)` folds into a plain value

Decorator-based do notation:

```python
from aikernel_net import Result, Try, do

@do(Result)
def pipeline():
    context = yield Try.run(lambda: load_context())
    route = yield Try.run(lambda: route_context(context))
    return route
```

`Result` は例外を failure として捕捉します。`Option` と `Either` は pure
short-circuit container であり、例外は通常どおり伝播します。

## GPU Capability Packages

CUDA、ROCm、DirectML、Vulkan、native model runtime は外部 Capability package として
install してください。それらの package は model loading / inference 用の Python API を
提供できますが、shared monad と managed assembly discovery behavior はこの package と
整合させます。
