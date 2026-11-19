import hashlib
import json
from pathlib import Path
import sys

root=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,x):p.write_text(json.dumps(x,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
files=["Choice.cs","Choice.csproj","prepare_inputs.py","score.py","source-inputs.json","source-hashes.json","expectations.json",
       "primary-cases.json","primary-mapping.json","reverse-cases.json","reverse-mapping.json",
       "app/AIKernel.Core.Tests.dll","app/AIKernel.Kernel.dll"]
manifest=root/"preregistration.json"
if sys.argv[1:]==["freeze"]:
    if manifest.exists():raise SystemExit("already frozen")
    assert sha(root/"app/AIKernel.Kernel.dll")==read(root.parent/"tested-build.json")["kernelSha256"]
    write(manifest,{"diagnosticOnly":True,"algorithm":"Qwen3CpuModel.ClassifyAsync over the next-token logits of all eligible single-token symbols",
        "stop":"Score all 6 original-order cells. Only run reversed mapping on all 6 original-order cells passing.",
        "files":{n:sha(root/n) for n in files}})
    print(sha(manifest));sys.exit(0)
arm,result_path=sys.argv[1:]
assert arm in ("primary","reverse")
assert all(sha(root/n)==h for n,h in read(manifest)["files"].items())
if arm=="reverse":assert read(root/"primary-score.json")["allPassed"]
result=read(Path(result_path)); mapping=read(root/(arm+"-mapping.json")); expected=read(root/"expectations.json")["expected"]
assert result["diagnosticOnly"] and result["modelHash"]=="3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3"
assert len(result["rows"])==result["planned"]==len(mapping)==len(expected)==6
scored=[]
for row,keys,ref in zip(result["rows"],mapping,expected,strict=True):
    assert row["id"]==keys["id"]==ref["id"] and row["promptSha256"]==keys["promptSha256"]
    answer=row["answer"];symbol=answer["Text"]; selected=keys["mapping"][symbol]
    assert answer["FinishReason"]=="classified" and answer["OutputTokens"]==1
    assert hashlib.sha256(symbol.encode()).hexdigest()==row["responseSha256"]
    scored.append(dict(id=row["id"],promptSha256=row["promptSha256"],responseSha256=row["responseSha256"],
        symbol=symbol,selectedFinding=selected,expectedFinding=ref["expectedFinding"],correct=selected==ref["expectedFinding"]))
score=dict(diagnosticOnly=True,arm=arm,correct=sum(r["correct"] for r in scored),completed=len(scored),
           allPassed=all(r["correct"] for r in scored),modelHash=result["modelHash"],resultSha256=sha(Path(result_path)),rows=scored)
write(root/(arm+"-score.json"),score);print(json.dumps(score))
