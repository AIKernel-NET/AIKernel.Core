from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parent
server=root.parent/"server"
rawdir=server/"server-paired"
def read(p):return json.loads(p.read_bytes())
def sha(b):return hashlib.sha256(b).hexdigest()
def digest(p):return sha(p.read_bytes())
def save(n,obj): (root/n).write_text(json.dumps(obj,ensure_ascii=False,indent=2)+"\n",encoding="utf8")
if (root/"preregistration.json").exists():raise SystemExit("Already frozen")
id="ordered-holdout-dimension-exact"
getrow=lambda p:next(r for r in read(p) if r["id"]==id)
original=getrow(rawdir/"cases.json")
row=getrow(rawdir/"result.json")
meta=row["result"]["Evaluation"]["Metadata"]
raw=[r for r in read(rawdir/"result.json.responses.json") if r["id"]==id]
draft=next(r["answer"] for r in raw if r["stage"]=="draft" and r["council"]=="Pathos")
reading=next(r["answer"] for r in raw if r["stage"]=="reading")
assert all(r["stage"]!="final" or r["council"]!="Pathos" for r in raw)
assert not row["result"]["Evaluation"]["Succeeded"] and original["requiredVotes"]["Pathos"]==["Approve"]
assert meta["prompt.contract"]=="canonical-reasoned-finding-v28" and draft["FinishReason"]=="length" and draft["OutputTokens"]==192
assert reading["FinishReason"]=="stop" and sha(reading["Text"].encode())==meta["reading.response.sha256"]
assert sha(draft["Text"].encode())==meta["Pathos.draft.response.sha256"]
assert digest(rawdir/"cases.json")==digest(server/"paired-cases.json")==read(rawdir/"transferred-files.json")["cases.json"]
save("cases.json",[dict(id="d1",proposal=original["proposal"],facts=original["facts"],reading=reading["Text"],
    readingPromptSha256=meta["reading.prompt.sha256"],readingSha256=meta["reading.response.sha256"],romSha256=meta["Pathos.rom.sha256"],
    comparisonSha256=meta["comparison.sha256"],recordedDraft192=draft["Text"],draftResponseSha256=meta["Pathos.draft.response.sha256"],
    draftPromptSha256=meta["Pathos.draft.prompt.sha256"],draftOutputTokens=draft["OutputTokens"],draftFinishReason=draft["FinishReason"])])
save("reference.json",dict(originalId=id,council="Pathos",expectedFinding="supported",requiredVote=original["requiredVotes"]["Pathos"],
    baselineGate=row["result"]["Gate"]["Accepted"],baselineEvaluationError=row["result"]["Evaluation"]["ErrorCode"],
    modelSha256=meta["model.sha256"],originalCasesSha256=digest(rawdir/"cases.json"),originalRecordedResultsSha256=digest(rawdir/"result.json"),
    originalRecordedResponsesSha256=digest(rawdir/"result.json.responses.json"),baselineDraft=draft))
a=root/"app";a.mkdir(exist_ok=True)
for f in (root.parent/"app-v28").iterdir():
    if f.is_file():shutil.copy2(f,a/f.name)
shutil.copy2(root/"bin/Release/net10.0/AIKernel.Core.Tests.dll",a/"AIKernel.Core.Tests.dll")
files=["cases.json","reference.json","prepare.py","score.py","Program.cs","Probe.csproj","app/AIKernel.Core.Tests.dll","app/AIKernel.Kernel.dll"]
save("preregistration.json",dict(diagnosticOnly=True,priorTestedKernel="98bda15926941db1f1912f34d4ebc764a467a3e18245a2f0b43755d7c562c8be",
    rule="Run the original one-case Pathos draft at 256 tokens. Require identical original visible 192-token prefix, natural stop, nonempty <=1024 UTF-16 units, and a valid existing v28 model selection. One short English 25-word reason instruction may be tried if incomplete, oversized or otherwise invalid. No retry after a valid wrong finding. A prefix mismatch leaves the baseline comparison unverified. One extra reason maximum, 5 minutes per route, 10 minutes total.",
    selectedFindingRequired="supported",rawIdsUnavailable=True,officialVotes=0,files={n:digest(root/n) for n in files}))
print(digest(root/"preregistration.json"))
