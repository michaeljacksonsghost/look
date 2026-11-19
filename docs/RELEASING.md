# Windows release workflow

## Requirements

- Windows 10/11 x64.
- .NET 10 SDK.
- Microsoft Edge WebView2 Evergreen Runtime on the target machine.
- Inno Setup 6 to compile the installer.

The installer bundles Microsoft's Evergreen WebView2 bootstrapper and installs the runtime silently when it is missing. The portable ZIP includes the .NET runtime but still requires the WebView2 Evergreen Runtime.

Microsoft recommends the Evergreen runtime for most WebView2 apps because it receives automatic security and compatibility updates. The fixed runtime is much larger and requires the app publisher to service it.

Reference: [Microsoft's WebView2 distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

## Build and test

From the repository root in PowerShell:

```powershell
dotnet restore .\Look.slnx
dotnet test .\Look.slnx --configuration Release
dotnet build .\Look.slnx --configuration Release
```

## Build release artifacts

Install Inno Setup 6, then run:

```powershell
.\scripts\build-release.ps1
```

Outputs are written to `artifacts/`:
- `look-portable-win-x64.zip`
- `look-setup-0.1.0-win-x64.exe`

The portable package includes `look-portable.cmd`. Launching through this file passes `--portable`, which stores the browser profile and app data in a `Data` folder beside the executable. The regular installer uses `%LOCALAPPDATA%\look`.

## CI

The GitHub Actions Windows workflow runs restore, tests, publish, installer compilation, and uploads the generated packages as a workflow artifact.

## Release safeguards

- Never publish a package if tests or installer compilation fail.
- Test both packages on a clean Windows 10 and Windows 11 x64 machine.
- Verify WebView2 Runtime detection and installation.
- Verify regular and portable data paths are separate.
- Verify install, upgrade, uninstall, and data-retention behavior.
- Publish checksums and release notes with every public release.
- Do not publish as 1.0 until the production checklist and extension matrix have passed.
