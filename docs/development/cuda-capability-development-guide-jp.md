# CUDA Capability 開発ガイド

このガイドは、CUDA を直接利用したい LLM / SLM 開発者向けの
AIKernel 外部 Capability module 開発ガイドです。

AIKernel.Core において CUDA は任意機能です。既定の .NET / Python
インストール経路では CUDA、LibTorch、native bridge は要求しません。
GPU 対応は、信頼済みホストが明示的にインストールして登録する外部
Capability module として提供します。

## レイヤ構造

```text
User host / Tools / Provider package
  -> ICapabilityModuleRegistry / ICapabilityModuleInvoker
  -> CUDA Capability module
  -> AIKernel.Core.Memory abstractions
  -> AIKernel.Kernel OS memory mapper
  -> Native C ABI bridge
  -> CUDA / LibTorch / model runtime
```

ルール:

- Core は OS 非依存の抽象だけを持ちます。
- Kernel は OS 固有の memory mapping 実装を持ちます。
- CUDA Capability module は Core 抽象だけを利用し、Kernel を参照しません。
- Native ABI は安定した C ABI 境界とし、CUDA / LibTorch / C++ 型を公開しません。
- native library、model file、mapper の失敗は必ず fail-closed に閉じます。

## 仮想メモリレイヤー

Core は memory mapping 契約を提供します。

- `IMemoryMapper.Open(path, accessMode) -> Result<IMemoryRegion>`
- `IMemoryRegion.Pointer`
- `IMemoryRegion.Length`
- `IMemoryRegion.Info`
- `IMemoryRegion.Unmap() -> Result<bool>`

Kernel は既定の OS 実装を提供します。

- `Win32MemoryMapper` / `Win32MemoryRegion`
- `PosixMemoryMapper` / `PosixMemoryRegion`

信頼済みホストは `AddAIKernelKernel()` により既定 mapper を登録します。
CUDA Capability package は DI 経由で `IMemoryMapper` を受け取り、model payload
の検証と mapping に使えます。現在の LibTorch 参照 module は native ABI を変更せず、
mapper を fail-closed な検証レイヤーとして利用します。

Capability module が Win32 / POSIX API を直接呼び出してはいけません。OS 固有の
mapping 実装は Kernel か、ホスト所有の mapper 実装に配置してください。

## 参照 Module

CUDA 13.0 の参照実装は、外部の `AIKernel.Cuda13.0` repository に置き、
Windows の 1 つの runtime 組み合わせだけを対象にします。

```text
AIKernel.Cuda13.0/
  src/
    AIKernel.Cuda13.0.Libtorch2.12.win-x64/
  native/
  tests/
```

対象:

- Windows / MSVC / `win-x64`
- LibTorch 2.12.0
- CUDA 13.0
- C ABI functions: `load_model`, `unload_model`, `forward`

この runtime が必要な GPU ホストでのみインストールしてください。
参照 CUDA package は split distribution を採用します。NuGet.org には managed dependency を
持つ小さな metadata package を置き、LibTorch、CUDA、cuDNN、native bridge を含む
full runtime `.nupkg` は GitHub Release asset として配布します。

```bash
dotnet nuget add source <folder-containing-full-cuda-nupkg> --name AIKernel-CUDA
dotnet add package AIKernel.Cuda13.0.Libtorch2.12.win-x64 --version 0.1.1
```

GitHub Release page URL は NuGet source ではありません。full `.nupkg` を先に取得し、
そのファイルがあるフォルダを local source として追加してください。

信頼済みホストで descriptor と invoker を登録します。

```csharp
services.AddAIKernelKernel();

services.AddSingleton<IMemoryMapper, Win32MemoryMapper>();
services.AddSingleton<ICapabilityModuleInvoker, LibTorchCapabilityInvoker>();

var descriptor = LibTorchCapabilityDescriptor.Create();
await registry.RegisterAsync(descriptor, cancellationToken);
```

Core の既定 invoker は fail-closed です。metadata 登録はできますが、暗黙に実行権限を
与えません。信頼済みホストでのみ差し替えてください。

## Native ABI 境界

公開 C ABI は安定させます。

```c
int32_t load_model(const char* path);
int32_t unload_model(int32_t handle);
int32_t forward(
    int32_t handle,
    const int32_t* input_ids,
    int32_t length,
    ForwardResultNative* out_result);
```

実装指針:

- session は整数 handle で表現します。
- session の実体は native C++ 内部に閉じます。
- model loading、tensor、CUDA device selection は ABI の背後に隠します。
- 出力 struct / buffer は caller-allocates パターンにします。
- status code を返し、managed 側で fail-closed result に変換します。

## モナド Pipeline 利用

ホスト側の CUDA orchestration は `Result<T>` /
`ResultStep<TState,TValue>` pipeline として表現します。これにより load、route、
forward、unload、fallback の経路が観測可能かつ決定論的になります。

```csharp
var pipeline =
    from mapped in memoryMapper.Open(modelPath, MemoryAccessMode.Read)
    from loaded in InvokeLoadModel(mapped.Info.Path)
    from output in InvokeForward(loaded.ModelHandle, inputIds)
    select output;
```

Replay 可能なユーザランド制御フローでは `ResultStep` を使います。

```csharp
var run =
    from route in ResultStep<string, KernelProviderRoutingDecision>
        .Success("cuda-route", cudaDecision)
        .Where(static decision => decision.ProviderId == "libtorch.cuda")
    from forward in InvokeCudaForwardStep(route)
    select forward;
```

指針:

- 失敗し得る操作は `Bind` / `SelectMany` で合成します。
- 純粋な投影だけを `Map` / `Select` にします。
- 決定論的な reject 条件は `Where` にします。
- model path hash、native status code、device metadata、replay log hash を metadata に残します。
- Capability 境界をまたいで例外を投げず、`Result` failure に変換します。

## Python 利用

`AIKernel.Python` は AIKernel.Core の一部であり、既定インストールは CUDA-free です。

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

GPU 固有の Python / native binding は、対応する外部 CUDA Capability repository が
Python package を提供する場合にそこからインストールしてください。Capability が
full NuGet runtime package のみを提供する場合は、その repository の GitHub Release
install 手順に従います。

```bash
pip install git+https://github.com/AIKernel-NET/<matching-cuda-python-capability>.git
```

Python は外側 API と monad helper を公開します。OS memory mapping や Kernel 内部は
Python 側に再実装しません。

## 他 CUDA Version / Linux CUDA

CUDA module は Core の外側で管理します。別の CUDA version、LibTorch version、
OS/RID、model runtime、Linux CUDA が必要な場合は、CUDA Capability repository を
fork して新しい Capability module を作成してください。Windows `win-x64` package に
複数の native target を混在させません。

推奨命名:

```text
AIKernel.Cuda13.0.Libtorch2.12.win-x64
AIKernel.Cuda13.0.Libtorch2.12.win-arm64
AIKernel.Cuda13.0.Libtorch2.12.linux-x64
AIKernel.Cuda12.4.Libtorch2.3.win-x64
AIKernel.ROCm6.Libtorch2.12.linux-x64
AIKernel.DirectML.win-x64
AIKernel.Vulkan.linux-x64
```

新 module を作る場合:

1. C ABI を維持するか、ABI version を明示します。
2. 一意な `CapabilityId` を持つ `CapabilityModuleDescriptor` を追加します。
3. runtime files は Core と既定 package payload の外側に置きます。
4. AIKernel.Core の `IMemoryMapper` を利用し、Capability module から Kernel を参照しません。
5. platform 固有の native build file は module 内に置きます。
6. runtime 不在、invalid handle、invalid model path、mapper failure の fail-closed test を追加します。
7. 必要な environment variable と runtime search path を文書化します。

Linux CUDA 対応は、native Linux server 環境を準備した後、外部 CUDA repository
または fork 側で実装してください。AIKernel.Core に Linux include / lib path を
追加しないでください。

## Checklist

- CUDA は opt-in のままにする。
- Core / Python の既定 install は CUDA なしで動く。
- Native ABI は C 互換型だけを使う。
- 動的 buffer は caller が所有する。
- Capability module は Kernel を参照しない。
- Memory mapping failure は `Result` failure にする。
- Replay metadata に hash と native status を含める。
- Fail-closed 境界の test を追加する。
