"""Compare identical-history FP32 traces; tolerances are fixed, not fitted per tensor."""
import argparse
import json
from pathlib import Path
import numpy as np


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("reference", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    candidate = json.loads((args.candidate / "result.json").read_text())
    reference = json.loads((args.reference / "result.json").read_text())
    # Native scalar FP32 vs the upstream batched PyTorch reductions are not bitwise identical.
    max_abs_limit, rms_limit = 1e-3, 1e-4
    rows = []
    names = {p.name for p in args.reference.glob("*.f32")}
    names |= {p.name for p in args.candidate.glob("*.f32") if p.name.startswith("logits-")}
    for name in sorted(names):
        a = np.fromfile(args.candidate / name, dtype="<f4").astype(np.float64)
        b = np.fromfile(args.reference / name, dtype="<f4").astype(np.float64)
        if a.shape != b.shape or not np.isfinite(a).all() or not np.isfinite(b).all():
            raise ValueError(f"Invalid comparison shape/numbers: {name}")
        difference = a-b
        maximum, rms = float(np.abs(difference).max()), float(np.sqrt(np.mean(difference**2)))
        rows.append({"name": name, "elements": len(a), "maximumAbsoluteError": maximum, "rootMeanSquareError": rms,
                     "pass": maximum <= max_abs_limit and rms <= rms_limit})
    report = {"limits": {"maximumAbsoluteError": max_abs_limit, "rootMeanSquareError": rms_limit},
              "tokensEqual": candidate["tokens"] == reference["tokens"], "tokens": candidate["tokens"], "comparisons": rows}
    report["pass"] = bool(rows) and report["tokensEqual"] and all(row["pass"] for row in rows)
    args.output.write_text(json.dumps(report, indent=2))
    print(json.dumps({"pass": report["pass"], "comparisons": len(rows), "tokensEqual": report["tokensEqual"],
                      "maximumAbsoluteError": max(r["maximumAbsoluteError"] for r in rows),
                      "maximumLogitError": max(r["maximumAbsoluteError"] for r in rows if r["name"].startswith("logits-"))}))
    if not report["pass"]:
        raise SystemExit("Numerical gate failed")


if __name__ == "__main__":
    main()
