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

The quantized arithmetic check independently decodes actual model bytes and compares scalar, AVX and AVX2 paths. Unsupported AVX-512 is rejected on the Xeon E-2174G development machine. This does not constitute execution on an AVX-only machine or validation of the Q1 path on an AVX-512 server.

Model format references: [Bonsai 1.7B](https://huggingface.co/prism-ml/Bonsai-1.7B-gguf), [Bonsai 4B](https://huggingface.co/prism-ml/Bonsai-4B-gguf), [PrismML quantization implementation](https://github.com/PrismML-Eng/llama.cpp/blob/master/ggml/src/ggml-quants.c).

## Hosting primitives

`PrivateFileCredentialProvider` resolves an immutable allowlist of names to private files. It rejects public-root files, symlinks and permissive Unix file modes. Use restrictive Windows ACLs. Do not accept paths or secret names from public requests.

`DurableTokenBudget` reserves before external dispatch, appends flushed checksummed records, and settles once using authoritative usage. Missing usage remains charged. Its journal is private runtime state, must survive restart and must not be deleted to replenish limits. Corrupt or incomplete journals fail closed. It is a single-host budget, not a distributed ledger, and does not defend against an attacker controlling the same OS account.

日本語: 対応対象はQwen3 denseのF32 / Q1_0 g128です。CPUの命令を実行時に分岐し、利用者入力の特殊トークンと信頼したチャット境界を分離します。分類精度・応答速度はモデルと指示文に依存します。公開デモの起動手順・資源計測はWebSiteの問い合わせデモ文書を参照してください。
