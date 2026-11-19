# look

A quiet, compact Windows browser inspired by the interaction principles of Search Browser for macOS. Independent implementation; no Search branding or icon is reused.

**Status:** early WPF foundation and browser-engine compatibility spike. Not a finished browser; Chrome extension compatibility is not yet established.

## Build
Requires Windows 10/11 x64, .NET 10 SDK and Microsoft Edge WebView2 Runtime.

```powershell
dotnet restore .\Look.slnx
dotnet build .\Look.slnx -c Release
dotnet run --project .\src\Look.App\Look.App.csproj
```

## Extension compatibility gate
WebView2 is a prototype only. It is not equivalent to full Chrome extension support. Validate MV3 service workers, content scripts, extension popups/options, storage, permissions, commands, native messaging and representative extensions (Bitwarden, Dark Reader, uBlock Origin Lite where supported). If the compatibility bar is not met, evaluate CEF before committing to the engine. Keep UI and browser state engine-agnostic.

## Principles
- Local-first; no account, ads, analytics or telemetry by default.
- Credentials must use Windows Credential Manager / DPAPI, never plaintext.
- No automatic password filling.
- Private browsing uses a disposable isolated profile.
- Installer and portable builds are both planned, not yet available.

## Provenance
Independent implementation. If code is copied or adapted from the MIT-licensed Search project, preserve its copyright and licence notice and document the provenance. No endorsement is implied.
