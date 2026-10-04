# ChromaDB.VectorData

A [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client), and published as the `ChromaDotNet.VectorData` package.

> This is a community project. It is not affiliated with or endorsed by Chroma.

See the [package README](src/ChromaDB.VectorData/README.md) for how to use it, what is supported and the limitations.

## Building and testing

```bash
dotnet build ChromaDB.VectorData.slnx
dotnet test test/ChromaDB.VectorData.UnitTests
dotnet test test/ChromaDB.VectorData.ConformanceTests
```

The conformance tests start Chroma in a container with [Testcontainers](https://dotnet.testcontainers.org/), so they need Docker.

## Origin

The provider started as a copy of the Qdrant provider of [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI), under the MIT license, adapted to Chroma.
