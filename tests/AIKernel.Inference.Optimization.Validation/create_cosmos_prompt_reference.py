"""Generate tokenizer/prompt expectations from public Qwen3-VL assets already downloaded by the operator."""
from pathlib import Path
import json,sys,shutil
import numpy as np
from PIL import Image
import transformers
from transformers import AutoTokenizer,Qwen2VLImageProcessorFast,Qwen3VLVideoProcessor,Qwen3VLProcessor
from transformers.video_utils import VideoMetadata
from transformers.models.qwen3_vl.modeling_qwen3_vl import Qwen3VLModel
from transformers import Qwen3VLConfig
from types import SimpleNamespace

root=Path(sys.argv[1]);base=root/'public-base';base.mkdir(exist_ok=True)
assert transformers.__version__=='4.57.1'
for name in ['tokenizer.json','tokenizer_config.json','config.json']:
    shutil.copyfile(root/('base-'+name),base/name)
for source,name in [('base-processor.json','preprocessor_config.json'),('base-video-processor.json','video_preprocessor_config.json'),('base-chat-template.json','chat_template.json')]:
    shutil.copyfile(root/source,base/name)
tokenizer=AutoTokenizer.from_pretrained(base,local_files_only=True)
settings=dict(patch_size=16,temporal_patch_size=2,merge_size=2,image_mean=[.5]*3,image_std=[.5]*3,do_resize=False)
processor=Qwen3VLProcessor(image_processor=Qwen2VLImageProcessorFast(**settings),tokenizer=tokenizer,
 video_processor=Qwen3VLVideoProcessor(**settings),chat_template=json.loads((root/'base-chat-template.json').read_text(encoding='utf8'))['chat_template'])
cases=[]
for text in ['Hello, world!', '日本語の写真を説明してください。', 'Cafe\u0301 🚀\n 123456', "We're testing.\n\n最後",'a\t b  c',
             '𐐀abc 😀hello 𝔘nicode', "'S 'ſ 'RE 'LL", ' a\u2003\u0085\r\n \t b  \n']:
    cases.append({'text':text,'tokens':tokenizer.encode(text,add_special_tokens=False)})
frames=np.stack([np.full((32,64,3),i*70,dtype=np.uint8) for i in range(3)])
prompts=[]
for mode in ['text','image','video','mixed']:
    content=[] if mode=='text' else [{'type':mode}]
    if mode=='mixed':content=[{'type':'image'},{'type':'text','text':' 比較: '},{'type':'video'},{'type':'image'}]
    content.append({'type':'text','text':'内容を説明してください。'})
    text=processor.apply_chat_template([{'role':'system','content':'You are helpful.'},{'role':'user','content':content}],tokenize=False,add_generation_prompt=True)
    kw={} if mode=='text' else {'images':[Image.fromarray(frames[0])]} if mode=='image' else {
      'videos':[frames],'video_metadata':[VideoMetadata(total_num_frames=3,fps=2.,frames_indices=[0,1,2])],'do_sample_frames':False}
    if mode=='mixed':kw['images']=[Image.fromarray(frames[0]),Image.fromarray(frames[1])]
    result=processor(text=[text],return_tensors='pt',**kw)
    stub=SimpleNamespace(config=Qwen3VLConfig.from_pretrained(base,local_files_only=True))
    positions,delta=Qwen3VLModel.get_rope_index(stub,result.input_ids,result.get('image_grid_thw'),result.get('video_grid_thw'))
    prompts.append({'mode':mode,'system':'You are helpful.','text':'内容を説明してください。','tokens':result.input_ids[0].tolist(),
       'positions':positions[:,0,:].T.tolist(),'nextPosition':int(positions.max())+1})
(root/'prompt-reference.json').write_text(json.dumps({'tokenizer':cases,'prompts':prompts},indent=2),encoding='utf8')
print('Saved tokenizer and four multimodal prompt/position cases.')
