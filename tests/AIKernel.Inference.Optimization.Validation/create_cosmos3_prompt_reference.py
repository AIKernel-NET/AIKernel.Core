"""Check public pinned tokenizers, templates, patches and MRoPE positions."""
from pathlib import Path
import json
import sys
import numpy as np
import torch
from PIL import Image
from transformers import AutoConfig, AutoTokenizer, AutoModelForImageTextToText
from transformers import Cosmos3EdgeProcessor, Cosmos3EdgeImageProcessor, Cosmos3EdgeVideoProcessor
from transformers import Qwen3VLProcessor, Qwen2VLImageProcessor, Qwen3VLVideoProcessor
from transformers.video_utils import VideoMetadata

root=Path(sys.argv[1]);config=AutoConfig.from_pretrained(root,local_files_only=True)
edge=config.model_type=="cosmos3_edge"
tokenizer=AutoTokenizer.from_pretrained(root,local_files_only=True)
settings=dict(patch_size=16,temporal_patch_size=1 if edge else 2,merge_size=2,image_mean=[.5]*3,image_std=[.5]*3,do_resize=False)
template=(root/"chat_template.jinja").read_text(encoding="utf-8") if edge else json.loads((root/"chat_template.json").read_text(encoding="utf-8"))["chat_template"]
processor=(Cosmos3EdgeProcessor if edge else Qwen3VLProcessor)(tokenizer=tokenizer,
    image_processor=(Cosmos3EdgeImageProcessor if edge else Qwen2VLImageProcessor)(**settings),
    video_processor=(Cosmos3EdgeVideoProcessor if edge else Qwen3VLVideoProcessor)(**settings),chat_template=template)
with torch.device("meta"): model=AutoModelForImageTextToText.from_config(config)
cases=[]
for text in ['Hello, world!', '日本語の写真を説明してください。', 'Cafe\u0301 🚀\n 123456', "We're testing.\n\n最後",'a\t b  c',
             '𐐀abc 😀hello 𝔘nicode', "'S 'ſ 'RE 'LL", ' a\u2003\u0085\r\n \t b  \n']:
    cases.append(dict(text=text,tokens=tokenizer.encode(text,add_special_tokens=False)))
frames=np.stack([np.full((32,64,3),i*70,dtype=np.uint8) for i in range(3)])
prompts=[]
for system,thinking in [("You are helpful.",True),("",True)]+([("You are helpful.",False),("",False)] if edge else []):
    for mode in ["text","image","video","mixed"]:
        content=[] if mode=="text" else [dict(type=mode)]
        if mode=="mixed":content=[dict(type="image"),dict(type="text",text=" 比較: "),dict(type="video"),dict(type="image")]
        content.append(dict(type="text",text="内容を説明してください。"))
        messages=([dict(role="system",content=system)] if system else [])+[dict(role="user",content=content)]
        rendered=processor.apply_chat_template(messages,tokenize=False,add_generation_prompt=True,enable_thinking=thinking)
        kw={}
        if mode in ["image","mixed"]:kw["images"]=[Image.fromarray(frames[0])]+([Image.fromarray(frames[1])] if mode=="mixed" else [])
        if mode in ["video","mixed"]:kw.update(videos=[frames],video_metadata=[VideoMetadata(total_num_frames=3,fps=2.,frames_indices=[0,1,2])],do_sample_frames=False)
        result=processor(text=[rendered],return_tensors="pt",return_metadata=True,**kw)
        positions,_=model.model.get_rope_index(result.input_ids,result.mm_token_type_ids,result.get("image_grid_thw"),result.get("video_grid_thw"))
        prompts.append(dict(mode=mode,system=system,thinking=thinking,text="内容を説明してください。",tokens=result.input_ids[0].tolist(),
                            positions=positions[:,0,:].T.tolist(),nextPosition=int(positions.max())+1))
(root/"prompt-reference.json").write_text(json.dumps(dict(tokenizer=cases,prompts=prompts),indent=2),encoding="utf-8")
print("Saved",len(cases),"tokenizer and",len(prompts),"multimodal prompt cases:",root)
