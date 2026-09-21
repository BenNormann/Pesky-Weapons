# Publishes the latest WebGL build to the public GitHub Pages repository.
#
#   1. In Unity: Pesky > Build Web            (writes .\Builds\Web)
#   2. In PowerShell at the repo root:  .\publish.ps1
#
# One-time setup: create an EMPTY public repository on GitHub named Pesky-Weapons-Build, then
# after the first publish turn Pages on there: Settings > Pages > Deploy from a branch >
# main > / (root). The game is then served at https://bennormann.github.io/Pesky-Weapons-Build/
#
# Every player must be on the same published build (the join handshake refuses a protocol
# version mismatch), so publish after every change that touches the netcode.

param(
    [string]$BuildDir    = (Join-Path $PSScriptRoot "Builds\Web"),
    [string]$PublishRepo = (Join-Path (Split-Path $PSScriptRoot -Parent) "Pesky-Weapons-Build"),
    [string]$Remote      = "https://BenNormann@github.com/BenNormann/Pesky-Weapons-Build.git",
    [string]$UserName    = "Ben Normann",
    [string]$UserEmail   = "81779011+BenNormann@users.noreply.github.com",
    [string]$Message     = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $BuildDir "index.html"))) {
    throw "No WebGL build found at '$BuildDir' (index.html missing). Run Pesky > Build Web in Unity first."
}

if (-not (Test-Path $PublishRepo)) {
    Write-Host "Cloning $Remote into $PublishRepo ..."
    git clone $Remote $PublishRepo
    if ($LASTEXITCODE -ne 0) { throw "Clone failed. Does the GitHub repository Pesky-Weapons-Build exist?" }
}

# Safety: only ever wipe a folder that really is the publish repository.
if (-not (Test-Path (Join-Path $PublishRepo ".git"))) {
    throw "'$PublishRepo' is not a git repository. Refusing to touch it."
}
$origin = git -C $PublishRepo remote get-url origin
if ($origin -notlike "*Pesky-Weapons-Build*") {
    throw "'$PublishRepo' points at '$origin', not at Pesky-Weapons-Build. Refusing to touch it."
}

# The identity is set inside the repository before anything is committed.
git -C $PublishRepo config user.name  $UserName
git -C $PublishRepo config user.email $UserEmail
git -C $PublishRepo checkout -B main | Out-Null

# Replace the site's contents with the new build (everything except .git).
Get-ChildItem -LiteralPath $PublishRepo -Force |
    Where-Object { $_.Name -ne ".git" } |
    Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $BuildDir "*") -Destination $PublishRepo -Recurse -Force

# Without this file GitHub Pages runs Jekyll and drops files from Build/.
New-Item -ItemType File -Path (Join-Path $PublishRepo ".nojekyll") -Force | Out-Null

if (-not $Message) {
    $sourceCommit = git -C $PSScriptRoot rev-parse --short HEAD
    $Message = "Publish build from $sourceCommit"
}

git -C $PublishRepo add -A
git -C $PublishRepo diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "Nothing changed since the last publish."
    exit 0
}
git -C $PublishRepo commit -m $Message
git -C $PublishRepo log -1 --format="Committed %h as %an <%ae>"
git -C $PublishRepo push -u origin main
Write-Host "Published. Pages takes about a minute; hard-refresh the browser (Ctrl+F5)."
