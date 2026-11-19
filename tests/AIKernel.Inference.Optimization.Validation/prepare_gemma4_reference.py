"""Fetch pinned, checksummed Google tokenizer/config assets. Does not download model weights."""
import argparse
import hashlib
import json
from pathlib import Path
from urllib.request import urlopen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    evidence = Path(__file__).resolve().parents[2] / "docs/development/evidence/gemma4-e2b-20261115"
    source = evidence / "reference-provenance.json"
    provenance = json.loads(source.read_text())
    args.output.mkdir(parents=True, exist_ok=True)
    for name in ["config.json", "tokenizer.json", "tokenizer_config.json", "chat_template.jinja"]:
        item = provenance["files"][name]
        path = args.output / name
        if path.exists():
            data = path.read_bytes()
        else:
            with urlopen(item["url"], timeout=90) as response:
                data = response.read(item["bytes"]+1)
        if len(data) != item["bytes"] or hashlib.sha256(data).hexdigest() != item["sha256"]:
            raise ValueError(f"Pinned asset mismatch: {name}")
        path.write_bytes(data)
        print(name, "verified")
    (args.output / "reference-provenance.json").write_bytes(source.read_bytes())


if __name__ == "__main__":
    main()
