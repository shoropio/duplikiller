param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root "src\DupliKiller.App"
$out = Join-Path $root "publish\win-x64"

if (-not $Version) {
    $xml = [xml](Get-Content (Join-Path $app "DupliKiller.App.csproj"))
    $Version = $xml.Project.PropertyGroup.Version
}
if (-not $Version) { $Version = "1.0.0" }

Write-Host "== Publicando DupliKiller v$Version (win-x64, self-contained single-file) =="

dotnet publish $app -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out "DupliKiller.App.exe"
if (-not (Test-Path $exe)) { throw "No se encontro $exe" }

$zip = Join-Path $root "publish\DupliKiller-$Version-win-x64.zip"
Compress-Archive -Path $exe -DestinationPath $zip -Force
Write-Host "== ZIP: $zip =="

$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "No se encontro Inno Setup en $iscc" }

& $iscc (Join-Path $root "installer\DupliKiller.iss") "/DMyAppVersion=$Version"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "== Listo. Artifactos en publish\ =="
Get-ChildItem (Join-Path $root "publish") | Select-Object Name, Length, LastWriteTime
