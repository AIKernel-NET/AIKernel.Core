"""Run the pinned Transformers Gemma 4 graph on the same dequantized GGUF weights.

Validation only: PyTorch/Transformers/gguf are not AIKernel runtime dependencies.
Large embedding tables stay encoded; only requested rows are dequantized. All
other parameters use gguf-py's independent decoder and the upstream FP32 graph.
"""
import argparse
import hashlib
import inspect
import json
from pathlib import Path
import time
import numpy as np
import torch
from torch import nn
from gguf import GGUFReader
from gguf.quants import dequantize
from transformers.models.gemma4.configuration_gemma4 import Gemma4TextConfig
from transformers.models.gemma4.modeling_gemma4 import Gemma4TextModel, Gemma4TextRotaryEmbedding


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path)
    parser.add_argument("assets", type=Path, help="Pinned Google config.json and provenance")
    parser.add_argument("input", type=Path, help="Prompt IDs, optional teacher IDs, steps and tracePositions")
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    provenance = json.loads((args.assets / "reference-provenance.json").read_text())
    checks = {"config.json": args.assets / "config.json", "modeling_gemma4.py": Path(inspect.getfile(Gemma4TextModel)),
              "configuration_gemma4.py": Path(inspect.getfile(Gemma4TextConfig))}
    for name, path in checks.items():
        if hashlib.sha256(path.read_bytes()).hexdigest() != provenance["files"][name]["sha256"]:
            raise ValueError(f"Reference source differs from pinned revision: {name}")
    torch.set_num_threads(4)
    torch.set_num_interop_threads(1)
    torch.set_grad_enabled(False)
    torch.use_deterministic_algorithms(True)
    cfg = Gemma4TextConfig(**json.loads((args.assets / "config.json").read_text())["text_config"])
    cfg._attn_implementation = "eager"
    cfg.dtype = torch.float32
    reader = GGUFReader(str(args.model))
    tensors = {t.name: t for t in reader.tensors}

    def weight(name):
        tensor = tensors[name]
        return torch.from_numpy(dequantize(tensor.data, tensor.tensor_type).copy())

    class MappedEmbedding(nn.Module):
        def __init__(self, name, scale):
            super().__init__()
            self.tensor = tensors[name]
            self.scale = scale

        def forward(self, ids):
            rows = [dequantize(self.tensor.data[int(i):int(i)+1], self.tensor.tensor_type) for i in ids.flatten()]
            result = np.concatenate(rows).reshape(*ids.shape, int(self.tensor.shape[0]))
            return torch.from_numpy(result.copy()) * self.scale

    with torch.device("meta"):
        model = Gemma4TextModel(cfg)
    model.embed_tokens = MappedEmbedding("token_embd.weight", cfg.hidden_size**.5)
    model.embed_tokens_per_layer = MappedEmbedding("per_layer_token_embd.weight", cfg.hidden_size_per_layer_input**.5)
    model.rotary_emb = Gemma4TextRotaryEmbedding(cfg, device="cpu")
    names = {
        "input_layernorm.weight": "attn_norm.weight", "post_attention_layernorm.weight": "post_attention_norm.weight",
        "pre_feedforward_layernorm.weight": "ffn_norm.weight", "post_feedforward_layernorm.weight": "post_ffw_norm.weight",
        "self_attn.q_proj.weight": "attn_q.weight", "self_attn.k_proj.weight": "attn_k.weight",
        "self_attn.v_proj.weight": "attn_v.weight", "self_attn.o_proj.weight": "attn_output.weight",
        "self_attn.q_norm.weight": "attn_q_norm.weight", "self_attn.k_norm.weight": "attn_k_norm.weight",
        "mlp.gate_proj.weight": "ffn_gate.weight", "mlp.up_proj.weight": "ffn_up.weight", "mlp.down_proj.weight": "ffn_down.weight",
        "per_layer_input_gate.weight": "inp_gate.weight", "per_layer_projection.weight": "proj.weight",
        "post_per_layer_input_norm.weight": "post_norm.weight",
    }
    roots = {"norm.weight": "output_norm.weight", "per_layer_model_projection.weight": "per_layer_model_proj.weight",
             "per_layer_projection_norm.weight": "per_layer_proj_norm.weight"}
    used = {"token_embd.weight", "per_layer_token_embd.weight", "rope_freqs.weight"}
    for name, param in list(model.named_parameters()):
        if name.startswith("layers."):
            _, layer, suffix = name.split(".", 2)
            source = f"blk.{layer}.{names[suffix]}"
        else:
            source = roots[name]
        value = weight(source)
        if value.shape != param.shape:
            raise ValueError(f"Wrong parameter shape: {name}: {value.shape} != {param.shape}")
        parent, field = name.rsplit(".", 1)
        setattr(model.get_submodule(parent), field, nn.Parameter(value, requires_grad=False))
        used.add(source)
    for i, layer in enumerate(model.layers):
        name = f"blk.{i}.layer_output_scale.weight"
        layer.layer_scalar = weight(name)
        used.add(name)
    if used != tensors.keys():
        raise ValueError(f"Unmapped weights: {tensors.keys()-used}")
    if any(t.is_meta for t in list(model.parameters()) + list(model.buffers())):
        raise ValueError("Uninitialized reference state")
    model.eval()
    print("Official graph loaded with independent GGUF decode", flush=True)

    data = json.loads(args.input.read_text())
    prompt = data["prompt"]
    teacher = data.get("teacher", [])
    trace_positions = set(data.get("tracePositions", []))
    current_positions = list(range(len(prompt)))

    def capture(layer, stage, tensor):
        for j, position in enumerate(current_positions):
            if position in trace_positions:
                array = tensor.detach().float().cpu().numpy()[0, j].reshape(-1)
                array.astype("<f4").tofile(args.output / f"p{position}_{layer}_{stage}.f32")

    model.embed_tokens.register_forward_hook(lambda m, a, out: capture(-1, "embedding", out))
    original_projection = model.project_per_layer_inputs

    def projection(*a, **kw):
        out = original_projection(*a, **kw)
        capture(-1, "ple", out)
        return out

    model.project_per_layer_inputs = projection
    model.norm.register_forward_hook(lambda m, a, out: capture(-1, "final_norm", out))
    for i, layer in enumerate(model.layers):
        layer.pre_feedforward_layernorm.register_forward_pre_hook(lambda m, a, i=i: capture(i, "attention", a[0]))
        layer.per_layer_input_gate.register_forward_pre_hook(lambda m, a, i=i: capture(i, "ffn", a[0]))
        layer.register_forward_hook(lambda m, a, out, i=i: capture(i, "layer", out))

    # Output projection is tied to the token embedding. Decode bounded chunks;
    # never expand the much larger per-layer embedding table.
    def logits(hidden):
        tensor = tensors["token_embd.weight"]
        chunks = []
        for start in range(0, cfg.vocab_size, 8192):
            matrix = torch.from_numpy(dequantize(tensor.data[start:start+8192], tensor.tensor_type).copy())
            chunks.append(torch.nn.functional.linear(hidden, matrix))
        result = torch.cat(chunks, dim=-1)
        return torch.tanh(result / cfg.final_logit_softcapping) * cfg.final_logit_softcapping

    start = time.monotonic()
    out = model(input_ids=torch.tensor([prompt]), use_cache=True, logits_to_keep=0)
    generated = []
    for step in range(data.get("steps", 1)):
        values = logits(out.last_hidden_state[:, -1:, :])
        values.flatten().numpy().astype("<f4").tofile(args.output / f"logits-{step}.f32")
        generated.append(int(values.argmax()))
        print(f"step {step}: {generated[-1]}", flush=True)
        if step + 1 < data.get("steps", 1):
            current_positions = [len(prompt) + step]
            token = teacher[step] if step < len(teacher) else generated[-1]
            out = model(input_ids=torch.tensor([[token]]), cache=out.cache, use_cache=True, logits_to_keep=0)
    result = {"tokens": generated, "elapsedSeconds": time.monotonic()-start,
              "torch": torch.__version__, "reference": "Transformers Gemma4TextModel/eager/fp32",
              "sameQuantizedWeights": True, "originalBfloat16CheckpointCompared": False}
    (args.output / "result.json").write_text(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
