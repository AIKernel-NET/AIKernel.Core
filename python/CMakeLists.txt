cmake_minimum_required(VERSION 3.24)

project(aikernel_python LANGUAGES NONE)

option(AIKERNEL_PYTHON_INCLUDE_MANAGED "Include AIKernel managed assemblies" ON)
set(AIKERNEL_MANAGED_CONFIGURATION "Release" CACHE STRING "AIKernel managed build configuration")
set(AIKERNEL_MANAGED_TFM "net10.0" CACHE STRING "AIKernel managed target framework")

set(AIKERNEL_REPO_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")

if(AIKERNEL_PYTHON_INCLUDE_MANAGED)
  find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

  set(AIKERNEL_MANAGED_PUBLISH_ROOT "${CMAKE_BINARY_DIR}/aikernel-managed")
  set(AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR "${AIKERNEL_MANAGED_PUBLISH_ROOT}/kernel")
  add_custom_target(aikernel_managed ALL
    COMMAND "${DOTNET_EXECUTABLE}" publish
      "${AIKERNEL_REPO_ROOT}/src/AIKernel.Kernel/AIKernel.Kernel.csproj"
      -c "${AIKERNEL_MANAGED_CONFIGURATION}"
      -f "${AIKERNEL_MANAGED_TFM}"
      -o "${AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR}"
    WORKING_DIRECTORY "${AIKERNEL_REPO_ROOT}"
    COMMENT "Building AIKernel managed assemblies for AIKernel.Python")

  set(AIKERNEL_MANAGED_INSTALL_DIRS
    "${AIKERNEL_MANAGED_KERNEL_PUBLISH_DIR}/")

  install(DIRECTORY
    ${AIKERNEL_MANAGED_INSTALL_DIRS}
    DESTINATION aikernel/managed
    FILES_MATCHING
      PATTERN "*.dll"
      PATTERN "*.deps.json")
endif()
