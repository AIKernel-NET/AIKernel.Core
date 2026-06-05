#include "libtorch_bridge.h"

#include <torch/script.h>
#include <torch/torch.h>

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <iterator>
#include <limits>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace {

constexpr int32_t kSuccess = 0;
constexpr int32_t kInvalidArgument = -1;
constexpr int32_t kModelNotFound = -2;
constexpr int32_t kLoadFailed = -3;
constexpr int32_t kForwardFailed = -4;
constexpr int32_t kMaxOutputTokens = 64;
constexpr int32_t kMaxLogits = 4096;

struct MemoryRegion {
  explicit MemoryRegion(std::string model_path) : path(std::move(model_path)) {}

  std::string path;
  const void* pointer = nullptr;
  size_t length = 0;
  // Reserved for a future mmap / CreateFileMapping backed implementation.
};

struct VmRegion {
  explicit VmRegion(std::string model_path) : memory(std::move(model_path)) {}

  MemoryRegion memory;
};

struct Session {
  torch::Device device;
  std::shared_ptr<torch::jit::script::Module> module;
  std::shared_ptr<VmRegion> region;
};

struct Tensor {
  torch::Tensor value;
};

std::mutex g_registry_mutex;
std::unordered_map<int32_t, std::shared_ptr<Session>> g_sessions;
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

std::shared_ptr<Session> create_session(const char* path) {
  auto region = std::make_shared<VmRegion>(std::string(path));
  auto device = select_device();
  auto module = std::make_shared<torch::jit::script::Module>(
      torch::jit::load(region->memory.path, device));
  module->eval();

  return std::make_shared<Session>(
      Session{device, std::move(module), std::move(region)});
}

Tensor create_input_tensor(
    const int32_t* input_ids,
    int32_t length,
    const torch::Device& device) {
  std::vector<int64_t> tokens;
  tokens.reserve(static_cast<size_t>(length));

  for (int32_t i = 0; i < length; ++i) {
    tokens.push_back(static_cast<int64_t>(input_ids[i]));
  }

  return Tensor{
      torch::from_blob(tokens.data(), {1, length}, torch::kInt64)
          .clone()
          .to(device)};
}

Tensor execute_forward(
    const Session& session,
    const Tensor& input) {
  std::vector<torch::jit::IValue> inputs;
  inputs.emplace_back(input.value);

  return Tensor{session.module->forward(inputs).toTensor()};
}

int32_t copy_logits_to_result(
    const Tensor& output,
    ForwardResultNative* out_result) {
  auto logits = output.value
      .reshape({-1})
      .to(torch::kCPU)
      .to(torch::kFloat32)
      .contiguous();

  const auto total_elements = logits.numel();
  if (total_elements <= 0) {
    out_result->status = kForwardFailed;
    return kForwardFailed;
  }

  const auto capped_total = std::min<int64_t>(
      total_elements,
      static_cast<int64_t>(std::numeric_limits<int32_t>::max()));
  const auto total_logits = static_cast<int32_t>(capped_total);
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
}

}  // namespace

extern "C" {

AIKERNEL_EXPORT int32_t load_model(const char* path) {
  if (path == nullptr || std::strlen(path) == 0) {
    return kInvalidArgument;
  }

  try {
    auto session = create_session(path);

    std::lock_guard<std::mutex> lock(g_registry_mutex);
    if (g_next_handle == std::numeric_limits<int32_t>::max()) {
      return kLoadFailed;
    }

    const auto handle = g_next_handle++;
    g_sessions.emplace(handle, std::move(session));

    return handle;
  } catch (...) {
    return kLoadFailed;
  }
}

AIKERNEL_EXPORT int32_t unload_model(int32_t handle) {
  if (handle <= 0) {
    return kInvalidArgument;
  }

  std::lock_guard<std::mutex> lock(g_registry_mutex);
  return g_sessions.erase(handle) == 0 ? kModelNotFound : kSuccess;
}

AIKERNEL_EXPORT int32_t forward(
    int32_t handle,
    const int32_t* input_ids,
    int32_t length,
    ForwardResultNative* out_result) {
  zero_result(out_result);

  if (out_result == nullptr || input_ids == nullptr || length <= 0 || handle <= 0) {
    return kInvalidArgument;
  }

  std::shared_ptr<Session> session;

  {
    std::lock_guard<std::mutex> lock(g_registry_mutex);
    const auto stored_session = g_sessions.find(handle);
    if (stored_session == g_sessions.end()) {
      out_result->status = kModelNotFound;
      return kModelNotFound;
    }

    session = stored_session->second;
  }

  try {
    const auto input = create_input_tensor(input_ids, length, session->device);
    const auto output = execute_forward(*session, input);
    return copy_logits_to_result(output, out_result);
  } catch (...) {
    out_result->status = kForwardFailed;
    return kForwardFailed;
  }
}

}  // extern "C"
