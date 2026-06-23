# AIKernel.Core リリースチェックリスト

[English](release-checklist.md)

この checklist は、AIKernel.Core packages を v0.1.3 正典シリーズ向けに準備するための
ものです。maintainer が publication step を明示するまで、stable `0.1.3` package は
作成しません。

共有の release order、versioning、Python wrapper rule、PyPI Trusted Publishing
requirements は
[AIKernel GPU rev3 Migration v0.1.3](https://github.com/AIKernel-NET/AIKernel.NET/blob/main/docs/migration/v0.1.3-gpu-rev3-migration.md)
で定義します。

## Package Scope

AIKernel.Core は以下の managed runtime packages を公開します。

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.TestKit`

Python distribution は `aikernel-net`、import name は `aikernel_net` です。薄い
managed assembly loading helper、generated managed API catalog、development /
education 向けの CTG-ROM sample asset を公開します。

AIKernel.Core は CUDA、LibTorch、Native ABI、GPU runtime、Capability 固有 binary を
公開しません。GPU support は外部 Capability repository から提供します。

## Development Versioning

local integration 中は development package version を使います。

- NuGet: `0.1.3-dev{buildNumber}`
- Python: `0.1.3.dev{buildNumber}`

stable `0.1.3` artifact は、AIKernel.NET contract packages が確定した後、依存関係順に
作成します。

## Preflight

repository root で実行します。

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore `
  -p:UseLocalPackageVersion=true `
  -p:LocalPackageBuildNumber=<buildNumber>
```

shared workspace root で実行します。

```powershell
py AIKernel.NET\tools\check_bilingual_xml_docs.py AIKernel.Core\src
```

`python/` で実行します。

```powershell
py -m compileall src tests
py -m pytest
py -m build --wheel
py -m twine check dist\aikernel_net-0.1.3*.whl
```

## NuGet Package Checks

公開または local consumption の前に、生成済み `.nupkg` を確認します。

- package id が Core package family と一致する
- development package は `0.1.3-dev{buildNumber}` を使う
- stable package は、明示的に要求された場合のみ `0.1.3` を使う
- license が `Apache-2.0`
- repository metadata が AIKernel.Core repository を指す
- README と icon assets が必要な package に含まれる
- CUDA、LibTorch、Native ABI、外部 Capability binary が含まれない
- `AIKernel.Vfs` package dependency が存在しない
- AIKernel.NET contract packages 参照が matching v0.1.3 contract package line を解決する

## Python Wheel Checks

Python wheel について確認します。

- tag が `py3-none-any`
- `aikernel_net/py.typed` を含む
- `dist-info/licenses/LICENSE` を含む
- `aikernel_net/managed/` 配下に managed assemblies を含む
- generated `api_catalog.py` を含む
- `aikernel_net/samples/ctg_rom/` 配下に CTG-ROM sample assets を含む
- CUDA、LibTorch、Native ABI、GPU runtime files を含まない
- Result / Option / Either / Try helpers と DSL pipeline helpers を公開する

## Trusted Publishing Checks

Python release tag を push する前に確認します。

- `.github/workflows/publish-pypi.yml` が intended stable tag で実行される
- publish job が `pypi` GitHub Environment を使う
- publish job が `id-token: write` を持つ
- workflow が `pypa/gh-action-pypi-publish@release/v1` を使う
- workflow に PyPI API token、`TWINE_USERNAME`、`TWINE_PASSWORD` が含まれない
- build と publish step が分離されている

`aikernel-net` project の PyPI Trusted Publisher は、この repository が発行する
GitHub OIDC claims と一致している必要があります。

| Field | Value |
| --- | --- |
| PyPI project | `aikernel-net` |
| Owner | `AIKernel-NET` |
| Repository | `AIKernel.Core` |
| Workflow | `publish-pypi.yml` |
| Environment | `pypi` |

PyPI が `invalid-publisher` を返す場合、workflow を token credential 方式へ戻しては
いけません。PyPI project 側の Trusted Publisher entry を上記の値に合わせて修正し、
失敗した publish job を rerun します。

## Publish Order

1. AIKernel.NET contract packages を先に公開する。
2. AIKernel.Core package family を公開する。
3. `aikernel-net` を公開する。
4. 共有の依存関係順に、Control、Providers、CUDA、Wasm、Tools へ進む。

## Post-Publish Smoke Check

clean な consumer project で確認します。

```powershell
dotnet new console
dotnet add package AIKernel.Core --version 0.1.3
dotnet add package AIKernel.Kernel --version 0.1.3
dotnet build
```

stable Python:

```powershell
py -m venv .venv
.\.venv\Scripts\python -m pip install aikernel-net==0.1.3
.\.venv\Scripts\python -c "import aikernel_net; print(aikernel_net.__version__)"
```
