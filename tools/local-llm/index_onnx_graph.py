"""Index the verified FP32 ONNX graph for the restricted Qwen3 CPU execution plan.

Rebuild with: python tools/local-llm/index_onnx_graph.py model.onnx output.tsv
The graph digest covers domains, opsets, attributes, edges, dynamic shapes and all
inline constants. An export with different semantics requires a new reviewed plan.
"""
from pathlib import Path
import hashlib
import mmap
import sys

GRAPH_SHA256 = "0a8f1cfe531fc7b15d26db55090f0667863f0c62c952dff128cf23cd421e3fbc"


def integer(data, start):
    value = 0
    for shift in range(0, 70, 7):
        byte = data[start]
        start += 1
        value |= (byte & 127) << shift
        if byte < 128:
            return value, start
    raise ValueError("Invalid protobuf integer")


def fields(data, start, limit):
    while start < limit:
        key, start = integer(data, start)
        wire = key % 8
        if wire == 2:
            length, start = integer(data, start)
            end = start + length
        elif wire == 0:
            _, end = integer(data, start)
        else:
            end = start + {1: 8, 5: 4}[wire]
        if end > limit:
            raise ValueError("Field outside graph")
        yield key // 8, start, end
        start = end


def layout(data, start, end):
    shape = []
    external = {}
    payload = None
    for key, a, b in fields(data, start, end):
        if key == 1:
            shape.append(integer(data, a)[0])
        elif key == 2 and integer(data, a)[0] != 1:
            raise ValueError("Expected FP32")
        elif key == 8:
            name = data[a:b].decode()
        elif key == 9:
            payload = ("onnx/model.onnx", a, b - a)
        elif key == 13:
            values = {k: data[x:y].decode() for k, x, y in fields(data, a, b)}
            external[values[1]] = values[2]
    if external:
        assert external["location"] == "model.onnx_data" and payload is None
        payload = ("onnx/model.onnx_data", int(external["offset"]), int(external["length"]))
    if not payload or len(shape) not in (1, 2):
        raise ValueError(name)
    rows, cols = shape if len(shape) == 2 else (1, shape[0])
    assert payload[2] == rows * cols * 4
    return (name, rows, cols, *payload)


def main(graph, output):
    with graph.open("rb") as stream, mmap.mmap(stream.fileno(), 0, access=mmap.ACCESS_READ) as data:
        if hashlib.sha256(data).hexdigest() != GRAPH_SHA256:
            raise ValueError("Unreviewed ONNX graph")
        a, b = next((s, e) for k, s, e in fields(data, 0, len(data)) if k == 7)
        tensors = [layout(data, s, e) for k, s, e in fields(data, a, b) if k == 5]
    assert len(tensors) == len({v[0] for v in tensors}) == 312
    with output.open("w", encoding="utf-8", newline="\n") as out:
        for tensor in tensors:
            out.write("\t".join(map(str, tensor)) + "\n")


if __name__ == "__main__":
    main(*map(Path, sys.argv[1:]))
