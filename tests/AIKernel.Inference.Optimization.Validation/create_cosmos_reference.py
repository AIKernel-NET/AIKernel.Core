"""Independent small Qwen3-VL reference; no gated Cosmos weights are downloaded.

Uses Transformers 4.57.1, the Cosmos-era Qwen3-VL processor contract. Synthetic
weights test architecture/numerics, not real Cosmos language or vision quality.
"""
from pathlib import Path
import argparse, hashlib, json
import numpy as np
import torch
import transformers
from transformers import Qwen3VLConfig, Qwen3VLForConditionalGeneration, Qwen2VLImageProcessorFast, Qwen3VLVideoProcessor

p=argparse.ArgumentParser();p.add_argument('output',type=Path);p.add_argument('--weight-dtype',choices=['f32','bf16','f16'],default='f32');a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
torch.set_num_threads(1);torch.manual_seed(437)
assert transformers.__version__=='4.57.1', 'Use the pinned Cosmos reference environment.'
definition={
 'model_type':'qwen3_vl','architectures':['Qwen3VLForConditionalGeneration'],'tie_word_embeddings':True,
 'image_token_id':299,'video_token_id':300,'vision_start_token_id':301,'vision_end_token_id':302,
 'text_config':{'model_type':'qwen3_vl_text','attention_bias':False,'attention_dropout':0.,'hidden_act':'silu',
 'hidden_size':32,'intermediate_size':64,'num_hidden_layers':4,'num_attention_heads':4,'num_key_value_heads':2,
 'head_dim':8,'vocab_size':320,'rms_norm_eps':1e-6,'rope_theta':5000000.,'max_position_embeddings':1024,
 'rope_scaling':{'rope_type':'default','mrope_interleaved':True,'mrope_section':[2,1,1]},'eos_token_id':303,'tie_word_embeddings':True},
 'vision_config':{'model_type':'qwen3_vl','hidden_act':'gelu_pytorch_tanh','hidden_size':32,'intermediate_size':64,
 'depth':3,'num_heads':4,'num_position_embeddings':16,'out_hidden_size':32,'patch_size':16,
 'spatial_merge_size':2,'temporal_patch_size':2,'in_channels':3,'deepstack_visual_indexes':[0,1,2]}}
config=Qwen3VLConfig(**definition);config._attn_implementation='eager'
model=Qwen3VLForConditionalGeneration(config).float().eval()
model.to({'f32':torch.float32,'bf16':torch.bfloat16,'f16':torch.float16}[a.weight_dtype]).save_pretrained(a.output)
model=Qwen3VLForConditionalGeneration.from_pretrained(a.output,dtype=torch.float32,attn_implementation='eager').eval()
# Reload, rather than model.float(): converting the whole module to BF16 also
# rounds nonpersistent rotary frequency buffers. Reload reconstructs those FP32
# buffers from the configuration and independently decodes only stored weights.
(a.output/'config.json').write_text(json.dumps(definition,indent=2))

def patchify(frames,video):
    settings=dict(patch_size=16,temporal_patch_size=2,merge_size=2,image_mean=[.5]*3,image_std=[.5]*3,do_resize=False)
    if video:
        result=Qwen3VLVideoProcessor(**settings)(videos=[frames],do_sample_frames=False,return_tensors='pt')
        return result.pixel_values_videos,result.video_grid_thw
    result=Qwen2VLImageProcessorFast(**settings)(images=[frames[0]],return_tensors='pt')
    return result.pixel_values,result.image_grid_thw

base=(np.arange(64*96*3,dtype=np.int64)*37%256).astype(np.uint8).reshape(64,96,3)
for mode in ['text','image','video']:
    target=a.output/mode;target.mkdir(exist_ok=True)
    kw={};info={'mode':mode,'frames':[]}
    if mode=='text':ids=[1,2,3,4];types=[0]*4
    else:
        frames=np.stack([base]) if mode=='image' else np.stack([base,np.roll(base,3,axis=1),255-base])
        times=[0.] if mode=='image' else [0.,.5,1.1]
        for i,(frame,time) in enumerate(zip(frames,times)):
            name=f'frame-{i}.rgb';(target/name).write_bytes(frame.tobytes());info['frames'].append({'file':name,'width':96,'height':64,'time':time})
        pixels,grid=patchify(frames,mode=='video');pixels.numpy().tofile(target/'patches.f32')
        if mode=='image':
            ids=[1,301]+[299]*6+[302,2];types=[0,0]+[1]*6+[0,0]
            kw={'pixel_values':pixels,'image_grid_thw':grid}
        else:
            ids=[1,4,5,301]+[300]*6+[302,6,7,301]+[300]*6+[302,2];types=[0]*4+[2]*6+[0]*4+[2]*6+[0]*2
            kw={'pixel_values_videos':pixels,'video_grid_thw':grid}
        with torch.no_grad():
            features=model.model.visual(pixels,grid)
        features[0].numpy().tofile(target/'vision.f32')
        for i,x in enumerate(features[1]):x.numpy().tofile(target/f'deep-{i}.f32')
    ids=torch.tensor([ids]);types=torch.tensor([types])
    positions,delta=model.model.get_rope_index(ids,kw.get('image_grid_thw'),kw.get('video_grid_thw'))
    with torch.no_grad():output=model(input_ids=ids,**kw,use_cache=True)
    output.logits.numpy().tofile(target/'logits.f32')
    generated=[];generation=[];next_position=int(positions.max())+1
    for step in range(8):
        generation.append(output.logits[0,-1].numpy());next_token=int(output.logits[0,-1].argmax());generated.append(next_token)
        if step<7:
            with torch.no_grad():output=model(input_ids=torch.tensor([[next_token]]),position_ids=torch.full((3,1,1),next_position+step),past_key_values=output.past_key_values,use_cache=True)
    np.stack(generation).tofile(target/'generation.f32')
    info.update(tokens=ids[0].tolist(),positions=positions[:,0,:].T.tolist(),generated=generated,nextPosition=next_position,
      visualIndices=[-1 if t==0 else sum(x!=0 for x in types[0,:i+1].tolist())-1 for i,t in enumerate(types[0].tolist())])
    (target/'input.json').write_text(json.dumps(info,indent=2))
(a.output/'provenance.json').write_text(json.dumps({'transformers':transformers.__version__,'torch':torch.__version__,'seed':437,'weightDtype':a.weight_dtype,
 'synthetic':True,'weightsSha256':hashlib.sha256((a.output/'model.safetensors').read_bytes()).hexdigest()},indent=2))
print('Created text/image/video reference fixtures:',a.output)
