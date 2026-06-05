#include "libtorch_bridge.h"

#include <torch/script.h>
#include <torch/torch.h>

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

namespace {

constexpr int32_t kSuccess = 0;
constexpr int32_t kInvalidArgument = -1;
constexpr int32_t kModelNotFound = -2;
constexpr int32_t kLoadFailed = -3;
constexpr int32_t kForwardFailed = -4;
constexpr int32_t kMaxOutputTokens = 64;
constexpr int32_t kMaxLogits = 4096;

std::mutex g_registry_mutex;
std::unordered_map<int32_t, torch::jit::script::Module> g_models;
int32_t g_next_handle = 1;

void zero_result(ForwardResultNative* result) {
  if (result == nullptr) {
    return;
  }

  result->status = kInvalidArgument;
  result->output_token_count = 0;
  std::fill(std::begin(result->output_token_ids), std::end(result->output_token_ids), 0);
  result->logit_count = 0;
  std::fill(std::begin(result->logits), std::end(result->logits), 0.0f);
}

torch::Device select_device() {
  if (torch::cuda::is_available()) {
    return torch::Device(torch::kCUDA);
  }

  return torch::Device(torch::kCPU);
}

}  // namespace

extern "C" {

AIKERNEL_EXPORT int32_t load_model(const char* path) {
  if (path == nullptr || std::strlen(path) == 0) {
    return kInvalidArgument;
  }

  try {
    auto module = torch::jit::load(std::string(path), select_device());
    module.eval();

    std::lock_guard<std::mutex> lock(g_registry_mutex);
    const auto handle = g_next_handle++;
    g_models.emplace(handle, std::move(module));

    return handle;
  } catch (...) {
    return kLoadFailed;
  }
}

AIKERNEL_EXPORT int32_t unload_model(int32_t handle) {
  std::lock_guard<std::mutex> lock(g_registry_mutex);
  return g_models.erase(handle) == 0 ? kModelNotFound : kSuccess;
}

AIKERNEL_EXPORT int32_t forward(
    int32_t handle,
    const int32_t* input_ids,
    int32_t length,
    ForwardResultNative* out_result) {
  zero_result(out_result);

  if (out_result == nullptr || input_ids == nullptr || length <= 0) {
    return kInvalidArgument;
  }

  torch::jit::script::Module module;

  {
    std::lock_guard<std::mutex> lock(g_registry_mutex);
    const auto model = g_models.find(handle);
    if (model == g_models.end()) {
      out_result->status = kModelNotFound;
      return kModelNotFound;
    }

    module = model->second;
  }

  try {
    const auto device = select_device();
    std::vector<int64_t> tokens;
    tokens.reserve(static_cast<size_t>(length));

    for (int32_t i = 0; i < length; ++i) {
      tokens.push_back(static_cast<int64_t>(input_ids[i]));
    }

    auto input = torch::from_blob(tokens.data(), {1, length}, torch::kInt64)
                     .clone()
                     .to(device);

    std::vector<torch::jit::IValue> inputs;
    inputs.emplace_back(input);

    auto output = module.forward(inputs).toTensor();
    auto logits = output.reshape({-1}).to(torch::kCPU).contiguous();

    const auto total_logits = static_cast<int32_t>(logits.numel());
    const auto logit_count = std::min(total_logits, kMaxLogits);
    const float* logits_data = logits.data_ptr<float>();

    out_result->status = kSuccess;
    out_result->logit_count = logit_count;

    for (int32_t i = 0; i < logit_count; ++i) {
      out_result->logits[i] = logits_data[i];
    }

    const auto token_count = std::min(kMaxOutputTokens, logit_count);
    out_result->output_token_count = token_count > 0 ? 1 : 0;

    if (token_count > 0) {
      auto top_index = std::max_element(logits_data, logits_data + logit_count) - logits_data;
      out_result->output_token_ids[0] = static_cast<int32_t>(top_index);
    }

    return kSuccess;
  } catch (...) {
    out_result->status = kForwardFailed;
    return kForwardFailed;
  }
}

}  // extern "C"
