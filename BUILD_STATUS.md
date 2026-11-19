# Build status

## Implemented in source

The current development branch contains:
- WPF browser shell with address/search navigation.
- Engine abstraction and a WebView2 adapter, with a separate WebView2 instance per tab.
- Persistent WebView2 profile shared by regular tabs.
- Tab title/address/loading state and back/forward/reload/stop navigation.
- Tab context actions: pin, duplicate, mute, close.
- Find-in-page overlay and Ctrl+F shortcut.
- Local JSON persistence for session tabs, bookmarks, browsing history, and recently closed tabs.
- Ctrl+Shift+T restore for the most recently closed tab.
- WebView2 unpacked-extension installation and management (list, enable, disable, remove).
- Windows GitHub Actions workflow and xUnit test project for address parsing and browser-data persistence.

## Verification status

- [ ] Windows CI build completed successfully.
- [ ] Unit tests passed in CI.
- [ ] Manual smoke test on Windows 10/11.
- [ ] Multi-tab navigation and history verified.
- [ ] Session restore, bookmark/history persistence verified.
- [ ] Unpacked extension install/disable/remove verified with real extensions.

This environment cannot run the Windows WPF application or .NET Windows runtime. No successful compile or manual runtime result is claimed. The GitHub Actions workflow must be checked before treating the current code as build-verified.

## Known limitations

- WebView2 extension support is partial: extensions are installed from unpacked local folders; toolbar buttons and popup entry points are not yet hosted. Full Manifest V3 and real-extension compatibility remains unverified.
- No CRX or Chrome Web Store installation flow.
- No downloads manager yet.
- Tabs share one persistent profile; private windows and cookie isolation are not implemented.
- Recently closed tabs are persisted, but only the last closed tab is currently restored by shortcut.
- Tab reordering, split view, Spaces, reader mode, content blocking, settings, and credential vault are not implemented.
- Installer, portable package, signing, updater, and rollback do not exist yet.
