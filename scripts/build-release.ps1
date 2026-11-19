$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$portableZip = Join-Path $artifacts "look-portable-win-x64.zip"
$bootstrapper = Join-Path $artifacts "MicrosoftEdgeWebview2Setup.exe"

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Push-Location $root
try {
    dotnet restore ".\Look.slnx"
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }
    dotnet build ".\Look.slnx" --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }
    dotnet test ".\Look.slnx" --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }
    dotnet publish ".\src\Look.App\Look.App.csproj" --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true --output $publish
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed." }

    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper
    Copy-Item $bootstrapper $publish

    $launcher = @(
        "@echo off",
        "set RUNTIME={F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        'reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\%RUNTIME%" /v pv >nul 2>&1',
        "if not errorlevel 1 goto launch",
        'reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\%RUNTIME%" /v pv >nul 2>&1',
        "if not errorlevel 1 goto launch",
        'reg query "HKCU\Software\Microsoft\EdgeUpdate\Clients\%RUNTIME%" /v pv >nul 2>&1',
        "if not errorlevel 1 goto launch",
        "echo Installing the Microsoft Edge WebView2 Runtime required by look...",
        '"%~dp0MicrosoftEdgeWebview2Setup.exe" /install',
        "if errorlevel 1 (echo Runtime installation failed. & pause & exit /b 1)",
        ":launch",
        'start "" "%~dp0look.exe" --portable'
    ) -join [Environment]::NewLine
    Set-Content -Path (Join-Path $publish "look-portable.cmd") -Value $launcher -Encoding ASCII

    Copy-Item (Join-Path $root "README.md") $publish
    if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
    Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $portableZip
    Remove-Item (Join-Path $publish "look-portable.cmd") -Force
    Remove-Item (Join-Path $publish "MicrosoftEdgeWebview2Setup.exe") -Force

    $isccPath = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
    if (-not $isccPath) {
        $candidate = Join-Path (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6") "ISCC.exe"
        if (Test-Path $candidate) { $isccPath = $candidate }
    }
    if ($isccPath) {
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
