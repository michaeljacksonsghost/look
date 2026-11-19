# Build status

## Implemented in source

The current development branch contains:
- WPF browser shell with address/search navigation.
- Browser-engine interface and WebView2 adapter.
- Separate WebView2 control and navigation state per tab.
- Shared environment manager with reference counting for regular and isolated profiles.
- Navigation, back/forward/reload/stop, and find-in-page.
- Tab close, duplicate, pin, mute, drag reorder, and keyboard shortcuts.
- Local JSON persistence for session tabs, recently closed tabs, bookmarks, history, and downloads.
- Save-as download flow, completion/interruption tracking, and recent downloads list.
- Basic unpacked-extension install/list/enable/disable/remove.
- Private windows with a separate temporary profile/data directory; the directory is deleted on normal close where Windows releases the files.
- Explicit portable mode that stores profile and browser data beside the executable in a Data directory.
- Version metadata, changelog, release instructions, PowerShell packaging script, and Inno Setup installer definition.
- Windows GitHub Actions workflow intended to build, test, package, and upload both x64 distributions.
- xUnit tests for address parsing and browser-data persistence.

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
- Private-window data is stored in a temporary directory and removed on normal close; crash recovery cleanup and forensic privacy review remain outstanding.
- No separate persistent user profiles beyond the default profile and private-window profiles.
- Tab suspension/resume and renderer crash recovery are not implemented.
- History search and per-entry deletion are not implemented.
- Content blocking, reader mode, custom CSS/element hiding, split view, Spaces, and tab switcher are not implemented.
- Credential vault and site permission manager are not implemented.
- Installer and portable package definitions exist, but actual artifacts have not yet been built and verified.
- Code signing, updater, rollback, and release checksums remain outstanding.
