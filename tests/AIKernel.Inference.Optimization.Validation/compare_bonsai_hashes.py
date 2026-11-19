"""Functional Bonsai CPU/CUDA regression against a previously recorded 128-token baseline."""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runner", type=Path)
    parser.add_argument("models", type=Path)
    parser.add_argument("baseline", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    baseline = json.loads(args.baseline.read_text())
    results = []
    for entry in baseline["pairs"]:
        model = entry["model"]
        path = args.models / f"bonsai{model}b/Bonsai-{model}B-Q1_0.gguf"
        run = subprocess.run([str(args.runner.resolve()), str(path), entry["mode"], entry["precision"]],
                             capture_output=True, text=True, check=True, timeout=900)
        actual = json.loads(run.stdout.strip().splitlines()[-1])
        result = {"model": model, "mode": entry["mode"], "precision": entry["precision"],
                  "tokens": actual["OutputTokens"], "sha256": actual["GeneratedTokenSha256"],
                  "pass": actual["OutputTokens"] == 128 and actual["GeneratedTokenSha256"] == entry["sha256"]}
        results.append(result)
        args.output.write_text(json.dumps({"pairs": results, "performanceBenchmark": False}, indent=2))
        print(json.dumps(result), flush=True)
        if not result["pass"]:
            raise SystemExit("Bonsai hash regression")


if __name__ == "__main__":
    main()
