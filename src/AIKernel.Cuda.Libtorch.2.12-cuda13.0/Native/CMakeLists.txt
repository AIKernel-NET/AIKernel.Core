cmake_minimum_required(VERSION 3.24)

project(aikernel_libtorch_bridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT WIN32)
  message(FATAL_ERROR "AIKernel.Cuda.Libtorch.2.12-cuda13.0 currently supports Windows/MSVC only.")
endif()

set(AIKERNEL_PLATFORM "win-x64")

if(DEFINED ENV{AIKERNEL_LIBTORCH_PATH})
  file(TO_CMAKE_PATH "$ENV{AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
elseif(DEFINED AIKERNEL_LIBTORCH_PATH)
  file(TO_CMAKE_PATH "${AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
elseif(EXISTS "${CMAKE_SOURCE_DIR}/../Runtime/${AIKERNEL_PLATFORM}/libtorch/include")
  set(TORCH_PATH "${CMAKE_SOURCE_DIR}/../Runtime/${AIKERNEL_PLATFORM}/libtorch")
else()
  set(TORCH_PATH "${CMAKE_SOURCE_DIR}/../../../../ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch")
endif()

message(STATUS "Using LibTorch path: ${TORCH_PATH}")

set(AIKERNEL_ENV_FILE "${CMAKE_SOURCE_DIR}/../../../../ref/env.txt")
if(EXISTS "${AIKERNEL_ENV_FILE}")
  file(STRINGS "${AIKERNEL_ENV_FILE}" AIKERNEL_ENV_LINES)
  foreach(AIKERNEL_ENV_LINE IN LISTS AIKERNEL_ENV_LINES)
    if(AIKERNEL_ENV_LINE MATCHES "^CUDA_PATH=(.*)$")
      file(TO_CMAKE_PATH "${CMAKE_MATCH_1}" AIKERNEL_CUDA_PATH)
      if(AIKERNEL_CUDA_PATH MATCHES "/bin$")
        get_filename_component(CUDAToolkit_ROOT "${AIKERNEL_CUDA_PATH}" DIRECTORY)
      else()
        set(CUDAToolkit_ROOT "${AIKERNEL_CUDA_PATH}")
      endif()
    endif()
  endforeach()
endif()

find_package(CUDAToolkit QUIET)

if(NOT CUDAToolkit_FOUND)
  message(FATAL_ERROR
    "CUDA Toolkit 13.0 is required for AIKernel.Cuda.Libtorch.2.12-cuda13.0. "
    "Install CUDA 13.0 or set CUDA_PATH / CUDAToolkit_ROOT before configuring.")
endif()

add_library(libtorch_bridge SHARED
  libtorch_bridge.cpp
  libtorch_bridge.h)

target_include_directories(libtorch_bridge SYSTEM PRIVATE
  "${TORCH_PATH}/include"
  "${TORCH_PATH}/include/torch/csrc/api/include")

target_link_directories(libtorch_bridge PRIVATE "${TORCH_PATH}/lib")

target_link_libraries(libtorch_bridge PRIVATE
  torch
  torch_cpu
  torch_cuda
  c10
  c10_cuda)

target_compile_definitions(libtorch_bridge PRIVATE AIKERNEL_LIBTORCH_BRIDGE_EXPORTS)

if(NOT MSVC)
  message(FATAL_ERROR "AIKernel LibTorch bridge currently requires MSVC.")
endif()

target_compile_options(libtorch_bridge PRIVATE
  /W4
  /permissive-
  /wd4251
  /wd4275
  /wd4267
  /wd4100
  /wd4702)
