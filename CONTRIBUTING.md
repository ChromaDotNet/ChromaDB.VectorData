# Contributing

Issues and pull requests are welcome.

## Build and test

See [Building and testing](README.md#building-and-testing) for:

- the commands
- Docker
- the variables that choose the Chroma version, or a server that is already running, like Chroma Cloud

## Pull requests

- A fix comes with a test that fails without it. That is a unit test, and also a conformance test when the behavior depends on Chroma.
- A behavior that differs between Chroma versions is tested on the versions where it changes. The package README names those versions.
- Public types and members have XML documentation. The documentation goes in the package.
- Versions follow semantic versioning. The release notes of the package say what changed.

The CI is in [.github/workflows/ci.yml](.github/workflows/ci.yml). It runs the unit tests and the conformance tests, and publishes an application with NativeAOT.

Every change merged into `main` builds the package. A `v*` tag publishes it on NuGet.
