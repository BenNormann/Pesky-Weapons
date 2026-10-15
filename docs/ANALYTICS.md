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

## 4. How it is built

| Piece | Where | What it does |
|---|---|---|
| `RunAnalyticsWatcher` | `Scripts/Session/Analytics/` | Host only. Observes the sim once a frame: the run timer starting (the 3+ check), the first time a living player enters each room (ghosts pass doors freely, so they do not count), each vote's result, ROUND_RESULT. Builds the report; sends nothing. |
| `RunReport` | `Scripts/Session/Analytics/` | The three tabs' rows; JsonUtility, so its field names are the Sheet's columns. Tested in `Tests/EditMode/RunReportTests.cs`. |
| `RunDirector` | `Scripts/Game/` | Owns the watcher on the host (it knows which room of the run a point is in); clients never create one. Leaving the run scene mid-run sends the run as abandoned. |
| `AnalyticsSender` + `PeskyAnalytics.jslib` | `Scripts/Game/`, `Plugins/WebGL/` | `navigator.sendBeacon` to `window.PESKY_ANALYTICS_URL`, as `text/plain` (no CORS preflight). The run so far is held by the page and sent on `pagehide` if the tab closes mid-run. |
| `Code.gs` | `tools/analytics/` | The Apps Script: appends the rows, ignores a run id it already has. |

Changing what is recorded: add the field to the row class in `RunReport`, its column to `TABS` in `Code.gs`
(at the END of the list, so old rows still line up), and bump both schema versions together.

## 5. Testing it

- **Editor, any player count:** the report is never sent; with 3+ players the finished run's JSON is printed
  to the console (`[pesky] analytics: not sent ...`). With fewer: `analytics: run not recorded, 2 player(s)`.
- **The Sheet, end to end:** open the published build in three browser windows with
  `?debug=1&analytics=1&relay=force` (debug for the console lines, analytics to send anyway, the relay so
  three windows on one network stay connected). Host, join twice, play into the first room, finish or let a
  short timer run out. A `runs` row, its `rooms` rows and any `votes` rows appear in the Sheet within seconds.
  Then close the host's tab mid-run in a second run: a row with `ended` = `abandoned` appears.
- **Two players:** the same with two windows sends nothing.

After testing, delete the test rows from the Sheet (or filter on `build`) before the playtest.

## 6. Charts (in the Sheet)

Each chart is a pivot table and a chart on it: **Insert > Pivot table** on the tab, then **Insert > Chart**
with the pivot table selected. They update as runs arrive.

| GDD metric | Tab | Pivot table | Chart |
|---|---|---|---|
| **Where runs stall** | `rooms` | Rows: `index` (or `room`). Values: `seconds` AVERAGE, `cleared` COUNTA and a filter on `cleared` = FALSE for "runs that ended here" | Column chart, one bar per room |
| **How runs end** | `runs` | Rows: `outcome`. Columns: `players_start`. Values: `run_id` COUNTA. Filter `ended` = `result` | Stacked column, or a pie for all player counts together |
| **Vote accuracy** | `votes` | Rows: `banished`. Values: `n` COUNTA (and `at_s` AVERAGE for when votes happen) | Pie; a histogram of `at_s` for timing |

## 7. For the GDD

**Methodology Used**

1. Data Collection:
   - The host's game records each run with 3 or more players (Unity C#, `RunAnalyticsWatcher`) and sends one
     report when the run ends, through a Google Apps Script web app into a Google Sheet. Runs abandoned
     mid-way are sent too and marked as such.
   - Link to data collection: *(the Sheet's link)*
2. Data Visualization:
   - Google Sheets pivot tables and charts on the `runs`, `rooms` and `votes` tabs, updating as runs arrive.
