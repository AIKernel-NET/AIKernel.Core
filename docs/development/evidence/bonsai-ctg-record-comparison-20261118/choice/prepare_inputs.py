import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
v25 = root.parent.parent
def read(p):
    return json.loads(p.read_text(encoding="utf-8-sig"))
def save(p, obj):
    p.write_text(json.dumps(obj, ensure_ascii=False, indent=2)+"\n",encoding="utf-8")
def sha(data):
    return hashlib.sha256(data).hexdigest()

cases = {r["id"]:r for r in read(v25/"guarded-cases.json")}
coverage = {r["id"]:r["phase"] for r in read(v25/"focus-summary.json")["rows"]}
order = [
    ("ordered-holdout-status-invented","Logos","proposal_defect"),
    ("ordered-holdout-dimension-wrong","Logos","proposal_defect"),
    ("ordered-holdout-status-invented","Ethos","proposal_defect"),
    ("ordered-holdout-dimension-wrong","Ethos","proposal_defect"),
    ("ordered-holdout-status-exact","Logos","supported"),
    ("ordered-holdout-dimension-exact","Logos","supported"),
]
inputs=[]; expected=[]; sources=[]
for id, role, finding in order:
    case=cases[id]
    folder=v25/("server-"+coverage[id])
    results=folder/"result.json"; raw=folder/"result.json.responses.json"
    meta=next(r for r in read(results) if r["id"]==id)["result"]["Evaluation"]["Metadata"]
    rows=[r for r in read(raw) if r["id"]==id and r["stage"]=="reading"]
    assert len(rows)==1
    answer=rows[0]["answer"]; reading=answer["Text"]
    assert answer["FinishReason"]=="stop" and sha(reading.encode())==meta["reading.response.sha256"]
    identifier=id+"-"+role
    inputs.append(dict(id=identifier,council=role,proposal=case["proposal"],facts=case["facts"],reading=reading,
        readingSha256=meta["reading.response.sha256"],readingPromptSha256=meta["reading.prompt.sha256"],
        v25PromptSha256=meta[role+".prompt.sha256"],romSha256=meta[role+".rom.sha256"]))
    expected.append(dict(id=identifier,council=role,expectedFinding=finding))
    sources.append(dict(id=identifier,phase=folder.name,readingSha256=meta["reading.response.sha256"],
        readingPromptSha256=meta["reading.prompt.sha256"],v25PromptSha256=meta[role+".prompt.sha256"],
        v25ResultsSha256=sha(results.read_bytes()),v25RawSha256=sha(raw.read_bytes())))
save(root/"source-inputs.json",inputs);save(root/"source-hashes.json",sources)
save(root/"expectations.json",dict(diagnosticOnly=True,expected=expected,protocol="Run all six primary cells. Run all six reversed cells only if every primary cell selects the expected finding with one classified token. No cell is a formal council vote."))
