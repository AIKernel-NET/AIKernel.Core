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

現在の参照実装は次です。

```text
src/AIKernel.Cuda.Libtorch.2.12-cuda13.0
```

対象:

- Windows / MSVC
- LibTorch 2.12.0
- CUDA 13.0
- C ABI functions: `load_model`, `unload_model`, `forward`

この runtime が必要な GPU ホストでのみインストールしてください。

```bash
dotnet add package AIKernel.Cuda.Libtorch.2.12-cuda13.0 --version 0.0.5
```

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

`AIKernel.Python` の既定インストールは CUDA-free です。

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

任意の CUDA managed Capability assembly を同梱する場合:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python \
  --config-settings=cmake.define.AIKERNEL_PYTHON_INCLUDE_CUDA_CAPABILITY=ON
```

Windows native bridge も install 時に build する場合:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python \
  --config-settings=cmake.define.AIKERNEL_PYTHON_BUILD_NATIVE=ON
```

Python は外側 API と monad helper を公開します。OS memory mapping や Kernel 内部は
Python 側に再実装しません。

## 他 CUDA Version / Linux CUDA

現在の CUDA module は Windows/MSVC + CUDA 13.0 の参照 module です。
別の CUDA version、LibTorch version、model runtime、Linux CUDA が必要な場合は、
既存 module をテンプレートとして新しい Capability module を作成してください。

推奨命名:

```text
AIKernel.Cuda.<Runtime>.<runtime-version>-cuda<cuda-version>
AIKernel.Cuda.Libtorch.2.12-cuda13.0
AIKernel.Cuda.Libtorch.2.13-cuda13.1
AIKernel.Cuda.Libtorch.Linux.2.12-cuda13.0
```

新 module を作る場合:

1. C ABI を維持するか、ABI version を明示します。
2. 一意な `CapabilityId` を持つ `CapabilityModuleDescriptor` を追加します。
3. runtime files は Core と既定 package payload の外側に置きます。
4. `IMemoryMapper` を利用し、Capability module から Kernel を参照しません。
5. platform 固有の native build file は module 内に置きます。
6. runtime 不在、invalid handle、invalid model path、mapper failure の fail-closed test を追加します。
7. 必要な environment variable と runtime search path を文書化します。

Linux CUDA 対応は、native Linux server 環境を準備した後、新しい native module として
実装してください。Windows 参照 module に Linux include / lib path を混ぜないでください。

## Checklist

- CUDA は opt-in のままにする。
- Core / Python の既定 install は CUDA なしで動く。
- Native ABI は C 互換型だけを使う。
- 動的 buffer は caller が所有する。
- Capability module は Kernel を参照しない。
- Memory mapping failure は `Result` failure にする。
- Replay metadata に hash と native status を含める。
- Fail-closed 境界の test を追加する。
