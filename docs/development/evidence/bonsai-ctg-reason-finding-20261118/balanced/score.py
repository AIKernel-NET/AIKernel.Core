import hashlib,json,math,struct,sys
from pathlib import Path
root=Path(__file__).resolve().parent
def read(path):return json.loads(path.read_bytes())
def h(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def save(n,o):(root/n).write_text(json.dumps(o,indent=2,ensure_ascii=False,allow_nan=False)+'\n',encoding='utf8')
def exact(bits):return struct.unpack('>f',struct.pack('>i',bits))[0]
registered=read(root/'preregistration.json')
assert all(h(root/k)==v for k,v in registered['files'].items())
actual=read(root/'result.json'); raw_input=read(root/'balanced-cases.json')
assert actual['diagnosticOnly'] and actual['modelHash']==registered['model'] and actual['planned']==len(actual['rows'])==len(raw_input)==20
design=read(root/'design.json'); base={cell['pair']:cell for cell in read(root/'key.json')}; by_cell={k:[] for k in base}
hash_prior={row['promptSha256']:row for row in read(root.parent/'ablation/score.json')['rows'] if row['arm'] in ('reason-primary','reason-cycle')}
seen=set()
for x,d,r in zip(raw_input,design,actual['rows'],strict=True):
    assert x['id']==d['id']==r['id'] and r['id'] not in seen
    seen.add(r['id']); assert r['promptSha256']==x['promptSha256']==d['promptSha256'] and r['romSha256']==x['romSha256']
    assert r['role']==x['role'] and [s['symbol'] for s in r['raw']]==x['labels']
    values=[exact(v['bits']) for v in r['raw']]
    finite=all(map(math.isfinite,values));assert r['allFinite']==finite
    if finite: assert values==[v['score'] for v in r['raw']]
    maxima=[x['labels'][i] for i,v in enumerate(values) if finite and v==max(values)]
    assert maxima==r['localMaxima']
    prior=hash_prior.get(r['promptSha256'])
    by_cell[d['pair']].append(dict(rotation=d['rotation'],id=d['id'],values=values if finite else None,
        canon=d['findingIndices'],maxima=maxima,promptSha256=d['promptSha256'],priorSymbol=prior['symbol'] if prior else None))
reports=[];adapter=[]
for p,rows in by_cell.items():
    ref=base[p];rows.sort(key=lambda row:row['rotation']);keys=ref['findings'];n=len(keys)
    assert [r['rotation'] for r in rows]==list(range(n))
    assert all(len({r['canon'][symbol] for r in rows})==n for symbol in range(n))
    finite=all(row['values'] is not None for row in rows)
    if finite:
        accumulated=[0.0]*n
        for r in rows:
            vals=r['values']; mean=sum(vals)/n
            for index,score in zip(r['canon'],vals,strict=True):accumulated[index]+=(score-mean)
        means=[s/n for s in accumulated]
        ranked=sorted(means,reverse=True); gap=ranked[0]-ranked[1]
        win=keys[means.index(ranked[0])] if gap>0 else None
        adapter.append(dict(id=ref['caseId']+'-'+ref['role'],findings=keys,rotations=[dict(bySymbol=r['values'],findingIndices=r['canon']) for r in rows]))
    else:
        means=None;gap=None;win=None
    used=[r for r in rows if r['priorSymbol'] is not None]
    pinned=[len(r['maxima'])==1 and r['priorSymbol']==r['maxima'][0] for r in used]
    assert len(used)==2
    reports.append(dict(pair=p,caseId=ref['caseId'],role=ref['role'],expected=ref['expected'],selected=win,correct=win==ref['expected'],finite=finite,gap=gap,meanCenteredLogitsByFinding=dict(zip(keys,means,strict=True)) if finite else None,previousArgmaxMatches=pinned,rows=rows))
ok=sum(r['correct'] for r in reports)
usable=all(r['finite'] and r['selected'] is not None and all(r['previousArgmaxMatches']) for r in reports)
save('score.json',dict(diagnosticOnly=True,preregistrationSha256=h(root/'preregistration.json'),resultsSha256=h(root/'result.json'),model=actual['modelHash'],cells=6,measuredForwardCalls=20,correct=ok,allAggregateSelectionsValid=usable,wronglySupportedDefects=sum(r['expected']=='proposal_defect' and r['selected']=='supported' for r in reports),correctPreviouslySupported=sum(r['expected']=='supported' and r['correct'] for r in reports),advance=usable and ok==6,rows=reports))
if len(adapter)==6:save('logits-for-csharp.json',dict(cells=adapter))
print([(r['caseId'],r['role'],r['selected'],r['gap'],r['correct']) for r in reports])

