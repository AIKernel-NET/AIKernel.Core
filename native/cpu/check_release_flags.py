"""Verify the commands that CMake will actually execute, not just the cache type."""
import json
import shlex
import sys
from pathlib import Path

entries = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
if not entries:
    raise SystemExit("Empty compilation database")
for entry in entries:
    arguments = entry.get("arguments") or shlex.split(entry["command"])
    optimizations = [arg for arg in arguments if arg.startswith("-O")]
    if not optimizations or optimizations[-1] != "-O3":
        raise SystemExit(f"Expected effective -O3 for {entry['file']}: {optimizations}")
    if "-ffp-contract=off" not in arguments:
        raise SystemExit(f"Missing controlled FP contraction for {entry['file']}")
print(f"Verified -O3 and -ffp-contract=off for {len(entries)} compilation units")
