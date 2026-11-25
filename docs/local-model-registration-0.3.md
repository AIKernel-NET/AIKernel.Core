# Opening a registered model in 0.3

LocalLlm hosts can keep using `chat.local` with the same arguments, cancellation token and response metadata. Direct Kernel callers can register a graph and let the Kernel select its execution path. The following example uses the Bonsai registration shipped with the LocalLlm Provider.

```csharp
using AIKernel.Kernel.Inference;

var registration = DenseModelRegistration.Parse(File.ReadAllText("bonsai-1.7b.json"), "bonsai-1.7b");
using var model = registration.OpenSession(modelPath, threads: 4, context: 1024);
var reply = await model.GenerateAsync("Answer briefly.", "How do I get started?", 64, cancellationToken);
```

The host supplies the GGUF path and registration. The Kernel verifies the checkpoint SHA-256, tokenizer digest, revisions and graph dimensions on open. For Gemma and GPT-OSS the built-in registrations check their architecture, tensor shapes and model-specific metadata. A successful open of these built-ins does not authenticate the model publisher.

| Previous direct API | 0.3 migration | Responsibility |
| --- | --- | --- |
| `Qwen3CpuModel` | `DenseModelRegistration.Parse(...).OpenSession(...)` | The registered dense session owns generation, trusted-prefix state and workspace lifetime. The checkpoint binds tensor and tokenizer roles. |
| `Qwen3GpuModel` | Use the dense registration and provide a device budget | The Kernel admits accelerator kernels. The original wrapper retains its explicit CUDA, Q8 and GPU diagnostic APIs during 0.3. |
| `Qwen3VulkanModel` | Use the dense registration and provide a device budget | The Kernel discovers a compute device and the Vulkan implementation owns buffer placement and dispatch. |
| `Gemma4E2BCpuModel` | `ModelRegistration.Gemma4E2B.OpenSession(path, threads: 4, context: 1024)` | The E2B profile describes the layer pattern and KV owners. Per-layer input and the resident accelerator graph preserve the E2B operations. |
| `GptOss20BCpuModel` | `ModelRegistration.GptOss20B.OpenSession(path, threads: 4, context: 512)` | The expert profile specifies 32 experts and top four. The router breaks ties by ascending ID. Harmony owns role markers and final-channel decoding. |
| `DenseModelRegistration.Open(...)` | `registration.OpenSession(...)` | The original method still returns the original direct-use type. |

**Migration schedule.** The five direct types and the old `Open` method issue compiler warnings in 0.3 on .NET 10. Their removal is planned for 0.4 on .NET 11. Hosts that deliberately pin a diagnostic or experimental accelerator mode can continue using its explicit 0.3 entry point during migration.

## Placement and memory

### Qwen3-0.6B CPU snapshots

Use `qwen3-0.6b` as the registration ID. Choose one of the profiles shipped in the LocalLlm Provider's `profiles/` directory. Keep the upstream directory structure, including the configuration, tokenizer and generation files.

| Profile | Local path passed to `OpenSession` | Verified upstream revision |
| --- | --- | --- |
| `qwen3-06b-onnx.json` | `onnx/model.onnx`, with its sibling `model.onnx_data` | `onnx-community/Qwen3-0.6B-ONNX@79346d370dfae34801d26993fda9ae2372900fd0` |
| `qwen3-06b-safetensors.json` | `model.safetensors` | `Qwen/Qwen3-0.6B@c1899de289a04d12100db370d81485cdf75e47ca` |

The ONNX reader accepts the exact FP32 graph fingerprint above. Any change to its operators, connections or dynamic axes requires a new reader review. The SafeTensors reader imports BF16 and FP32 directly. Both use the same dense CPU session. Opening a session builds a private FP32 backing in the OS temporary directory. Allow approximately 2.4 GB of free temporary storage per open Qwen session in addition to the checkpoint. The file is released on disposal. Set the existing weight memory options to use bounded paging.

For a repack of the pinned SafeTensors weights, point the session at `model.safetensors.index.json`. Beside it, put a UTF-8 file named `model.safetensors.index.json.lock.json`. Its JSON object maps the index filename and each shard filename to the SHA-256 of that file. Copy the SafeTensors profile and set `weightsSha256` to the SHA-256 of the lock file itself. Every artifact is read from the handle used to verify it. The Kernel checks the index, shard membership and the original revision's per-tensor digest before making the session available. For the Provider, set `RegisteredLocalLlmSettings.RegistrationFile` to the updated profile.

### Other registered graphs

A host can supply `deviceBudgetBytes` in bytes, or configure `AIKERNEL_DEVICE_BUDGET_BYTES` for sessions with no explicit budget. The default is zero. With a positive budget and a checkpoint small enough to attempt device residency, the Kernel checks CUDA availability first and can use Vulkan for the dense graph. A supplied `GgufMemoryOptions` selects bounded host paging. The routed MXFP4 graph uses host kernels. Driver allocation and kernel initialization errors are surfaced to the caller. Budget the working state in addition to the encoded checkpoint.

Paged GPT-OSS sessions expose `GetResourceResidency(layerIndex, resourceIndex)` for the gate, up and down projections together. `ResourceDispatchCounts` reports selection counts across layers per expert ID. Observing residency leaves page contents and eviction order intact. An OS-managed mapping returns no AIKernel page count.

### Optional read-ahead for dense and expert CPU weights

The host can enable read-ahead in `RegisteredLocalLlmSettings.WeightMemory` or pass the same options to a direct session. The following configuration keeps a 7 GB process budget and allows up to 8 MiB for speculative reads.

```csharp
var memory = new GgufMemoryOptions
{
    SwapDirectory = privateSwapDirectory,
    MaximumResidentBytes = 7_000_000_000,
    Prefetch = new() { Enabled = true, Depth = 1, MaximumBytes = 8L << 20 }
};
using var session = ModelRegistration.GptOss20B.OpenSession(modelPath, 4, 128, memory, deviceBudgetBytes: 0);
var answer = await session.GenerateAsync("", question, 8, cancellationToken);
var counters = session.GetWeightPrefetchStatistics();
```

Read-ahead defaults to off. The depth ranges from 1 to 4 and the staging limit from 1 to 64 MiB. Dense sessions look ahead by this many layers and offer the first cold pages of their query and key projections. GPT-OSS offers up to this many expert gate pages using the selections for the same layer on the preceding token, or the preceding layer when there is no history. Each request starts with an empty expert history.

A background reader stages immutable pages within the budget left after the demand cache, runtime reserve and transfer allowance. If there is less than one page of headroom it stays off. The demand path claims the staged bytes on its first fault. A demand for a queued page takes priority, and a demand for an in-flight page joins that read. Normal VM residency and eviction apply when the demand arrives.

If an expert prediction misses, the next eligible prediction is 16 layer visits later. Consecutive misses double the interval up to 128 visits. A hit or the end of the request resets the interval. Dense look-ahead follows the known layer order independently.

The counters are cumulative for the lifetime of a session. `Issued` counts admitted predictions, `Hits` counts staged pages consumed by demand, `Misses` counts expired or unselected predictions, and `Cancelled` includes superseded queued reads and work dropped on pressure or request completion. `Failures` counts optional read failures. `DemandReads` measures logical demand reads from the backing file. The OS may serve them from its file cache. `WaitedForInFlight` counts demand joins. Reading the counters does not touch VM residency. Mapped sessions return a null snapshot.

### Frequency-based expert retention (development branch)

Paged GPT-OSS CPU sessions can keep frequently routed experts in host RAM. The policy uses part of the configured weight cache. It applies to all three expert projections and takes effect as their pages are requested.

~~~csharp
var memory = new GgufMemoryOptions
{
    SwapDirectory = swapDirectory,
    MaximumResidentBytes = 7_000_000_000,
    WeightCacheBytes = 5_000_000_000,
    ExpertFrequency = new()
    {
        Enabled = true,
        Window = 8,
        PromoteAt = 3,
        DemoteAt = 1,
        MinimumResidency = 4,
        MaximumBytes = 512L << 20
    }
};
using var session = ModelRegistration.GptOss20B.OpenSession(
    modelPath, 4, 128, memory, deviceBudgetBytes: 0);
var result = await session.GenerateAsync("", prompt, maxNewTokens: 8);
var placement = session.GetExpertPlacement();
~~~

Each layer tracks its own rolling window of 2–256 router decisions. Promotion starts after the first complete window. Among eligible experts selected on this visit, higher counts get priority and ties retain router order. The thresholds must satisfy `0 ≤ DemoteAt < PromoteAt ≤ Window`. Minimum residency is measured in visits to the same layer (1–4096). The byte allowance must be at least 1 MiB and is capped by the actual weight cache with one page reserved for demand. An expert whose three projections exceed the remaining allowance stays in the normal cache. When the process address-space budget shrinks, the store releases preferences across layers before resizing. Explicit memory trims can evict preferred pages.

The feature defaults to disabled. Read-ahead has a separate switch. Placement observations contain the policy, the current preferred page count and the last 4096 transitions with sequence numbers. A transition records layer, expert, direction, visit, window count, allowance and whether budget pressure triggered it. Use `GetResourceResidency(layer, expert)` to inspect actual RAM residence. Reading either diagnostic preserves the eviction order and does not read weight pages.

## Registering a base and a delta

A standalone registration remains valid. A host can also supply one document with a `profiles` object and the name of the selected `profile`. Put the complete standalone registration under `profiles.base`. For example, add `"local": { "extends": "base", "shape": { "layers": 28 } }` to that object and set `"profile": "local"`. The selected profile inherits omitted shape fields. The parent is resolved first and the child overrides only the declared fields. All IDs refer to the same host-supplied document. Unknown keys, duplicate keys, missing parents and inheritance cycles fail registration. The resolved registration goes through the same checkpoint validation as a standalone registration.
