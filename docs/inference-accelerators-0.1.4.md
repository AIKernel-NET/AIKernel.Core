# Optional inference accelerators for 0.1.4

AIKernel implements the model and matrix kernels itself. Vulkan uses Silk.NET.Vulkan
2.23.0 as an API binding; it does not introduce another LLM runtime. The existing
CPU FP32-activation path remains the default. CUDA constructors retain their original
signature and behavior.

## Vulkan (including Intel integrated graphics)

Enumerate with Qwen3VulkanModel.EnumerateDevices(), select the desired vendor/name,
then pass its Ordinal to Qwen3VulkanModel. Vulkan and CUDA device ordinals differ.
The development P630 enumerated as Vulkan ordinal 1; do not hardcode that elsewhere.
The implementation requires Vulkan 1.1, a compute queue, support for 128-lane workgroups, and
coherent host-visible storage memory. It uses no window or presentation surface.

Encoded Q1_0/FP32 weights stay in a bounded Vulkan buffer (default limit 1 GiB).
Integrated GPUs share system memory; DeviceWeightBytes is an allocation size, not
dedicated VRAM. Descriptor alignment, per-device storage-buffer ranges and dispatch
limits are checked. Scratch buffers are bounded to 16 MiB total and cleared after
requests. Attention, normalization, RoPE, KV state and sampling remain on the CPU.
Requests are serialized. Cancellation is checked between dispatches, after the
submitted GPU work completes; a driver hang is not a cancellable managed operation.
Failures are reported, never silently relabeled CPU fallback as GPU execution.

The GLSL and embedded SPIR-V are in src/AIKernel.Kernel/Inference/Shaders.
Rebuild with glslang -V --target-env vulkan1.1 quantized_gemv.comp -o quantized_gemv.spv.
The portable shader requires no FP16/int8 storage or subgroup operations. Devices
reporting compute-stage subgroup arithmetic use the 32-lane shader: each lane
handles four packed weights, with subgroup reduction and one workgroup barrier.
AIKERNEL_VULKAN_KERNEL=portable or subgroup explicitly selects a kernel; an
unsupported explicit selection fails. The default probes actual capabilities.
Build quantized_gemv_subgroup.comp with the same glslang command as the portable
shader. Q1_0 bit 1 means +1; bit 0 means -1 in both implementations.

AIKERNEL_VULKAN_PROFILE=1 enables internal validation diagnostics: timestamp queries
when the compute queue supports them, host call/wait timings and allocation flags.
Normal inference allocates no query pool and performs no timestamp reads. The
validation probe compares actual GEMV with a checksum-producing encoded-byte read
kernel. This is a diagnostic effective-throughput estimate, not a DRAM bandwidth
measurement: cache reuse, shader instructions and dispatch overhead remain.

The development P630 driver reports shaderIntegerDotProduct=true but all 8-bit
integer-dot accelerated properties false. API availability must not be interpreted
as hardware DP4A acceleration. No Intel INT8 speedup is claimed.

## Q1_0 weights versus Q8 activations

The Bonsai files used in this experiment already contain Q1_0 weights (128 values per
18-byte block). The new optional quantization changes the input activation vector,
not those model files: per 128 elements, scale = max(abs(x))/127; round ties to even,
clamp to [-127,127]. All-zero and finite subnormal blocks are handled explicitly;
nonfinite inputs are rejected. Scales and input-derived pooled buffers are cleared.
This changes precision and can change generated tokens. Benchmark it as a separate
experiment, recording output length/text and never promising identical answers.

AIKERNEL_CPU_GEMV accepts the original scalar/avx/avx2/avx512/auto settings plus:

These choices are also accepted by the per-model `instructionSet` constructor argument.
Omitting the argument retains automatic selection when no environment override is set.
`avx2-int8-q8` uses native VPMADDUBSW/VPMADDWD with unsigned 0/1 weights and
signed Q8 activations, followed by `2*dot - sum(activation)` per 128-element block.
The pair sum is bounded by ±254, so VPMADDUBSW does not saturate. This requires
the updated optional CPU library; `avx2-q8` remains the rounded-FP32 control.

| Setting | Q1_0 matrix arithmetic |
| --- | --- |
| scalar-q8 | Independent signed integer dot reference, then block scaling |
| avx-q8, avx2-q8, avx512-q8 | Common Q8 rounding, restored to FP32 for the selected vector kernel |
| avx-vnni-q8 | 256-bit VEX VNNI unsigned-byte x signed-byte integer dot |
| avx512-vnni-q8 | 512-bit VNNI integer dot |
| amx-int8-q8 | AMX signed INT8 tile dot (explicit opt-in; real hardware validation pending) |
| q8-auto | Best available initial Q8 path, with phase/thread tuning when TuneCpuAsync is called |

CpuInt8Acceleration.AvailableBackends reports actual usable candidates. VNNI and AMX
need the optional aikernel_cpu_accel native library. Original FP32 CPU inference needs
neither this library nor CUDA. q8-auto excludes AMX until an AMX host has independently
validated it. Tuning uses a separate precision identity/cache and a Q8 reference, so
FP32 and Q8 timings/caches are never silently mixed. Current prefill is repeated GEMV,
not batched GEMM; no general AMX or VNNI speedup is assumed.

For CUDA, the new six-argument Qwen3GpuModel constructor takes quantizeActivations.
Qwen3VulkanModel has the same optional boolean. When true, both use the identical Q8
rounding, restore those values to FP32, and run the existing FP32 matrix arithmetic.
ActivationPrecision reports q8-block128-fp32-accumulate. This does NOT use GPU INT8
instructions or Tensor Cores. It is a comparable activation-precision experiment,
not an INT8 GPU throughput benchmark. FP32-weight tensors keep their original path.

## ISA and operating-system guards

AVX-VNNI uses a byte-to-eight-bits lookup table; AVX-512 VNNI expands 64 packed
bits through VPMOVM2B and AND 1. Both retain unsigned 0/1 weights for VPDPBUSD and
apply 2*dot-sum(input); input sums are computed once per activation block. The
AVX-512 variant groups four output rows and keeps the two half-block accumulators
independent. Block scales are applied before summing blocks.

Native dispatch is compiled for baseline x64; VNNI translation units are separately
compiled for AVX2 or AVX-512. CPU CPUID and OS XSTATE checks run before dispatch. The 512-bit path requires
AVX-512F/BW/DQ/VL as well as VNNI, matching its compiler target.
AMX additionally requires AMX-TILE and AMX-INT8 plus OS tile-state support and an
explicit permission request on the executing worker. Linux uses arch_prctl;
Windows resolves optional XSTATE APIs dynamically. Unsupported hosts return a status
before any tile instruction. No root or privileged operation is attempted.

The user-provided AMX target is a Xeon 4400+ environment running Rocky Linux 8.10.
The exact SKU and kernel feature backports have not been probed on that machine.
Do not enable tiles based on the distribution name or kernel version string alone;
the CPUID, XCR0 and arch_prctl support/permission checks remain authoritative.
The Linux shared library loads on the separate Rocky 8.10 Server used for VNNI,
which does not validate AMX execution on the Xeon target.

AMX supports the INT8 tile kernel in this change. Windows/Linux compilation and
unsupported-CPU rejection can be verified here; real AMX execution was explicitly
excluded from the requested hardware testing. Do not describe it as performance-
validated, or claim AMX-BF16 support from this INT8 implementation.

Primary API references:
- [Linux XSTATE permissions](https://docs.kernel.org/arch/x86/xstate.html)
- [Windows optional XSTATE](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-enableprocessoptionalxstatefeatures)
- [Khronos compute shaders](https://docs.vulkan.org/guide/latest/compute_shaders.html)

## Build and packaging

Build native/cpu with CMake (x64 Windows/MSVC or Linux/GCC with AVX-VNNI and AMX
intrinsics support). No global -march=native or global /arch is permitted. On Linux,
verify the shared object's GLIBC symbol requirements against the target OS. The
initial GCC 13 build requires only GLIBC 2.2.5/2.3/2.4 and was loaded on Rocky 8.10.

Release staging supplies AIKernelCpuNativeRoot with win-x64/aikernel_cpu_accel.dll
and linux-x64/libaikernel_cpu_accel.so, alongside the existing AIKernelCudaNativeRoot.
Kernel packages include both native CPU libraries, Silk.NET dependencies, and the
embedded shader. A 0.1.4 package without either CPU native asset fails packaging.
The older verified v4 distribution does not contain these new features; it must not
be described as verifying this change.

## Validation and benchmark contract

AIKernel.Accelerator.Validation provides inventory, matrix and generation modes.
Matrix mode checks actual GGUF subsets, tails, unaligned tensor ranges, FP32 and Q8-rounded vectors,
cancellation and disposal. GPU results are compared against an independent scalar
reference; native integer results against the Q8 integer reference. Core unit tests
also check quantization error bounds, zero/subnormal/nonfinite input and CPU tuning.

Generation mode records the model/assembly/native hashes, actual selected device,
precision, complete output, token counts, and five timings after a full warmup.
Use the same 4 physical CPU cores, prompt, 1024-token context and 96-token upper bound
as the earlier experiment. Keep all samples. Separate FP32 and Q8 charts, and label
shared-host load uncertainty and display-active GPUs. A matching short answer is
not a proof of numerical correctness for every input or of answer quality.

The benchmark harness accepts AIKERNEL_BENCH_CPU_WORKERS=1..4. Worker studies keep
the same four physical cores and DOTNET_PROCESSOR_COUNT=4, changing only the engine
worker ceiling. OS/runtime/driver helper threads are distinct from active engine
workers; their existence alone is not evidence of CPU oversubscription.
