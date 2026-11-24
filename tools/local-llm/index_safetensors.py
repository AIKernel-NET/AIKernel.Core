"""Regenerate the revision-pinned BF16/FP32 tensor digests used for reshards."""
import hashlib
import json
import struct
import sys
from pathlib import Path

import numpy as np


def index(checkpoint, target):
    with checkpoint.open("rb") as stream:
        actual = hashlib.file_digest(stream, "sha256").hexdigest()
        if actual != "f47f71177f32bcd101b7573ec9171e6a57f4f4d31148d38e382306f42996874b":
            raise ValueError("Expected the pinned upstream model.safetensors")
        stream.seek(0)
        length, = struct.unpack("<Q", stream.read(8))
        header = json.loads(stream.read(length))
        start = stream.tell()
        with target.open("w", encoding="ascii", newline="\n") as output:
            for name, spec in sorted(header.items()):
                if name == "__metadata__":
                    continue
                if spec["dtype"] != "BF16":
                    raise ValueError(spec["dtype"])
                first, last = spec["data_offsets"]
                stream.seek(start + first)
                bf16, fp32 = hashlib.sha256(), hashlib.sha256()
                remaining = last - first
                while remaining:
                    chunk = stream.read(min(remaining, 1 << 20))
                    if not chunk:
                        raise EOFError(name)
                    bf16.update(chunk)
                    fp32.update(np.left_shift(np.frombuffer(chunk, "<u2").astype("<u4"), 16).astype("<u4", copy=False))
                    remaining -= len(chunk)
                print(name, bf16.hexdigest(), fp32.hexdigest(), sep="\t", file=output)


if __name__ == "__main__":
    index(Path(sys.argv[1]), Path(sys.argv[2]))
