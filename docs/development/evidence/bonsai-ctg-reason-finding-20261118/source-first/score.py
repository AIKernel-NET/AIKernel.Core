import hashlib,json,sys
from pathlib import Path
root=Path(__file__).resolve().parent
read=lambda p:json.loads(p.read_bytes())
digest=lambda b:hashlib.sha256(b).hexdigest()
reg=read(root/"preregistration.json")
assert all(digest((root/k).read_bytes())==v for k,v in reg["files"].items())
ref=read(root/"expectations.json");prompts=read(root/"prompts.json")
p=Path(sys.argv[1]);out=read(p)
assert out["diagnosticOnly"] and out["planned"]==len(ref)==len(prompts)==len(out["rows"])==6
assert out["modelHash"]==reg["modelSha256"] and out["kernelHash"]==reg["files"]["app/AIKernel.Kernel.dll"]
rows=[]
for r,key,t in zip(out["rows"],ref,prompts,strict=True):
    assert r["id"]==key["id"]==t["id"]
    assert r["reasonPromptSha256"]==t["reasonPromptSha256"] and r["selectionBaseSha256"]==t["selectionBaseSha256"]
    reason,answer=r["reason"],r["answer"]
    ok=bool(reason and reason["FinishReason"]=="stop" and reason["Text"].strip() and len(reason["Text"].encode("utf-16-le"))//2<=1024)
    if reason:assert r["reasonResponseSha256"]==digest(reason["Text"].encode())
    if not ok:assert r["actualModelCalls"]==1 and answer is None and r["finding"] is None and r["error"]
    if answer:assert ok and r["actualModelCalls"]==2 and digest(answer["Text"].encode())==r["responseSha256"]
    valid=bool(ok and answer and answer["FinishReason"]=="classified" and answer["OutputTokens"]==1 and answer["Text"] in ("Yes","No","Unknown"))
    selected={"Yes":"supported","No":"proposal_defect","Unknown":"evaluation_unavailable"}[answer["Text"]] if valid else None
    assert selected==r["finding"]
    if valid:assert r["error"] is None
    else:assert r["error"]
    rows.append(dict(id=r["id"],originalId=key["originalId"],reason=None if not reason else reason["Text"],
        selection=None if not answer else answer["Text"],finding=selected,expectedFinding=key["expectedFinding"],valid=valid,
        reasonValid=ok,errorStage=r["errorStage"],error=r["error"],correct=bool(valid and selected==key["expectedFinding"]),
        reasonPromptSha256=r["reasonPromptSha256"],reasonResponseSha256=r["reasonResponseSha256"],
        selectionPromptSha256=r["promptSha256"],selectionResponseSha256=r["responseSha256"]))
score=dict(diagnosticOnly=True,formalVotes=0,modelSha256=out["modelHash"],preregistrationSha256=digest((root/"preregistration.json").read_bytes()),
    priorRegistrationSha256=reg["priorRegistrationSha256"],actualCalls=sum(x["actualModelCalls"] for x in out["rows"]),
    correct=sum(x["correct"] for x in rows),allPass=all(x["correct"] for x in rows),resultSha256=digest(p.read_bytes()),rows=rows)
(root/"score.json").write_text(json.dumps(score,indent=2,ensure_ascii=False)+"\n",encoding="utf8");print(score["correct"],"/ 6")
