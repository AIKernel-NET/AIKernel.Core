# CUDA Capability Development Guide

This guide is for LLM / SLM developers who want to use CUDA directly through an
AIKernel external Capability module.

CUDA is optional in AIKernel.Core. The default .NET and Python installation
paths do not require CUDA, LibTorch, or a native bridge. GPU support is supplied
by external Capability modules that are explicitly installed and registered by a
trusted host.

## Driver API inference

Monolith uses the shared Native C ABI in ../../native/cuda. The library
loads the operating system's NVIDIA driver on demand and JIT-loads embedded
PTX. It also implements the same math ABI for the host. The CUDA context
provides page-locked host buffers and device allocations. Model and KV
state belong to the managed address spaces.

When the driver or device is unavailable, the host constructs RAM and SSD
tiers and runs the same inference pipeline. On a GPU host, physical blocks
are borrowed for synchronous C ABI operations. The driver submits transfers
asynchronously on a private stream and synchronizes before returning.
Use the native-cuda-wheels workflow to build and validate the two wheel
artifacts. See [the native package](../../native/cuda/README.md).

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

### Block paging

`VirtualMemorySpace` in `AIKernel.Kernel.Memory` exposes a logical block
address space. Set its block count, block size, RAM budget, and swap directory.
Unwritten blocks read as zeros. Reads of swapped or device-resident blocks
promote them to RAM. RAM evicts the least recently used block to VRAM when
configured. A full VRAM tier evicts its oldest block to the swap file. If a
device allocation runs out of memory during automatic eviction, the RAM block
goes to swap. Call `MoveBlock` to choose a tier explicitly and
`GetBlockTier` to inspect residency.

```csharp
using AIKernel.Kernel.Memory;

const int blockSize = 64 * 1024;
using var cuda = new CudaVirtualMemoryDevice(deviceOrdinal: 0);
using var memory = new VirtualMemorySpace(
    blockCount: 4096, blockSize: blockSize, ramBlocks: 128,
    swapDirectory: ssdDirectory, vramBlocks: 128, device: cuda);

var bytes = new byte[blockSize];
bytes[0] = 42;
memory.WriteBlock(0, bytes);
memory.MoveBlock(0, VirtualMemoryTier.Vram);
memory.MoveBlock(0, VirtualMemoryTier.Swap);
memory.ReadBlock(0, bytes);
```

Each read and write transfers exactly one block. Operations on a space are
serialized, and a failed transfer preserves the source block. The optional
CUDA device uses the installed NVIDIA driver on 64-bit Windows or Linux.
Keep the device alive for the lifetime of its spaces. Omit the device and
VRAM budget to use RAM and swap. Other device backends can implement
`IVirtualMemoryDevice` from `AIKernel.Core.Memory`.

Use a private, writable SSD directory for the swap file. The space creates
the file on the first spill, reuses freed slots, and deletes it when disposed.
Its block addresses and data live for the lifetime of the space. Model hosts
can use the block API for their own payloads and buffers. `UseDeviceBlock`
keeps one block resident for a synchronous operation. The CUDA backend
provides `MultiplyRows` to run FP32 matrix-vector multiplication directly
against that block. `GetStatistics` exposes tier counts and transfer counters.

Call `ReserveRam(minimumBlocks)` before work starts to allocate and touch
reusable host buffers within the configured RAM tier capacity. `GetRamUsage()`
reports the capacity, reservation, active pages, spare buffers, and current
and peak buffer allocations in bytes. Forks share this accounting. Page
transfers can require temporary buffers beyond the configured payload capacity,
so keep headroom when choosing the capacity. `TrimRam` releases spare buffers
and clears the reservation. Use OS working-set and account-level measurements
for memory outside these buffers, including the runtime and file cache.

## Reference Module

The CUDA 13.0 reference implementation belongs in the external
`AIKernel.Cuda13.0` repository and targets one Windows runtime combination only:

```text
AIKernel.Cuda13.0/
  src/
    AIKernel.Cuda13.0.Libtorch2.12.win-x64/
  native/
  tests/
```

It targets:

- Windows / MSVC / `win-x64`
- LibTorch 2.12.0
- CUDA 13.0
- C ABI functions: `load_model`, `unload_model`, `forward`

Install it only on GPU hosts that explicitly need this runtime.
The reference CUDA package uses split distribution: NuGet.org carries a small
metadata package with managed dependencies, while the full runtime `.nupkg` is a
GitHub Release asset because it includes LibTorch, CUDA, cuDNN, and the native
bridge.

```bash
dotnet nuget add source <folder-containing-full-cuda-nupkg> --name AIKernel-CUDA
dotnet add package AIKernel.Cuda13.0.Libtorch2.12.win-x64 --version 0.1.3
```

Do not use a GitHub Release page URL directly as a NuGet source. Download the
full `.nupkg` first, then add its containing folder as a local source.

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
Capability repository when it provides a Python package. If the Capability ships
only a full NuGet runtime package, follow that repository's GitHub Release
installation instructions instead:

```bash
pip install git+https://github.com/AIKernel-NET/<matching-cuda-python-capability>.git
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
