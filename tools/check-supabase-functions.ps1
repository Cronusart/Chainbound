$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$functionsRoot = Join-Path $repoRoot "Supabase\functions"
$denoConfig = Join-Path $functionsRoot "deno.json"

$denoCommand = Get-Command deno -ErrorAction SilentlyContinue
if (-not $denoCommand) {
    $defaultWindowsDeno = Join-Path $env:USERPROFILE ".deno\bin\deno.exe"
    if (Test-Path $defaultWindowsDeno) {
        $denoCommand = $defaultWindowsDeno
    }
}

if (-not $denoCommand) {
    throw "Deno is not installed or is not on PATH. Install Deno, restart the terminal, then rerun this script."
}

if (-not (Test-Path $denoConfig)) {
    throw "Missing Deno config: $denoConfig"
}

$files = Get-ChildItem -Path $functionsRoot -Recurse -Filter "*.ts" -File |
    Where-Object { $_.FullName -notmatch "\\node_modules\\" } |
    Sort-Object FullName

foreach ($file in $files) {
    $relative = Resolve-Path -Relative $file.FullName
    Write-Host "deno check $relative"
    & $denoCommand check --config $denoConfig $file.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "deno lint Supabase/functions"
& $denoCommand lint --config $denoConfig $functionsRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "deno fmt --check Supabase/functions"
& $denoCommand fmt --config $denoConfig --check $functionsRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
