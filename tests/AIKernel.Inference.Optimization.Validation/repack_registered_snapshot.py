"""Prepare a two-shard BF16 or losslessly widened FP32 acceptance fixture from the pinned model."""
import argparse
import hashlib
import json
import shutil
import struct
from pathlib import Path

import numpy as np


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_shard(source, base, tensors, output, widen):
    header = {}
    offset = 0
    for name, tensor in tensors:
        if tensor["dtype"] != "BF16":
            raise ValueError(name)
        first, last = tensor["data_offsets"]
        end = offset + (last - first) * (2 if widen else 1)
        header[name] = dict(shape=tensor["shape"], dtype="F32" if widen else "BF16", data_offsets=[offset, end])
        offset = end
    encoded = json.dumps(header, separators=(",", ":")).encode()
    encoded += b" " * (-len(encoded) % 8)
    with output.open("xb") as target:
        target.write(struct.pack("<Q", len(encoded)))
        target.write(encoded)
        for _, tensor in tensors:
            first, last = tensor["data_offsets"]
            source.seek(base + first)
            remaining = last - first
            while remaining:
                chunk = source.read(min(remaining, 1 << 20))
                if not chunk:
                    raise EOFError(output.name)
                remaining -= len(chunk)
                if widen:
                    chunk = (np.frombuffer(chunk, "<u2").astype("<u4") << 16).astype("<u4", copy=False)
                target.write(chunk)
    return offset


def repack(snapshot, output, profile, widen):
    data = json.loads(profile.read_bytes())
    weights = snapshot / "model.safetensors"
    if digest(weights) != data["weightsSha256"]:
        raise ValueError("Expected the pinned upstream checkpoint")
    info = Path(__file__).resolve().parents[2] / "src/AIKernel.Kernel/Inference/Import/Data/safetensors.json"
    output.mkdir(parents=True, exist_ok=False)
    for name in json.loads(info.read_bytes())["files"]:
        if name != weights.name:
            shutil.copyfile(snapshot / name, output / name)
    lock, assignments = {}, {}
    size = 0
    with weights.open("rb") as source:
        length, = struct.unpack("<Q", source.read(8))
        tensors = json.loads(source.read(length))
        base = source.tell()
        tensors.pop("__metadata__", None)
        entries = list(tensors.items())
        middle = len(entries) // 2
        for part, group in enumerate((entries[:middle], entries[middle:]), 1):
            path = output / f"model-{part:05}-of-00002.safetensors"
            size += write_shard(source, base, group, path, widen)
            lock[path.name] = digest(path)
            assignments.update({name: path.name for name, _ in group})
    index = output / "model.safetensors.index.json"
    index.write_bytes(json.dumps(dict(metadata={"total_size": size}, weight_map=assignments), sort_keys=True).encode())
    lock[index.name] = digest(index)
    receipt = index.with_suffix(".json.lock.json")
    receipt.write_bytes(json.dumps(lock, sort_keys=True).encode())
    data["weightsSha256"] = digest(receipt)
    (output / "registration.json").write_bytes(json.dumps(data).encode())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    parser.add_argument("output", type=Path, help="A new, empty fixture directory")
    parser.add_argument("profile", type=Path)
    parser.add_argument("--fp32", action="store_true")
    args = parser.parse_args()
    repack(args.snapshot, args.output, args.profile, args.fp32)
