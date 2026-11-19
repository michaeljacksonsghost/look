# Browser engine decision record

**Status: provisional WebView2 prototype; production gate remains open.**

## Current evidence

The current app uses Microsoft Edge WebView2. The WebView2 SDK exposes profile-level extension APIs:
- Install an unpacked extension from a local folder with a `manifest.json`.
- Enumerate installed extensions.
- Enable, disable, and remove extensions.
- Persist installed extensions in the corresponding browser profile.

Microsoft's documentation also explicitly warns that extensions designed for browser UI interactions (for example toolbar icons and popups) can load but have missing UI entry points because WebView2 does not provide those browser UI surfaces. The current look implementation therefore exposes basic unpacked-extension management only. It does not claim Chrome Web Store support or full Chrome extension compatibility.

References:
- [WebView2 AddBrowserExtensionAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync)
- [CoreWebView2BrowserExtension](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2browserextension)
- [WebView2 extension profile interface and UI limitation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2profile7)

## Implemented in look

- Enable browser extensions in the WebView2 environment.
- Install unpacked extensions from a selected local folder.
- List installed extensions.
- Enable, disable, and remove installed extensions.
- Keep engine APIs behind `IBrowserEngine`.

These code paths are not yet verified by a Windows runtime test.

## Known gaps

- No Chrome Web Store / CRX installation flow.
- No extension toolbar icon strip or popup host.
- No extension options/side-panel UI.
- No verified MV3 service-worker lifecycle/recovery matrix.
- No verified support matrix for individual `chrome.*` APIs.
- No real extension has yet been installed and exercised in a Windows build.

## Validation matrix

| Capability | WebView2 evidence | look status |
|---|---|---|
| Unpacked extension install | SDK API exists | Implemented, unverified |
| List / enable / disable / remove | SDK APIs exist | Implemented, unverified |
| MV3 service worker lifecycle | Must test per extension | Not verified |
| Content scripts / isolated worlds | Must test per extension | Not verified |
| chrome.storage / permissions / alarms | Must test API-by-API | Not verified |
| Toolbar action / popup UI | WebView2 docs warn UI entry points are missing | Not implemented |
| CRX / Chrome Web Store installation | Not provided by the unpacked-folder API | Not implemented |
| Native messaging | Requires separate host integration | Not implemented |

## Decision rule

Keep WebView2 as the current development engine while core browsing and extension behavior are tested. Do not lock the production engine until the matrix has been run against real target extensions. If the required extensions depend on unsupported browser UI or APIs, compare alternatives and consider building missing browser UI surfaces. CEF is not assumed to provide complete Chrome Web Store compatibility either; it must pass the same tests.

## Architecture rule

UI and browser state must not depend directly on WebView2 types. Engine-specific code belongs behind `IBrowserEngine`. If a different engine is selected, replace the adapter rather than rewriting the tab/workspace model.
