# Build status

## Implemented in source

The current development branch contains:
- WPF browser shell with address/search navigation.
- Browser-engine interface and WebView2 adapter.
- Separate WebView2 control and navigation state per tab.
- Shared environment manager with reference counting for regular and isolated profiles.
- Navigation, back/forward/reload/stop, and find-in-page.
- Tab close, duplicate, pin, mute, drag reorder, keyboard shortcuts, and background tab suspend/resume.
- Persistent Spaces: create named workspaces, filter the tab strip by active space, and move tabs between spaces.
- Named persistent browser profiles, each with its own WebView2 data folder, cookies/storage, bookmarks, history, settings, and extensions.
- Resizable two-pane split view, with pane swapping by selecting the secondary tab.
- Text-first reader mode with original URL restoration.
- Per-domain custom CSS editor and click-to-hide element picker; CSS is injected at document creation and applied to matching hostnames.
- Local JSON persistence for session tabs (autosaved on navigation/tab changes), recently closed tabs, bookmarks, history, and downloads.
- Searchable Library window for bookmarks, full browsing history, and downloads, with individual deletion and open actions.
- Save-as download flow, executable-file warning, completion/interruption tracking, and recent downloads list.
- Per-request site permission prompts that default to deny and are not persisted.
- Basic unpacked-extension install/list/enable/disable/remove using the current WebView2 profile.
- User-configurable per-domain request blocking with an allowlist and per-profile settings.
- Private windows with a separate temporary profile/data directory; the directory is deleted on normal close where Windows releases the files.
- Explicit portable mode that stores profile and browser data beside the executable in a Data directory.
- Portable launcher checks for WebView2 Runtime and runs the Microsoft bootstrapper if it is missing; installer includes the same bootstrapper.
- Version metadata, changelog, release instructions, PowerShell packaging script, and Inno Setup installer definition.
- Windows GitHub Actions workflow intended to build, test, package, and upload both x64 distributions.
- Development privacy notice and pre-release threat model documenting local data locations, trust boundaries, and remaining security work.
- xUnit tests for address parsing, browser-data persistence, library deletion, custom CSS settings, spaces, and named profiles.

## Verification status

- [ ] Windows CI build completed successfully.
- [ ] Unit tests passed in CI.
- [ ] Installer compiled successfully in CI.
- [ ] Portable ZIP generated and launched successfully.
- [ ] Manual smoke test on Windows 10/11.
- [ ] Multi-tab navigation and back/forward history verified.
- [ ] Session restore, bookmark/history, download, and private-window cleanup verified.
- [ ] Unpacked extension install/disable/remove verified with real extensions.
- [ ] Clean install, upgrade, uninstall, and portable data isolation verified.

This environment cannot run the Windows WPF application or .NET Windows runtime. No successful compile, package build, or manual runtime result is claimed. The Windows GitHub Actions run must be inspected before treating this milestone as build-verified.

## Known limitations

- WebView2 extension support is partial: unpacked local folders only, with no browser toolbar action/popup host. Full Manifest V3 behavior, native messaging, and Chrome Web Store/CRX installation remain unverified or unsupported.
- Private-window data is stored in a temporary directory, removed on normal close, and stale private folders are cleaned on next startup when no active window holds the exclusive lock; forensic privacy review remains outstanding.
- Profile management supports create/open/rename/delete; profile import/export and cross-process single-instance protection remain outstanding.
- Tab suspension/resume is implemented in source but not yet runtime-verified; renderer crash recovery is not implemented.
- Library search and individual bookmark/history/download-record deletion are implemented in source; runtime verification is still pending.
- Reader mode currently extracts readable text from article/main/body into a clean typography view; images and rich article structure are not preserved, and the original page is reloaded when exiting.
- 3–4 pane layouts and a dedicated tab switcher are not implemented.
- Domain blocking is a simple user-managed host block/allow list, not a maintained ad/tracker filter list; service-worker/shared-worker requests are outside the current document-source filter.
- Credential vault and persistent permission review/revocation UI are not implemented; current site grants are one-time.
- Installer and portable package definitions exist; the latest Windows CI run is being used to validate build, tests, and artifact generation.
- Code signing, updater, rollback, and release checksums remain outstanding.
