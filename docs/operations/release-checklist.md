# AIKernel.Core Release Checklist

This checklist is for publishing the AIKernel.Core 0.1.0 package family and the
CPU-only Python binding on the 2026-06-09 prototype validation release line.

The 0.0.x design-implementation phase is complete. The 0.1.0 release validates
the runtime with prototype applications, external Capability modules, and
control-plane execution scaffolding.

## Package Scope

AIKernel.Core publishes the managed runtime packages:

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.Providers.MicrosoftAI`
- `AIKernel.TestKit`

The stable Python binding publishes the `aikernel-net` package to PyPI as a
universal `py3-none-any` CPU-only wheel. Import it as `aikernel_net`. The PyPI
package named `aikernel` is a different project.

Development builds should use GitHub Packages with a separate distribution name
such as `aikernel-net-dev` and versions like `0.1.0-dev.1`. Development
packages may contain breaking changes and are intended for CI/CD validation.

AIKernel.Core does not publish CUDA, LibTorch, native ABI, GPU runtime, or
Capability-specific binaries. GPU support is supplied by external Capability
repositories.

## Preflight

Run from the repository root:

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
```

Run from `python/`:

```powershell
py -m compileall src tests
py -m pytest
py -m build --wheel
py -m twine check dist/aikernel_net-0.1.0-py3-none-any.whl
```

## NuGet Package Checks

Before publishing, inspect the generated `.nupkg` files and verify:

- package ids and versions are `0.1.0`
- license is `Apache-2.0`
- repository metadata points at the AIKernel.Core repository
- README and icon assets are included where expected
- no CUDA, LibTorch, native ABI, or external Capability binaries are included
- no `AIKernel.Vfs` package dependency exists
- references to AIKernel.NET contract packages use `0.1.0`

## Python Wheel Checks

Verify the Python wheel:

- is tagged `py3-none-any`
- includes `aikernel_net/py.typed`
- includes `dist-info/licenses/LICENSE`
- includes managed assemblies under `aikernel_net/managed/`
- does not include CUDA, LibTorch, native ABI, or GPU runtime files
- exposes Result / Option / Either / Try helpers and DSL pipeline helpers

## External Capability Boundary

CUDA Capability packages are not part of this release. The reference CUDA
Capability uses split distribution:

- NuGet.org receives a small metadata package with managed AIKernel dependencies
- GitHub Releases receive the full runtime `.nupkg`

Core documentation should refer GPU users to the matching external Capability
repository and should not imply that CUDA is installed by default.

## Publish Order

1. Publish AIKernel.NET contract packages first.
2. Publish AIKernel.Core package family.
3. Publish the CPU-only stable `aikernel-net` Python package to PyPI.
4. Publish external Capability metadata packages only after their managed
   dependencies are available.
5. Attach external Capability full runtime packages to their GitHub Releases.

## Post-Publish Smoke Check

In a clean consumer project:

```powershell
dotnet new console
dotnet add package AIKernel.Core --version 0.1.0
dotnet add package AIKernel.Kernel --version 0.1.0
dotnet build
```

For stable Python:

```powershell
py -m venv .venv
.\.venv\Scripts\python -m pip install aikernel-net==0.1.0
.\.venv\Scripts\python -c "import aikernel_net; print(aikernel_net.__version__)"
```

Do not use `aikernel-net-dev` for user-facing documentation or stable release
smoke checks.
