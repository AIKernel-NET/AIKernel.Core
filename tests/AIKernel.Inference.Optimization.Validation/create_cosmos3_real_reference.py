"""Official FP32 reference on imported, unchanged NVIDIA Cosmos3 weights.
Preparation/validation only. Never used by the AIKernel inference runtime.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import numpy as np
import torch
import transformers
from PIL import Image
from transformers import AutoConfig, AutoTokenizer, AutoModelForImageTextToText
from transformers import Cosmos3EdgeProcessor, Cosmos3EdgeImageProcessor, Cosmos3EdgeVideoProcessor
from transformers import Qwen3VLProcessor, Qwen2VLImageProcessor, Qwen3VLVideoProcessor
from transformers.video_utils import VideoMetadata

p=argparse.ArgumentParser();p.add_argument("model",type=Path);p.add_argument("output",type=Path);a=p.parse_args()
a.output.mkdir(parents=True,exist_ok=True);torch.set_num_threads(4)
config=AutoConfig.from_pretrained(a.model,local_files_only=True);edge=config.model_type=="cosmos3_edge"
index=json.loads((a.model/"model.safetensors.index.json").read_text())
for name in sorted(set(index["weight_map"].values())):
    if not (a.output/name).exists():os.link(a.model/name,a.output/name)
for name in ["config.json","model.safetensors.index.json"]:shutil.copyfile(a.model/name,a.output/name)
tokenizer=AutoTokenizer.from_pretrained(a.model,local_files_only=True)
settings=dict(patch_size=16,temporal_patch_size=1 if edge else 2,merge_size=2,image_mean=[.5]*3,image_std=[.5]*3,do_resize=False)
template=(a.model/"chat_template.jinja").read_text(encoding="utf-8") if edge else json.loads((a.model/"chat_template.json").read_text(encoding="utf-8"))["chat_template"]
processor=(Cosmos3EdgeProcessor if edge else Qwen3VLProcessor)(tokenizer=tokenizer,
    image_processor=(Cosmos3EdgeImageProcessor if edge else Qwen2VLImageProcessor)(**settings),
    video_processor=(Cosmos3EdgeVideoProcessor if edge else Qwen3VLVideoProcessor)(**settings),chat_template=template)
model=AutoModelForImageTextToText.from_pretrained(a.model,dtype=torch.float32,attn_implementation="eager",local_files_only=True).eval()
frames=np.zeros((8,64,96,3),dtype=np.uint8)
for i in range(8):frames[i,:,:,min(i//3,2)]=255
for mode in ["text","image","video"]:
    print("Reference",a.model.name,mode,flush=True)
    target=a.output/mode;target.mkdir(exist_ok=True);info=dict(mode=mode,frames=[])
    question="What is 2 + 2? Answer briefly." if mode=="text" else "What color is the image? Answer briefly." if mode=="image" else "Describe the colors in the video in order. Answer briefly."
    content=([] if mode=="text" else [dict(type=mode)])+[dict(type="text",text=question)]
    messages=[dict(role="system",content="You are helpful."),dict(role="user",content=content)]
    text=processor.apply_chat_template(messages,tokenize=False,add_generation_prompt=True,enable_thinking=False)
    kw={}
    if mode=="image":kw["images"]=[Image.fromarray(frames[0])]
    if mode=="video":kw.update(videos=[frames],video_metadata=[VideoMetadata(total_num_frames=8,fps=2.,frames_indices=list(range(8)))],do_sample_frames=False)
    inputs=processor(text=[text],return_tensors="pt",return_metadata=True,**kw)
    if mode!="text":
        for i,frame in enumerate(frames[:1] if mode=="image" else frames):
            name=f"frame-{i}.rgb";(target/name).write_bytes(frame.tobytes())
            info["frames"].append(dict(file=name,width=96,height=64,time=i*.5))
        pixels=inputs["pixel_values" if mode=="image" else "pixel_values_videos"]
        grid=inputs["image_grid_thw" if mode=="image" else "video_grid_thw"]
        pixels.numpy().tofile(target/"patches.f32")
        with torch.no_grad():features=model.model.get_image_features(pixels,grid)
        torch.cat(features.pooler_output).numpy().tofile(target/"vision.f32")
        if not edge:
            for i,feature in enumerate(features.deepstack_features):torch.cat(feature).numpy().tofile(target/f"deep-{i}.f32")
    ids=inputs.input_ids;types=inputs.mm_token_type_ids
    positions,_=model.model.get_rope_index(ids,types,inputs.get("image_grid_thw"),inputs.get("video_grid_thw"))
    # Video metadata belongs to preprocessing, not the forward model's kwargs.
    inputs.pop("video_metadata",None)
    with torch.no_grad():output=model(**inputs,position_ids=positions,use_cache=True)
    output.logits.numpy().tofile(target/"logits.f32")
    next_position=int(positions.max())+1;generated=[];generation=[];smoke=[];ended=False
    for step in range(64):
        next_token=int(output.logits[0,-1].argmax())
        if step<8:generation.append(output.logits[0,-1].numpy().copy());generated.append(next_token)
        if not ended:smoke.append(next_token);ended=next_token==config.text_config.eos_token_id
        if step>=7 and ended:break
        if step<63:
            with torch.no_grad():output=model(input_ids=torch.tensor([[next_token]]),position_ids=torch.full((3,1,1),next_position+step),cache=output.past_key_values,use_cache=True)
    np.stack(generation).tofile(target/"generation.f32")
    info.update(tokens=ids[0].tolist(),positions=positions[:,0,:].T.tolist(),generated=generated,nextPosition=next_position,
        visualIndices=[-1 if t==0 else sum(x!=0 for x in types[0,:i+1].tolist())-1 for i,t in enumerate(types[0].tolist())],
        text=tokenizer.decode(generated,skip_special_tokens=True),smokeTokens=smoke,smokeText=tokenizer.decode(smoke,skip_special_tokens=True))
    (target/"input.json").write_text(json.dumps(info,indent=2),encoding="utf-8")
    print(mode,repr(info["smokeText"]),flush=True)
(a.output/"provenance.json").write_text(json.dumps(dict(synthetic=False,transformers=transformers.__version__,torch=torch.__version__,
    referenceDtype="float32",storedWeights="unchanged imported NVIDIA snapshot",manifest=json.loads((a.model/"import-manifest.json").read_text())),indent=2),encoding="utf-8")
