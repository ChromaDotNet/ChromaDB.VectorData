# Contributing

The provider moved to [CommunityToolkit.VectorData.Chroma](https://www.nuget.org/packages/CommunityToolkit.VectorData.Chroma) and is developed in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI): open issues and pull requests there. This repository will be archived.

## Build and test

See [Building and testing](https://github.com/ChromaDotNet/ChromaDB.VectorData/blob/v0.4.3/README.md#building-and-testing), in the README of 0.4.3, for:

- the commands
- Docker
- the variables that choose the Chroma version, or a server that is already running, like Chroma Cloud

## Pull requests

- A fix comes with a test that fails without it. That is a unit test, and also a conformance test when the behavior depends on Chroma.
- New functionality comes with tests in the automated test suite, which the CI runs on every pull request.
- A behavior that differs between Chroma versions is tested on the versions where it changes. The package README names those versions.
- Public types and members have XML documentation. The documentation goes in the package.
- Versions follow semantic versioning. The release notes of the package say what changed.

The CI is in [.github/workflows/ci.yml](.github/workflows/ci.yml). It runs the unit tests and the conformance tests, and publishes an application with NativeAOT.

Every change merged into `main` builds the package. A `v*` tag publishes it on NuGet.
