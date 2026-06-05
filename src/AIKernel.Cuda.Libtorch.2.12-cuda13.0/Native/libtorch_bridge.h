#pragma once

#include <stdint.h>

#if defined(_WIN32)
#if defined(AIKERNEL_LIBTORCH_BRIDGE_EXPORTS)
#define AIKERNEL_EXPORT __declspec(dllexport)
#else
#define AIKERNEL_EXPORT __declspec(dllimport)
#endif
#else
#define AIKERNEL_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct ForwardResultNative {
  int32_t status;
  int32_t output_token_count;
  int32_t output_token_ids[64];
  int32_t logit_count;
  float logits[4096];
} ForwardResultNative;

AIKERNEL_EXPORT int32_t load_model(const char* path);
AIKERNEL_EXPORT int32_t unload_model(int32_t handle);
AIKERNEL_EXPORT int32_t forward(
    int32_t handle,
    const int32_t* input_ids,
    int32_t length,
    ForwardResultNative* out_result);

#ifdef __cplusplus
}
#endif
