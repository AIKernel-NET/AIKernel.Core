cmake_minimum_required(VERSION 3.24)
project(aikernel_cuda_driver LANGUAGES C)

if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8 OR NOT CMAKE_SYSTEM_PROCESSOR MATCHES "^(AMD64|amd64|x86_64)$")
    message(FATAL_ERROR "The CUDA Driver ABI is built for Windows or Linux x86-64.")
endif()
if(NOT WIN32 AND NOT CMAKE_SYSTEM_NAME STREQUAL "Linux")
    message(FATAL_ERROR "The CUDA Driver ABI is built for Windows or Linux.")
endif()

set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS src/kernels.ptx)
file(READ "${CMAKE_CURRENT_SOURCE_DIR}/src/kernels.ptx" PTX_LITERAL)
string(REPLACE "\\" "\\\\" PTX_LITERAL "${PTX_LITERAL}")
string(REPLACE "\"" "\\\"" PTX_LITERAL "${PTX_LITERAL}")
string(REPLACE "\n" "\\n\"\n\"" PTX_LITERAL "${PTX_LITERAL}")
configure_file(src/embedded_ptx.h.in embedded_ptx.h @ONLY)

add_library(aikernel_cuda_driver SHARED src/aikernel_cuda.c src/driver.c src/cpu_gemv.c
    src/cpu_dot_avx.c src/cpu_dot_avx2.c src/cpu_dot_avx512.c)
target_include_directories(aikernel_cuda_driver PRIVATE "${CMAKE_CURRENT_BINARY_DIR}")
set_target_properties(aikernel_cuda_driver PROPERTIES C_STANDARD 11 C_STANDARD_REQUIRED YES
    C_VISIBILITY_PRESET hidden MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
if(MSVC)
    target_compile_options(aikernel_cuda_driver PRIVATE /W4 /WX)
    # Never apply /arch to the dispatcher or the whole DLL.
    set_source_files_properties(src/cpu_dot_avx.c PROPERTIES COMPILE_OPTIONS /arch:AVX)
    set_source_files_properties(src/cpu_dot_avx2.c PROPERTIES COMPILE_OPTIONS /arch:AVX2)
    set_source_files_properties(src/cpu_dot_avx512.c PROPERTIES COMPILE_OPTIONS /arch:AVX512)
else()
    target_compile_options(aikernel_cuda_driver PRIVATE -Wall -Wextra -Werror)
    target_link_libraries(aikernel_cuda_driver PRIVATE ${CMAKE_DL_LIBS} m)
endif()
install(TARGETS aikernel_cuda_driver RUNTIME DESTINATION aikernel_cuda_driver LIBRARY DESTINATION aikernel_cuda_driver)
