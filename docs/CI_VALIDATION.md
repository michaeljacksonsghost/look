# Windows CI validation

This pull request validates the current source after fixing the non-web address parsing regression found by the previous Windows test run.

The workflow must complete the Release build, all xUnit tests, self-contained win-x64 publish, portable ZIP generation, and Inno Setup installer compilation.
