# look privacy notice (development build)

look is an independent browser project. This notice describes the current source implementation, not a certified privacy or security guarantee.

## Data stored locally

- **Default profile:** `%LOCALAPPDATA%\look`.
- **Named profiles:** `%LOCALAPPDATA%\look\Profiles\<profile-id>`, each with its own WebView2 user-data directory and browser-data file.
- **Private windows:** a unique temporary directory under `%TEMP%\look-private`. look removes it when the private window closes and attempts to remove abandoned folders on the next startup.
- **Browser data:** local JSON files hold session tabs, Spaces, bookmarks, history, download metadata, and browser settings.
- **Downloads:** files are saved to a location chosen by the user, normally the Windows Downloads folder.
- **Extensions:** installed extension data is held in the corresponding WebView2 profile.

## Network and third parties

- look currently has no account, sync service, advertising, analytics, or telemetry implementation.
- Websites you visit and extensions you install can still collect data according to their own behavior and policies.
- WebView2 is powered by the Microsoft Edge runtime. Its runtime and network behavior are subject to Microsoft's terms and privacy documentation.
- look's manual domain-block list is not a comprehensive tracker/ad-blocking service.

## Credentials

The current development build does not include a password vault, password sync, or automatic password filling. Do not treat look as a password manager.

## Private browsing limitations

Private windows use a separate temporary WebView2 data directory and do not save look's history, bookmarks, download metadata, or session to the normal profile. Temporary files may remain recoverable from disk until Windows or the user removes them. Private mode is not forensic erasure and does not hide activity from websites, networks, DNS providers, or installed monitoring software.

## Current limitations

- Browser metadata JSON is not encrypted by look.
- Private-profile cleanup has not yet been verified on Windows crash/restart scenarios.
- Extension behavior and permissions have not yet passed a security review.
- The installer is not yet code-signed and there is no update mechanism.
- This notice must be reviewed before a public release.
