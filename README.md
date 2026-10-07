[![ChromaDotNet](https://raw.githubusercontent.com/ChromaDotNet/.github/main/assets/logo-64.png)](https://chromadotnet.org)

# ChromaDB.VectorData

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/ChromaDotNet/ChromaDB.VectorData/badge)](https://scorecard.dev/viewer/?uri=github.com/ChromaDotNet/ChromaDB.VectorData)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/15279/badge)](https://www.bestpractices.dev/projects/15279)

A [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview). It is built on [ChromaDB.Client](https://github.com/ChromaDotNet/ChromaDB.Client) and published as the `ChromaDotNet.VectorData` package. It has the same code as `CommunityToolkit.VectorData.Chroma`, proposed in [CommunityToolkit/AI#58](https://github.com/CommunityToolkit/AI/pull/58), and moves to it when that package ships.

> This is a community project. It is not affiliated with or endorsed by Chroma.

See the [package README](src/ChromaDB.VectorData/README.md) for how to use it, what is supported, and the limitations.

Website: [chromadotnet.org](https://chromadotnet.org)

## Building and testing

```bash
dotnet build ChromaDB.VectorData.slnx
dotnet test test/ChromaDB.VectorData.UnitTests
dotnet test test/ChromaDB.VectorData.ConformanceTests
```

The conformance tests start Chroma in a container with [Testcontainers](https://dotnet.testcontainers.org/), so they need Docker.

By default they use `chromadb/chroma:1.5.9`. Set `CHROMA_IMAGE` to pick another Chroma release, like `chromadb/chroma:1.5.0`.

To run them against a server that is already running, like Chroma Cloud, set the same variables as the ChromaDB.Client tests:

```bash
CHROMA_TEST_URI=https://api.trychroma.com CHROMA_TEST_TOKEN=<api key> CHROMA_TEST_TENANT=<tenant> CHROMA_TEST_DATABASE=<database> dotnet test test/ChromaDB.VectorData.ConformanceTests
```

The tests delete only the collections they create.

Hybrid search needs the Search API and the sparse vector indexes of Chroma Cloud. Its tests run only against Chroma Cloud, and are skipped otherwise.

`dotnet publish test/NativeAot -c Release -o native-aot` builds the NativeAOT check. The check runs against `CHROMA_URI` (default `http://localhost:8000`).

## Origin

The provider started as a copy, under the MIT license, of the Qdrant provider in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI). It was then adapted to Chroma.

It is now proposed back to the AI Community Toolkit, as `CommunityToolkit.VectorData.Chroma`, in [CommunityToolkit/AI#58](https://github.com/CommunityToolkit/AI/pull/58). This repository publishes it until the toolkit ships that package. Then this repository will be archived, and the package deprecated in favor of that one. The API is the same, so moving changes only the package reference and the namespace, from `ChromaDB.VectorData` to `CommunityToolkit.VectorData.Chroma`.
