# CPU Qwen3 inference (0.1.4 development)

This implementation runs dense Qwen3 GGUF models with **F32 and Q1_0 g128** tensors. It is not a general GGUF loader. Other architectures and quantization types fail explicitly. No CUDA or LibTorch library, external inference service, or native model runtime is required.

```csharp
using AIKernel.Kernel.Inference;

using var model = new Qwen3CpuModel(modelPath, cpuThreads: 4, contextTokens: 1024);
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var result = await model.GenerateAsync(
    "Draft a brief reply in Japanese.",
    "The application reports a missing model.",
    maxNewTokens: 96,
    cancellationToken: deadline.Token);
Console.WriteLine(result.Text);
Console.WriteLine(result.FinishReason); // stop or length
```

`ClassifyAsync(system, user, labels, cancellationToken)` scores only the supplied single-token labels. It reports the winning label, not a calibrated confidence probability. Prompts still affect classification quality.

## Bounds and state

- Dense models up to the four-billion-parameter class; context 64–2048 tokens; 1–6 CPU workers; 1–256 output tokens.
- The tokenizer uses GGUF Qwen2 BPE vocabulary/merges and NFC. Literal special-token spellings in user input do not introduce chat roles.
- Trusted Qwen chat boundaries and the no-thinking prefix use dedicated token IDs. Only the final answer is returned; incomplete control sequences fail.
- Weights remain read-only memory mapped. Per-request KV and activation buffers are cleared after use.
- Only a trusted system-prefix KV is reused, never user text or generated replies.
- `WarmupAsync(system, cancellationToken)` prepares that prefix before admission. Publish readiness only after all required prefixes are prepared. It does not persist KV across process restarts.
- Calls on a model instance are serialized. Dispose the model only after active calls have stopped.
- Cancellation is checked during token, layer and row work. A proxy can delay propagation of a disconnected browser.
- The caller must independently verify model provenance, SHA-256 and license before loading. Model paths are trusted operator configuration.

## CPU selection

The default selects AVX-512F, then AVX2+FMA, AVX, scalar. .NET intrinsic support includes operating-system support. `AIKERNEL_CPU_GEMV=scalar|avx|avx2|avx512|auto` can select a path for testing; unsupported forced paths throw. AVX does not use FMA or AVX2 instructions.

The original constructor retains this automatic behavior. To force a matrix instruction
set for one model without changing process-wide environment variables:

```csharp
using var model = new Qwen3CpuModel(path, 4, 1024, instructionSet: "avx2");
Console.WriteLine(model.RequestedInstructionSet); // requested policy
Console.WriteLine(model.CpuBackend);               // actual selection
```

`CpuInstructionSets.Available` lists CPU/OS/native-library-supported choices. An explicit
`"auto"` overrides the environment and preserves FP32 activations. Null inherits the
environment; without an environment override it is automatic. Unknown names and
unsupported forced choices fail before opening the model. Forced choices remain fixed
even if `TuneCpuAsync` is called or another caller changes the environment later.
The selection concerns Q1_0 matrix kernels, not a global ban on other instructions
used by the .NET runtime, tokenizer, attention or OS.

For same-CPU ISA comparisons, use `CpuAutoTuningOptions.TuneThreadCount = false`
to hold workers constant during automatic ISA calibration; its production default
remains true. `GenerateBenchmarkAsync` explicitly ignores EOS and generates exactly
the requested number of greedy steps (1–256); ordinary `GenerateAsync` still stops
at EOS. Benchmark output is not intended as a customer reply. Decode time includes
output projection and sampling; for N generated tokens there are N−1 additional
transformer forwards. Report prefill separately from Decode milliseconds/N.

```text
AIKernel.FixedToken.Benchmark cpu model.gguf --workers 4 --tokens 128
AIKernel.FixedToken.Benchmark cpu model.gguf --instruction-set avx --workers 4 --tokens 128
AIKernel.FixedToken.Benchmark cpu model.gguf --instruction-set avx2 --workers 4 --tokens 128
AIKernel.FixedToken.Benchmark --list-instruction-sets
```

The benchmark's omitted `--instruction-set` is `auto`, calibrated before warmup.
Q8 remains an opt-in precision change (`q8-auto` or an explicit `*-q8` choice).

Direct quantized-vector computation is the default. `AIKERNEL_Q1_KERNEL=lookup` is an experimental alternative. Previous development-machine timings were affected by concurrent work and must be repeated after reboot before drawing performance conclusions. AVX-512F support does not imply use of VNNI or BF16.

## Optional GPU matrix offload

`Qwen3GpuModel` uses the same transformer with quantized matrices resident on CUDA.
Attention, normalization, RoPE and KV remain on the CPU. It requires the matching
AIKernel CUDA Driver library and an NVIDIA driver; CUDA Toolkit and LibTorch are
not runtime dependencies. The CPU API above still loads no CUDA library.
See [GPU setup, allocation limits and validation](../native/cuda/README.md#quantized-qwen3-matrix-offload).

GPU functional checks and performance comparisons are separate. Development-machine
CPU/GPU performance measurements are on hold until a reboot and a quiet background
load; diagnostic timings from functional tests must not be presented as benchmark results.

## Validation

```text
dotnet run --project tests/AIKernel.CpuInference.Validation -- MODEL.gguf tests/AIKernel.CpuInference.Validation/fixtures/bonsai-tokenizer.json --checks
dotnet run --project tests/AIKernel.Hosting.Validation
```

The tokenizer fixture was generated with Hugging Face tokenizers 0.22.2 from the official Bonsai 1.7B tokenizer JSON, SHA-256 `aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4`. It tests full trusted chat templates as well as Japanese, English, combining accents, emoji and leading newlines. Tokenizer reference tooling is not a runtime dependency.

The quantized arithmetic check independently decodes 257 rows of actual model bytes and compares scalar, AVX, AVX2 and AVX-512 paths at 1/2/4 workers, for direct-vector and lookup kernels. All passed on the EPYC 9555 XServer on 2026-11-14. Unsupported AVX-512 is rejected on the Xeon E-2174G development machine. The separate physical i7-3770K / Ubuntu validation also passed with AVX=true and AVX2/FMA/AVX-512=false, including automatic calibration and cache reuse. These finite checks are not exhaustive numerical proof. The fixed Japanese inquiry produced identical 48-token replies across the tested AVX2/AVX-512 runs (96-token ceiling).

Model format references: [Bonsai 1.7B](https://huggingface.co/prism-ml/Bonsai-1.7B-gguf), [Bonsai 4B](https://huggingface.co/prism-ml/Bonsai-4B-gguf), [PrismML quantization implementation](https://github.com/PrismML-Eng/llama.cpp/blob/master/ggml/src/ggml-quants.c).

## Hosting primitives

`PrivateFileCredentialProvider` resolves an immutable allowlist of names to private files. It rejects public-root files, symlinks and permissive Unix file modes. Use restrictive Windows ACLs. Do not accept paths or secret names from public requests.

`DurableTokenBudget` reserves before external dispatch, appends flushed checksummed records, and settles once using authoritative usage. Missing usage remains charged. Its journal is private runtime state, must survive restart and must not be deleted to replenish limits. Corrupt or incomplete journals fail closed. It is a single-host budget, not a distributed ledger, and does not defend against an attacker controlling the same OS account.

日本語: 対応対象はQwen3 denseのF32 / Q1_0 g128です。CPUの命令を実行時に分岐し、利用者入力の特殊トークンと信頼したチャット境界を分離します。分類精度・応答速度はモデルと指示文に依存します。公開デモの起動手順・資源計測はWebSiteの問い合わせデモ文書を参照してください。


## Measured CPU selection

Call `TuneCpuAsync(new CpuAutoTuningOptions(privateProfileDirectory, verifiedModelSha256))` before warmup to enable calibration. The model identity must be the SHA-256 already verified when installing the immutable GGUF. The host keeps admission closed during calibration. Explicit `AIKERNEL_CPU_GEMV=scalar|avx|avx2|avx512` overrides tuning; unavailable instructions are rejected. Automatic choices include supported AVX2/FMA and AVX-512 paths (or the supported AVX/scalar fallback) and worker ceilings 1, 2, 4, 6 within the constructor and effective processor limits.

Prefill and decode have separate choices. Current prefill remains sequential token GEMV; there is no batched GEMM or VNNI kernel in this engine. Calibration samples Q, FFN gate/down matrices from the first, middle and final layers; decode also samples the output projection. It checks candidate arithmetic against scalar results, warms each candidate, then uses three paired rounds with reversed order. Median timings feed a 0.25 EMA; an incumbent changes only for an estimated improvement above 5 percent. This is a workload sample, not proof of a globally optimal kernel or thread count.

The default calibration budget is two seconds, with at most one in-flight matrix probe of overrun. Incomplete measurements do not create a winning profile. Cache keys include verified model hash and shape, CPU brand/features, affinity, effective processors, thread/context ceilings, OS/runtime and the compiled kernel module ID. Profiles are bounded JSON, atomically replaced in a private directory. Invalid, unsupported, future-dated or expired profiles are remeasured. Fixed system KV stays private to the model and can be reused across numerically validated kernel choices.

`CpuGeneration` exposes prefill/decode milliseconds and forward-token counts. Aggregate, context-bucketed online timing is retained without prompt/reply text and saved at the next calibration. It does not directly compare unrelated customer prompts or route random customer requests to an experimental path. The WebSite host recalibrates after a 15-minute interval only if no request has run for two minutes and it obtains exclusive admission. Operators can omit tuning or pin a backend to preserve a chosen configuration.

GEMV divides output rows between workers, aligning boundaries to actual 64-byte output addresses. One-worker calls bypass Parallel.For. Multi-worker calls reuse the .NET thread pool; no persistent busy-spin loop was added. These changes address this GEMV output boundary only, not every possible source of false sharing in the engine.

## RAM and SSD scope

The GGUF mapping is read-only and uses OS page caching. `VirtualMemorySpace` separately supports budgeted RAM and SSD swap, but this Qwen3 engine's mutable KV arrays are not connected to that paging API. A 7 GB resident / 12 GB logical LLM workload has not been validated. Increasing the virtual-address limit does not establish a resident-memory bound. Such an integration requires admission accounting for model pages, KV, activations, temporary workspaces and runtime overhead, plus bounded encrypted swap I/O and failure tests.

The quantized matrix tables have an explicit static initializer. Tuning can invoke multiplication before the first embedding row read; leaving initialization as beforefieldinit caused lazy static-base helpers to remain in the hot Q1 loop under the tested .NET 10.0.15 JIT. A before/after disassembly check found three GC-static-base helper call sites before the fix and none afterward. Keep first-use calibration and cached-startup benchmarks in performance validation.

## Optimization baseline and stage diagnostics (2026-11-15)

The November 15 fixed-token prototype is frozen before algorithm changes: 225
included requests and 15 excluded diagnostic requests, preserved as complete
blocks with their exclusion reasons. It is not an optimized release claim.
Future measurements must use separate output directories, fixed workers/affinity,
the same power scheme and two consecutive 15-second CPU averages at or below 10%.
The public shared-host label is Server; its account budget remains six CPU workers
in total and 7 GiB resident memory, including the concurrently running demo.

Set `AIKERNEL_INFERENCE_PROFILE=1` **before constructing** a model to attach an
`InferenceProfile` to completed CPU, CUDA or Vulkan results. The fixed-token
harness exposes this as `--profile true`. Prefill and Decode separately aggregate
embedding, matrix, normalization, RoPE, attention, SwiGLU, residual and argmax wall
time. Q8 quantization is a nested part of Matrix, so do not add it twice. Attention
includes KV writes and the head-worker dispatch. No per-worker timing is collected.
When disabled, no profiling object, timestamps, GC counters or stage dictionary
are created; existing generation timings still operate as before.

Request-stage diagnostics include tokenization, state allocation, prefix copying
and state clearing. The allocation delta is process-wide GC allocation, including
runtime worker activity; it is not retained memory, RSS or solely this request.
Managed request time excludes wrapper-level GPU scratch cleanup. External wall time
remains the end-to-end measurement. The actual model dimensions, CPU backend and
activation precision accompany each profile.

Instrumentation-only validation on the development machine, Bonsai 1.7B, fixed
128 tokens, four physical cores, five requests per block:

| Path | Profile off median | Profile on median | Repeated off median |
| --- | ---: | ---: | ---: |
| AVX2 FP32 | 13.724 s | 13.808 s | 14.175 s |
| RTX 4000 SFF Ada FP32 offload | 9.722 s | 9.726 s | — |

All CPU token hashes matched the frozen November 15 AVX2 sequence. CUDA profile
on/off hashes also matched each other. Differences of +0.61% and +0.05% are within
the observed sample variability; this is not proof of zero overhead. On this CPU
condition Matrix accounted for approximately 88% of elapsed work, SwiGLU 2% and
RoPE 0.7%. CUDA Matrix includes staging, transfers, kernels and synchronization;
it cannot be interpreted as pure GPU arithmetic. The GGUF reports width 2048,
hidden size 6144, 28 layers, 16 query heads and eight KV heads.

For a separate CUDA timeline diagnostic, also set `AIKERNEL_CUDA_EVENT_PROFILE=1`,
or use `--profile true --cuda-events true` in CUDA benchmark mode. This requires
the matching native library with `ak_profile_configure` and `ak_profile_read`.
Four events on the existing private stream bracket GEMV upload, kernel and download;
the existing synchronization is timed separately on the host. Event intervals
include stream scheduling gaps, and host wait overlaps device time. Never sum
these overlapping measures or use event-instrumented latency as the performance
baseline. No CUDA events or event timers are used when this option is disabled.
See the [CUDA Driver event API](https://docs.nvidia.com/cuda/cuda-driver-api/cuda_driver_api/group__CUDA__EVENT.html).

### Order and correctness gates for 0.1.4

1. Preserve this instrumentation-only baseline before changing algorithms.
2. Reuse RoPE trigonometric values, request workspaces and shared-input Q8
   preparation. Require unchanged token hashes for the same actual kernel and
   precision; cancellation and cross-request isolation must also pass.
3. Group accumulator-order changes and any measured worthwhile SwiGLU/Norm/Dot
   vectorization in a separate numerical change. Compare against independent
   scalar calculations and explicit logit tolerances before taking new hashes.
   Keeping `auto` at FP32 does not make changed summation order bit-identical.

`auto` remains FP32. Q8 precision remains explicit. Full GPU-resident forward/KV,
true batched prefill, NUMA placement and expanded AMX work are later stages, not
requirements silently folded into this first arithmetic-preserving change.

Workspace reuse must clear its used high-water range before reuse, including KV,
logits, attention scores, quantized buffers and pinned host staging. A trusted
system-prefix cache is private model state, excluded from per-request clearing;
it must never contain inquiry tokens and must be cleared on replacement/disposal.
GPU wipe failure must reach the caller; context destruction alone is not evidence
of VRAM zeroing. Preserve both the original inference failure and any cleanup
failure, and stop admission to an instance whose cleanup failed.

GPU auto selection must cover the output projection during startup warmup, not
only layer shapes. Record per-process shape choices and repeat across launches;
retune after GPU-resident forward changes. Replay diagnostics should retain actual
kernel names, activation precision and per-phase worker counts alongside model
and binary identity, rather than recording only the requested `auto` option.

For future scale-only caches, FP16 scales add 2/18 (about 11%) of Q1 payload size;
decoded FP32 scales add 4/18 (about 22%). Account for this difference in RAM limits.
Neither estimate is a measured resident-memory guarantee.
