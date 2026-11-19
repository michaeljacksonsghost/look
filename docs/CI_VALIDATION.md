# Windows CI validation

This pull request validates the current source tree on a native Windows runner.

The workflow must:
1. Restore and build the full .NET solution in Release.
2. Run the xUnit tests without rebuilding.
3. Publish a self-contained win-x64 application.
4. Produce a portable ZIP and an Inno Setup installer.

A green workflow confirms compilation, automated tests, and packaging on the runner. It does not replace manual Windows checks for WebView2 navigation, extension behavior, profile isolation, split panes, privacy cleanup, or installation and uninstallation.
