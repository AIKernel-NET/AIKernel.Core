"""Score the blinded 2x2 experiment; diagnostic findings never enter CTG."""
import hashlib,json
from pathlib import Path
from collections import Counter
root=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_bytes())
def sha(b):return hashlib.sha256(b).hexdigest()
def save(name,obj):(root/name).write_text(json.dumps(obj,indent=2,ensure_ascii=False)+'\n',encoding='utf8')
arms=("no-reason-primary","no-reason-cycle","reason-primary","reason-cycle")
pr=read(root/"preregistration.json")
assert all(sha((root/path).read_bytes())==h for path,h in pr['frozenInputs'].items())
design=read(root/"design.json")
prior=read(root/"reused-results.json")
rows=[]
for a,arm in enumerate(arms):
    if a:
        path=root/(arm+"-result.json")
        raw=read(path)
        assert raw['diagnosticOnly'] and raw['planned']==len(raw['rows'])==6 and raw['modelHash']==pr['model']
        calls=raw['rows']
    else:
        calls=[dict(id=f"c{v['pair']+1:02}",promptSha256=v['promptSha256'],answer=dict(Text=v['symbol'],OutputTokens=1,FinishReason='classified'),responseSha256=v['responseSha256']) for v in prior]
    for i,(d,out) in enumerate(zip((r for r in design if r['arm']==arm),calls,strict=True)):
        assert d['id']==out['id'] and d['promptSha256']==out['promptSha256']
        ans=out['answer']; s=ans['Text']; assert ans['FinishReason']=='classified' and ans['OutputTokens']==1 and s in d['mapping'] and out['responseSha256']==sha(s.encode())
        selected=d['mapping'][s]
        rows.append(dict(id=d['id'],arm=arm,pair=i,role=d['role'],expected=d['expected'],selected=selected,symbol=s,correct=d['expected']==selected,promptSha256=d['promptSha256'],responseSha256=out['responseSha256'],caseId=d['caseId']))
def at(arm):return sorted((r for r in rows if r['arm']==arm),key=lambda r:r['pair'])
def contrast(left,right):
    pairs=list(zip(at(left),at(right),strict=True));return dict(left=left,right=right,sameSymbol=sum(a['symbol']==b['symbol'] for a,b in pairs),sameFinding=sum(a['selected']==b['selected'] for a,b in pairs),improved=sum(not a['correct'] and b['correct'] for a,b in pairs),regressed=sum(a['correct'] and not b['correct'] for a,b in pairs))
groups={}
for arm in arms:
    r=at(arm)
    groups[arm]=dict(cells=len(r),correct=sum(x['correct'] for x in r),symbols=dict(Counter(x['symbol'] for x in r)),findings=dict(Counter(x['selected'] for x in r)),supportedOnDefect=sum(x['expected']=='proposal_defect' and x['selected']=='supported' for x in r),defectOnSupported=sum(x['expected']=='supported' and x['selected']=='proposal_defect' for x in r))
report=dict(diagnosticOnly=True,model=pr['model'],preregistrationSha256=sha((root/'preregistration.json').read_bytes()),newMeasuredCalls=18,matchedPriorCalls=6,groups=groups,contrasts=[contrast(arms[0],arms[1]),contrast(arms[2],arms[3]),contrast(arms[0],arms[2]),contrast(arms[1],arms[3])],resultSha256={a:sha((root/(a+"-result.json")).read_bytes()) for a in arms[1:]},rows=rows)
save('score.json',report)
print(json.dumps({k:report[k] for k in ('groups','contrasts')},ensure_ascii=False))

