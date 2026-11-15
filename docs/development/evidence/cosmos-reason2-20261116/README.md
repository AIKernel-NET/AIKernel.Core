# Cosmos Reason2 architecture validation — 2026-11-16

Status: **preview; the gated Cosmos checkpoint has not been downloaded or tested**.
`summary.json` keeps that limitation machine-readable. Model metadata returned HTTP 401 with the existing credential.
No tokens, checkpoints, or user media are stored in this directory.

## Independent references

The architecture tests instantiate the official Transformers **4.57.1** Qwen3-VL graph with synthetic weights (seed 437).
They cover text, 64×96 RGB images and three timestamped video frames with temporal padding, vision position interpolation,
all DeepStack features, incremental KV decoding and eight greedy output tokens per mode.
There are 16 numerical checks per storage dtype (F32/BF16/F16), with max absolute gate 1e-5 and exact output token IDs.
RGB patch layout and normalization are compared to the actual official Fast image/video processors, not a copy of the C# loop.

BF16/F16 files are reloaded into an independently reconstructed FP32 reference model. Simply calling `.bfloat16().float()`
would also round nonpersistent RoPE buffers, which are not checkpoint weights; that is not the reference used here.
`*-comparison.json` records absolute and RMS errors. `avx-bf16-comparison.json` uses `DOTNET_EnableAVX2=0` on the development CPU;
it is a runtime fallback check, not an AVX-only hardware benchmark.

`prompt-reference.json` uses publicly accessible Qwen3-VL-2B-Instruct tokenizer metadata, with exact text/image/video/mixed
token IDs and three-axis positions. Model control token strings in ordinary user text are deliberately not activated.
`provenance.json` records the public revision, verified asset hashes and official reference source hashes.
This public base metadata does not substitute for testing NVIDIA's gated Cosmos checkpoint.

The optional FFmpeg adapter was checked with a generated PNG and a lossless eight-frame video; four selected frames had
exact RGB bytes and timestamps 0, 0.5, 1, 1.5 seconds. Cancellation terminated decoding.
`unit-tests.json` records all 900 unit tests passing, including malformed safetensors, traversal, dtype dots, media budgets and clearing.

## Reproduce

Use an isolated reference Python environment with `transformers==4.57.1`, PyTorch CPU, compatible Torchvision, Pillow, NumPy and safetensors.
The recorded run used torch 2.15.0+cpu, torchvision 0.29.2+cpu and Pillow 13.0.0. Production inference has no Python dependency.

```text
python tests/AIKernel.Inference.Optimization.Validation/create_cosmos_reference.py <fixture> --weight-dtype bf16
dotnet build tests/AIKernel.Inference.Optimization.Validation -c Release -p:GeneratePackageOnBuild=false
<harness> --cosmos-reference <fixture> <comparison-directory>
```

Repeat the reference command for f32 and f16. The generator emits no gated/model-derived weights.
For prompts, download the public assets at the pinned URLs in `provenance.json` under the `base-*` filenames expected by
`create_cosmos_prompt_reference.py`, then run that script and `<harness> --cosmos-tokenizer <asset-directory>`.

After access to the real model is available, use:

```text
<harness> --cosmos-generate <local-model-folder> <request.json> <result.json>
```

The JSON request accepts `system`, `maxNewTokens`, an optional local `ffmpeg` executable, and ordered `content` items:
`{"type":"text","text":"..."}`, `{"type":"image","path":"...","width":256,"height":256}` or `{"type":"video","path":"..."}`.
This real-checkpoint command has been compiled but not executed against Cosmos weights. A successful tiny-model comparison
does not waive that release gate or establish a Server 7GB memory result.
