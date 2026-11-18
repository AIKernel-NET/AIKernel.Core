from pathlib import Path
import hashlib,json
p=Path(__file__).resolve().parent
def read(q):return json.loads(q.read_bytes())
def digest(q):return hashlib.sha256(q.read_bytes()).hexdigest()
def hs(text):return hashlib.sha256(text.encode()).hexdigest()
pr=read(p/"preregistration.json")
assert all(digest(p/n)==s for n,s in pr['files'].items())
raw=read(p/"result.json"); cs=read(p/"cases.json"); key=read(p/"key.json")
assert raw["diagnosticOnly"] and raw["modelHash"]==pr["model"] and raw["kernelHash"]==pr["kernel"]
assert raw["tokenIds"]==pr["tokenIds"] and raw["planned"]==len(raw["rows"])==len(cs)==len(key)==16
reports=[]
for row,c,k in zip(raw["rows"],cs,key,strict=True):
    assert c["id"]==row["id"]==k["id"] and c["promptSha256"]==row["promptSha256"]==hs(c["system"]+"\n"+c["user"])
    assert c["romSha256"]==row["romSha256"]==pr["rom"] and c["labels"]==pr["labels"]
    a=row["answer"];symbol=a["Text"]
    assert a["FinishReason"]=="classified" and a["OutputTokens"]==1 and symbol in c["labels"] and row["responseSha256"]==hs(symbol)
    reports.append(dict(**k,selection=symbol,correct=k["expected"]==symbol,promptSha256=row["promptSha256"],responseSha256=row["responseSha256"]))
ok=sum(x["correct"] for x in reports)
sc=dict(diagnosticOnly=True,calls=16,correct=ok,advance=ok==16,
    correctlyDetectedDefects=sum(r["selection"]==r["expected"]=="Bad" for r in reports),
    spuriousDefects=sum(r["selection"]=="Bad" and r["expected"]!="Bad" for r in reports),
    missedDefects=sum(r["expected"]=="Bad" and r["selection"]!="Bad" for r in reports),
    preregistrationSha256=digest(p/"preregistration.json"),resultSha256=digest(p/"result.json"),rows=reports)
(p/"score.json").write_text(json.dumps(sc,indent=2,ensure_ascii=False)+"\n",encoding="utf8")
print([(r["sourceId"],r["expected"],r["selection"]) for r in reports]);print(f"{ok}/16, advance={ok==16}")

