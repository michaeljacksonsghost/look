# Final Windows CI validation

This validation includes the current profile management implementation (create, open, rename, delete) and its automated tests, in addition to the existing browser, workspace, privacy, reader, CSS, and packaging code.

A green run is required for Release compilation, xUnit tests, self-contained x64 publish, portable ZIP creation, and Inno Setup compilation. It does not replace hands-on Windows testing of browser behavior and security.


## Successful Windows run

- Workflow run: [#44498282282](https://github.com/michaeljacksonsghost/look/actions/runs/44498282282)
- Result: **success** — Release build, xUnit tests, self-contained win-x64 publish, portable ZIP, and Inno Setup installer all completed.
- Uploaded artifact: `look-windows-x64` (116,413,730 bytes).
- Artifact SHA-256: `1dc80d94fa89a0f029ac98f62feb66030fa194655aa5843805161e5b511cb58f`.
- Artifact expiry: 17 February 2027.

The artifact can be downloaded from the workflow run page. It is a CI artifact, not a signed public release. No interactive Windows smoke test has been completed.


## Installer warning cleanup

A follow-up changed the deprecated Inno Setup `x64` architecture alias to `x64compatible`, allowing x64 Windows and Windows 11 on Arm while avoiding the compiler deprecation warning. This change is being re-run through the Windows packaging workflow.
