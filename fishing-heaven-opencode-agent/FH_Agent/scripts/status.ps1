$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AgentDir  = Split-Path -Parent $ScriptDir
$Project   = Split-Path -Parent $AgentDir

$Source = Join-Path $Project "CSharp_Upload\FH_simple.cs"
$Dest   = Join-Path $Project "Assets\Scripts\FishingHeavenDemo.cs"
$Scene  = Join-Path $Project "Assets\Scenes\Main.unity"

Write-Host "PROJECT=$Project"
Write-Host "FH_SIMPLE_EXISTS=$([bool](Test-Path -LiteralPath $Source))"
Write-Host "DEPLOYED_SCRIPT_EXISTS=$([bool](Test-Path -LiteralPath $Dest))"
Write-Host "MAIN_SCENE_EXISTS=$([bool](Test-Path -LiteralPath $Scene))"

if (Test-Path -LiteralPath $Source) {
    Write-Host "SOURCE_SHA256=$((Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash)"
}

if (Test-Path -LiteralPath $Dest) {
    Write-Host "DEST_SHA256=$((Get-FileHash -LiteralPath $Dest -Algorithm SHA256).Hash)"
}
