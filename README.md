# ChromaDB.VectorData

A [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDB.Client](https://github.com/ChromaDotNet/ChromaDB.Client), and published as the `ChromaDotNet.VectorData` package.

> This is a community project. It is not affiliated with or endorsed by Chroma.

See the [package README](src/ChromaDB.VectorData/README.md) for how to use it, what is supported and the limitations.

## Building and testing

```bash
dotnet build ChromaDB.VectorData.slnx
dotnet test test/ChromaDB.VectorData.UnitTests
dotnet test test/ChromaDB.VectorData.ConformanceTests
```

The conformance tests start Chroma in a container with [Testcontainers](https://dotnet.testcontainers.org/), so they need Docker.

`CHROMA_IMAGE` picks another Chroma release, like `chromadb/chroma:1.5.0`. To run them against a server already running, like Chroma Cloud, set the same variables as the tests of ChromaDotNet.Client; the tests delete only the collections they create:

```bash
CHROMA_TEST_URI=https://api.trychroma.com CHROMA_TEST_TOKEN=<api key> CHROMA_TEST_TENANT=<tenant> CHROMA_TEST_DATABASE=<database> dotnet test test/ChromaDB.VectorData.ConformanceTests
```

Hybrid search needs the Search API and the sparse vector indexes of Chroma Cloud: its tests run only against Chroma Cloud, and are skipped otherwise.

## Origin

The provider started as a copy of the Qdrant provider of [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI), under the MIT license, adapted to Chroma.
