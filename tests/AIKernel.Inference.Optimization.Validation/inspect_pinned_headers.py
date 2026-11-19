"""Opt-in integration check: download bounded, pinned GGUF prefixes, never full models."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from urllib.request import Request, urlopen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runner", type=Path, help="Built AIKernel.Core.Tests.exe (validation harness)")
    parser.add_argument("output", type=Path, help="Cache directory outside tracked source")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    evidence = repo / "docs/development/evidence/model-expansion-20261115"
    provenance = json.loads((evidence / "header-provenance.json").read_text())
    expected = json.loads((evidence / "header-validation.json").read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    for item, reference in zip(provenance, expected, strict=True):
        limit = item["headerBytes"]
        if not 0 < limit <= 64 * 1024 * 1024:
            raise ValueError("Unbounded header download requested")
        path = args.output / (item["name"] + ".header")
        if not path.exists():
            request = Request(item["sourceUrl"], headers={"Range": f"bytes=0-{limit-1}"})
            with urlopen(request, timeout=60) as response:
                if response.status == 206 and not response.headers.get("Content-Range", "").startswith("bytes 0-"):
                    raise ValueError("Unexpected download range")
                # A server may ignore Range; close after the bounded read in either case.
                data = response.read(limit)
            if len(data) != limit or hashlib.sha256(data).hexdigest() != item["headerSha256"]:
                raise ValueError("Downloaded prefix does not match the pinned artifact")
            path.write_bytes(data)
        if path.stat().st_size != limit or hashlib.sha256(path.read_bytes()).hexdigest() != item["headerSha256"]:
            raise ValueError("Cached prefix does not match the pinned artifact")
        run = subprocess.run([str(args.runner.resolve()), "--inspect-header", str(path.resolve()), str(item["fileBytes"])],
                             check=True, capture_output=True, text=True, timeout=60)
        actual = json.loads(run.stdout)
        if actual != {k: v for k, v in reference.items() if k != "model"}:
            raise ValueError(f"Directory differs from pinned inventory: {item['name']}")
        results.append({"model": item["name"], **actual})
        print(f"{item['name']}: directory matched; generation was not tested", flush=True)
    (args.output / "inspection.json").write_text(json.dumps(results, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
