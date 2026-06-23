# AIKernel.Python

[日本語](README-ja.md)

Python binding for AIKernel.Core functional primitives, standard provider
contracts, and managed assembly discovery.

The default package is CPU-only and is published as a universal
`py3-none-any` wheel for Windows and Linux:

```bash
pip install aikernel-net
```

The PyPI package named `aikernel` belongs to another project. AIKernel.NET uses
the distribution name `aikernel-net` to avoid that collision. Import the module
as `aikernel_net`.

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for the current release notes.

## Release Channels

Stable user releases are published to PyPI:

- distribution: `aikernel-net`
- versions: `0.1.3`, then later stable releases
- policy: stable releases only

Development releases are reserved for CI/CD and developer validation:

- distribution: `aikernel-net`
- versions: `0.1.3.dev{buildNumber}` style prereleases
- policy: local validation and CI/CD only

User documentation defaults to the PyPI stable package. Use development
packages only for CI/CD or integration testing.

The v0.1.3 package also exposes the generated managed API catalog through
`managed_api_catalog()`, `managed_api_summary()`, `managed_type_names()`, and
`find_managed_type(full_name)`.

For source-based local validation, install directly from GitHub:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

Use a clean virtual environment or force a reinstall when validating a local
checkout, especially if an older local `aikernel-net` package was installed
previously. This is a development workflow, not the stable user install path:

```bash
pip install --force-reinstall \
  git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

GPU integrations are opt-in Capability packages and are not installed by
default. CUDA, ROCm, DirectML, and native model runtimes may publish their own
platform-specific packages, but the base `aikernel-net` package stays universal.

## Scope

`AIKernel.Python` is CUDA-free by default and does not include a native ABI
bridge, CUDA Capability DLL, LibTorch binary, or GPU runtime. GPU integrations
belong to external Capability repositories such as `AIKernel.Cuda13.0.Libtorch2.12.win-x64`.

The intended install paths are:

- C# applications on Windows or Linux: install the `AIKernel.*` NuGet packages.
- Python applications on Windows or Linux: install the `aikernel-net` pip package.
- GPU hosts: explicitly add a matching external Capability package.

The package provides:

- `Result`, `Option`, `Either`, and `Try`
- synchronous and asynchronous monad composition helpers
- `do(...)` and `async_do(...)` notation
- managed AIKernel assembly discovery helpers
- standard provider contract discovery helpers
- package/runtime layout discovery

The package does not provide:

- `load_model`
- `forward`
- `unload_model`
- `libtorch_bridge`
- Win32 / POSIX memory mapping implementations
- Kernel internals
- CUDA / LibTorch runtime code

Install GPU-specific Python or native bindings from the matching external
Capability repository or package.

## Managed Assemblies

By default, the package build runs `dotnet publish` and bundles the managed
AIKernel assemblies under `aikernel_net/managed`:

- `AIKernel.Abstractions.dll`
- `AIKernel.Common.dll`
- `AIKernel.Core.dll`
- `AIKernel.Kernel.dll`
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

For wrapper-only development or CI tests without a .NET SDK, disable managed
assembly bundling:

```bash
pip install -e . \
  --config-settings=cmake.define.AIKERNEL_PYTHON_INCLUDE_MANAGED=OFF
pytest
```

For PyPI publication, build the wheel from the repository source tree so the
managed assemblies can be bundled into `aikernel_net/managed`:

```bash
python -m build --wheel
python -m twine check dist/aikernel_net-0.1.3-py3-none-any.whl
```

## Bundled CTG-ROM Sample

`aikernel-net` includes the minimal Monolith CTG-ROM as a sample asset so Python
users can inspect the same Canon, Council, Gate, RejectPolicy, and locale layout
used by the C# packages without cloning every repository:

```python
import aikernel_net

root = aikernel_net.ctg_rom_sample_path()
files = aikernel_net.ctg_rom_sample_files()
```

The sample is copied from the canonical AIKernel.NET ROM tree and is distributed
as data only. The Python package does not execute CTG Gate logic.

## API

```python
import aikernel_net

assemblies = aikernel_net.managed_assemblies()
layout = aikernel_net.runtime_layout()
```

The wrapper surface is intentionally small:

- `managed_assemblies() -> ManagedAssemblySet`
- `require_managed_assemblies() -> ManagedAssemblySet`
- `runtime_layout() -> RuntimeLayout`
- `standard_provider_contracts() -> tuple[StandardProviderContract, ...]`
- `standard_provider(provider_id) -> StandardProviderContract`
- `standard_capability(capability_id) -> CapabilityContract`
- `standard_provider_managed_types() -> tuple[str, ...]`
- `CapabilityContract`
- `StandardProviderContract`
- `ProviderCliManifest`
- `ProviderManifest`
- `CoreCapabilityModuleContract`
- `provider_manifest_from_dict(...) -> ProviderManifest`
- `load_provider_manifest(path) -> ProviderManifest`
- `rom_storage_contract(...) -> CoreCapabilityModuleContract`
- `vfs_git_contract(...) -> CoreCapabilityModuleContract`

The package includes inline type hints and a `py.typed` marker for PEP 561
compatible type checkers.

## Standard Providers

The Python package exposes the Core standard provider contract surface without
reimplementing provider execution logic:

- `MINIMAL_RUNTIME_PROVIDER`
- `LOCAL_EXECUTION_PROVIDER`
- `VFS_PROVIDER`
- `SKILL_PROVIDER`
- `SYSTEM_INFO_PROVIDER`

```python
from aikernel_net import standard_provider, standard_capability

runtime = standard_provider("aikernel.runtime.minimal")
vfs = standard_capability("aikernel.vfs")
```

`SkillProvider` loads `SKILL.md` files and creates capabilities dynamically, so
its Python descriptor exposes provider identity and managed type information
rather than a fake static capability ID. Its managed invoker type is also
listed because the C# `SkillProvider` itself implements dynamic skill
invocation.

The package also includes descriptor helpers for provider manifest JSON files
and Core-owned ROM/VFS Git contracts. These helpers preserve the contract shape
used by Core, including manifest-declared capability order and Core contract
operation order, and do not load assemblies or execute providers in Python.

## Monad Syntax

`AIKernel.Python` includes lightweight `Result`, `Option`, `Either`, and `Try`
helpers so Python user-land pipelines can mirror the C# `AIKernel.Common`
monad style without copying Kernel or Capability internals.

Method-chain style:

```python
from aikernel_net import Try

result = (
    Try.run(lambda: load_history())
    .bind(lambda history: Try.run(lambda: parse_json(history)))
    .bind(lambda document: Try.run(lambda: validate(document)))
)
```

`Try(lambda: ...)` is also supported as a shorthand for `Try.run(lambda: ...)`.

LINQ-style aliases are available for Python method chains:

```python
from aikernel_net import Right, Success, Try

route = (
    Try.run(lambda: load_context())
    .select(lambda context: context.strip())
    .where(lambda context: len(context) > 0)
    .select_many(
        lambda context: Success("cli" if context.startswith("aik") else "llm"),
        lambda context, provider: {"context": context, "provider": provider},
    )
    .tap(lambda decision: audit(decision))
)

capability = (
    Right({"provider": "cli", "operation": "observe"})
    .where(lambda decision: decision["provider"] == "cli", lambda: "not-cli")
    .select(lambda decision: decision["operation"])
)
```

The Python aliases map to the C# Common names:

- `select(...)` mirrors LINQ `Select` and delegates to `map(...)`
- `select_many(...)` mirrors LINQ `SelectMany` and is expressed as `bind + map`
- `tap(...)` observes success / Some / Right values without changing them
- `where(...)` filters in the same short-circuit style as the matching monad
- `match(...)` folds `Result`, `Option`, or `Either` into a plain value
- `or_else(...)` is available on `Option`

For users porting C# examples directly, PascalCase aliases are also available:
`Map`, `Bind`, `Tap`, `Where`, `Select`, `SelectMany`, `Match`, and
`Option.OrElse`.

Async pipelines are available through `async_result`, `async_option`, and
`async_either`, matching the C# `Task<Result<T>>`, `Task<Option<T>>`, and
`Task<Either<L,R>>` extension style:

```python
from aikernel_net import Success, async_result

async def load_context_result():
    return Success(await load_context_async())

async def route_provider(context):
    return Success("cli" if context.startswith("aik") else "llm")

async def is_routable(context):
    return len(context) > 0

route = await (
    async_result(load_context_result())
    .Select(lambda context: context.strip())
    .Where(is_routable)
    .SelectMany(route_provider, lambda context, provider: {
        "context": context,
        "provider": provider,
    })
    .Tap(lambda decision: audit_async(decision))
)
```

Decorator-based do notation:

```python
from aikernel_net import Result, Try, do

@do(Result)
def pipeline():
    context = yield Try.run(lambda: load_context())
    route = yield Try.run(lambda: route_context(context))
    return route
```

Async do notation is available when yielded steps may be awaitables,
`AsyncResult`, `AsyncOption`, or `AsyncEither` instances:

```python
from aikernel_net import Result, Success, async_do, async_result

async def load_context_result():
    return Success(await load_context_async())

@async_do(Result)
def async_pipeline():
    context = yield load_context_result()
    route = yield async_result(route_provider(context))
    return route

result = await async_pipeline()
```

`Result` captures exceptions as failures. `Option` is a pure short-circuit
container and propagates exceptions normally. `Either` is also pure:
`Right(value)` flows through `bind` / `map`, while `Left(value)` short-circuits
without capturing exceptions. `do(Result)`, `do(Option)`, `do(Either)`,
`async_do(Result)`, `async_do(Option)`, and `async_do(Either)` are supported;
only the `Result` forms convert exceptions into failures.

## GPU Capability Packages

CUDA, ROCm, DirectML, Vulkan, and native model runtimes should be installed as
external Capability packages. Those packages may provide their own Python API
for model loading and inference while depending on this package for shared
monad and managed assembly discovery behavior.
