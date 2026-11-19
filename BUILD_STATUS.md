# Build status

## Current milestone

The initial WPF shell has been extended with:
- A browser-engine interface and a WebView2 adapter.
- One WebView2 control per tab, with independent page/navigation state in the current implementation.
- Lazy creation of each tab's browser engine.
- Tab title/address/loading event updates and Ctrl+Tab switching.
- A production checklist and a staged roadmap.
- A Windows GitHub Actions workflow for restore, Release build, and unit tests.
- An xUnit project covering address/search parsing.

## Verification

- [ ] Windows CI build completed successfully.
- [ ] Unit tests passed in CI.
- [ ] Manual smoke test on Windows 10/11.
- [ ] Multi-tab navigation verified with separate pages and back/forward history.

This environment does not have the .NET SDK installed, so no local compile result is claimed. The first Windows Actions run must be checked before treating this milestone as build-verified.

## Known limitations

- WebView2 remains a prototype engine. Chrome extension compatibility is not established.
- Tabs currently share one persistent WebView2 profile; private profiles and cookie isolation are not implemented.
- History, bookmarks, downloads, session restore, split view, and settings are not implemented yet.
- Installer and portable packages do not exist yet.
