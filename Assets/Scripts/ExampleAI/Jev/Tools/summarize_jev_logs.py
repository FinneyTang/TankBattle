#!/usr/bin/env python3
"""Aggregate Jev tank logs (Logs/Jev/*.jsonl) into per-match and per-style tables.

Usage:
    python3 summarize_jev_logs.py [LOG_DIR_OR_FILES...] [--json OUT.json] [--max-hp 100] [--since 20260922_1400]

Works with or without the match-end `summary` record: everything is recomputed from the
per-decision records, so matches that were stopped early are included (flagged incomplete).
"""
import argparse
import glob
import json
import os
import statistics
import sys
from collections import Counter, defaultdict

MOVE_ORDER = ["star", "enemy", "teammate", "home", "away", "hold", "roam", "sidestep", "missing"]
HP_ORDER = ["full", "high", "medium", "low", "critical"]
INPUT_PRICE_PER_MILLION = 0.042


def move_category(option):
    if not option:
        return "missing"
    if option.startswith("star_"):
        return "star"
    if option.startswith("enemy_"):
        return "enemy"
    if option.startswith("teammate_"):
        return "teammate"
    if option == "away_from_enemies":
        return "away"
    return option


def hp_level_from_int(hp, max_hp):
    ratio = hp / max_hp if max_hp else 0
    if ratio >= 0.99:
        return "full"
    if ratio >= 0.7:
        return "high"
    if ratio >= 0.4:
        return "medium"
    return "low" if ratio >= 0.2 else "critical"


def hp_level(record, max_hp):
    state = record.get("state") or {}
    self_state = state.get("self") or {}
    text = self_state.get("hp")
    if isinstance(text, str) and "," in text:
        return text.split(",", 1)[0].strip()
    if "hp" in record:
        return hp_level_from_int(record["hp"], max_hp)
    return "unknown"


def pct(counter):
    total = sum(counter.values())
    if not total:
        return "-"
    keys = sorted(counter, key=lambda k: (-counter[k], MOVE_ORDER.index(k) if k in MOVE_ORDER else 99))
    return "  ".join(f"{k} {100.0 * counter[k] / total:.0f}%" for k in keys)


def percentile(values, q):
    if not values:
        return 0.0
    values = sorted(values)
    idx = min(len(values) - 1, int(round(q * (len(values) - 1))))
    return values[idx]


def analyse_file(path, max_hp):
    start, summary = None, None
    decisions, failures = [], []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                rec = json.loads(line)
            except json.JSONDecodeError:
                continue
            kind = rec.get("kind")
            if kind == "start":
                start = rec
            elif kind == "decision":
                decisions.append(rec)
            elif kind == "failure":
                failures.append(rec)
            elif kind == "summary":
                summary = rec

    info = {
        "file": os.path.basename(path),
        "team": (start or {}).get("team") or (decisions[0]["team"] if decisions else "?"),
        "style": (start or {}).get("play_style", "n/a"),
        "dodge": (start or {}).get("dodge_mode", "Jev" if (start or {}).get("jev_dodge") else "n/a"),
        "interval": (start or {}).get("decision_interval"),
        "decisions": len(decisions),
        "failures": len(failures),
        "urgent": sum(1 for d in decisions if d.get("urgent")),
        "complete": summary is not None,
    }
    if not decisions:
        info.update(duration=0, latency_avg=0, latency_p50=0, latency_p95=0, latency_max=0,
                    input_tokens=0, output_tokens=0, cost=0, req_per_s=0, score=None,
                    move=Counter(), aim=Counter(), applied=Counter(), move_by_hp={}, home_prob_by_hp={},
                    winner=(summary or {}).get("winner"))
        return info

    latencies = [d.get("latency_ms", 0.0) for d in decisions]
    input_tokens = sum((d.get("usage") or {}).get("input_tokens", 0) for d in decisions)
    output_tokens = sum((d.get("usage") or {}).get("output_tokens", 0) for d in decisions)
    # Time.time is not reset when the runner reloads the scene, so measure the span, not the max.
    times = [d.get("t", 0.0) for d in decisions]
    duration = max(times) - min(times)

    move, aim, applied = Counter(), Counter(), Counter()
    move_by_hp = defaultdict(Counter)
    home_prob_by_hp = defaultdict(list)
    for d in decisions:
        m = d.get("move") or {}
        a = d.get("aim") or {}
        cat = move_category(m.get("answer"))
        move[cat] += 1
        applied[m.get("result") or "missing"] += 1
        if a.get("answer") is not None:
            aim["none" if a["answer"] == "none" else "enemy"] += 1
        level = hp_level(d, max_hp)
        move_by_hp[level][cat] += 1
        probs = m.get("probabilities") or {}
        if "home" in probs:
            home_prob_by_hp[level].append(probs["home"])

    info.update(
        duration=duration,
        latency_avg=statistics.fmean(latencies),
        latency_p50=percentile(latencies, 0.5),
        latency_p95=percentile(latencies, 0.95),
        latency_max=max(latencies),
        input_tokens=input_tokens,
        output_tokens=output_tokens,
        cost=input_tokens / 1_000_000 * INPUT_PRICE_PER_MILLION,
        req_per_s=(len(decisions) + len(failures)) / duration if duration > 1 else 0.0,
        score=(summary or {}).get("score", decisions[-1].get("score")),
        winner=(summary or {}).get("winner"),
        move=move, aim=aim, applied=applied,
        move_by_hp={k: dict(v) for k, v in move_by_hp.items()},
        home_prob_by_hp={k: statistics.fmean(v) for k, v in home_prob_by_hp.items() if v},
    )
    return info


def merge(infos):
    agg = {
        "matches": len(infos),
        "complete": sum(1 for i in infos if i["complete"]),
        "decisions": sum(i["decisions"] for i in infos),
        "failures": sum(i["failures"] for i in infos),
        "urgent": sum(i["urgent"] for i in infos),
        "input_tokens": sum(i["input_tokens"] for i in infos),
        "output_tokens": sum(i["output_tokens"] for i in infos),
        "cost": sum(i["cost"] for i in infos),
        "duration": sum(i["duration"] for i in infos),
        "move": Counter(), "aim": Counter(), "applied": Counter(),
        "move_by_hp": defaultdict(Counter),
    }
    weighted_latency = 0.0
    for i in infos:
        agg["move"].update(i["move"])
        agg["aim"].update(i["aim"])
        agg["applied"].update(i["applied"])
        for level, counts in i["move_by_hp"].items():
            agg["move_by_hp"][level].update(counts)
        weighted_latency += i["latency_avg"] * i["decisions"]
    agg["latency_avg"] = weighted_latency / agg["decisions"] if agg["decisions"] else 0.0
    agg["latency_max"] = max((i["latency_max"] for i in infos), default=0.0)
    scores = [i["score"] for i in infos if i["score"] is not None]
    agg["score_avg"] = statistics.fmean(scores) if scores else None
    wins = [i for i in infos if i["complete"] and i.get("winner") == i["team"]]
    agg["wins"] = len(wins)
    agg["req_per_s"] = (agg["decisions"] + agg["failures"]) / agg["duration"] if agg["duration"] > 1 else 0.0
    return agg


def print_per_match(infos):
    print("## Per match\n")
    print("| file | team | style | dodge | done | decisions | urgent | fail | game s | req/s | lat avg/p95/max ms | tokens in/out | cost $ | score | move answers | aim |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for i in infos:
        print(f"| {i['file']} | {i['team']} | {i['style']} | {i['dodge']} | {'yes' if i['complete'] else 'no'} | "
              f"{i['decisions']} | {i['urgent']} | {i['failures']} | {i['duration']:.0f} | {i['req_per_s']:.2f} | "
              f"{i['latency_avg']:.0f}/{i['latency_p95']:.0f}/{i['latency_max']:.0f} | "
              f"{i['input_tokens']:,}/{i['output_tokens']:,} | {i['cost']:.4f} | "
              f"{'-' if i['score'] is None else i['score']} | {pct(i['move'])} | {pct(i['aim'])} |")
    print()


def print_groups(infos):
    groups = defaultdict(list)
    for i in infos:
        groups[(i["style"], i["dodge"])].append(i)
    print("## By play style / dodge mode\n")
    for (style, dodge), members in sorted(groups.items()):
        agg = merge(members)
        print(f"### style={style}  dodge={dodge}  ({agg['matches']} matches, {agg['complete']} complete, "
              f"{agg['wins']} wins of complete)\n")
        print(f"- decisions {agg['decisions']} (urgent {agg['urgent']}, failed {agg['failures']}), "
              f"{agg['req_per_s']:.2f} req/s over {agg['duration']:.0f} game-seconds")
        print(f"- latency avg {agg['latency_avg']:.0f} ms, max {agg['latency_max']:.0f} ms")
        avg_in = agg['input_tokens'] / agg['decisions'] if agg['decisions'] else 0
        print(f"- tokens in {agg['input_tokens']:,} / out {agg['output_tokens']:,} "
              f"(avg {avg_in:.0f} in per request), cost ~${agg['cost']:.4f}")
        if agg["score_avg"] is not None:
            print(f"- score avg {agg['score_avg']:.1f}")
        print(f"- move answers: {pct(agg['move'])}")
        print(f"- aim answers: {pct(agg['aim'])}")
        print(f"- applied: {pct(agg['applied'])}")
        print("- move answers by my HP level:")
        for level in HP_ORDER + [k for k in agg["move_by_hp"] if k not in HP_ORDER]:
            if level in agg["move_by_hp"]:
                counts = agg["move_by_hp"][level]
                print(f"    - {level:8s} [{sum(counts.values()):4d}]: {pct(counts)}")
        print()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", help="log files or directories (default: Logs/Jev next to the project)")
    parser.add_argument("--json", help="also write the aggregated data to this file")
    parser.add_argument("--max-hp", type=int, default=100, help="MaxHP used to bucket hp when the state is not logged")
    parser.add_argument("--since", help="only files whose timestamp part is >= this (yyyyMMdd_HHmmss prefix)")
    parser.add_argument("--min-decisions", type=int, default=5, help="skip logs with fewer decisions")
    args = parser.parse_args()

    paths = args.paths
    if not paths:
        here = os.path.dirname(os.path.abspath(__file__))
        project = os.path.abspath(os.path.join(here, "..", "..", "..", "..", ".."))
        paths = [os.path.join(project, "Logs", "Jev")]
    files = []
    for p in paths:
        if os.path.isdir(p):
            files.extend(sorted(glob.glob(os.path.join(p, "*.jsonl"))))
        else:
            files.extend(sorted(glob.glob(p)))
    if args.since:
        files = [f for f in files if os.path.basename(f).rsplit("_", 2)[-2:] and
                 "_".join(os.path.basename(f)[:-6].rsplit("_", 2)[-2:]) >= args.since]
    if not files:
        print("no log files found", file=sys.stderr)
        return 1

    infos = [analyse_file(f, args.max_hp) for f in files]
    infos = [i for i in infos if i["decisions"] >= args.min_decisions]
    if not infos:
        print("no logs with enough decisions", file=sys.stderr)
        return 1

    print_per_match(infos)
    print_groups(infos)

    if args.json:
        serialisable = []
        for i in infos:
            j = dict(i)
            for key in ("move", "aim", "applied"):
                j[key] = dict(i[key])
            serialisable.append(j)
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(serialisable, fh, indent=2, ensure_ascii=False)
        print(f"wrote {args.json}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
