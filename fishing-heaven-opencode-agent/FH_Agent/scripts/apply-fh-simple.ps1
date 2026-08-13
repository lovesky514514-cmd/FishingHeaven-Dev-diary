$ErrorActionPreference = "Stop"

# This script must live at:
# <ProjectRoot>\FH_Agent\scripts\apply-fh-simple.ps1

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$AgentDir   = Split-Path -Parent $ScriptDir
$Project    = Split-Path -Parent $AgentDir

$Source     = Join-Path $Project "CSharp_Upload\FH_simple.cs"
$DestDir    = Join-Path $Project "Assets\Scripts"
$Dest       = Join-Path $DestDir "FishingHeavenDemo.cs"
$BackupRoot = Join-Path $Project "_FH_BACKUP"

function Fail([string]$Message, [int]$Code) {
    Write-Host "[ERROR] $Message" -ForegroundColor Red
    exit $Code
}

Write-Host "============================================================"
Write-Host " Fishing Heaven - FH_simple Safe Apply"
Write-Host "============================================================"
Write-Host "PROJECT: $Project"
Write-Host "SOURCE : $Source"
Write-Host "DEST   : $Dest"
Write-Host ""

if (-not (Test-Path -LiteralPath $Source)) {
    Fail "CSharp_Upload\FH_simple.cs not found." 2
}

# Read-only structural sanity check.
$sourceText = Get-Content -LiteralPath $Source -Raw

if ($sourceText -notmatch 'class\s+\w+\s*:\s*MonoBehaviour') {
    Fail "FH_simple.cs does not contain a MonoBehaviour class." 3
}

$sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash

New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

$backupPath = "NONE"

if (Test-Path -LiteralPath $Dest) {
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = Join-Path $BackupRoot "before_$stamp"
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

    $backupPath = Join-Path $backupDir "FishingHeavenDemo.cs"
    Copy-Item -LiteralPath $Dest -Destination $backupPath -Force
}

# Copy byte-for-byte. No transformation.
Copy-Item -LiteralPath $Source -Destination $Dest -Force

$destHash = (Get-FileHash -LiteralPath $Dest -Algorithm SHA256).Hash

Write-Host "SOURCE_SHA256: $sourceHash"
Write-Host "DEST_SHA256:   $destHash"
Write-Host "BACKUP:        $backupPath"

if ($sourceHash -ne $destHash) {
    Fail "HASH MISMATCH after copy." 10
}

Write-Host "HASH_MATCH: YES" -ForegroundColor Green
Write-Host "STATUS: APPLIED" -ForegroundColor Green
Write-Host ""
Write-Host "C# content was copied without modification."
exit 0
