"""Generate independent CPU logits and first-layer observations for the pinned Qwen3 graph."""
import argparse
import hashlib
import json
import tempfile
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
from tokenizers import Tokenizer

GRAPH_SHA256 = "0a8f1cfe531fc7b15d26db55090f0667863f0c62c952dff128cf23cd421e3fbc"
PROMPTS = {
    "en": ("Answer briefly.", "What is the capital of France?"),
    "ja": ("簡潔に答えてください。", "日本の首都はどこですか？"),
}
STAGES = {
    "embedding": "/model/embed_tokens/Gather/output_0",
    "norm": "/model/layers.0/input_layernorm/output_0",
    "query": "/model/layers.0/attn/q_proj/MatMul/output_0",
}


def session(path):
    options = ort.SessionOptions()
    options.intra_op_num_threads = 4
    options.inter_op_num_threads = 1
    return ort.InferenceSession(str(path), options, providers=["CPUExecutionProvider"])


def inputs(ids, past, cache):
    return dict(cache, input_ids=np.array([ids], np.int64),
                attention_mask=np.ones((1, past + len(ids)), np.int64),
                position_ids=np.arange(past, past + len(ids), dtype=np.int64)[None, :])


def empty_cache(model):
    return {v.name: np.zeros((1, 8, 0, 128), np.float32) for v in model.get_inputs()
            if v.name.startswith("past_key_values.")}


def generate(model, tokenizer, destination):
    for lang, (system, user) in PROMPTS.items():
        prompt = (f"<|im_start|>system\n{system}<|im_end|>\n<|im_start|>user\n{user}<|im_end|>\n"
                  "<|im_start|>assistant\n<think>\n\n</think>\n\n")
        ids = tokenizer.encode(prompt, add_special_tokens=False).ids
        feed = inputs(ids, 0, empty_cache(model))
        generated = []
        for step in range(16):
            logits, *present = model.run(None, feed)
            scores = logits[0, -1, :]
            scores.tofile(destination / f"{lang}-{step:02}.f32")
            token = int(np.argmax(scores))
            generated.append(token)
            if token in (151643, 151645):
                break
            cache = {v.name.replace("present.", "past_key_values.", 1): a
                     for v, a in zip(model.get_outputs()[1:], present)}
            feed = inputs([token], len(ids) + step, cache)
        text = tokenizer.decode(generated, skip_special_tokens=True).strip()
        record = dict(system=system, user=user, input=ids, generated=generated, text=text,
                      textSha256=hashlib.sha256(text.encode()).hexdigest(), onnxRuntime=ort.__version__)
        (destination / f"{lang}.json").write_text(json.dumps(record, ensure_ascii=False), encoding="utf-8")


def trace(graph, destination):
    doc = onnx.load(graph, load_external_data=False)
    for stage, name in STAGES.items():
        dim = 2048 if stage == "query" else 1024
        doc.graph.output.append(onnx.helper.make_tensor_value_info(name, onnx.TensorProto.FLOAT, [1, "seq", dim]))
    # Keep relative external-data references valid. Never write into the original graph.
    with tempfile.NamedTemporaryFile(dir=graph.parent, suffix=".onnx", delete=False) as temp:
        path = Path(temp.name)
    try:
        onnx.save(doc, path)
        del doc
        model = session(path)
        for lang in PROMPTS:
            ids = json.loads((destination / f"{lang}.json").read_bytes())["input"]
            arrays = model.run(list(STAGES.values()), inputs(ids, 0, empty_cache(model)))
            for stage, values in zip(STAGES, arrays):
                values.tofile(destination / f"{lang}-{stage}.f32")
    finally:
        path.unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--trace-only", action="store_true")
    args = parser.parse_args()
    graph = args.snapshot / "onnx/model.onnx"
    with graph.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != GRAPH_SHA256:
            raise ValueError("Unexpected ONNX graph")
    args.output.mkdir(parents=True, exist_ok=True)
    if not args.trace_only:
        generate(session(graph), Tokenizer.from_file(str(args.snapshot / "tokenizer.json")), args.output)
    trace(graph, args.output)
