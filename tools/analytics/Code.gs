// Pesky Weapons analytics: the Google Apps Script behind the playtest Sheet (docs/ANALYTICS.md).
//
// Paste this into the Sheet's Extensions > Apps Script editor and deploy it as a web app
// (Execute as: Me, Who has access: Anyone). The game's host POSTs one JSON report per run
// (navigator.sendBeacon, Content-Type text/plain so the browser sends no CORS preflight);
// doPost appends it to three tabs, creating each tab and its header row the first time.
//
// A run is written once: a report whose run_id is already in the runs tab is ignored, so a
// page that sends twice (the end of the run, then the tab closing) adds nothing.

var SCHEMA_VERSION = 1;

var TABS = {
  runs: ['run_id', 'received_at', 'started_at', 'build', 'players_start', 'players_end', 'mages',
         'outcome', 'ended', 'duration_s', 'timer_s', 'rooms_total', 'rooms_reached',
         'votes_called', 'mage_banished'],
  rooms: ['run_id', 'index', 'room_id', 'room', 'entered_s', 'seconds', 'cleared'],
  votes: ['run_id', 'n', 'at_s', 'players', 'banished']
};

function doPost(e) {
  var lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    var report = JSON.parse(e.postData.contents);
    if (!report || report.v !== SCHEMA_VERSION || !report.run || !report.run.run_id) {
      return reply('ignored: not a v' + SCHEMA_VERSION + ' report');
    }
    var book = SpreadsheetApp.getActiveSpreadsheet();
    var runs = tab(book, 'runs');
    if (hasRun(runs, report.run.run_id)) return reply('ignored: run already recorded');

    var received = new Date().toISOString();
    var run = report.run;
    run.received_at = received;
    append(runs, 'runs', [run]);
    append(tab(book, 'rooms'), 'rooms', withRunId(report.rooms, run.run_id));
    append(tab(book, 'votes'), 'votes', withRunId(report.votes, run.run_id));
    return reply('ok');
  } catch (err) {
    return reply('error: ' + err);
  } finally {
    lock.releaseLock();
  }
}

// A browser opening the web app URL sees this: a quick way to check the deployment is live.
function doGet() {
  return reply('Pesky Weapons analytics endpoint, schema v' + SCHEMA_VERSION + '. POST a run report here.');
}

function tab(book, name) {
  var sheet = book.getSheetByName(name);
  if (!sheet) {
    sheet = book.insertSheet(name);
    sheet.appendRow(TABS[name]);
    sheet.setFrozenRows(1);
  }
  return sheet;
}

function hasRun(runs, runId) {
  var last = runs.getLastRow();
  if (last < 2) return false;
  var ids = runs.getRange(2, 1, last - 1, 1).getValues();
  for (var i = 0; i < ids.length; i++) if (ids[i][0] === runId) return true;
  return false;
}

function withRunId(rows, runId) {
  rows = rows || [];
  for (var i = 0; i < rows.length; i++) rows[i].run_id = runId;
  return rows;
}

// One sheet row per object, in the tab's header order; a missing field is an empty cell.
function append(sheet, name, rows) {
  if (!rows || rows.length === 0) return;
  var header = TABS[name];
  var values = rows.map(function (row) {
    return header.map(function (key) {
      var v = row[key];
      return v === undefined || v === null ? '' : v;
    });
  });
  sheet.getRange(sheet.getLastRow() + 1, 1, values.length, header.length).setValues(values);
}

function reply(text) {
  return ContentService.createTextOutput(text).setMimeType(ContentService.MimeType.TEXT);
}
