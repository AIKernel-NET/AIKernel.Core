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
