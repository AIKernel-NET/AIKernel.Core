"""Official Transformers Cosmos3 graph, small independent deterministic fixtures.
Weights are synthetic: this tests architecture, not real model answer quality.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import torch
import transformers
from safetensors.torch import save_file
from transformers import AutoConfig, AutoModelForImageTextToText
from transformers import Cosmos3EdgeImageProcessor, Cosmos3EdgeVideoProcessor, Qwen2VLImageProcessor, Qwen3VLVideoProcessor

p = argparse.ArgumentParser()
p.add_argument("family", choices=["Edge", "Nano"])
p.add_argument("source", type=Path)
p.add_argument("output", type=Path)
p.add_argument("--weight-dtype", choices=["f32", "bf16", "f16"], default="f32")
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=True)
torch.set_num_threads(1)
torch.manual_seed(437)
edge = a.family == "Edge"
definition = json.loads((a.source / "config.json").read_text())
definition.pop("model", None)
definition.update(tie_word_embeddings=False, image_token_id=299, video_token_id=300, vision_start_token_id=301, vision_end_token_id=302)
t, v = definition["text_config"], definition["vision_config"]
t.update(hidden_size=32, intermediate_size=64, num_hidden_layers=4, num_attention_heads=4, num_key_value_heads=2,
         head_dim=8, vocab_size=320, max_position_embeddings=1024, eos_token_id=303, tie_word_embeddings=False)
rope = t["rope_parameters" if edge else "rope_scaling"]
rope["mrope_section"] = [2, 1, 1]
v.update(hidden_size=32, intermediate_size=64)
if edge:
    v.update(num_hidden_layers=3, num_attention_heads=4, num_patches=16)
    definition["projector_hidden_size"] = 80
    definition.pop("projector_config", None)
else:
    v.update(depth=3, num_heads=4, num_position_embeddings=16, out_hidden_size=32, deepstack_visual_indexes=[0, 1, 2])
(a.output / "config.json").write_text(json.dumps(definition))
config = AutoConfig.from_pretrained(a.output, local_files_only=True)
config._attn_implementation = "eager"
model = AutoModelForImageTextToText.from_config(config).float().eval()
model.to({"f32": torch.float32, "bf16": torch.bfloat16, "f16": torch.float16}[a.weight_dtype]).save_pretrained(a.output)
# save_pretrained reverses checkpoint naming conversions. AIKernel's imported
# format is canonical, so keep identical bytes under actual module names here.
save_file({k:v.contiguous().clone() for k,v in model.state_dict().items()}, a.output/"model.safetensors")
# Rebuild nonpersistent FP32 rotary buffers; only serialized weights are rounded.
model = AutoModelForImageTextToText.from_pretrained(a.output, dtype=torch.float32, attn_implementation="eager").eval()
(a.output / "config.json").write_text(json.dumps(definition, indent=2))
settings = dict(patch_size=16, temporal_patch_size=1 if edge else 2, merge_size=2, image_mean=[.5]*3, image_std=[.5]*3, do_resize=False)
image_processor = (Cosmos3EdgeImageProcessor if edge else Qwen2VLImageProcessor)(**settings)
video_processor = (Cosmos3EdgeVideoProcessor if edge else Qwen3VLVideoProcessor)(**settings)
base = (np.arange(64*96*3, dtype=np.int64)*37 % 256).astype(np.uint8).reshape(64, 96, 3)
for mode in ["text", "image", "video"]:
    target = a.output / mode
    target.mkdir(exist_ok=True)
    kw, info = {}, {"mode": mode, "frames": []}
    ids, types = [1, 2, 3, 4], [0]*4
    if mode != "text":
        frames = np.stack([base]) if mode == "image" else np.stack([base, np.roll(base, 3, axis=1), 255-base])
        for i, frame in enumerate(frames):
            name = f"frame-{i}.rgb"
            (target/name).write_bytes(frame.tobytes())
            info["frames"].append(dict(file=name, width=96, height=64, time=[0., .5, 1.1][i]))
        if mode == "image":
            batch = image_processor(images=[frames[0]], return_tensors="pt")
            pixels, grid = batch.pixel_values, batch.image_grid_thw
            kw = dict(pixel_values=pixels, image_grid_thw=grid)
        else:
            batch = video_processor(videos=[frames], do_sample_frames=False, return_tensors="pt")
            pixels, grid = batch.pixel_values_videos, batch.video_grid_thw
            kw = dict(pixel_values_videos=pixels, video_grid_thw=grid)
        pixels.numpy().tofile(target / "patches.f32")
        ids, types = [1], [0]
        for frame in range(int(grid[0, 0])):
            ids += [4, 5, 301] + [299 if mode == "image" else 300]*6 + [302]
            types += [0]*3 + [1 if mode == "image" else 2]*6 + [0]
        ids += [2]
        types += [0]
        with torch.no_grad():
            features = model.model.get_image_features(pixels, grid)
        torch.cat(features.pooler_output).numpy().tofile(target / "vision.f32")
        if not edge:
            for i, feature in enumerate(features.deepstack_features):
                (torch.cat(feature) if isinstance(feature,tuple) else feature).numpy().tofile(target/f"deep-{i}.f32")
    ids, types = torch.tensor([ids]), torch.tensor([types])
    positions, delta = model.model.get_rope_index(ids, types, kw.get("image_grid_thw"), kw.get("video_grid_thw"))
    with torch.no_grad():
        output = model(input_ids=ids, mm_token_type_ids=types, position_ids=positions, **kw, use_cache=True)
    output.logits.numpy().tofile(target/"logits.f32")
    generated, generation = [], []
    next_position = int(positions.max())+1
    for step in range(8):
        generation.append(output.logits[0, -1].numpy())
        next_token = int(output.logits[0, -1].argmax())
        generated.append(next_token)
        if step < 7:
            with torch.no_grad():
                output = model(input_ids=torch.tensor([[next_token]]), position_ids=torch.full((3, 1, 1), next_position+step),
                               past_key_values=output.past_key_values, use_cache=True)
    np.stack(generation).tofile(target/"generation.f32")
    info.update(tokens=ids[0].tolist(), positions=positions[:, 0, :].T.tolist(), generated=generated, nextPosition=next_position,
        visualIndices=[-1 if t == 0 else sum(x != 0 for x in types[0, :i+1].tolist())-1 for i, t in enumerate(types[0].tolist())])
    (target/"input.json").write_text(json.dumps(info, indent=2))
(a.output/"provenance.json").write_text(json.dumps(dict(transformers=transformers.__version__, torch=torch.__version__, seed=437,
    weightDtype=a.weight_dtype, synthetic=True, family=a.family, weightsSha256=hashlib.sha256((a.output/"model.safetensors").read_bytes()).hexdigest()), indent=2))
print("Created official Cosmos3 fixtures:", a.output)
