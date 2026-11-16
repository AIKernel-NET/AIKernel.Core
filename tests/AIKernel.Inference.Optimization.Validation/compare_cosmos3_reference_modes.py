"""Measure official batched vs cached single-token forward before judging parity.
Uses only official model modules for both vision and language computation.
"""
import argparse,json
from pathlib import Path
import numpy as np
import torch
from transformers import AutoModelForImageTextToText

p=argparse.ArgumentParser();p.add_argument("model",type=Path);p.add_argument("fixture",type=Path)
p.add_argument("--mode",choices=["text","image","video"]);a=p.parse_args();torch.set_num_threads(4)
model=AutoModelForImageTextToText.from_pretrained(a.model,dtype=torch.float32,attn_implementation="eager",local_files_only=True).eval()
edge=model.config.model_type=="cosmos3_edge";summary=[]
for mode in ([a.mode] if a.mode else ["text","image","video"]):
    root=a.fixture/mode;info=json.loads((root/"input.json").read_text())
    ids=torch.tensor([info["tokens"]]);positions=torch.tensor(info["positions"]).T[:,None,:]
    mask=torch.tensor([[i>=0 for i in info["visualIndices"]]])
    deep=None
    with torch.no_grad():
        embedding=model.get_input_embeddings()(ids)
        if mode!="text":
            frames=info["frames"];temporal=1 if edge else 2
            grid=torch.tensor([[(len(frames)+temporal-1)//temporal,frames[0]["height"]//16,frames[0]["width"]//16]])
            pixels=torch.from_numpy(np.fromfile(root/"patches.f32",dtype=np.float32).reshape(-1,768*temporal))
            features=model.model.get_image_features(pixels,grid)
            embedding[mask]=torch.cat(features.pooler_output)
            if not edge:deep=[torch.cat(x) for x in features.deepstack_features]
        cache=None;outputs=[]
        def forward(x,pos,visual_index):
            global cache
            kw={}
            if not edge and visual_index>=0:
                kw=dict(visual_pos_masks=torch.ones((1,1),dtype=torch.bool),deepstack_visual_embeds=[x[visual_index:visual_index+1] for x in deep])
            out=model.model.language_model(inputs_embeds=x,position_ids=pos,cache=cache,use_cache=True,**kw)
            cache=out.past_key_values
            return model.lm_head(out.last_hidden_state)[0,0]
        for i in range(ids.shape[1]):
            logits=forward(embedding[:,i:i+1],positions[:,:,i:i+1],info["visualIndices"][i]);outputs.append(logits.numpy().copy())
        outputs=np.stack(outputs);outputs.tofile(root/"sequential-logits.f32")
        generated=[];generation=[]
        for step in range(8):
            generation.append(logits.numpy().copy());token=int(logits.argmax());generated.append(token)
            if step<7:logits=forward(model.get_input_embeddings()(torch.tensor([[token]])),torch.full((3,1,1),info["nextPosition"]+step),-1)
        generation=np.stack(generation);generation.tofile(root/"sequential-generation.f32")
    record=dict(info);record["generated"]=generated;(root/"sequential-input.json").write_text(json.dumps(record,indent=2))
    for name,actual in [("logits",outputs),("generation",generation)]:
        batch=np.fromfile(root/(name+".f32"),dtype=np.float32).reshape(actual.shape)
        difference=np.abs(actual-batch);at=np.unravel_index(int(difference.argmax()),difference.shape)
        check=dict(mode=mode,name=name,maxAbsolute=float(difference.max()),rms=float(np.sqrt(np.mean(difference.astype(np.float64)**2))),
                   maximumPosition=int(at[0]),vocabularyId=int(at[1]),batchedValue=float(batch[at]),sequentialValue=float(actual[at]),
                   generatedTokensMatch=generated==info["generated"])
        print(json.dumps(check),flush=True);summary.append(check)
(a.fixture/("reference-modes"+("-"+a.mode if a.mode else "")+".json")).write_text(json.dumps(summary,indent=2))
