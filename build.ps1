param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building metriX - Standalone Executable  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Error "Could not find C# compiler at $cscPath"
    exit 1
}

$outputDir = Join-Path $PSScriptRoot "bin\$Configuration"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$outputExe = Join-Path $outputDir "metriX.exe"
$manifestPath = Join-Path $PSScriptRoot "app.manifest"
$iconPath = Join-Path $PSScriptRoot "app.ico"

$sources = Get-ChildItem -Path (Join-Path $PSScriptRoot "src") -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

$references = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.IO.Compression.dll",
    "System.IO.Compression.FileSystem.dll"
)

$refArgs = $references | ForEach-Object { "/r:$_" }

$compilerArgs = @(
    "/target:winexe",
    "/platform:$Platform",
    "/out:$outputExe",
    "/win32manifest:$manifestPath",
    "/win32icon:$iconPath",
    "/optimize+"
) + $refArgs + $sources

Write-Host "Compiling $($sources.Count) source files..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $cscPath -ArgumentList $compilerArgs -NoNewWindow -Wait -PassThru

if ($proc.ExitCode -eq 0) {
    $fileInfo = Get-Item $outputExe
    $sizeKb = [Math]::Round($fileInfo.Length / 1024, 1)
    Write-Host "`n Build successful!" -ForegroundColor Green
    Write-Host " Output: $outputExe ($sizeKb KB)" -ForegroundColor Green
} else {
    Write-Error "`n Build failed with exit code $($proc.ExitCode)"
    exit $proc.ExitCode
}
