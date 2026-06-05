cmake_minimum_required(VERSION 3.24)

project(aikernel_libtorch_bridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(WIN32)
  set(AIKERNEL_PLATFORM "win-x64")
elseif(UNIX AND NOT APPLE)
  set(AIKERNEL_PLATFORM "linux-x64")
else()
  message(FATAL_ERROR "AIKernel LibTorch bridge supports Windows and Linux only.")
endif()

if(DEFINED ENV{AIKERNEL_LIBTORCH_PATH})
  file(TO_CMAKE_PATH "$ENV{AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
elseif(DEFINED AIKERNEL_LIBTORCH_PATH)
  file(TO_CMAKE_PATH "${AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
elseif(EXISTS "${CMAKE_SOURCE_DIR}/../Runtime/${AIKERNEL_PLATFORM}/libtorch/include")
  set(TORCH_PATH "${CMAKE_SOURCE_DIR}/../Runtime/${AIKERNEL_PLATFORM}/libtorch")
elseif(WIN32)
  set(TORCH_PATH "${CMAKE_SOURCE_DIR}/../../../../ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch")
else()
  set(TORCH_PATH "${CMAKE_SOURCE_DIR}/../../../../ref/libtorch-shared-with-deps-2.12.0+cu130/libtorch")
endif()

message(STATUS "Using LibTorch path: ${TORCH_PATH}")

find_package(CUDAToolkit QUIET)

if(NOT CUDAToolkit_FOUND)
  message(FATAL_ERROR
    "CUDA Toolkit 13.0 is required for AIKernel.Cuda.Libtorch.2.12-cuda13.0. "
    "Install CUDA 13.0 or set CUDA_PATH / CUDAToolkit_ROOT before configuring.")
endif()

include_directories("${TORCH_PATH}/include")
include_directories("${TORCH_PATH}/include/torch/csrc/api/include")
link_directories("${TORCH_PATH}/lib")

find_package(Torch REQUIRED PATHS "${TORCH_PATH}" NO_DEFAULT_PATH)

add_library(libtorch_bridge SHARED
  libtorch_bridge.cpp
  libtorch_bridge.h)

target_link_libraries(libtorch_bridge PRIVATE "${TORCH_LIBRARIES}")
target_compile_definitions(libtorch_bridge PRIVATE AIKERNEL_LIBTORCH_BRIDGE_EXPORTS)

if(MSVC)
  target_compile_options(libtorch_bridge PRIVATE /W4 /permissive-)
else()
  target_compile_options(libtorch_bridge PRIVATE -Wall -Wextra -Wpedantic)
endif()
