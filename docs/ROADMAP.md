# Build roadmap

## 0 — Foundation
- [x] Establish product name and WPF project structure.
- [x] Compact shell, tabs, address/search parsing and WebView2 prototype.
- [x] Document the open engine compatibility gate.
- [x] Add production checklist and Windows CI workflow.
- [x] Introduce engine abstraction and per-tab WebView2 controls.
- [ ] Confirm successful Windows CI build and manual runtime verification.

## 1 — Engine proof of concept (release-critical)
- [x] Move WebView2 behind the engine interface.
- [x] Enable WebView2's unpacked-extension API and basic extension management.
- [ ] Run extension compatibility matrix with real target extensions.
- [ ] Host extension toolbar buttons, popups, and options UI.
- [ ] Evaluate CEF or other Chromium embedding options against the same matrix.
- [ ] Select production engine based on measured compatibility.

## 2 — Browser fundamentals
- [x] Implement per-tab WebView2 instances and independent navigation state (runtime verification pending).
- [x] Add stop, find-in-page, pin, duplicate, mute, close, and drag reorder actions.
- [x] Add local session restore/autosave, recently closed tabs, bookmarks, and history.
- [x] Add save-as downloads and a recent downloads list.
- [x] Add searchable Library view and individual bookmark/history/download-record deletion (runtime verification pending).
- [x] Add background tab suspend/resume (runtime verification pending).
- [ ] Add renderer crash recovery.
- [ ] Add complete keyboard shortcut map and tab switcher.
- [ ] Add tests for tab lifecycle and session recovery.

## 3 — Search-inspired workspace
- [x] Add separate temporary profile/data location for private windows (runtime/privacy verification pending).
- [x] Add named Spaces for tab organization and moving tabs between Spaces (runtime verification pending).
- [ ] Persistent user profiles with explicit cookie/storage isolation.
- [x] Two-pane split view with resizing and pane switching (runtime verification pending).
- [ ] Extend split layout to three and four panes.
- [x] Add text-first reader mode and clean reading typography (runtime verification pending).
- [ ] Preserve images, links, article structure, and scroll position in reader mode.
- [x] Add user-managed per-domain blocking and allowlist exceptions (runtime verification pending).
- [ ] Add maintained ad/tracker filter lists and per-site controls.
- [ ] Document-start element hiding and custom CSS.
- [ ] Picture-in-picture and media controls.

## 4 — Privacy and security
- [x] Implement private profile cleanup on normal close and stale-folder cleanup at next startup (runtime/privacy verification pending).
- [x] Add per-request site permission prompts (default deny, allow once).
- [ ] Add permission review/revocation UI for any future persisted grants.
- [ ] Windows Credential Manager / DPAPI vault.
- [ ] No automatic password filling.
- [ ] Safe-download executable warning flow.
- [ ] Threat model, dependency audit, and security tests.

## 5 — Interface and distribution
- [x] Add version metadata and changelog.
- [x] Add repeatable x64 publish script, portable mode, and installer definition.
- [x] Add Windows CI packaging workflow.
- [ ] Verify portable ZIP and installer artifacts on clean Windows machines.
- [ ] Reference-faithful visual polish, accessibility, and responsive sizing.
- [ ] Complete empty/error/offline states and remove placeholder controls.
- [ ] Code signing, update/rollback strategy, and clean install/uninstall tests.

See [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md), [BUILD_STATUS.md](../BUILD_STATUS.md), and [ENGINE_DECISION.md](ENGINE_DECISION.md).
