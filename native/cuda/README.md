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
changes do not yet add quantized GEMV, VNNI, or BF16 kernels.

Linux packages for XServer require glibc 2.28 compatibility. Build in the
manylinux_2_28 environment, then test that exact library on both AVX2 and
AVX-512 hosts. Do not distribute a library built against the development
machine's newer glibc without checking its symbol requirements.
