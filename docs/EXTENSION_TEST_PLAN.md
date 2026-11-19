# Extension compatibility test plan

The fixture in `tests/fixtures/extension-mv3` is a deliberately small Manifest V3 extension. Install it as an unpacked extension from the look Extensions menu on a Windows machine with the WebView2 Runtime installed.

## Fixture checks

- [ ] Install the folder and confirm the extension appears in the manager.
- [ ] Enable and disable the extension.
- [ ] Open an HTTP and HTTPS page and confirm the content-script badge appears.
- [ ] Open the extension popup and press **Ping service worker**.
- [ ] Confirm the worker replies and the count increments across popup openings.
- [ ] Close/reopen the popup and confirm `chrome.storage.local` persists.
- [ ] Navigate to a new page and confirm the content script runs at document start.
- [ ] Restart look and confirm extension state and storage persist.
- [ ] Remove the extension and confirm its content script no longer runs.

## Real-world extension matrix

Do not claim general extension compatibility based only on the fixture. Test current versions of:
- Bitwarden (vault popup, login fill only if later implemented, storage, permissions).
- Dark Reader (content scripts, dynamic style injection, storage).
- uBlock Origin Lite (declarative rules, service worker, filtering behavior).
- One productivity extension with an options page and keyboard command.

For each extension record:
- install and update success;
- toolbar/action and popup behavior;
- options page behavior;
- content scripts and isolated-world behavior;
- Manifest V3 service-worker start/suspend/recovery;
- storage, permissions, alarms, commands, and context menus;
- CPU/memory impact and errors after browser restart.

## Results

No fixture or third-party extension has been manually exercised on a Windows desktop yet. The matrix remains an explicit release gate. The WebView2 extension APIs currently used by look do not by themselves provide a Chrome Web Store installation flow or a complete extension toolbar/popup experience.
