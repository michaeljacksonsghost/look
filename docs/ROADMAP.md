# Build roadmap

## 0 — Foundation
- [x] Establish product name and WPF project structure.
- [x] Compact shell, tabs, address/search parsing and WebView2 prototype.
- [x] Document the open engine compatibility gate.
- [ ] Compile and run on Windows.

## 1 — Engine proof of concept
- [ ] Move WebView2 behind a complete IBrowserEngine adapter.
- [ ] Test MV3 extension compatibility and representative extensions.
- [ ] Select WebView2 or CEF based on evidence.

## 2 — Browser fundamentals
- [ ] Real per-tab engine lifecycle and lazy instantiation.
- [ ] Session restore, history, bookmarks and downloads.
- [ ] Find-in-page, reader mode, split view and tab switcher.

## 3 — Search-inspired features
- [ ] Pinned tabs and top/side tab rail.
- [ ] Resizable split view and focused pane.
- [ ] Disposable isolated private profile.
- [ ] Network content blocking and per-site exceptions.
- [ ] Document-start element hiding and picture-in-picture.

## 4 — Privacy and security
- [ ] Windows Credential Manager / DPAPI vault.
- [ ] No automatic password filling.
- [ ] Permission controls, safe downloads and security tests.

## 5 — Distribution
- [ ] Signed x64 installer.
- [ ] Portable x64 package and runtime prerequisite docs.
- [ ] Update/rollback strategy and clean uninstall tests.
