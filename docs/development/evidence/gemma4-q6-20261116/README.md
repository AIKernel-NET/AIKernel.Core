# Gemma Q6_K verification, 2026-11-16

`summary.json` is derived from the 42 saved benchmark JSON files (7 paths × before/after × 3 processes).
Before: `bebeafdf440cd21d3601f8a283ad809460ed6f22`; after: source hashes and managed/native binary hashes are recorded here.
The GGUF SHA-256 was verified on both development and Server machines. No model weights or raw private traces are committed.

## Reproduce

Build `tests/AIKernel.Inference.Optimization.Validation` in Release, preserving the native binaries recorded in the manifests.
Run its `AIKernel.Core.Tests` harness with:

```text
--gemma-benchmark <model.gguf> benchmark-input.json <result.json> 1
```

Set `GEMMA_PROFILE=0`, `GEMMA_CONTEXT=1024`, `GEMMA_THREADS=4`, `GEMMA_BACKEND=Auto`, `GEMMA_ISA=Avx2` for development CPU.
Use `GEMMA_BACKEND=CudaResident` and `GEMMA_GPU=0` (Ada) or `1` (T1000) for GPU controls.
Server uses context 2048, 6 workers, affinity 0–5, and `Avx2`, `Avx512`, `AvxVnniResidual`, `Avx512VnniResidual`.
Use the memory settings documented in the benchmark result JSON. Start each development measurement after 3 consecutive one-second CPU samples <=10%.
Freeze both builds, use separate processes, keep the input and native assets identical, and vary before/after execution order.

`bits-*.json` compares 229 arrays bitwise before/after. `official-*.json` compares 228 arrays/checks against the independently decoded official graph;
the gates are max absolute error 1e-3, RMS 1e-4 and 12 token IDs. The reference revisions are in `reference-provenance.json`.
`paging-*.json` and Server official files cover forced cache eviction. `bonsai-*.json` preserves prior Bonsai baselines.
`unit-tests.json` records 889 passed; `server-tests.json` records 35 passed.

`local-jit-check.json` records optimized-JIT inspection: Q6 shifts use immediate operands, helpers inline, no calls to Half conversion or generic decoder, no new FMA.
The analyzer still emits CA1857 for helper parameters even though these callers are literal constants; measured optimized code has the required instructions.

Run `python plot.py` with matplotlib to regenerate PNG/SVG using only saved observations. No new inference is performed by plotting.
The graph uses one seconds axis and Development AVX2 before = 100%; whiskers are observed min/max, not confidence intervals.
