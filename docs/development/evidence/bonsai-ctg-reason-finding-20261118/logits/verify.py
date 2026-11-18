"""Check all scores on the registered 3 cells and compare prefix vs first answer position."""
import hashlib
import itertools
import json
import math
import struct
from pathlib import Path
root=Path(__file__).resolve().parent
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_bytes())
def from_bits(bits):return struct.unpack('>f',struct.pack('>i',bits))[0]
pr=read(root/'preregistration.json')
assert pr['casesSha256']==h(root/'logit-cases.json') and pr['harnessSha256']==h(root/'app/AIKernel.Core.Tests.dll') and pr['kernelSha256']==h(root/'app/AIKernel.Kernel.dll')
cases=read(root/'logit-cases.json')
raw=read(root/'result.json')
assert raw['planned']==len(raw['rows'])==len(cases)==3 and raw['diagnosticOnly'] and raw['modelHash']==pr['model']
report=[]
for case,row in zip(cases,raw['rows'],strict=True):
    assert row['id']==case['id'] and row['promptSha256']==case['promptSha256'] and row['romSha256']==case['romSha256']
    assert row['answer']['FinishReason']=='classified' and row['answer']['OutputTokens']==1
    assert row['responseSha256']==hashlib.sha256(row['answer']['Text'].encode()).hexdigest()
    item={key:row[key] for key in ('id','promptSha256','prefill','decode','prefillThreads','decodeThreads','publicPick','historyTokens','vocabulary')}
    item['publicMatchesBoth']=row['publicMatchesFull'] and row['publicMatchesSingle']
    for tag,stage,body in [('first',row,row['labels']),('prefix',row['afterPrefix'],row['afterPrefix']['byLabel'])]:
        assert stage['fullNonfinite']==stage['singleNonfinite']==0 and [x['label'] for x in body]==case['labels']
        for x in body:
            assert all(math.isfinite(from_bits(x[k])) for k in ('fullBits','singleBits'))
            assert struct.pack('>f',x['full'])==struct.pack('>i',x['fullBits']) and struct.pack('>f',x['single'])==struct.pack('>i',x['singleBits'])
            assert 1<=x['fullVocabularyRank']<=row['vocabulary']
        defs={r['label']:from_bits(r['fullBits']) for r in body}
        singles={r['label']:from_bits(r['singleBits']) for r in body}
        scores=sorted(defs.values(),reverse=True)
        assert math.isclose(scores[0]-scores[1],stage['fullMargin' if tag=='first' else 'margin'],abs_tol=1e-12)
        w=max(defs,key=defs.get);assert w==stage['fullPick' if tag=='first' else 'prefixPick']
        assert max(singles,key=singles.get)==stage['singlePick']
        item[tag]={'selected':w,'margin':scores[0]-scores[1],'logits':defs,'singleLogits':singles,'topTieCount':sum(v==scores[0] for v in defs.values()),'allTies':[[a,b] for a,b in itertools.combinations(defs,2) if defs[a]==defs[b]],'fullVsSingleBitwise':all(r['fullBits']==r['singleBits'] for r in body),'fullVocabularyRanks':{v['label']:v['fullVocabularyRank'] for v in body},'historyTokens':stage['historyTokens'],'logitsSha256':stage['logitsSha256']}
    assert row['afterPrefix']['literal']==pr['prefixLiteral'] and row['afterPrefix']['historyTokens']==row['historyTokens']+len(row['afterPrefix']['tokenIds'])
    item['prefixChangesWinner']=item['first']['selected']!=item['prefix']['selected']
    report.append(item)
result=dict(diagnosticOnly=True,cells=3,model=raw['modelHash'],preregistrationSha256=h(root/'preregistration.json'),resultSha256=h(root/'result.json'),rows=report)
(root/'logit-summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(report,ensure_ascii=False))

