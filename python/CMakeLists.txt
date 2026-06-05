cmake_minimum_required(VERSION 3.24)

project(aikernel_python_native LANGUAGES CXX)

option(AIKERNEL_PYTHON_BUILD_NATIVE "Build AIKernel Native ABI bridge" ON)
option(AIKERNEL_PYTHON_INCLUDE_MANAGED "Include AIKernel managed assemblies" ON)
set(AIKERNEL_MANAGED_CONFIGURATION "Release" CACHE STRING "AIKernel managed build configuration")
set(AIKERNEL_MANAGED_TFM "net10.0" CACHE STRING "AIKernel managed target framework")

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT WIN32)
  message(FATAL_ERROR "AIKernel.Python native bridge currently supports Windows/MSVC only.")
endif()

if(NOT MSVC)
  message(FATAL_ERROR "AIKernel.Python native bridge currently requires MSVC.")
endif()

set(AIKERNEL_REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")
set(AIKERNEL_NATIVE_ROOT "${AIKERNEL_REPO_ROOT}/src/AIKernel.Cuda.Libtorch.2.12-cuda13.0/Native")

if(AIKERNEL_PYTHON_INCLUDE_MANAGED)
  find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

  set(AIKERNEL_MANAGED_PUBLISH_ROOT "${CMAKE_BINARY_DIR}/aikernel-managed")
  set(AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR "${AIKERNEL_MANAGED_PUBLISH_ROOT}/kernel")
  set(AIKERNEL_MANAGED_CAPABILITY_PUBLISH_DIR "${AIKERNEL_MANAGED_PUBLISH_ROOT}/capability")

  add_custom_target(aikernel_managed ALL
    COMMAND "${DOTNET_EXECUTABLE}" publish
      "${AIKERNEL_REPO_ROOT}/src/AIKernel.Kernel/AIKernel.Kernel.csproj"
      -c "${AIKERNEL_MANAGED_CONFIGURATION}"
      -f "${AIKERNEL_MANAGED_TFM}"
      -o "${AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR}"
    COMMAND "${DOTNET_EXECUTABLE}" publish
      "${AIKERNEL_REPO_ROOT}/src/AIKernel.Cuda.Libtorch.2.12-cuda13.0/AIKernel.Cuda.Libtorch.2.12-cuda13.0.csproj"
      -c "${AIKERNEL_MANAGED_CONFIGURATION}"
      -f "${AIKERNEL_MANAGED_TFM}"
      -o "${AIKERNEL_MANAGED_CAPABILITY_PUBLISH_DIR}"
    WORKING_DIRECTORY "${AIKERNEL_REPO_ROOT}"
    COMMENT "Building AIKernel managed assemblies for AIKernel.Python")

  install(DIRECTORY
    "${AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR}/"
    "${AIKERNEL_MANAGED_CAPABILITY_PUBLISH_DIR}/"
    DESTINATION aikernel/managed
    FILES_MATCHING
      PATTERN "*.dll"
      PATTERN "*.deps.json")
endif()

if(AIKERNEL_PYTHON_BUILD_NATIVE)
  if(DEFINED ENV{AIKERNEL_LIBTORCH_PATH})
    file(TO_CMAKE_PATH "$ENV{AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
  elseif(DEFINED AIKERNEL_LIBTORCH_PATH)
    file(TO_CMAKE_PATH "${AIKERNEL_LIBTORCH_PATH}" TORCH_PATH)
  elseif(WIN32 AND EXISTS "${AIKERNEL_REPO_ROOT}/ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch/include")
    set(TORCH_PATH "${AIKERNEL_REPO_ROOT}/ref/libtorch-win-shared-with-deps-2.12.0+cu130/libtorch")
  elseif(WIN32 AND EXISTS "${AIKERNEL_REPO_ROOT}/src/AIKernel.Cuda.Libtorch.2.12-cuda13.0/Runtime/win-x64/libtorch/include")
    set(TORCH_PATH "${AIKERNEL_REPO_ROOT}/src/AIKernel.Cuda.Libtorch.2.12-cuda13.0/Runtime/win-x64/libtorch")
  else()
    message(FATAL_ERROR
      "AIKERNEL_LIBTORCH_PATH must point to a LibTorch 2.12.0 + CUDA 13.0 distribution. "
      "PyPI publishing is intentionally disabled; install from GitHub with the required native toolchain.")
  endif()

  message(STATUS "Using LibTorch path: ${TORCH_PATH}")

  set(AIKERNEL_ENV_FILE "${AIKERNEL_REPO_ROOT}/ref/env.txt")
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
      "CUDA Toolkit 13.0 is required for AIKernel.Python native install. "
      "Install CUDA 13.0 or set CUDA_PATH / CUDAToolkit_ROOT before pip install.")
  endif()

  add_library(libtorch_bridge SHARED
    "${AIKERNEL_NATIVE_ROOT}/libtorch_bridge.cpp"
    "${AIKERNEL_NATIVE_ROOT}/libtorch_bridge.h")

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

  target_compile_options(libtorch_bridge PRIVATE
    /W4
    /permissive-
    /wd4251
    /wd4275
    /wd4267
    /wd4100
    /wd4702)

  install(TARGETS libtorch_bridge
    RUNTIME DESTINATION aikernel/native
    LIBRARY DESTINATION aikernel/native
    ARCHIVE DESTINATION aikernel/native)
endif()
