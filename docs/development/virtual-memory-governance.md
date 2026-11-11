# Virtual memory and device governance

Core defines the resource estimate, telemetry, swap policy, snapshot manifest,
and shared mapping contracts. Kernel owns native mapping, CUDA registration,
cryptography, NVML, and the device queue.

## Forking and snapshots

`ForkSpace` shares resident RAM, device pages, and swap slots under the
same physical budgets. The first write to a shared page separates the
writing space. The last reference releases the page.

`Hibernate` atomically writes materialized pages and optional continuation
bytes. SHA-256 covers the header, continuation, page indexes, and contents.
`Restore` validates the file and maps the pages through `IMemoryMapper`.
CPU compute callbacks can borrow a clean mapped page directly. GPU reads
promote pages on demand. Keep the file immutable and accessible while its
restored views are open. Authenticate files separately when accepting
snapshots from an untrusted source.

Snapshots are persistent plaintext exports of materialized state such as
the KV cache. An encrypted swap space requires explicit `allowPlaintext`
to export one.

## Secure swap

`SwapEncryptionPolicy` uses a volatile AES-GCM key, unique nonces, and
authentication of the slot index. `ZeroWipe` clears released host and
device storage and overwrites the logical swap ranges before deletion.
Storage controller remapping and SSD wear leveling are outside the file
overwrite interface. Encryption protects older ciphertext retained by flash.

## Scheduling and telemetry

Use one `LayerQosScheduler` per device. Enter with a priority and a spill
callback, then call `Checkpoint` after each synchronous layer. A higher
priority waiter runs after the active request's spill completes. A failed
spill closes the scheduler to further work.

`ResourceAdmissibilityPolicy` estimates RAM and VRAM pages and PCIe
paging from model dimensions, token counts, and route budgets.
`NvmlDeviceTelemetry` reads a device by PCI identity. Optional sensors may
be unavailable. Use `DeviceHealthPolicy` when selecting a device or CPU route.

## Shared input

The producer creates the named mapping, fixes its size, writes the input,
and signals that it is ready. The consumer opens it with
`ISharedMemoryMapper.OpenShared`. Keep the mapping open and its byte range
stable during the transfer. `VirtualMemorySpace.ImportSharedBlock`
page-locks the range and issues CUDA DMA directly into a VRAM block.
CUDA registration failures return a structured status. The caller
controls channel permissions and producer/consumer synchronization.
