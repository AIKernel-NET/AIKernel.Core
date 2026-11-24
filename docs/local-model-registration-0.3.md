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

A host can supply `deviceBudgetBytes` in bytes, or configure `AIKERNEL_DEVICE_BUDGET_BYTES` for sessions with no explicit budget. The default is zero. With a positive budget and a checkpoint small enough to attempt device residency, the Kernel checks CUDA availability first and can use Vulkan for the dense graph. A supplied `GgufMemoryOptions` selects bounded host paging. The routed MXFP4 graph uses host kernels. Driver allocation and kernel initialization errors are surfaced to the caller. Budget the working state in addition to the encoded checkpoint.

Paged GPT-OSS sessions expose `GetResourceResidency(layerIndex, resourceIndex)` for the gate, up and down projections together. `ResourceDispatchCounts` reports selection counts across layers per expert ID. Observing residency leaves page contents and eviction order intact. An OS-managed mapping returns no AIKernel page count.

## Registering a base and a delta

A standalone registration remains valid. A host can also supply one document with a `profiles` object and the name of the selected `profile`. Put the complete standalone registration under `profiles.base`. For example, add `"local": { "extends": "base", "shape": { "layers": 28 } }` to that object and set `"profile": "local"`. The selected profile inherits omitted shape fields. The parent is resolved first and the child overrides only the declared fields. All IDs refer to the same host-supplied document. Unknown keys, duplicate keys, missing parents and inheritance cycles fail registration. The resolved registration goes through the same checkpoint validation as a standalone registration.
