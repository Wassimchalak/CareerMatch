#!/usr/bin/env python3
"""Analyze independently labeled, consented and anonymized AI match cases.

CSV: case_id,human_score,ai_score,seniority_gap,repeat_group
No example or synthetic scores are generated.
"""
import argparse
import collections
import csv
import json
import statistics
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("csv")
parser.add_argument("--output", default="results/matching_summary.json")
args = parser.parse_args()

with open(args.csv, newline="", encoding="utf-8") as f:
    rows = list(csv.DictReader(f))
if not rows:
    parser.error("Provide actual evaluated cases; the input CSV was empty")
required = {"case_id", "human_score", "ai_score", "seniority_gap", "repeat_group"}
if not required.issubset(rows[0]):
    parser.error("CSV must contain: " + ", ".join(sorted(required)))
for row in rows:
    row["human_score"] = float(row["human_score"])
    row["ai_score"] = float(row["ai_score"])
    if not 0 <= row["human_score"] <= 100 or not 0 <= row["ai_score"] <= 100:
        parser.error("All scores must be within 0..100")
errors = [abs(r["ai_score"] - r["human_score"]) for r in rows]
out = {"n": len(rows), "mae_points": round(statistics.mean(errors), 2)}
for threshold in (50, 60, 70):
    tp = sum(r["ai_score"] >= threshold and r["human_score"] >= threshold for r in rows)
    fp = sum(r["ai_score"] >= threshold and r["human_score"] < threshold for r in rows)
    fn = sum(r["ai_score"] < threshold and r["human_score"] >= threshold for r in rows)
    out[f"threshold_{threshold}"] = {
        "precision": tp/(tp+fp) if tp+fp else None,
        "recall": tp/(tp+fn) if tp+fn else None,
        "selected_by_ai": tp+fp
    }
by_gap = collections.defaultdict(list)
repeat = collections.defaultdict(list)
for row in rows:
    by_gap[row["seniority_gap"].strip().lower()].append(row["ai_score"])
    if row["repeat_group"].strip():
        repeat[row["repeat_group"].strip()].append(row["ai_score"])
out["mean_ai_score_by_seniority_gap"] = {
    k: round(statistics.mean(v), 2) for k,v in by_gap.items()
}
out["repeat_score_range_by_group"] = {
    k: max(v)-min(v) for k,v in repeat.items() if len(v)>1
}
Path(args.output).parent.mkdir(parents=True, exist_ok=True)
Path(args.output).write_text(json.dumps(out, indent=2), encoding="utf-8")
print(json.dumps(out, indent=2))
