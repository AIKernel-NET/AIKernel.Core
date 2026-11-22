# AIKernel.Core Release Notes

[日本語](RELEASE_NOTES-ja.md)

## 0.1.4 (release candidate in development)

Use `WithDistributedApplication()` to start and stop the registered HTTP/MCP listener with the Generic Host. The application supplies the handlers for its routes and tools.

[Inference and governed operations API examples](https://github.com/AIKernel-NET/AIKernel.NET/blob/dev/docs/overview/inference-api-0-1-4.md).

For an existing 0.1.3 application, start with the [model selection and first-reply checks](https://github.com/AIKernel-NET/AIKernel.NET/blob/dev/docs/overview/upgrade-0-1-4.md).

- Imported Cosmos 3 Edge and Nano checkpoints generate text from text, RGB images and timestamped video frames on CPU. The runtime requires neither CUDA nor LibTorch. Multi-turn generation preserves each speaker and earlier attachments.
- Monolith can register either local reasoner with a pinned SHA-256 import manifest. The bilingual website chat checks availability and accepts follow-up questions about the same image without a visitor API key.
- Cosmos 3 Edge and Nano ran with four CPU workers under a 7 GB model-host limit. Replies and generated token IDs matched the reference in all six text, image and video cases. Peak host RSS was 3.564 GB with the default 128 MB KV cache. Across the six requests the host wrote 84 KiB to SSD at that setting and 18.09 GB with an 8 MiB KV cache. [Development-machine comparison and test conditions (Japanese)](docs/development/evidence/cosmos3-server-7gb-20261120/README.md)
- GPT-OSS-20B ran with SSD paging under the Server's 7 GB physical-memory budget. Its generated tokens matched llama.cpp on the two validation prompts.
- Gemma 4 E2B supports text generation on CPU and a resident forward pass with weights and KV on supported NVIDIA GPUs. [Supported GGUF and numerical checks (Japanese)](docs/gemma4-e2b-inference.md)
- Bonsai 1.7B evaluates a proposed operation separately from host observations using the governance ROM. Across 16 newly evaluated Server cases and 60 replayed cases, all 30 expected approvals and 46 expected denials matched. [Operating guide and evaluation breakdown (Japanese)](docs/development/bonsai-ctg-operations-jp.md)
- Cosmos prefill reuses weight pages across tiles of up to 32 input tokens when the weights exceed the cache. The private host audit reports model load, prefill and decode time separately. [Memory settings and diagnostics (Japanese)](docs/cosmos3-inference.md)
- CPU is the standard distribution. Supported GPUs can use the optional CUDA Driver path. The legacy LibTorch add-on stays at 0.1.3.

## 0.1.3

- Align Common, Core, Kernel, Hosting, and TestKit with the 0.1.3 AIKernel.NET contracts. Core remains the managed runtime; CUDA, LibTorch, and other accelerator runtimes are outside its package boundary.
- The Python distribution target is `aikernel-net` (`import aikernel_net`). Validate the wheel's managed assemblies and CTG-ROM sample separately from the NuGet packages.
- Follow the [Core release checklist](docs/operations/release-checklist.md) before building and publishing stable artifacts.

## 0.1.2

**June 16th, 2026 - Canonical runtime line.**

AIKernel.Core 0.1.2 aligns the deterministic runtime packages with AIKernel.NET 0.1.2.

- Publish `AIKernel.Common`, `AIKernel.Core`, `AIKernel.Hosting`, and `AIKernel.Kernel` as the synchronized Core package family.
- Align CTG runtime documentation and Python wrapper guidance with the 0.1.2 public release flow.
- Keep Provider implementations outside Core and route provider-owned capabilities through AIKernel.Providers.
- Preserve deterministic CTG evaluator behavior while exposing the runtime surface needed by Control and Tools.
