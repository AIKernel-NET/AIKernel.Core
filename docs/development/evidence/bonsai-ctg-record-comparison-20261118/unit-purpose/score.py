"""Score only the frozen four Logos findings; retain reasons for separate review."""
import argparse
import hashlib
import json
from pathlib import Path


MODEL = "3d7c6c90dd98717a203adb22d5eacd2581850e40aa5327e144b97766cae5f7e3"
ROOT = Path(__file__).resolve().parent


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)


def check(ok, message):
    if not ok:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def text_hash(value):
    return digest(value.encode("utf-8"))


def utf16_length(value):
    return len(value.encode("utf-16-le", errors="surrogatepass")) // 2


def parse_answer(raw, finish, source_indices):
    check(finish == "stop" and utf16_length(raw) <= 8192, "Incomplete or oversized response")
    value = raw.strip()
    for ending in ("\r\n", "\n"):
        start = "```json" + ending
        end = ending + "```"
        if value.startswith(start) and value.endswith(end):
            value = value[len(start):-len(end)]
            break
    result = json.loads(value, object_pairs_hook=unique_object)
    check(isinstance(result, dict) and set(result) == {"reason", "finding", "source"}, "Invalid assessment fields")
    reason, finding, source = (result[k] for k in ("reason", "finding", "source"))
    check(isinstance(reason, str) and reason.strip() and utf16_length(reason) <= 2048, "Invalid reason")
    check(finding in ("supported", "proposal_defect", "evaluation_unavailable"), "Invalid Logos finding")
    check(type(source) is int and 0 <= source < len(source_indices), "Invalid source")
    return {"reason": reason, "finding": finding, "source": source, "sourceKey": source_indices[source]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("result", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "score.json")
    args = parser.parse_args()
    registration = read(ROOT / "preregistration.json")
    inputs_path = ROOT / "matrix-cases.json"
    fixture, actual = read(inputs_path), read(args.result)
    check(digest(inputs_path.read_bytes()) == registration["inputSha256"], "Unregistered matrix bytes")
    plan = registration["planned"]
    ids = [p["id"] for p in plan]
    check(len(set(ids)) == len(ids) == 4 and [p["id"] for p in fixture] == ids, "Invalid four-cell plan")
    check(actual["diagnosticOnly"] is True and actual["planned"] == 4 and actual["modelHash"] == MODEL, "Wrong runner/model")
    check([r["id"] for r in actual["rows"]] == ids, "Incomplete or reordered results")
    scored = []
    for expected, prompt, raw in zip(plan, fixture, actual["rows"]):
        sha = text_hash(prompt["system"] + "\n" + prompt["user"])
        check(prompt["diagnosticOnly"] is True and
              sha == prompt["promptSha256"] == raw["promptSha256"] == expected["promptSha256"], "Prompt digest mismatch")
        answer = raw["answer"]
        value, finish = answer["Text"], answer["FinishReason"]
        check(raw["responseSha256"] == text_hash(value), "Raw answer digest mismatch")
        row = {"id": expected["id"], "promptSha256": sha, "responseSha256": raw["responseSha256"],
               "finishReason": finish, "expectedFinding": expected["expectedFinding"], "raw": value}
        try:
            detail = parse_answer(value, finish, expected["sourceIndices"])
            row.update(detail, schemaValid=True, findingMatches=detail["finding"] == expected["expectedFinding"])
        except ValueError as exc:
            row.update(schemaValid=False, findingMatches=False, error=str(exc))
        scored.append(row)
    correct = sum(row["findingMatches"] for row in scored)
    report = {"diagnosticOnly": True, "modelSha256": MODEL, "resultSha256": digest(args.result.read_bytes()),
              "registeredInputSha256": registration["inputSha256"], "complete": len(scored),
              "schemaValid": sum(row["schemaValid"] for row in scored), "findingMatches": correct,
              "fourWayPatternMatches": correct == 4,
              "reasonReview": "Review each reason against the physical relation and the authority. Correct labels alone are insufficient.",
              "rows": scored}
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(scored)} complete; {correct}/4 diagnostic findings match. Output {args.output}")


if __name__ == "__main__":
    main()
