# Windows CI validation

This pull request validates the current look source tree on a native Windows runner.

The required workflow restores and builds the full .NET solution in Release, runs the xUnit suite, publishes a self-contained win-x64 app, and produces portable and installer packages. A green result is a build/package gate only; manual Windows browser, extension, privacy, and install/uninstall tests remain required.
