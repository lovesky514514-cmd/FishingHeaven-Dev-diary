param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Read-Host "Tuanjie project root path"
}

$ProjectPath = $ProjectPath.Trim('"')

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Write-Host "[ERROR] Project path not found: $ProjectPath" -ForegroundColor Red
    exit 2
}

if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath "Assets"))) {
    Write-Host "[ERROR] The target does not look like a Tuanjie/Unity project (Assets missing)." -ForegroundColor Red
    exit 3
}

Write-Host ""
Write-Host "Installing Fishing Heaven OpenCode Agent into:"
Write-Host $ProjectPath
Write-Host ""

# OpenCode agent and commands
$targetOpenCode = Join-Path $ProjectPath ".opencode"
New-Item -ItemType Directory -Force -Path $targetOpenCode | Out-Null
Copy-Item -LiteralPath (Join-Path $RepoRoot ".opencode\agents") `
          -Destination $targetOpenCode -Recurse -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot ".opencode\commands") `
          -Destination $targetOpenCode -Recurse -Force

# Runtime scripts
$targetAgent = Join-Path $ProjectPath "FH_Agent"
New-Item -ItemType Directory -Force -Path $targetAgent | Out-Null
Copy-Item -LiteralPath (Join-Path $RepoRoot "FH_Agent\scripts") `
          -Destination $targetAgent -Recurse -Force

# C# upload folder.
$targetUpload = Join-Path $ProjectPath "CSharp_Upload"
New-Item -ItemType Directory -Force -Path $targetUpload | Out-Null

$repoSimple = Join-Path $RepoRoot "CSharp_Upload\FH_simple.cs"
$targetSimple = Join-Path $targetUpload "FH_simple.cs"

# Do not silently overwrite an existing project upload file.
if (-not (Test-Path -LiteralPath $targetSimple)) {
    Copy-Item -LiteralPath $repoSimple -Destination $targetSimple -Force
    Write-Host "[OK] Installed initial CSharp_Upload\FH_simple.cs"
}
else {
    Write-Host "[KEEP] Existing CSharp_Upload\FH_simple.cs was not overwritten."
}

Copy-Item -LiteralPath (Join-Path $RepoRoot "CSharp_Upload\README.md") `
          -Destination (Join-Path $targetUpload "README.md") -Force

Write-Host ""
Write-Host "[OK] Agent installed."
Write-Host ""
Write-Host "Next:"
Write-Host "  cd /d `"$ProjectPath`""
Write-Host "  opencode"
Write-Host ""
Write-Host "Then run:"
Write-Host "  /fh-status"
Write-Host "  /fh-apply"
