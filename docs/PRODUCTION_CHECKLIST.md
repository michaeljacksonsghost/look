# look — production checklist

This is the release acceptance checklist for the Windows browser. A feature is not complete until its implementation, tests, and user-facing behavior have been verified on Windows 10/11 x64.

## 0. Project foundation
- [x] Establish product name, repository, WPF solution, and initial shell.
- [x] Record engine compatibility as an explicit release gate.
- [x] Add Windows CI for restore, build, and tests.
- [ ] Establish versioning, release notes, and a repeatable build process.
- [ ] Verify clean checkout builds on a fresh Windows machine.

## 1. Browser engine and extensions — critical gate
- [x] Implement the engine adapter and keep engine-specific APIs out of the UI.
- [ ] Compare WebView2 and CEF against the same extension test matrix.
- [ ] Verify Manifest V3 service-worker startup, suspension, and recovery.
- [ ] Verify content scripts, isolated worlds, and document-start injection.
- [ ] Verify extension popup, options, side-panel, and toolbar surfaces.
- [ ] Verify chrome.storage, permissions, commands, context menus, and alarms.
- [ ] Verify extension install, update, disable, remove, and permission revocation.
- [ ] Verify native messaging if the chosen engine supports it.
- [ ] Test real extensions: Bitwarden, Dark Reader, uBlock Origin Lite, and at least one productivity extension.
- [ ] Document pass/fail results and known incompatibilities before selecting the production engine.
- [ ] Do not advertise Chrome Web Store compatibility until the matrix passes.

## 2. Core browsing
- [ ] Independent page instance and navigation history per tab.
- [ ] Lazy tab creation, background loading, suspend/resume, and crash recovery.
- [ ] Back, forward, reload, stop, address/search, and new-tab behavior.
- [ ] Tab close, duplicate, reorder, pin, mute, and keyboard navigation.
- [ ] Session save/restore and recently closed tabs.
- [ ] Find in page and downloads manager.
- [ ] Bookmarks and browsing history with search and deletion.
- [ ] Correct handling of redirects, downloads, external protocols, and file URLs.

## 3. Search-inspired workspace
- [ ] Spaces for organizing tabs and pinned pages.
- [ ] Profiles with explicit cookie/storage isolation.
- [ ] Two-to-four-pane split view with resizing and focus switching.
- [ ] Reader mode and clean reading typography.
- [ ] Per-site content blocking and allow-list exceptions.
- [ ] Element hiding/custom CSS applied before first paint where possible.
- [ ] Picture-in-picture and media controls.
- [ ] Tab switcher, peek links, and useful context menus.

## 4. Privacy and security
- [ ] No account, ads, analytics, or telemetry by default.
- [ ] Private windows use isolated, disposable storage and leave no session on close.
- [ ] Site permissions are visible, understandable, and revocable.
- [ ] Credentials use Windows Credential Manager / DPAPI; never plaintext.
- [ ] No automatic password filling; suggestions require an explicit user gesture.
- [ ] Safe-download handling and executable warning flow.
- [ ] Review renderer isolation, process boundaries, IPC, and navigation policies.
- [ ] Dependency and vulnerability review before each release.
- [ ] Threat model, privacy notice, and data-location documentation.

## 5. Interface and accessibility
- [ ] Match the reference's compact, calm visual hierarchy without copying its assets.
- [ ] Responsive layout at common laptop and desktop sizes.
- [ ] Consistent hover, focus, active, loading, and disabled states.
- [ ] Full keyboard operation and documented shortcuts.
- [ ] High-contrast/readability checks and Windows scaling tests.
- [ ] No dead controls, placeholder menu items, or misleading status labels.
- [ ] Empty, error, offline, and first-run states are polished.

## 6. Reliability and performance
- [ ] Automated unit tests for address parsing, settings, and browser state.
- [ ] Integration tests for tab lifecycle, navigation, and session restore.
- [ ] Test 1, 10, 25, and 50 open tabs.
- [ ] Test sleep/resume, network loss, renderer crash, and app restart.
- [ ] Measure cold start, memory per tab, idle CPU, and navigation responsiveness.
- [ ] Verify no unhandled exceptions or data loss during shutdown.

## 7. Distribution and release
- [ ] Reproducible x64 Release build.
- [ ] Portable package with user data stored beside the app only when explicitly configured.
- [ ] Installer with prerequisite detection and clean install/uninstall.
- [ ] Decide WebView2 Evergreen vs fixed runtime, or CEF runtime packaging.
- [ ] Code signing and timestamping plan.
- [ ] Update channel, signature verification, rollback, and release notes.
- [ ] Test upgrade, downgrade/rollback, and data migration.
- [ ] Publish checksums and a clear privacy/runtime requirements page.

## Release gates
- **Alpha:** core navigation, true per-tab state, stable Windows build.
- **Beta:** extension matrix decision, session/history/downloads, privacy controls, automated tests.
- **1.0:** security review, polished UX, installer + portable package, verified upgrade/uninstall, and no known data-loss defects.

## Implemented in source, awaiting Windows verification

The following capabilities now have initial code paths, but remain unchecked above until Windows CI and manual runtime testing confirm them:
- Session persistence and restoration.
- Recently closed tab restoration (Ctrl+Shift+T).
- Local bookmarks and browsing history, including clear-history action.
- Find-in-page (Ctrl+F), stop navigation, and tab context actions for pin, duplicate, mute, and close.
- WebView2 unpacked-extension install/list/enable/disable/remove.
- Session autosave on tab selection and successful navigation.
- User-managed domain blocking and allowlist settings.
- Resizable two-pane split view with pane switching.
- Named persistent profiles with isolated WebView2 storage and profile-specific browser data.
- Text-first reader mode that restores the original URL when exited.
- Per-domain custom CSS editor with document-creation injection and click-to-hide element picker.

WebView2's documented extension UI limitations mean toolbar action/popup hosting and full extension compatibility are still open requirements.

A checked item means verified, not merely coded. Items that depend on engine selection remain open until the compatibility gate is resolved.
