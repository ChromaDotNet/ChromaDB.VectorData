# Contributing

Issues and pull requests are welcome.

## Build and test

See [Building and testing](README.md#building-and-testing) for the commands, Docker and the variables that choose the Chroma version or a server already running, like Chroma Cloud.

## Pull requests

- A fix comes with a test that fails without it: a unit test, and a conformance test when the behavior depends on Chroma.
- A behavior that differs between Chroma versions is tested on the versions where it changes, and the package README names those versions.
- Public types and members have XML documentation, which goes in the package.
- Versions follow semantic versioning, and the release notes of the package say what changed.

The CI, in [.github/workflows/ci.yml](.github/workflows/ci.yml), runs the unit tests and the conformance tests and publishes an application with NativeAOT. Every change merged into `main` builds the package; a tag `v*` publishes it on NuGet.
