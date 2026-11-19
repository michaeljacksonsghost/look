# look

A quiet, compact Windows browser inspired by the interaction principles of Search Browser for macOS. Independent implementation; no Search branding or icon is reused.

**Status:** early development. Core browsing features and release scripts are being implemented, but Windows build/runtime verification and extension compatibility testing are still open. Do not treat this as a production-ready browser.

## Build from source

Requires Windows 10/11 x64, .NET 10 SDK, and the Microsoft Edge WebView2 Evergreen Runtime.

```powershell
dotnet restore .\Look.slnx
dotnet build .\Look.slnx -c Release
dotnet run --project .\src\Look.App\Look.App.csproj
```

## Current features in source

- Separate WebView2 control per tab.
- Navigation, back/forward, reload, stop, and address/search.
- Tab close, duplicate, pin, mute, drag reorder, and keyboard switching.
- Named Spaces for organizing tabs and moving tabs between workspaces.
- Find-in-page.
- Local session restore, recently closed tabs, bookmarks, history, and downloads.
- Searchable Library window with individual item deletion.
- Basic unpacked-extension install/list/enable/disable/remove.
- User-managed domain blocking and allowlist exceptions.
- Per-request site permission prompts and executable-download warnings.
- Background tab suspend/resume and crash-stale private-profile cleanup.
- Separate temporary browser profile for private windows.
- Portable-mode data folder and Windows installer/portable packaging scripts.

These are implementation claims, not a statement that every feature has passed Windows runtime verification. See [BUILD_STATUS.md](BUILD_STATUS.md).

## Extensions

WebView2 exposes APIs for managing unpacked extensions, which look currently uses. It does not provide the standard browser toolbar/popup entry points for extensions, so the extension experience is incomplete. Full Manifest V3 behavior, real extensions, native messaging, and Chrome Web Store/CRX installation have not been verified. Do not advertise Chrome Web Store compatibility.

See [engine decision record](docs/ENGINE_DECISION.md).

## Build release packages

On Windows, install Inno Setup 6 and run:

```powershell
.\scripts\build-release.ps1
```

The script runs tests, publishes a self-contained x64 app, creates a portable ZIP, and compiles the installer when Inno Setup is available. The portable launcher uses a `Data` directory beside the executable. Both distributions require the Microsoft Edge WebView2 Evergreen Runtime; the installer includes Microsoft's bootstrapper and installs it if missing.

See [release instructions](docs/RELEASING.md).

## Product principles

- Local-first; no account, ads, analytics, or telemetry by default.
- Credentials must use Windows Credential Manager / DPAPI, never plaintext.
- No automatic password filling.
- Private windows use a separate temporary profile, remove their data folder on normal close, and clean abandoned folders on the next startup; forensic privacy review is still required.
- Installer and portable builds are planned through the release script but have not yet been built and verified.

## Project documents

- [Production checklist](docs/PRODUCTION_CHECKLIST.md)
- [Build roadmap](docs/ROADMAP.md)
- [Build status](BUILD_STATUS.md)
- [Engine decision record](docs/ENGINE_DECISION.md)
- [Release workflow](docs/RELEASING.md)
- [Changelog](CHANGELOG.md)

## Provenance

Independent implementation. If code is copied or adapted from the MIT-licensed Search project, preserve its copyright and licence notice and document the provenance. No endorsement is implied.
