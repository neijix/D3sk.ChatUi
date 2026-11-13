# The gate before every commit: both front-ends' tests, the same ones CI runs (twin of check.sh).
#   C#  : restore, build (warnings are errors), every xUnit test, conformance included.
#   web : npm ci when node_modules is missing, then node --test (conformance, DOM, corpora).
# -Full adds `npm pack --dry-run` (what npm would publish). Everything else is already fast.
param([switch]$Full)
$ErrorActionPreference = 'Stop'

function Invoke-Step([string]$Name, [scriptblock]$Block) {
    Write-Host "== $Name"
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "check: '$Name' failed (exit $LASTEXITCODE)" }
}

Push-Location $PSScriptRoot
try {
    Invoke-Step 'dotnet test' { dotnet test D3sk.ChatUi.slnx -c Release }
    Set-Location packages/chat-ui
    if (-not (Test-Path node_modules)) {
        Invoke-Step 'npm ci' { npm ci --no-audit --no-fund }
    }
    Invoke-Step 'npm test' { npm test }
    if ($Full) {
        Invoke-Step 'npm pack --dry-run' { npm pack --dry-run }
    }
    Write-Host 'check: OK'
}
finally {
    Pop-Location
}
