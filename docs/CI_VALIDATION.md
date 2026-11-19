# Windows CI validation

This note records the first full Windows validation pass for the current source tree.

The workflow in `.github/workflows/windows-build.yml` restores the solution, builds Release, runs the xUnit suite, then invokes `scripts/build-release.ps1` to publish the self-contained x64 app and create portable and installer packages.

A green workflow is required before the source is described as build-verified. A passing build still does not replace a manual Windows smoke test for WebView2 behavior, extension compatibility, private-data cleanup, split panes, or installer/uninstaller behavior.
