# Browser engine decision record

**Status: open.** WebView2 is a prototype host, not yet approved as the production engine.

The macOS reference uses WebKit's native extension API and custom shims. WebView2 is convenient on Windows but is not equivalent to full Chrome extension support. Extension compatibility is an acceptance criterion.

## Validation sequence
1. Test MV3 background service-worker lifecycle and recovery.
2. Test content scripts, isolated worlds and document-start injection.
3. Test extension popup/options pages, storage, permissions and commands.
4. Test native messaging and install/update/signature handling.
5. Try representative real extensions and record feature-level pass/fail.
6. If WebView2 misses the required bar, evaluate CEF before committing to the engine.

Keep UI and browser state independent of WebView2 types. Engine-specific code belongs under Infrastructure/Engines/<EngineName>.
