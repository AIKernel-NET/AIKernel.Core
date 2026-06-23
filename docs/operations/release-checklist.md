# AIKernel.Core Release Checklist

[日本語](release-checklist-jp.md)

This checklist is for preparing AIKernel.Core packages for the v0.1.3 canonical
series. Do not create stable `0.1.3` packages until the maintainer explicitly
opens the publication step.

Shared release order, versioning, Python wrapper rules, and PyPI Trusted
Publishing requirements are defined by
[AIKernel GPU rev3 Migration v0.1.3](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/migration/v0.1.3-gpu-rev3-migration.md).

## Package Scope

AIKernel.Core publishes the managed runtime packages:

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.TestKit`

The Python distribution is `aikernel-net` and imports as `aikernel_net`.
It exposes thin managed assembly loading helpers, the generated managed API
catalog, and sample CTG-ROM assets for development and education.

AIKernel.Core does not publish CUDA, LibTorch, native ABI, GPU runtime, or
Capability-specific binaries. GPU support is supplied by external Capability
repositories.

## Development Versioning

Use development package versions during local integration:

- NuGet: `0.1.3-dev{buildNumber}`
- Python: `0.1.3.dev{buildNumber}`

Stable `0.1.3` artifacts are created later in dependency order after the
AIKernel.NET contract packages are finalized.

## Preflight

Run from the repository root:

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore `
  -p:UseLocalPackageVersion=true `
  -p:LocalPackageBuildNumber=<buildNumber>
```

Run from the shared workspace root:

```powershell
py AIKernel.NET\tools\check_bilingual_xml_docs.py AIKernel.Core\src
```

Run from `python/`:

```powershell
py -m compileall src tests
py -m pytest
py -m build --wheel
py -m twine check dist\aikernel_net-0.1.3*.whl
```

## NuGet Package Checks

Before publishing or consuming local packages, inspect the generated `.nupkg`
files and verify:

- package ids match the Core package family
- development packages use `0.1.3-dev{buildNumber}`
- stable packages, when explicitly requested, use `0.1.3`
- license is `Apache-2.0`
- repository metadata points at the AIKernel.Core repository
- README and icon assets are included where expected
- no CUDA, LibTorch, native ABI, or external Capability binaries are included
- no `AIKernel.Vfs` package dependency exists
- references to AIKernel.NET contract packages resolve to the matching v0.1.3
  contract package line

## Python Wheel Checks

Verify the Python wheel:

- is tagged `py3-none-any`
- includes `aikernel_net/py.typed`
- includes `dist-info/licenses/LICENSE`
- includes managed assemblies under `aikernel_net/managed/`
- includes the generated `api_catalog.py`
- includes CTG-ROM sample assets under `aikernel_net/samples/ctg_rom/`
- does not include CUDA, LibTorch, native ABI, or GPU runtime files
- exposes Result / Option / Either / Try helpers and DSL pipeline helpers

## Trusted Publishing Checks

Before pushing a Python release tag, verify:

- `.github/workflows/publish-pypi.yml` runs for the intended stable tag
- the publish job uses the `pypi` GitHub Environment
- the publish job has `id-token: write`
- the workflow uses `pypa/gh-action-pypi-publish@release/v1`
- the workflow contains no PyPI API token, `TWINE_USERNAME`, or
  `TWINE_PASSWORD`
- build and publish steps remain separated

The PyPI Trusted Publisher for the `aikernel-net` project must match the
GitHub OIDC claims emitted by this repository:

| Field | Value |
| --- | --- |
| PyPI project | `aikernel-net` |
| Owner | `AIKernel-NET` |
| Repository | `AIKernel.Core` |
| Workflow | `publish-pypi.yml` |
| Environment | `pypi` |

If PyPI reports `invalid-publisher`, do not change the workflow to use token
credentials. Fix the PyPI project Trusted Publisher entry so it matches the
table above, then rerun the failed publish job.

## Publish Order

1. Publish AIKernel.NET contract packages first.
2. Publish AIKernel.Core package family.
3. Publish `aikernel-net`.
4. Continue with Control, Providers, CUDA, Wasm, and Tools in the shared
   dependency order.

## Post-Publish Smoke Check

In a clean consumer project:

```powershell
dotnet new console
dotnet add package AIKernel.Core --version 0.1.3
dotnet add package AIKernel.Kernel --version 0.1.3
dotnet build
```

For stable Python:

```powershell
py -m venv .venv
.\.venv\Scripts\python -m pip install aikernel-net==0.1.3
.\.venv\Scripts\python -c "import aikernel_net; print(aikernel_net.__version__)"
```
