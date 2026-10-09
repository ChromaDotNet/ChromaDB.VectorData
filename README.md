[![ChromaDotNet](https://raw.githubusercontent.com/ChromaDotNet/.github/main/assets/logo-64.png)](https://chromadotnet.org)

# ChromaDB.VectorData

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/ChromaDotNet/ChromaDB.VectorData/badge)](https://scorecard.dev/viewer/?uri=github.com/ChromaDotNet/ChromaDB.VectorData)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/15279/badge)](https://www.bestpractices.dev/projects/15279)

> **Moved to [CommunityToolkit.VectorData.Chroma](https://www.nuget.org/packages/CommunityToolkit.VectorData.Chroma),** in the [AI Community Toolkit](https://github.com/CommunityToolkit/AI), where the provider is developed now. The `ChromaDotNet.VectorData` package is deprecated, and this repository will be archived.

## Moving

The API is the same: change the package reference, from `ChromaDotNet.VectorData` to `CommunityToolkit.VectorData.Chroma`, and the namespace, from `ChromaDB.VectorData` to `CommunityToolkit.VectorData.Chroma`. The [package README](src/ChromaDB.VectorData/README.md) shows the commands.

## History

This repository published the `ChromaDotNet.VectorData` package, a [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview) built on [ChromaDB.Client](https://github.com/ChromaDotNet/ChromaDB.Client), from 0.1.0 to 0.4.4. From 0.4.0 it had the same code as `CommunityToolkit.VectorData.Chroma`, which the AI Community Toolkit accepted in [CommunityToolkit/AI#58](https://github.com/CommunityToolkit/AI/pull/58).

The provider started as a copy, under the MIT license, of the Qdrant provider in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI). It was then adapted to Chroma.

> This is a community project. It is not affiliated with or endorsed by Chroma.

Website: [chromadotnet.org](https://chromadotnet.org)
