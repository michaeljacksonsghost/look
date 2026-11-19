$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$portableZip = Join-Path $artifacts "look-portable-win-x64.zip"

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Push-Location $root
try {
    dotnet restore ".\Look.slnx"
    dotnet test ".\Look.slnx" --configuration Release
    dotnet publish ".\src\Look.App\Look.App.csproj" --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true --output $publish

    $launcher = "@echo off" + [Environment]::NewLine + 'start "" "%~dp0look.exe" --portable'
    Set-Content -Path (Join-Path $publish "look-portable.cmd") -Value $launcher -Encoding ASCII

    Copy-Item (Join-Path $root "README.md") $publish
    if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
    Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $portableZip
    Remove-Item (Join-Path $publish "look-portable.cmd") -Force

    $isccPath = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
    if (-not $isccPath) {
        $candidate = Join-Path (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6") "ISCC.exe"
        if (Test-Path $candidate) { $isccPath = $candidate }
    }
    if ($isccPath) {
        $bootstrapper = Join-Path $artifacts "MicrosoftEdgeWebview2Setup.exe"
        Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper
        & $isccPath "/DAppVersion=0.1.0" (Join-Path $root "packaging\look.iss")
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }
    }
    else {
        Write-Warning "Inno Setup was not found. Portable ZIP was created; installer skipped. Install Inno Setup 6 and rerun to build the installer."
    }

    Write-Host "Release artifacts are in $artifacts"
}
finally {
    Pop-Location
}
