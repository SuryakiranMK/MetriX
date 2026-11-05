param(
    [string]$Version = "v1.0.1"
)

$stagingDir = "d:\Other\metriX\dist"
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Copy-Item "d:\Other\metriX\bin\Release\metriX.exe" (Join-Path $stagingDir "metriX.exe")
Copy-Item "d:\Other\metriX\app.ico" (Join-Path $stagingDir "app.ico")
Copy-Item "d:\Other\metriX\README.md" (Join-Path $stagingDir "README.md")
Copy-Item "d:\Other\metriX\LICENSE" (Join-Path $stagingDir "LICENSE")

$releasesDir = "d:\Other\metriX\releases"
if (-not (Test-Path $releasesDir)) { New-Item -ItemType Directory -Path $releasesDir | Out-Null }

$zipName = "metriX-$Version-windows-x64.zip"
$zipPath = Join-Path $releasesDir $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath

$exeHash = (Get-FileHash -Path "$stagingDir\metriX.exe" -Algorithm SHA256).Hash.ToLower()
$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLower()

$checksumContent = @"
$exeHash  metriX.exe
$zipHash  $zipName
"@

Set-Content -Path "d:\Other\metriX\SHA256SUMS.txt" -Value $checksumContent
Get-Item $zipPath, "d:\Other\metriX\SHA256SUMS.txt"
