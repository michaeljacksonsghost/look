# look threat model (pre-release)

## Assets

- Browsing history, bookmarks, open tabs, and session state.
- Cookies, local storage, and other website data in each WebView2 profile.
- Downloaded files and download metadata.
- User-installed browser extensions and their permissions.
- Private-session data in temporary storage.
- Per-site content-blocking and CSS settings.

## Trust boundaries

1. **look UI:** WPF controls, tab/workspace state, local JSON data, and engine adapter.
2. **WebView2 runtime:** Chromium-based rendering, network requests, cookies, storage, and renderer processes.
3. **Web content:** arbitrary remote HTML, JavaScript, redirects, and downloads.
4. **Extensions:** user-installed code that may receive access to pages and browser APIs according to granted permissions.
5. **Operating system and user account:** local filesystem, Windows account permissions, Credential Manager, and installed runtime.

## Threats and current mitigations

| Threat | Current mitigation | Remaining gap |
|---|---|---|
| Malicious site requests camera, microphone, location, or other WebView2 permissions | Requests default to deny; the user must approve each request; grants are not saved in the profile | Test every permission kind and confirm UI origin display on Windows |
| Malicious executable download | Warning before saving common executable/script file types; user chooses save path | No reputation service, malware scanning, Mark-of-the-Web validation, or signature inspection |
| Cross-site tracking / unwanted domains | User-managed host block list and allow exceptions | No maintained filter lists; service-worker and shared-worker coverage is incomplete |
| Unwanted page elements | Per-domain CSS injected at document creation and click-to-hide picker | CSS can break sites; no selector review/undo history yet |
| Private data left after crash | Exclusive lock file; abandoned temporary folders are cleaned at next startup | Runtime crash/restart tests and forensic review remain |
| Cross-profile cookie leakage | Separate WebView2 user-data folders for named profiles and private windows | Verify isolation in a Windows multi-profile test |
| Malicious extension | Extension installation is user-initiated and limited to unpacked folders in the current UI | No complete permission review, store signing, CRX verification, or extension sandbox audit |
| Local access to browser metadata | Data stored in the current Windows user's profile directory | JSON metadata is not encrypted by look |
| Renderer compromise | WebView2 uses the Edge runtime's process architecture and security controls | look has not independently audited sandbox configuration, site isolation, or IPC policy |

## Explicit non-goals in this build

- No password vault, password autofill, or credential sync.
- No account, cloud sync, telemetry, or advertising.
- No guarantee that private mode erases forensic traces.
- No claim of Chrome Web Store or complete Manifest V3 compatibility.
- No automatic installation of extensions or background software.

## Release blockers

Before 1.0:
- Complete a Windows security and privacy review.
- Test WebView2 permission prompts, downloads, renderer crashes, profile isolation, and private cleanup.
- Decide whether to ship extension support in WebView2 or a different engine after the compatibility matrix.
- Review extension permissions and prevent untrusted extension UI from impersonating browser chrome.
- Add secure credential storage only if a threat model and Windows Credential Manager/DPAPI implementation are reviewed.
- Code-sign installer and executable; document update integrity and rollback.
