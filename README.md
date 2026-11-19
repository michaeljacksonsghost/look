# look

A quiet, compact Windows browser inspired by the interaction principles of Search Browser for macOS. Independent implementation; no Search branding or icon is reused.

**Status:** early development. The current branch contains a WPF shell, an engine boundary, and a first per-tab WebView2 adapter. It is not yet a finished or release-tested browser.

## Build

Requires Windows 10/11 x64, .NET 10 SDK, and the Microsoft Edge WebView2 Runtime.

```powershell
dotnet restore .\Look.slnx
dotnet build .\Look.slnx -c Release
dotnet run --project .\src\Look.App\Look.App.csproj
```

A Windows GitHub Actions workflow builds the solution on pushes and pull requests.

## Extension compatibility gate

Extension support is a production requirement, not an assumption. WebView2 is only the first engine adapter and is not equivalent to full Chrome extension support. The engine decision remains open until Manifest V3 service workers, content scripts, extension UI, storage, permissions, commands, native messaging, install/update behavior, and representative real extensions have been tested. CEF will be evaluated against the same matrix if WebView2 does not meet the required bar.

Do not advertise Chrome Web Store compatibility until the test matrix passes.

## Product principles

- Local-first; no account, ads, analytics, or telemetry by default.
- Credentials must use Windows Credential Manager / DPAPI, never plaintext.
- No automatic password filling.
- Private browsing uses a disposable isolated profile.
- Both installer and portable distribution are planned, not yet available.

## Project documents

- [Production checklist](docs/PRODUCTION_CHECKLIST.md)
- [Build roadmap](docs/ROADMAP.md)
- [Engine decision record](docs/ENGINE_DECISION.md)

## Provenance

Independent implementation. If code is copied or adapted from the MIT-licensed Search project, preserve its copyright and licence notice and document the provenance. No endorsement is implied.
