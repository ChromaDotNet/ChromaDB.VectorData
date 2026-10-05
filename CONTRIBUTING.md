# Contributing

Issues and pull requests are welcome.

## Build and test

```bash
dotnet build ChromaDB.VectorData.slnx
dotnet test test/ChromaDB.VectorData.UnitTests
dotnet test test/ChromaDB.VectorData.ConformanceTests
```

The conformance tests start Chroma in a container with Testcontainers, so Docker must be running. The variables that choose the Chroma version, or a server already running like Chroma Cloud, are described in [Building and testing](README.md#building-and-testing).

## Pull requests

- A fix comes with a test that fails without it: a unit test, and a conformance test when the behavior depends on Chroma.
- A behavior that differs between Chroma versions is tested on the versions where it changes, and the package README names those versions.
- Public types and members have XML documentation, which goes in the package.
- Versions follow semantic versioning, and the release notes of the package say what changed.

The CI runs the unit tests on Linux and Windows and the conformance tests against Chroma 1.5.0, 1.5.9 and the latest release, and publishes an application with NativeAOT. Every change merged into `main` builds the package; a tag `v*` publishes it on NuGet.
