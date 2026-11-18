from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parent
p=root.parent
read=lambda f:json.loads(f.read_bytes())
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
old=read(root/"prior-registration.json")
assert sha(root/"prior-registration.json")==sha(p/"preregistration.json")=="7c2003d502be71301ecdd3a3f442c05fcf3a0b77466ac82c0575ce2e7df490e2"
assert not (root/"preregistration.json").exists()
identical=("cases.json","source-inputs.json","sources.json","expectations.json","prompts.json")
for k in identical: assert sha(root/k)==sha(p/k)==old["files"][k]
a=root/"app";a.mkdir(exist_ok=True)
for f in (p/"app").iterdir():
    if f.is_file():shutil.copy2(f,a/f.name)
shutil.copy2(root/"bin/Release/net10.0/AIKernel.Core.Tests.dll",a/"AIKernel.Core.Tests.dll")
names=(*identical,"Program.cs","Probe.csproj","score.py","freeze.py","prior-registration.json","app/AIKernel.Core.Tests.dll","app/AIKernel.Kernel.dll")
assert sha(a/"AIKernel.Kernel.dll")==old["files"]["app/AIKernel.Kernel.dll"]
reg=dict(old)
reg.pop("newModelCalls",None)
reg.update(priorRegistrationSha256=sha(p/"preregistration.json"),amendmentBeforeInference=True,
    amendment="Keep failed cells and their raw draft or selection when available, continue to later cells, score each invalid output as a failure. No candidate input, expected finding, original wording, or adoption criteria changed.",
    maximumNewModelCalls=12,files={k:sha(root/k) for k in names})
(root/"preregistration.json").write_text(json.dumps(reg,ensure_ascii=False,indent=2)+"\n",encoding="utf8");print(sha(root/"preregistration.json"))
