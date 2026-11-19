# Gemma 4 E2B optimization evidence

See [the report](../../gemma4-optimization-20261115-jp.md) and `comparison.json` for measurement scope, limits and results.
`raw/` contains three observations per plotted execution path and development CPU start gates.
Numerical comparison JSON retains all 228 comparisons per execution path. The original reference provenance is in
[the generation evidence](../gemma4-e2b-20261115/README.md).

Rebuild the managed harness with `dotnet publish tests/AIKernel.Inference.Optimization.Validation -c Release`.
Select `GEMMA_BACKEND=Reference`, `Auto`, or `Cuda`; `GEMMA_GPU=0|1`; `GEMMA_THREADS=4|6`;
`GEMMA_CONTEXT=1024|2048`. For bounded CPU paging set `GEMMA_SWAP_DIRECTORY` to a private directory;
`GEMMA_CACHE_BYTES=5000000000` (normal) or `33554432` (paging pressure). RSS ceiling defaults to 7,000,000,000 bytes.

Run `--gemma-benchmark MODEL INPUT_JSON OUTPUT_JSON 1` three times under the stated CPU start gate.
INPUT_JSON has the `prompt` token IDs from the original generation evidence and `steps: 128`.
For official comparisons use `--gemma-forward MODEL generation-input.json OUTPUT_DIRECTORY`, then `compare_gemma4.py`.
For public API lifecycle checks use `--gemma-lifecycle MODEL`.

CPU tests: `dotnet test tests/AIKernel.Core.Tests -c Release`.
GPU matrix oracle: `python native/cuda/tests/gemma_gpu.py NATIVE_DLL GPU_ORDINAL`.
Rebuild native CUDA using CMake Release. The committed Gemma PTX is generated with
`python native/cuda/build_gemma_ptx.py PATH_TO_NVRTC_LIBRARY` (`compute_75`, `--fmad=false`).
Model weights are not packaged. Match their SHA-256 and native/managed artifact hashes before comparing runs.
The measured assembly is `AIKernel.Kernel.dll` in the artifact manifest. The final assembly adds only
constructor-failure cleanup of the empty paging arena; its separate hash and final test counters are recorded.
