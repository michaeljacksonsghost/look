# Build roadmap

## 0 — Foundation
- [x] Establish product name and WPF project structure.
- [x] Compact shell, tabs, address/search parsing and WebView2 prototype.
- [x] Document the open engine compatibility gate.
- [x] Add an explicit production acceptance checklist.
- [x] Introduce an engine adapter and one WebView2 engine instance per tab.
- [ ] Confirm the first successful Windows CI build and run the app on Windows.

## 1 — Engine proof of concept (release-critical)
- [x] Move WebView2 behind the engine interface.
- [ ] Run the extension compatibility test matrix against WebView2.
- [ ] Evaluate CEF against the same test matrix.
- [ ] Select the production engine based on measured compatibility, not assumptions.
- [ ] Add automated tests for address parsing and tab state.

## 2 — Browser fundamentals
- [x] Independent WebView2 instance and navigation state per tab (initial implementation; needs Windows verification).
- [ ] Lazy tab creation, suspend/resume, and renderer crash recovery.
- [ ] Session save/restore and recently closed tabs.
- [ ] History, bookmarks, and downloads.
- [ ] Find-in-page, reader mode, and tab switcher.
- [ ] Reorder, pin, duplicate, and mute tabs.

## 3 — Search-inspired workspace
- [ ] Spaces for tab and pin organization.
- [ ] Profiles and explicit cookie/storage isolation.
- [ ] Two-to-four-pane split view with resizing and focus switching.
- [ ] Per-site content blocking and exceptions.
- [ ] Document-start element hiding and custom CSS.
- [ ] Picture-in-picture and media controls.

## 4 — Privacy and security
- [ ] Private browsing with disposable isolated storage.
- [ ] Site permission controls and revocation.
- [ ] Windows Credential Manager / DPAPI vault.
- [ ] No automatic password filling.
- [ ] Safe downloads, threat model, and security tests.

## 5 — Interface and distribution
- [ ] Reference-faithful visual polish, accessibility, and responsive sizing.
- [ ] Remove all placeholder controls and complete empty/error states.
- [ ] Signed x64 installer.
- [ ] Portable x64 package and runtime prerequisite docs.
- [ ] Update/rollback strategy and clean install/uninstall tests.

See [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md) for release acceptance criteria.
