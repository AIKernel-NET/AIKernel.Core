"""Import the text/vision reasoner from an immutable, public NVIDIA Cosmos3 snapshot.

Offline preparation tool only; AIKernel runtime does not depend on Python/Torch.
Requires a Transformers version containing Cosmos3Edge/Cosmos3Omni. Every source
shard is SHA-256 checked against HF's LFS metadata before copying tensor bytes.
Diffusion, audio and action-generation weights are deliberately excluded.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import urllib.request
from concurrent.futures import ThreadPoolExecutor

REVISIONS = {"Edge": "6afc1256321c3a8e623d89be3bc5117a1e90f96b",
             "Nano": "45666784803b3a7ad577e0def405f9afcc3bddb2"}


def rename(name, edge):
    name = re.sub(r"^(layers\.|embed_tokens\.|norm\.)", r"model.language_model.\1", name)
    if not edge:
        name = re.sub(r"^(blocks\.|merger\.|patch_embed\.|pos_embed\.|deepstack_merger_list\.)", r"model.visual.\1", name)
    for a, b in [("to_q", "q_proj"), ("to_k", "k_proj"), ("to_v", "v_proj"), ("to_out", "o_proj"),
                 ("norm_q", "q_norm"), ("norm_k", "k_norm")]:
        name = name.replace(f".self_attn.{a}.", f".self_attn.{b}.")
    if edge:
        name = name.replace(".mlp.up_proj.", ".mlp.fc1.").replace(".mlp.down_proj.", ".mlp.fc2.")
    return name


def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(4 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def fetch(url, path, expected=None, size=None, refresh=False):
    if not refresh and path.exists() and (expected is None or sha256(path) == expected):
        return
    part = path.with_suffix(path.suffix + ".partial")
    if size and size > 128 << 20:
        # Bound each request and allow retries without restarting a multi-GB file.
        block = 64 << 20
        def segment(start):
            end = min(start + block, size) - 1
            chunk_path = path.with_suffix(f".range-{start}")
            if chunk_path.exists() and chunk_path.stat().st_size == end-start+1:
                return chunk_path
            for attempt in range(3):
                try:
                    request = urllib.request.Request(url+f"?download=true&range_start={start}", headers={"Range": f"bytes={start}-{end}"})
                    with urllib.request.urlopen(request, timeout=60) as response, chunk_path.open("wb") as output:
                        if response.status != 206 or response.headers.get("Content-Range") != f"bytes {start}-{end}/{size}":
                            raise ValueError("Server did not honor the requested byte range")
                        shutil.copyfileobj(response, output, 1 << 20)
                    if chunk_path.stat().st_size != end-start+1:
                        raise ValueError("Incomplete range")
                    return chunk_path
                except Exception:
                    if attempt == 2:
                        raise
        with ThreadPoolExecutor(max_workers=4) as pool:
            chunks = list(pool.map(segment, range(0, size, block)))
        with part.open("wb") as output:
            for chunk in chunks:
                with chunk.open("rb") as source:
                    shutil.copyfileobj(source, output, 4 << 20)
    else:
        chunks = []
        with urllib.request.urlopen(url, timeout=120) as response, part.open("wb") as output:
            shutil.copyfileobj(response, output, 4 << 20)
    if expected and sha256(part) != expected:
        raise ValueError("Downloaded SHA-256 mismatch: " + path.name)
    part.replace(path)
    for chunk in chunks:
        chunk.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("family", choices=REVISIONS)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    os.environ["HF_HUB_DISABLE_IMPLICIT_TOKEN"] = "1"
    from huggingface_hub import HfApi
    import torch
    from transformers import AutoConfig, AutoModelForImageTextToText

    family, root = args.family, args.output.resolve()
    root.mkdir(parents=True, exist_ok=True)
    revision = REVISIONS[family]
    repo = "nvidia/Cosmos3-" + family
    base = f"https://huggingface.co/{repo}/resolve/{revision}/"
    info = HfApi(token=False).model_info(repo, revision=revision, files_metadata=True)
    files = {item.rfilename: item for item in info.siblings}
    if info.sha != revision:
        raise ValueError("Snapshot revision differs.")
    metadata = {}
    for name in ["config.json", "tokenizer.json", "tokenizer_config.json", "preprocessor_config.json",
                 "video_preprocessor_config.json", "processor_config.json", "chat_template.json", "chat_template.jinja"]:
        if name in files:
            fetch(base + name, root / name, refresh=True)
            metadata[name] = sha256(root / name)
    config = AutoConfig.from_pretrained(root, local_files_only=True, trust_remote_code=False)
    with torch.device("meta"):
        model = AutoModelForImageTextToText.from_config(config, trust_remote_code=False)
    required = {key: list(value.shape) for key, value in model.state_dict().items()}
    del model
    staging = root / "import-sources"
    staging.mkdir(exist_ok=True)
    fetch(base + "model.safetensors.index.json", staging / "index.json", refresh=True)
    source_map = json.loads((staging / "index.json").read_text(encoding="utf-8"))["weight_map"]
    selected = {}
    for source, shard in source_map.items():
        target = rename(source, family == "Edge")
        if target in required:
            if target in selected:
                raise ValueError("Duplicate mapped tensor: " + target)
            selected[target] = (source, shard)
    if set(selected) != set(required):
        raise ValueError("Missing required tensors: " + str(sorted(set(required) - set(selected))))
    output_map, evidence = {}, []
    for number, shard in enumerate(sorted({v[1] for v in selected.values()}), 1):
        item = files[shard]
        if item.lfs is None:
            raise ValueError("Missing source SHA-256: " + shard)
        source_path = staging / f"source-{number:02}.safetensors"
        print(f"{family}: download/verify {shard} ({item.size} bytes)", flush=True)
        fetch(base + shard, source_path, item.lfs.sha256, item.size)
        target_path = root / f"reasoner-{number:02}.safetensors"
        with source_path.open("rb") as source_file:
            length = struct.unpack("<Q", source_file.read(8))[0]
            if length > 16 << 20:
                raise ValueError("Source header too large")
            header = json.loads(source_file.read(length))
            offset, packed, sources = 0, {}, []
            for name, (source_name, source_shard) in selected.items():
                if source_shard != shard:
                    continue
                spec = header[source_name]
                if spec["shape"] != required[name] or spec["dtype"] not in ("F32", "BF16", "F16"):
                    raise ValueError("Unexpected source tensor: " + name)
                start, end = spec["data_offsets"]
                elements = 1
                for dimension in spec["shape"]:
                    elements *= dimension
                if start < 0 or end - start != elements * (4 if spec["dtype"] == "F32" else 2) or end + 8 + length > item.size:
                    raise ValueError("Invalid source tensor range")
                packed[name] = dict(dtype=spec["dtype"], shape=spec["shape"], data_offsets=[offset, offset + end - start])
                sources.append((name, start, end - start))
                offset += end - start
                output_map[name] = target_path.name
            encoded = json.dumps(packed, separators=(",", ":")).encode()
            encoded += b" " * (-len(encoded) % 8)
            temp = target_path.with_suffix(".partial")
            with temp.open("wb") as output:
                output.write(struct.pack("<Q", len(encoded)))
                output.write(encoded)
                for _, start, count in sources:
                    source_file.seek(8 + length + start)
                    while count:
                        chunk = source_file.read(min(count, 4 << 20))
                        if not chunk:
                            raise EOFError("Source tensor truncated")
                        output.write(chunk)
                        count -= len(chunk)
            temp.replace(target_path)
        evidence.append(dict(source=shard, sourceSha256=item.lfs.sha256, sourceBytes=item.size,
                             output=target_path.name, outputSha256=sha256(target_path), tensorCount=len(packed), tensorBytes=offset))
        print(f"{family}: imported {len(packed)} tensors; {offset} bytes", flush=True)
    (root / "model.safetensors.index.json").write_text(json.dumps({"metadata":{"total_size":sum(e["tensorBytes"] for e in evidence)},"weight_map": output_map}, indent=2), encoding="utf-8")
    (root / "import-manifest.json").write_text(json.dumps(dict(repository=repo, revision=revision,
        mode="text-image-video-to-text", requiredTensors=len(required), excludedSourceTensors=len(source_map)-len(selected),
        transformations="tensor names only; dtype and data bytes unchanged", metadataSha256=metadata, shards=evidence), indent=2), encoding="utf-8")
    print(f"{family}: complete ({len(required)} tensors)", flush=True)


if __name__ == "__main__":
    main()
