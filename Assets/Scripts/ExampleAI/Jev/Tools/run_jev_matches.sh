#!/usr/bin/env bash
# Play N unattended matches and summarise the Jev logs afterwards.
#
#   Tools/run_jev_matches.sh -n 5 -a Jev.MyTank -b TJQ.MyTank -c '{"PlayStyle":"Aggressive"}'
#
# Options:
#   -n N        matches to play (default 3)
#   -a SCRIPT   team A tank script (default Jev.MyTank)
#   -b SCRIPT   team B tank script (default TJQ.MyTank)
#   -m SECONDS  match time override (default: scene setting, 180)
#   -t SCALE    Time.timeScale (default 1; Jev requests are wall-clock, so >1 means fewer decisions per game second)
#   -c JSON     JevConfig overrides for this run, e.g. '{"PlayStyle":"Cautious","DodgeMode":"Reflex"}'
#   -M MODE     editor (default) or player
#               editor: runs a batch-mode Unity editor in play mode; the Unity editor must be CLOSED for this project
#               player: runs a headless player built with Tools > Jev > Build Headless Player
#   -u PATH     Unity editor binary (editor mode); default: the version in ProjectSettings/ProjectVersion.txt under /Applications/Unity/Hub/Editor
#   -p PATH     player app (player mode), default Builds/JevHeadless/TankBattleJev.app
#   -k          do not summarise afterwards
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../../../.." && pwd)"

MATCHES=3
TEAM_A="Jev.MyTank"
TEAM_B="TJQ.MyTank"
MATCH_TIME=""
TIME_SCALE=1
CONFIG_JSON=""
MODE="editor"
UNITY_BIN=""
PLAYER="$PROJECT_ROOT/Builds/JevHeadless/TankBattleJev.app"
SUMMARISE=1

while getopts "n:a:b:m:t:c:M:u:p:k" opt; do
  case "$opt" in
    n) MATCHES="$OPTARG" ;;
    a) TEAM_A="$OPTARG" ;;
    b) TEAM_B="$OPTARG" ;;
    m) MATCH_TIME="$OPTARG" ;;
    t) TIME_SCALE="$OPTARG" ;;
    c) CONFIG_JSON="$OPTARG" ;;
    M) MODE="$OPTARG" ;;
    u) UNITY_BIN="$OPTARG" ;;
    p) PLAYER="$OPTARG" ;;
    k) SUMMARISE=0 ;;
    *) echo "unknown option"; exit 2 ;;
  esac
done

LOG_DIR="$PROJECT_ROOT/Logs/Jev"
mkdir -p "$LOG_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
RUN_LOG="$LOG_DIR/run_$STAMP.log"
RESULTS="$LOG_DIR/matches_$STAMP.jsonl"

JEV_ARGS=(--jev-matches "$MATCHES" --jev-team-a "$TEAM_A" --jev-team-b "$TEAM_B"
          --jev-time-scale "$TIME_SCALE" --jev-results "$RESULTS")
[[ -n "$MATCH_TIME" ]] && JEV_ARGS+=(--jev-match-time "$MATCH_TIME")
[[ -n "$CONFIG_JSON" ]] && JEV_ARGS+=(--jev-config "$CONFIG_JSON")

echo "mode: $MODE   matches: $MATCHES   A=$TEAM_A  vs  B=$TEAM_B   timeScale $TIME_SCALE"
[[ -n "$CONFIG_JSON" ]] && echo "config override: $CONFIG_JSON"
echo "log:     $RUN_LOG"
echo "results: $RESULTS"

if [[ "$MODE" == "editor" ]]; then
  if [[ -z "$UNITY_BIN" ]]; then
    VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
    UNITY_BIN="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
  fi
  if [[ ! -x "$UNITY_BIN" ]]; then
    echo "Unity editor binary not found: $UNITY_BIN (use -u PATH)" >&2
    exit 1
  fi
  if [[ -f "$PROJECT_ROOT/Temp/UnityLockfile" ]]; then
    if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then
      echo "the project is open in another Unity editor; close it first (Temp/UnityLockfile exists)" >&2
      exit 1
    fi
    echo "removing stale Temp/UnityLockfile (no Unity process is running)"
    rm -f "$PROJECT_ROOT/Temp/UnityLockfile"
  fi
  # No -quit: JevMatchRunner calls EditorApplication.Exit when the last match is finished.
  "$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_ROOT" \
      -executeMethod Jev.EditorTools.JevBuild.RunMatches -logFile "$RUN_LOG" "${JEV_ARGS[@]}" || true
elif [[ "$MODE" == "player" ]]; then
  EXE="$PLAYER"
  if [[ -d "$PLAYER" && "$PLAYER" == *.app ]]; then
    EXE="$(find "$PLAYER/Contents/MacOS" -type f -perm -u+x | head -n 1)"
  fi
  if [[ ! -x "$EXE" ]]; then
    echo "player not found: $PLAYER (build it with Tools > Jev > Build Headless Player, or use -M editor)" >&2
    exit 1
  fi
  "$EXE" -batchmode -nographics -logFile "$RUN_LOG" "${JEV_ARGS[@]}" || true
else
  echo "unknown mode $MODE (editor|player)" >&2
  exit 2
fi

if [[ -f "$RESULTS" ]]; then
  echo
  echo "## Match results"
  python3 - "$RESULTS" <<'PY'
import json, sys
wins = {}
for line in open(sys.argv[1], encoding="utf-8"):
    r = json.loads(line)
    teams = "  ".join(f"{t['team']}:{t['script']}={t['score']}" for t in r["teams"])
    print(f"match {r['match_index']}: winner {r['winner']}   {teams}   ({r['wall_seconds']:.0f}s wall)")
    wins[r["winner"]] = wins.get(r["winner"], 0) + 1
print("wins:", ", ".join(f"{k}={v}" for k, v in sorted(wins.items())))
PY
else
  echo
  echo "no results file was written; check $RUN_LOG (grep JevRunner / error CS)" >&2
fi

if [[ "$SUMMARISE" == "1" ]]; then
  echo
  python3 "$SCRIPT_DIR/summarize_jev_logs.py" --since "$STAMP" "$LOG_DIR" || true
fi
