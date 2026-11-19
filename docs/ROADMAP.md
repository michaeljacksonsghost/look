# Build roadmap

## 0 — Foundation
- [x] Establish product name and WPF project structure.
- [x] Compact shell, tabs, address/search parsing and WebView2 prototype.
- [x] Document the open engine compatibility gate.
- [x] Add production checklist and Windows CI workflow.
- [x] Introduce an engine adapter and per-tab WebView2 controls.
- [ ] Confirm successful Windows CI build and manual runtime verification.

## 1 — Engine proof of concept (release-critical)
- [x] Move WebView2 behind the engine interface.
- [x] Enable WebView2's unpacked-extension API and add basic extension management.
- [ ] Run extension compatibility matrix with real target extensions.
- [ ] Host extension toolbar buttons, popups, and options UI.
- [ ] Evaluate CEF or other Chromium embedding options against the same matrix.
- [ ] Select production engine based on measured compatibility.

## 2 — Browser fundamentals
- [x] Implement per-tab WebView2 instances and independent navigation state (runtime verification pending).
- [x] Add stop, find-in-page, pin, duplicate, mute, and close tab actions.
- [x] Add local session restore, bookmarks, history, and recently closed tab restore.
- [ ] Add downloads manager and safe-download flow.
- [ ] Add lazy tab suspension/resume and renderer crash recovery.
- [ ] Add tab reordering, tab switcher, and full keyboard shortcut map.
- [ ] Add tests for tab lifecycle and session recovery.

## 3 — Search-inspired workspace
- [ ] Spaces for tab and pin organization.
- [ ] Profiles and explicit cookie/storage isolation.
- [ ] Two-to-four-pane split view with resizing and focus switching.
- [ ] Reader mode and clean reading typography.
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
- [ ] Complete empty/error/offline states and remove placeholder controls.
- [ ] Versioning, release notes, and repeatable build process.
- [ ] Signed x64 installer.
- [ ] Portable x64 package and runtime prerequisite docs.
- [ ] Update/rollback strategy and clean install/uninstall tests.

See [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md) for acceptance criteria and [ENGINE_DECISION.md](ENGINE_DECISION.md) for the extension compatibility gate.
