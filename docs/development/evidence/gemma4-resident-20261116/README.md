# Gemma 4 E2B: AVX-512 / VNNI / resident CUDA evidence

2026-11-16. Baseline source: `f04e45f`. Model SHA-256:
`fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634`.
The model and full `.f32` traces are kept outside Git; the comparison reports contain every checked tensor's error.

- `summary.json`: medians and observed ranges, with the previous measurements and fresh controls kept separate.
- `<path>-0/1/2.json`: individual 128-token fixed-length runs. `prior-*` files preserve the previous measurements.
- `gate-*.json`: three one-second CPU usage samples, all at or below 10%, before each development-machine run.
- `*-comparison.json`: official-reference comparisons. The fixed limits remain maximum absolute error `1e-3`, RMS `1e-4`, plus identical 12-step token IDs. `final-*` reports use the final managed/native build.
- `paging-*.json`: 32 MiB cache pressure; all 229 candidate arrays match the same ISA with a normal cache bit for bit. The official comparison checks 228 reference arrays.
- `memory-summary.json`: maximum of Linux `ru_maxrss`, `/proc` high-water RSS and .NET peak RSS. The budget is decimal 7 GB, not 7 GiB or an OS-enforced cgroup cap.
- `gpu-long-*.json`: 1920 input + 128 output, context 2048, compared with the previous CPU token sequence. These runs are functional checks under background load, **not performance measurements**.
- `cuda-abi-*.json`: real Windows GPU tests for pointer/shape checks, tie-breaking, one forward sync, eight-byte result, skipped-trace clearing, state reset and fail-closed behavior after non-finite output.
- `linux-cuda-final.json`: the same ABI check on a Linux RTX 3080 Ti; no inference speed comparison is inferred from this small test.
- `native-oracle.json`: independent double-precision Q4 dot oracle on Server AVX-VNNI and AVX-512 VNNI. `linux-abi.json` also records safe rejection of VNNI on the AVX-only validation CPU.
- `bonsai-fp32.json`, `bonsai-hashes.json`: unchanged Bonsai 1.7B/4B FP32 logit bits and 10 Q8-path token hashes.
- `unit-tests-summary.json`: 880 unit tests, with the SHA-256 of the complete local TRX. `server-unit.xml`: 26 Gemma tests repeated on the AVX-512 Server; the private home directory is normalized.
- `build-hashes.json`: frozen measured CPU DLLs, final managed/native DLLs and resident CUDA source/PTX hashes. CPU timing used the earlier CPU-only build; the final build repeats its numerical gate.
- `sdist-check.json`: source archive membership, a clean Windows Release build from that archive, and a real GPU ABI test.

## Reproduce

Publish `tests/AIKernel.Inference.Optimization.Validation` in Release. Place the matching native CPU or CUDA library next to the validation assembly.
The harness accepts `--gemma-benchmark MODEL INPUT_JSON OUTPUT_JSON 1` and `--gemma-forward MODEL INPUT_JSON TRACE_DIRECTORY`.
Use the prompt and steps recorded in the previous evidence; the timing run has 23 prompt tokens, 128 generated tokens, temperature zero and ignored EOS.
It executes one unmeasured forward, then measures prefill + 127 decode forwards. Load, allocation, cleanup and HTTP are excluded.
This short question normally answers `東京です。` and stops; ignoring EOS continues control tokens. It is a fixed-work test, not a claim of 128 natural-language answer tokens.

Set `GEMMA_BACKEND=Auto` and `GEMMA_ISA=Avx2|Avx512|AvxVnniResidual|Avx512VnniResidual` for CPU comparisons.
Server uses `GEMMA_THREADS=6`, `DOTNET_PROCESSOR_COUNT=6`, context 2048 and affinity to the same six logical CPUs.
Set `GEMMA_SWAP_DIRECTORY` to a private directory; the weight cache is 5,000,000,000 bytes (pressure test: 33,554,432).
The harness also monitors RSS every 100 ms and terminates its own child on a 7 GB breach.

Set `GEMMA_BACKEND=Cuda|CudaResident`, `GEMMA_GPU=0|1`, four workers and context 1024 for the development GPUs.
Do not run other builds or regressions concurrently with the timing runs. WSL was stopped with user authorization.
The balanced power plan and driver version are recorded in `local-environment.json`. Background activity during a run is not fully isolated.

For official comparisons, use `tests/AIKernel.Inference.Optimization.Validation/compare_gemma4.py` and the independently decoded FP32 reference from the previous Gemma validation.
Do not replace the reference logits with this implementation's output, or widen the gate to accept an optimization.

Regenerate the chart with `python plot.py` in an environment with matplotlib. The chart uses one horizontal seconds scale and normalizes percentages to the previous paged Server AVX2 result (53.64 seconds).
