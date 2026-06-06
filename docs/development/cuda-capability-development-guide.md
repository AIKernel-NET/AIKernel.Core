# CUDA Capability Development Guide

This guide is for LLM / SLM developers who want to use CUDA directly through an
AIKernel external Capability module.

CUDA is optional in AIKernel.Core. The default .NET and Python installation
paths do not require CUDA, LibTorch, or a native bridge. GPU support is supplied
by external Capability modules that are explicitly installed and registered by a
trusted host.

## Layering

```text
User host / Tools / Provider package
  -> ICapabilityModuleRegistry / ICapabilityModuleInvoker
  -> CUDA Capability module
  -> AIKernel.Core.Memory abstractions
  -> AIKernel.Kernel OS memory mapper
  -> Native C ABI bridge
  -> CUDA / LibTorch / model runtime
```

Rules:

- Core owns OS-independent abstractions only.
- Kernel owns OS-specific memory mapping implementations.
- CUDA Capability modules consume Core abstractions and do not reference Kernel.
- Native ABI signatures are stable C ABI boundaries and should not expose CUDA,
  LibTorch, or C++ types.
- Missing native libraries, model files, or mapper failures must fail closed.

## Virtual Memory Layer

Core provides the memory mapping contract:

- `IMemoryMapper.Open(path, accessMode) -> Result<IMemoryRegion>`
- `IMemoryRegion.Pointer`
- `IMemoryRegion.Length`
- `IMemoryRegion.Info`
- `IMemoryRegion.Unmap() -> Result<bool>`

Kernel provides the default OS implementations:

- `Win32MemoryMapper` / `Win32MemoryRegion`
- `PosixMemoryMapper` / `PosixMemoryRegion`

Trusted hosts register the default mapper with `AddAIKernelKernel()`. A CUDA
Capability package may accept `IMemoryMapper` through dependency injection, use
it to validate and map the model payload, and pass only the resolved path or
stable ABI data to the native bridge. The current LibTorch reference module keeps
the native ABI unchanged and uses the mapper as a fail-closed validation layer.

Capability modules should not call Win32 or POSIX APIs directly. Put OS-specific
mapping code in Kernel or in a host-owned mapper implementation.

## Reference Module

The CUDA 13.0 reference implementation belongs in a separate repository and
targets one Windows runtime combination only:

```text
AIKernel.Cuda13.0.Libtorch2.12.win-x64/
  src/
  native/
  tests/
```

It targets:

- Windows / MSVC / `win-x64`
- LibTorch 2.12.0
- CUDA 13.0
- C ABI functions: `load_model`, `unload_model`, `forward`

Install it only on GPU hosts that explicitly need this runtime:

```bash
dotnet add package AIKernel.Cuda13.0.Libtorch2.12.win-x64 --version 0.0.5
```

Then register the descriptor and invoker in the trusted host:

```csharp
services.AddAIKernelKernel();

services.AddSingleton<IMemoryMapper, Win32MemoryMapper>();
services.AddSingleton<ICapabilityModuleInvoker, LibTorchCapabilityInvoker>();

var descriptor = LibTorchCapabilityDescriptor.Create();
await registry.RegisterAsync(descriptor, cancellationToken);
```

The default Core invoker is fail-closed. It registers metadata but does not grant
execution permission. Replace it only in trusted hosts.

## Native ABI Boundary

Keep the public C ABI stable:

```c
int32_t load_model(const char* path);
int32_t unload_model(int32_t handle);
int32_t forward(
    int32_t handle,
    const int32_t* input_ids,
    int32_t length,
    ForwardResultNative* out_result);
```

Implementation guidance:

- Use integer handles for sessions.
- Keep sessions in native C++ internals.
- Keep model loading, tensors, and CUDA device selection behind the ABI.
- Use caller-allocated output structs and buffers.
- Return status codes; managed code converts them into fail-closed results.

## Monad Pipeline Usage

Host-side CUDA orchestration should be expressed as `Result<T>` /
`ResultStep<TState,TValue>` pipelines. This keeps load, route, forward, unload,
and fallback paths observable and deterministic.

```csharp
var pipeline =
    from mapped in memoryMapper.Open(modelPath, MemoryAccessMode.Read)
    from loaded in InvokeLoadModel(mapped.Info.Path)
    from output in InvokeForward(loaded.ModelHandle, inputIds)
    select output;
```

For replayable user-land control flow, use `ResultStep`:

```csharp
var run =
    from route in ResultStep<string, KernelProviderRoutingDecision>
        .Success("cuda-route", cudaDecision)
        .Where(static decision => decision.ProviderId == "libtorch.cuda")
    from forward in InvokeCudaForwardStep(route)
    select forward;
```

Guidelines:

- Use `Bind` / `SelectMany` for operations that may fail.
- Use `Map` / `Select` only for pure projections.
- Use `Where` for deterministic reject conditions.
- Record model path hashes, native status codes, device metadata, and replay log
  hashes in metadata.
- Do not throw across Capability boundaries; convert failures into `Result`.

## Python Usage

`AIKernel.Python` is part of AIKernel.Core and defaults to CUDA-free
installation:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Core.git#subdirectory=python
```

Install GPU-specific Python or native bindings from the matching external CUDA
Capability repository, for example:

```bash
pip install git+https://github.com/AIKernel-NET/AIKernel.Cuda13.0.Libtorch2.12.win-x64.git
```

Python exposes the outer API and monad helpers. It does not reimplement OS memory
mapping or Kernel internals.

## Other CUDA Versions And Linux CUDA

The CUDA module should be maintained outside Core. If you need another CUDA
version, another LibTorch version, another OS/RID, a different model runtime,
or Linux CUDA, fork the CUDA Capability repository and create a new Capability
module. Do not mix multiple native targets into the Windows `win-x64` package.

Recommended naming:

```text
AIKernel.Cuda13.0.Libtorch2.12.win-x64
AIKernel.Cuda13.0.Libtorch2.12.win-arm64
AIKernel.Cuda13.0.Libtorch2.12.linux-x64
AIKernel.Cuda12.4.Libtorch2.3.win-x64
AIKernel.ROCm6.Libtorch2.12.linux-x64
AIKernel.DirectML.win-x64
AIKernel.Vulkan.linux-x64
```

For a new module:

1. Keep the C ABI stable or version the ABI explicitly.
2. Add a new `CapabilityModuleDescriptor` with a unique `CapabilityId`.
3. Keep runtime files outside Core and outside default package payloads.
4. Consume AIKernel.Core `IMemoryMapper`; do not reference Kernel from the Capability module.
5. Add platform-specific native build files inside the module.
6. Add fail-closed tests for missing runtime, invalid handle, invalid model path,
   and mapper failures.
7. Document all required environment variables and runtime search paths.

Linux CUDA support should be implemented in the external CUDA repository or one
of its forks after the native Linux server environment is prepared. Do not add
Linux include/lib paths to AIKernel.Core.

## Checklist

- CUDA remains opt-in.
- Core and Python default installs work without CUDA.
- Native ABI uses C-compatible types only.
- Caller owns dynamic buffers.
- Capability module does not reference Kernel.
- Memory mapping failures return `Result` failures.
- Replay metadata includes hashes and native status.
- Tests cover fail-closed boundaries.
