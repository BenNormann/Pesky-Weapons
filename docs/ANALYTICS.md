# ANALYTICS

Playtest analytics for the GDD (alpha onward): what is recorded, where it goes, and how to set it up.

## 1. What and when

The **host** records each run and sends one report to a Google Sheet when it ends. Only the host does it:
it is the referee, it sees every outcome once, and a five-player run is one report, not five.

A run is recorded only when **3 or more players were present when its timer started** (the game is
for 3-8; one- and two-player runs are almost always us testing). The count is taken once, at the start:
a run that drops to two players mid-way is still sent (`players_end` says so), and a two-player run that
someone joins later is not.

Nothing is sent from the Editor or from a page opened with `?debug=1`, so our own tests stay out of the
data. `?analytics=1` overrides that for testing the pipeline itself.

No names or callsigns are sent: a random run id, slot counts and timings only.

## 2. The Sheet

Three tabs, created with their header rows by the first report. Times are seconds of GAME time since the
run's timer started (a vote's pause is not counted).

**runs** - one row per run.

| Column | Meaning |
|---|---|
| `run_id` | Random id of the run |
| `received_at` | When the Sheet got it (UTC) |
| `started_at` | When the timer started, by the host's clock (UTC) |
| `build` | The game's version string |
| `players_start`, `players_end` | Players present when the timer started / when the report was made |
| `mages` | How many Mages the run had (0 when the run was abandoned before the result named them) |
| `outcome` | `Escaped`, `Resurrected`, `TimedOut`, `WeaponsGone`, or `None` for an abandoned run |
| `ended` | `result` (the round ended normally) or `abandoned` (the host left or closed the tab mid-run) |
| `duration_s` | Timer start to the end |
| `timer_s` | The run's timer length |
| `rooms_total` | Rooms in the run |
| `rooms_reached` | The furthest room anybody entered (1-based; `rooms_total + 1` is the Exit room) |
| `votes_called` | Meetings held |
| `mage_banished` | TRUE when a vote banished a Mage |

**rooms** - one row per room of the run that anybody entered.

| Column | Meaning |
|---|---|
| `index` | Position in the run, 1-based |
| `room_id`, `room` | The room's id and its label |
| `entered_s` | When the first player entered it |
| `seconds` | From then until the first player entered the next room (or the Exit room); to the end of the run when nobody did |
| `cleared` | TRUE when somebody went on to the next room |

**votes** - one row per meeting.

| Column | Meaning |
|---|---|
| `n` | 1 for the first meeting of the run, 2 for the second... |
| `at_s` | When it ended |
| `players` | Players present then |
| `banished` | `mage`, `weapon` or `nobody` |

## 3. Setting it up (once, by whoever owns the Sheet)

1. Create a Google Sheet (any name). **Extensions > Apps Script**.
2. Replace the editor's contents with `tools/analytics/Code.gs` from this repository. Save.
3. **Deploy > New deployment**, type **Web app**. Execute as: **Me**. Who has access: **Anyone**.
   Deploy, authorize, and copy the **Web app URL** (`https://script.google.com/macros/s/.../exec`).
4. Open that URL in a browser: it should answer `Pesky Weapons analytics endpoint, schema v1`.
5. Put the URL in `Pesky Weapons Unity/Assets/WebGLTemplates/Pesky/index.html`:
   `window.PESKY_ANALYTICS_URL = '<that URL>'`, then build and publish as usual (`docs/WEB-BUILD.md`).

Changing `Code.gs` later: **Deploy > Manage deployments > edit > Version: New version** keeps the same URL.
A **new deployment** makes a new URL, which then has to go into `index.html` again.
