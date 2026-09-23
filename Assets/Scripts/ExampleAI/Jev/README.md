# Jev tank (TypeSafe System One)

A tank whose decisions are made by [TypeSafe's Jev model](https://docs.typesafe.ai/introduction).
Jev is not a text-generating LLM: it evaluates typed questions (Choice / Score / Noul) against a
JSON state and returns a choice, a probability distribution and a confidence, in roughly 150 ms.

Tank script name for the Match inspector: `Jev.MyTank`

## Setup

1. Get an API key from https://console.typesafe.ai/keys
2. Provide it in one of two ways (never commit it):
   - environment variable `TYPESAFE_API_KEY`, or
   - a file `~/.typesafe/api_key` containing only the key (more reliable when Unity is launched from the Dock)
3. Optional overrides in `~/.typesafe/tankbattle_jev.json`, e.g.

```json
{ "DecisionInterval": 0.5, "MoveSwitchConfidence": 0.4, "LogState": false }
```

Any public field of `JevConfig` can be overridden; keys you omit keep their defaults.

Without a key the tank runs on its built-in rule-based fallback (nearest star, home when low HP,
shoot the nearest visible enemy), so the scene still works.

## How it works

Each decision tick (default every 0.35 s) `JevWorldDescriber` turns the battlefield into a semantic,
English-only state (Jev is weak at arithmetic, counting and comparing numbers, and less accurate on
CJK text), for example `"hp": "low, dies in two hits"`, `"distance": "near"`,
`"competition": "an enemy is clearly closer to it than me"`.

One request carries two independent Choice questions, one per execution channel:

| Question | Options | Executes as |
|----------|---------|-------------|
| `move_to` | `star_<id>`, `enemy_N`, `teammate_N`, `away_from_enemies`, `home`, `hold`, `roam`, `sidestep` (Jev dodge mode only, while a missile threatens me) | NavMesh path to the target |
| `aim_at` | `enemy_N`, `none` | turret tracking; fires when aligned and line of sight is clear |

## Play style (steering by natural language)

`PlayStyle` picks a preset (`Balanced` (default), `Aggressive`, `Cautious`, `Greedy`) or `Custom`, which
uses the free text in `PlayStyleText`. The resolved sentence goes into the state as `self.personality`
and both questions ask Jev to decide "the way someone with my personality would". The presets describe
character only ("a hot-headed, fearless brawler", "a careful, patient survivor", "a greedy treasure
hunter") and deliberately contain no tactical rules: the experiment is whether Jev derives different
behaviour from personality alone. With `ShowPlayStyleInName` the tank is called `Jev-Aggressive` etc.,
so two personalities can fight each other and their logs stay apart. Compare them by the `move.answer` /
`aim.answer` distributions and the HP breakdown, not just by score.

Example `~/.typesafe/tankbattle_jev.json`:

```json
{ "PlayStyle": "Custom", "PlayStyleText": "I am a cowardly homebody who panics at the first sign of danger." }
```

## Distances

With `UsePathDistances` (default on) every distance shown to Jev (stars, enemies, teammates, home) and
the fallback's nearest-star choice use the NavMesh path length from the tank, not the straight line, so a
star behind a wall reads as `far`. Star `competition` compares my path length with the closest enemy's
path length to the same star. Unreachable targets count as twice the straight-line distance. Turret and
missile geometry (aiming, threats) stay straight-line, because missiles fly straight.

## Shooting (code only)

Jev decides *whom* to shoot; hitting is pure kinematics and stays in code (the mechanics follow the
2026 winner WBQ, minus its direct turret-transform write, which bypasses the turret speed limit).
With `EnableLeadAiming` (default on) the turret aims at the closed-form intercept point: the earliest
positive root of `|delta + v t| = s t`, capped by `MaxLeadSeconds`. The fire gate: inside 15 units any
ready shot goes; beyond that the allowed aiming error shrinks from 8° to 3.5° at 45 units, and a
sphere cast along the muzzle direction must not end in a wall or a teammate before the target. The red
gizmo sphere marks the lead point; a faint line joins it to the enemy's current position.

## Dodging

`DodgeMode` selects one of three behaviours (`"Off"`, `"Jev"`, `"Reflex"` (default)), so the two
approaches can be compared on equal footing. Every frame the describer projects each enemy missile's
straight path; missiles passing within `ThreatNearMissRadius` in the next `ThreatEtaSeconds` are
pre-selected, then a sphere cast along the missile's real path decides what it hits first: a wall or
another tank means no threat, my own collider means `WillHit`. Threats are always described to Jev in
the state, whatever the mode.

**Reflex (code only, after WBQ's EvadeMissile).** While a `WillHit` missile is between
`ReflexDodgeMinDistance` (closer is too late, turning only exposes the flank) and
`ReflexDodgeMaxDistance`, the executor overrides the destination with a `SidestepDistance` sidestep
perpendicular to the missile path, away from the side I am already on. With `ReflexDodgeSkipForKill`
the dodge is skipped when the enemy dies to my next shot and I can still take two hits. Jev's `move_to`
choice is never touched and Jev is not asked anything extra. The label shows `+REFLEX DODGE` while
active and the summary reports `reflex_dodges`.

**Jev.** Dodging is left to Jev. The threats appear in the state as, e.g.
`{"course":"on a direct course to hit me","arrival":"hits in about a second","coming_from":"from my left side"}`.
While a threat exists the `move_to` question gains a `sidestep` option. Two things shorten the reaction
chain: the first frame a missile is on a collision course triggers an immediate request instead of
waiting for the tick (`urgent: true` in the log), and switching into `sidestep` uses the lower
`SidestepSwitchConfidence`. The sidestep target is `SidestepDistance` units perpendicular to the missile
path, on the side I am already offset to. When the missile has passed the option expires and the tank
resumes its previous destination while asking Jev again. The summary reports `urgent_requests` and
`sidesteps`, and magenta gizmo lines show the projected paths of threatening missiles.

Confidence gating adds hysteresis: switching destination needs `MoveSwitchConfidence`, re-aiming only
`AimSwitchConfidence`. Targets that disappear (star taken, enemy died) are replaced immediately and a
new decision is requested. Requests are polled from `OnUpdate` rather than run in coroutines so a
dead (inactive) tank does not lose an in-flight request; its stale answer is discarded on respawn.

Failures: 429/529 back off exponentially; after `FallbackAfterFailures` consecutive failures the tank
switches to the fallback and probes Jev again every `FallbackRetryInterval` seconds.

## Statistics

`JevStats` counts requests (total, urgent, failed), latency (average, last, max), token usage (input,
output, average input per request), an estimated cost (`InputTokenPriceUsdPerMillion`, default $0.042;
output tokens are free on TypeSafe's price list) and dodges. With `ShowStatsInGizmo` (default on) the
counters are appended to the tank's yellow gizmo label in the Scene view; nothing is drawn on the game
screen. At match end the same numbers go to the Unity console as one line and to the jsonl log as a
`summary` record with a `stats` object.

**Decision distribution.** Every Jev answer is also counted by category: `move_to` as star / enemy /
teammate / home / away / hold / roam / sidestep, `aim_at` as enemy / none, plus how each answer was
applied (switched, same, kept_low_confidence, invalid) and the average confidence. The move split is
additionally broken down by my HP level (full / high / medium / low / critical), which is the quickest
way to see whether a play style changed anything: a cautious tank should show `home` appearing already
at `medium`, an aggressive one should show `enemy` dominating at `high`. The gizmo label shows the
overall percentages live; the console summary adds the HP breakdown; the jsonl `summary.decisions`
object has the raw counts.

## Unattended matches and log analysis

1. Close the Unity editor for this project (a batch editor cannot open a project that is already open).
2. Run matches:

```bash
Assets/Scripts/ExampleAI/Jev/Tools/run_jev_matches.sh -n 5 -a Jev.MyTank -b TJQ.MyTank -c '{"PlayStyle":"Aggressive"}'
```

   Default mode `-M editor` starts a batch-mode Unity editor (`-batchmode -nographics`) that opens
   `BattleField.unity` and enters play mode via `Jev.EditorTools.JevBuild.RunMatches`. `JevMatchRunner`
   (activated only by `--jev-matches`) sets the team scripts before each match, plays them back to back by
   reloading the scene, appends one line per match to `Logs/Jev/matches_<stamp>.jsonl` and exits the
   process. `-c` passes JevConfig overrides for that run only, so two styles can be compared with two
   invocations. Keep `-t 1`: Jev requests take wall-clock time, so a faster time scale means fewer
   decisions per game second. Expect about a minute of editor start-up per invocation.

   `-M player` runs a headless player built with **Tools > Jev > Build Headless Player** instead. Note that
   a player build of this project currently fails because a student script (`Class2025/LYF`) references
   `UnityEditor.Handles` without an editor guard; the editor mode sidesteps that.
3. Summarise any set of logs (works for matches stopped early too, they are flagged incomplete):

```bash
python3 Assets/Scripts/ExampleAI/Jev/Tools/summarize_jev_logs.py Logs/Jev
```

   Prints a per-match table (decisions, latency avg/p95/max, tokens, cost, score, move/aim split) and a
   per-style section with the move distribution broken down by HP level. `--json out.json` dumps the data.

## Logs

`Logs/Jev/Jev_<team>_<timestamp>.jsonl` (git-ignored) contains one JSON line per decision with
latency, token usage, both answers with probabilities and confidence, what was applied, and the full
state that was sent (disable with `"LogState": false`). A `summary` line is written at match end and
the same summary goes to the Unity console.

## Files

- `MyTank.cs` – decision loop, executor, fallback, gizmos
- `JevWorldDescriber.cs` – state description, dynamic option lists, option id resolution
- `TypeSafeClient.cs` – minimal `POST /v1/systemone` client with coroutine-free polling
- `JevConfig.cs` – tunables, config file override, API key loading
- `JevLogger.cs` – jsonl logging
