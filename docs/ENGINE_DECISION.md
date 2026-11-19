# Browser engine decision record

**Status: provisional WebView2 prototype; production gate remains open.**

## Current recommendation

Keep WebView2 as the development engine while the browser's core features mature. Do not yet declare it the production engine. The current extension requirement is stricter than "can load an unpacked extension": look needs to host extension toolbar actions, popups, options pages, background workers, permissions, storage, and real-world extensions reliably.

Microsoft documents WebView2 extension support and profile-level APIs for installing, listing, enabling, disabling, and removing extensions. Its host application does not automatically provide the browser chrome surfaces that extensions expect, so look must build those surfaces itself. The current implementation only has basic unpacked-extension management.

References:
- [WebView2 feature and API overview](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/overview-features-apis)
- [WebView2 AddBrowserExtensionAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync)
- [CoreWebView2BrowserExtension](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2browserextension)
- [WebView2 extension profile interface](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2profile7)

## Alternative considered: CEF / CefSharp

CefSharp is a maintained .NET wrapper for Chromium and offers WPF and WinForms controls. It can load extensions and lets the host create extension popup views, but CEF exposes only a subset of Chromium's extension APIs. CefSharp's own usage guide explicitly warns that extension support is limited. It also documents that sandboxing is not implemented in CefSharp, which is a significant security trade-off for a general-purpose browser.

Additional trade-offs:
- Native CEF/Chromium binaries and the Visual C++ runtime increase package size.
- CEF initialization and shutdown are process-wide lifecycle concerns.
- The WPF off-screen-rendering implementation has different performance characteristics from native-window hosting.
- Extension UI and API behavior still need per-extension testing.

References:
- [CefSharp repository and release branches](https://github.com/cefsharp/CefSharp)
- [CefSharp general usage and limitations](https://github.com/cefsharp/CefSharp/wiki/General-Usage)
- [CEF extension API support notes](https://github.com/chromiumembedded/cef/issues/1947)

CEF is therefore a candidate for a controlled prototype, not an assumed solution for full Chrome compatibility.

## Implemented in look

- Enable browser extensions in the WebView2 environment.
- Install unpacked extensions from a selected local folder.
- List installed extensions.
- Enable, disable, and remove installed extensions.
- Keep engine APIs behind `IBrowserEngine` and an injectable factory.
- Maintain separate WebView2 user-data folders for the default and private sessions.

These code paths are not yet verified by a Windows runtime test.

## Known gaps

- No Chrome Web Store / CRX installation flow.
- No extension toolbar icon strip or popup host.
- No extension options/side-panel UI.
- No verified MV3 service-worker lifecycle/recovery matrix.
- No verified support matrix for individual `chrome.*` APIs.
- No real extension has yet been installed and exercised in a Windows build.
- No CEF adapter or comparative benchmark has been built.

## Validation matrix

| Capability | WebView2 | CEF / CefSharp | look status |
|---|---|---|---|
| Unpacked extension install | Profile API exists | Extension loading APIs exist | WebView2 implemented, unverified |
| List / enable / disable / remove | Profile APIs exist | Host implementation required | WebView2 implemented, unverified |
| MV3 service-worker lifecycle | Must test per extension | Must test per extension | Not verified |
| Content scripts / isolated worlds | Must test per extension | Must test per extension | Not verified |
| chrome.storage / permissions / alarms | Must test API-by-API | Only a subset of Chromium APIs is exposed | Not verified |
| Toolbar action / popup UI | Host must provide surfaces | Host must create extension views | Not implemented |
| CRX / Chrome Web Store installation | Not provided by the unpacked-folder API | CRX must be unpacked and loaded by host | Not implemented |
| Native messaging | Separate host integration required | Separate host integration required | Not implemented |
| Renderer sandbox | WebView2 managed by Edge runtime | CefSharp documents sandboxing as unavailable | Security review pending |

## Decision rule

1. Keep the adapter replaceable.
2. Test representative extensions against the actual features look intends to support.
3. Prototype CEF only if WebView2 fails a required capability and CEF can demonstrate a better result.
4. Do not advertise Chrome Web Store compatibility unless the exact install and runtime behaviors have passed.
5. Do not ship a CEF-based build without a documented security decision about its sandbox limitations.

## Architecture rule

UI and browser state must not depend directly on WebView2 or CEF types. Engine-specific code belongs behind `IBrowserEngine` and `IBrowserEngineFactory`. If a different engine is selected, replace the adapter rather than rewriting the tab/workspace model.


## Additional candidate: DotNetBrowser

A stronger commercial alternative is [DotNetBrowser](https://teamdev.com/dotnetbrowser/), a Chromium component designed for .NET/WPF applications. TeamDev advertises Chrome Web Store installation and programmatic CRX loading, plus Chromium profiles. It is therefore a closer feature match to the extension requirement than WebView2 or the CefSharp subset.

The trade-off is cost and vendor dependency:
- The current published individual license is approximately **US$1,429**, with a 30-day evaluation period.
- A commercial license is required for continued use and distribution.
- The engine would still need a proof-of-concept for the exact extensions look intends to support, especially MV3 service workers, popup UI, permissions, and update behavior.

References:
- [DotNetBrowser features and Chrome extension support](https://teamdev.com/dotnetbrowser/)
- [DotNetBrowser licensing](https://teamdev.com/dotnetbrowser/docs/guides/licensing/)

**Decision:** do not add this commercial dependency or incur a license cost without explicit approval. If full Chrome extension compatibility is a hard requirement, the next evidence-based step is a time-limited DotNetBrowser trial and the same extension test matrix—not a blind switch to CEF.
