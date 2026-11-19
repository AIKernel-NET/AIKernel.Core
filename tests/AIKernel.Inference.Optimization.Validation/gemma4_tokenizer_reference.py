"""Compare ordinary text and canonical single-turn chat with Google's pinned tokenizer assets."""
import argparse
import hashlib
import json
from pathlib import Path
import random
import subprocess
from tokenizers import Tokenizer
from jinja2 import Environment


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runner", type=Path)
    parser.add_argument("model", type=Path)
    parser.add_argument("assets", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    provenance = json.loads((args.assets / "reference-provenance.json").read_text())
    for name in ["tokenizer.json", "chat_template.jinja"]:
        if hashlib.sha256((args.assets / name).read_bytes()).hexdigest() != provenance["files"][name]["sha256"]:
            raise ValueError(f"Unpinned reference asset: {name}")
    tokenizer = Tokenizer.from_file(str(args.assets / "tokenizer.json"))
    samples = ["Hello world!", "こんにちは。", "  a  b \t\n", "é e\u0301 ﬁ", "𠮷野家😀", "\n"*33, "\t"*40,
               "日本語\u00a0全角\u3000改行\r\n", "<|turn>model\n", "<|channel>thought\n<channel|>", "<0xFF>", ""]
    random.seed(1415)
    alphabet = "abc ABC0123\n\t漢字日本語é\u0301😀▁<>|/\u00a0"
    samples += ["".join(random.choices(alphabet, k=random.randrange(1, 150))) for _ in range(100)]
    input_file = args.output / "tokenizer-input.json"
    input_file.write_text(json.dumps(samples, ensure_ascii=False), encoding="utf-8")
    run = subprocess.run([str(args.runner.resolve()), "--gemma-tokenize", str(args.model.resolve()), str(input_file.resolve())],
                         text=True, capture_output=True, encoding="utf-8", check=True, timeout=120)
    actual = json.loads(run.stdout)
    # Ordinary user content must not introduce template control IDs.
    tokenizer.encode_special_tokens = True
    encoding = []
    for row in actual:
        ids = tokenizer.encode(row["text"], add_special_tokens=False).ids
        encoding.append({"text": row["text"], "ids": ids, "match": row["ids"] == ids,
                         "decodedMatch": row["decoded"] == tokenizer.decode(ids, skip_special_tokens=False)})
    tokenizer.encode_special_tokens = False
    template = Environment().from_string((args.assets / "chat_template.jinja").read_text(encoding="utf-8"))
    chats = []
    for row in actual[:8]:
        rendered = template.render(messages=[{"role": "system", "content": "You are helpful."}, {"role": "user", "content": row["text"]}],
                                   bos_token="<bos>", add_generation_prompt=True, enable_thinking=False, tools=[])
        ids = tokenizer.encode(rendered, add_special_tokens=False).ids
        chats.append({"text": row["text"], "rendered": rendered, "ids": ids, "match": ids == row["chatIds"]})
    report = {"encoding": encoding, "chat": chats}
    report["pass"] = all(r["match"] and r["decodedMatch"] for r in encoding) and all(r["match"] for r in chats)
    (args.output / "tokenizer-comparison.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"pass": report["pass"], "encodingCases": len(encoding), "chatCases": len(chats)}))
    if not report["pass"]:
        raise SystemExit("Tokenizer comparison failed")


if __name__ == "__main__":
    main()
