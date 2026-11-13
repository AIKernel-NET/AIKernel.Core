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

Direct quantized-vector computation is the default. `AIKERNEL_Q1_KERNEL=lookup` is an experimental alternative and was slower on the development machine. AVX-512F support does not imply use of VNNI or BF16.

## Validation

```text
dotnet run --project tests/AIKernel.CpuInference.Validation -- MODEL.gguf tests/AIKernel.CpuInference.Validation/fixtures/bonsai-tokenizer.json --checks
dotnet run --project tests/AIKernel.Hosting.Validation
```

The tokenizer fixture was generated with Hugging Face tokenizers 0.22.2 from the official Bonsai 1.7B tokenizer JSON, SHA-256 `aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4`. It tests full trusted chat templates as well as Japanese, English, combining accents, emoji and leading newlines. Tokenizer reference tooling is not a runtime dependency.

The quantized arithmetic check independently decodes 257 rows of actual model bytes and compares scalar, AVX, AVX2 and AVX-512 paths at 1/2/4 workers, for direct-vector and lookup kernels. All passed on the EPYC 9555 XServer on 2026-11-14. Unsupported AVX-512 is rejected on the Xeon E-2174G development machine. This does not constitute execution on an AVX-only machine or exhaustive numerical proof. The fixed Japanese inquiry produced identical 48-token replies across the tested AVX2/AVX-512 runs (96-token ceiling).

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
