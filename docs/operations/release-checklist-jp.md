# AIKernel.Core リリースチェックリスト

このチェックリストは、AIKernel.Core package family を公開するためのものです。

0.1.1.1 の CTG Core 更新では NuGet packages のみを公開します。この更新ラインでは
PyPI package を作成しません。Python / PyPI 手順は、Python release が別途明示的に
予定された場合だけ適用します。

0.1.1 release line では、2026-06-10 のプロトタイプ実証リリースとして
CPU-only の Python binding も公開しました。

0.0.x の設計実装フェーズは完了しました。0.1.1 では、プロトタイプアプリ、
外部 Capability module、Control Plane 実行基盤を用いて runtime を実証します。

## 公開対象

AIKernel.Core は以下の managed runtime packages を公開します。

- `AIKernel.Common`
- `AIKernel.Core`
- `AIKernel.Kernel`
- `AIKernel.Hosting`
- `AIKernel.TestKit`

`AIKernel.Providers.MicrosoftAI` は integration test で外部 Provider package として利用し、
その repository が更新されるまでは provider package line のまま扱います。

次の Python binding 更新は、公式 v0.1.2 正典シリーズで行う前提です。
`aikernel-net` package を PyPI に universal `py3-none-any` の CPU-only wheel として
公開します。import package は `aikernel_net` です。PyPI の `aikernel` は別プロジェクトです。

v0.1.2 正典シリーズ向けの Python 開発版は GitHub Packages に分離し、
`aikernel-net-dev` のような distribution name と `0.1.2.dev1` 形式の version を
使います。開発版は破壊的変更を許容し、CI/CD 検証向けとします。

AIKernel.Core は CUDA、LibTorch、Native ABI、GPU runtime、Capability 固有 binary を
公開しません。GPU 対応は外部 Capability repository から提供します。

## 事前確認

repository root で実行します。

```powershell
dotnet test AIKernel.Core.slnx -c Release --no-restore
dotnet pack AIKernel.Core.slnx -c Release --no-restore
```

shared workspace root で実行します。

```powershell
py AIKernel.NET\tools\check_bilingual_xml_docs.py AIKernel.Core\src
```

0.1.1.1 ではここで停止し、生成済み NuGet packages のみを公開対象にします。

`python/` で実行します。

```powershell
py -m compileall src tests
py -m pytest
py -m build --wheel
py -m twine check dist/aikernel_net-0.1.1-py3-none-any.whl
```

## NuGet package 確認

公開前に生成済み `.nupkg` を確認します。

- Core package id と version が CTG Core 更新では `0.1.1.1`
- license が `Apache-2.0`
- repository metadata が AIKernel.Core repository を指す
- README と icon assets が必要な package に含まれる
- CUDA、LibTorch、Native ABI、外部 Capability binary が含まれない
- `AIKernel.Vfs` package dependency が存在しない
- AIKernel.NET contract packages 参照が CTG Core 更新では `0.1.1.1`

## 契約 migration notes

0.1.1 の契約昇格では、安定 contract surface を AIKernel.NET 側に集約します。

- `KernelTimestamp` は `AIKernel.Dtos.Time` から提供します。Core は重複する
  timestamp DTO を所有しません。
- Control は `ControlCapabilityDescriptor` / `GpuControlDescriptor` のような
  未使用の実装側 descriptor 残骸を削除します。共有 capability manifest は
  `AIKernel.Dtos.Capabilities.CapabilityModuleDescriptor` を使用します。

## Python wheel 確認

0.1.1.1 の CTG Core 更新では、この section は skip します。

Python wheel について以下を確認します。

- tag が `py3-none-any`
- `aikernel_net/py.typed` を含む
- `dist-info/licenses/LICENSE` を含む
- `aikernel_net/managed/` 配下に managed assemblies を含む
- CUDA、LibTorch、Native ABI、GPU runtime files を含まない
- Result / Option / Either / Try helpers と DSL pipeline helpers を公開する

## 外部 Capability 境界

CUDA Capability はこのリリースには含めません。参照 CUDA Capability は
split distribution を使います。

- NuGet.org には managed AIKernel dependencies を持つ小さな metadata package を置く
- GitHub Releases には full runtime `.nupkg` を置く

Core documentation は、GPU ユーザーを対応する外部 Capability repository へ誘導し、
CUDA が既定でインストールされるような表現を避けます。

## 公開順序

1. AIKernel.NET contract packages を先に公開する。
2. AIKernel.Core package family を公開する。
3. 外部 Provider package は、その repository が更新されるまで独自の release line に維持する。
4. 0.1.1.1 CTG Core 更新では PyPI を skip し、次の公式 v0.1.2 正典シリーズに向けて Python packaging を準備する。
5. 外部 Capability metadata package は managed dependencies が利用可能になってから公開する。
6. 外部 Capability full runtime package を GitHub Release に添付する。

## 公開後 smoke check

clean な consumer project で確認します。

```powershell
dotnet new console
dotnet add package AIKernel.Core --version 0.1.1.1
dotnet add package AIKernel.Kernel --version 0.1.1.1
dotnet build
```

安定版 Python:

0.1.1.1 の CTG Core 更新では、この smoke check は skip します。

```powershell
py -m venv .venv
.\.venv\Scripts\python -m pip install aikernel-net==0.1.1
.\.venv\Scripts\python -c "import aikernel_net; print(aikernel_net.__version__)"
```

ユーザー向け documentation や安定版 smoke check では `aikernel-net-dev` を使いません。
