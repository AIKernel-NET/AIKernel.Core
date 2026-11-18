from pathlib import Path
import hashlib,json,sys
root=Path(__file__).resolve().parent
read=lambda p:json.loads(p.read_bytes())
sha=lambda b:hashlib.sha256(b).hexdigest()
reg=read(root/"preregistration.json");c=read(root/"cases.json")[0];ref=read(root/"reference.json")
assert all(sha((root/k).read_bytes())==h for k,h in reg["files"].items())
p=Path(sys.argv[1]);result=read(p)
assert result["diagnosticOnly"] and result["planned"]==1 and result["modelHash"]==ref["modelSha256"] and result["kernelHash"]==reg["priorTestedKernel"]
rows=result["rows"];assert 1<=len(rows)<=2 and rows[0]["reasonPromptSha256"]==c["draftPromptSha256"]
if len(rows)>1: assert rows[0]["fallbackEligible"] and rows[0]["finding"] is None and rows[1]["variant"]=="short-256"
scored=[]
for i,r in enumerate(rows):
    reason=r["reason"];ans=r["answer"];n=len(reason["Text"].encode("utf-16-le"))//2 if reason else 0
    prefix=(reason["OutputTokens"]>=192 and reason["Text"].startswith(c["recordedDraft192"].rstrip())) if reason and i==0 else None
    assert prefix==r["originalVisiblePrefixMatches"]
    if reason:assert sha(reason["Text"].encode())==r["reasonResponseSha256"] and n==r["visibleCharacters"]
    validreason=bool(reason and reason["FinishReason"]=="stop" and 0<n<=1024 and reason["Text"].strip() and (i>0 or prefix))
    if ans:
        assert validreason and sha(ans["Text"].encode())==r["selectionResponseSha256"]
    validchoice=bool(ans and ans["FinishReason"]=="classified" and ans["OutputTokens"]==1 and ans["Text"] in ("Yes","No","Unknown"))
    expected={"Yes":"supported","No":"proposal_defect","Unknown":"evaluation_unavailable"}
    assert (expected[ans["Text"]] if validchoice else None)==r["finding"]
    scored.append(dict(variant=r["variant"],finish=reason["FinishReason"] if reason else None,outputTokens=reason["OutputTokens"] if reason else None,
        characters=n,prefixMatches=prefix,validReason=validreason,validChoice=validchoice,selected=r["finding"],wordCount=len(reason["Text"].split()) if reason else None,
        expectedSelected=bool(validchoice and r["finding"]==ref["expectedFinding"]),failure=r["failure"],reasonPromptSha256=r["reasonPromptSha256"],
        reasonResponseSha256=r["reasonResponseSha256"],choicePromptSha256=r["selectionPromptSha256"],choiceResponseSha256=r["selectionResponseSha256"]))
score=dict(diagnosticOnly=True,formalVotes=0,resultSha256=sha(p.read_bytes()),preregistrationSha256=sha((root/"preregistration.json").read_bytes()),
    modelSha256=result["modelHash"],kernelSha256=result["kernelHash"],calls=result["actualModelCalls"],elapsedSeconds=result["elapsedSeconds"],attempts=scored)
(root/"score.json").write_text(json.dumps(score,ensure_ascii=False,indent=2)+"\n",encoding="utf8"); print(json.dumps(scored))
