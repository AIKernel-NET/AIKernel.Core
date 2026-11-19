# AIKernel CUDA Driver

AIKernel Native C ABI for Windows and Linux x86-64. Build with a C compiler
and CMake. The runtime loads NVIDIA's installed driver on demand. Python
uses ctypes to run the same primitives on CUDA or the host.

Build a Windows wheel with python -m build --wheel. Build and repair Linux
wheels in the manylinux_2_28_x86_64 image using auditwheel.

The C ABI uses caller-owned buffers. CUDA allocations belong to one private
context and calls on that context must be serialized. Transfers use page-locked
host buffers and complete before the ABI call returns. Context destruction
reclaims its device allocations, stream and module. See src/aikernel_cuda.h for
the ABI version, status codes and counters.

## CPU instruction dispatch

Host FP32 GEMV selects AVX-512, AVX2+FMA, AVX, or scalar at runtime. CPU and
OS XSAVE support are checked before calling an ISA-specific function. CUDA
selection remains independent. Keep the dispatcher at the baseline x86-64
ISA; never build the whole library with `-march=native` or `/arch:AVX512`.
CMake assigns MSVC `/arch` flags only to the three dedicated source files.
GCC/Clang use per-function target attributes. An AVX-only CPU does not need FMA.

`AIKERNEL_CPU_GEMV=auto` is the default. Validation can force `scalar`, `avx`,
`avx2`, or `avx512`. An unsupported or unknown forced value makes host GEMV
return `AK_INVALID` without touching the output. `ak_cpu_gemv_backend()` reports
the selected enum or `AK_CPU_UNAVAILABLE`. Set the environment before inference;
do not change process environment concurrently with calls.

Run `python tests/cpu_dispatch.py /absolute/path/to/library --benchmark` to
compare against a double-precision reference, exercise unaligned input/tails,
and verify unsupported-path rejection. The optional single-thread benchmark
uses a 256 x 2048 FP32 matrix; it does not measure LLM tokens/second. These
host CPU changes do not add VNNI or BF16 kernels.

Linux packages for XServer require glibc 2.28 compatibility. Build in the
manylinux_2_28 environment, then test that exact library on both AVX2 and
AVX-512 hosts. Do not distribute a library built against the development
machine's newer glibc without checking its symbol requirements.

## Quantized Qwen3 matrix offload

The managed `Qwen3GpuModel` runs the same bounded dense Qwen3/Bonsai transformer
as `Qwen3CpuModel`, with all matrix-vector products on the selected CUDA device.
Embedding lookup, normalization, RoPE, attention, KV storage and sampling remain
on the CPU. This is a hybrid execution path, not an all-GPU transformer.
GPU errors propagate; no matrix operation silently switches to the CPU.

~~~csharp
using var model = new AIKernel.Kernel.Inference.Qwen3GpuModel(
    modelPath, deviceOrdinal: 0, cpuThreads: 4, contextTokens: 1024,
    maximumWeightBytes: 1024L * 1024 * 1024);
await model.WarmupAsync(systemInstructions, cancellationToken);
var reply = await model.GenerateAsync(systemInstructions, inquiry, 96, cancellationToken);
Console.WriteLine(reply.Text);
~~~

Deploy the native library from this source revision alongside the managed assembly.
The official `AIKernel.Kernel` NuGet release includes matching `win-x64` and
`linux-x64` runtime assets, so normal RID-aware restore/publish selects the library.
Release packaging requires both assets; a source-only CPU build may omit them.
NVIDIA's installed driver is required; a CUDA Toolkit or LibTorch installation is
not required. CPU-only callers still use `Qwen3CpuModel` without loading this library.
An invalid device, missing native export, unsupported model or exhausted allocation
fails the request. Dispose the model after its active requests have stopped.

The device holds the encoded GGUF file within `maximumWeightBytes`. The budget
covers model weights, not total GPU usage: the driver context, matrix working
buffers and up to one MiB of upload staging are additional. Leave headroom for
other GPU users. Uploads are chunked; the whole model is never expanded to FP32.
Only supported dense Qwen3 layouts with FP32 or GGUF type 41 matrices are accepted;
this does not imply support for all models below four billion parameters.

The additive `ak_gemv_q1_0` export takes device-resident Q1_0 g128 blocks:
a little-endian FP16 scale followed by 16 sign bytes per 128 values. A zero bit
represents minus scale; a set bit represents plus scale. Columns must be a multiple
of 128 and the address must be aligned to two bytes. The export requires a CUDA
context. FP32 and quantized device GEMV accept bounded interior tensor ranges.
Transfers also accept interior ranges, while free requires the original allocation
base. Bounds are checked against allocations owned by the context before launch.

Run independent arithmetic and allocation-boundary checks on a real device:

~~~sh
python tests/q1_gpu.py /absolute/path/to/library --device 0
~~~

Run managed end-to-end validation with a local licensed model:

~~~sh
dotnet run -c Release --project ../../tests/AIKernel.GpuInference.Validation -- /path/to/Bonsai-4B-Q1_0.gguf
~~~

That harness compares CPU and GPU text for one fixed Japanese support inquiry,
checks the encoded-weight budget, serialization, cancellation and recovery.
Its timings are functional diagnostics, not controlled performance measurements.
Equal output on these inputs is regression evidence, not proof of universal
numerical equivalence. Benchmark CPU/GPU only after controlling background load,
with the same model hash, inputs, thread ceiling and warmup policy.

Python users install `aikernel-net[gpu]==0.1.4` and the .NET 10 runtime. Import
`aikernel_cuda_driver` before calling the managed GPU API via pythonnet, so the
matching native library from its platform wheel is loaded. The managed wrapper
wheel remains platform-independent; the optional driver wheel is available for
Windows x64 and manylinux 2.28+ x64. CPU-only imports do not require this extra.
