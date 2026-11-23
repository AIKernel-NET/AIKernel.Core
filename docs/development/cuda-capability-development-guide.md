# CUDA Capability Development Guide

For CUDA on Windows x64 and Linux x64, use the CUDA Driver bundled with `AIKernel.Kernel` 0.1.4. Its only native runtime prerequisite is the NVIDIA driver.

`AIKernel.Cuda13.0` is the legacy LibTorch reference implementation through 0.1.3. Maintenance has ended.

See the [CUDA Driver migration guide](https://github.com/AIKernel-NET/AIKernel.Providers/blob/dev/docs/CUDA-DRIVER-MIGRATION.md).

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
.NET host
  -> AIKernel.Kernel memory and inference APIs
  -> aikernel_cuda_driver ak_* C ABI
  -> NVIDIA driver
```

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

Trusted hosts register the default mapper with `AddAIKernelKernel()`.

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

## Use from .NET

```bash
dotnet add package AIKernel.Kernel --version 0.1.4
```

`CudaVirtualMemoryDevice` creates a CUDA context through `ak_create`. Use `Upload` and `Download` for VRAM transfers. Dispose memory blocks before the device.

For Python callers of the native ABI, see the [CUDA Driver wheel](../../native/cuda/README.md).
