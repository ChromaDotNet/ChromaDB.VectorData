[![ChromaDotNet](https://raw.githubusercontent.com/ChromaDotNet/.github/main/assets/logo-64.png)](https://chromadotnet.org)

# ChromaDotNet.VectorData

> **Moved to [CommunityToolkit.VectorData.Chroma](https://www.nuget.org/packages/CommunityToolkit.VectorData.Chroma).** This package is deprecated and gets no more releases.

## Moving

The API is the same. Change the package reference and the namespace:

```bash
dotnet remove package ChromaDotNet.VectorData
dotnet add package CommunityToolkit.VectorData.Chroma --prerelease
```

```csharp
// Before:
using ChromaDB.VectorData;

// After:
using CommunityToolkit.VectorData.Chroma;
```

The registrations, like `AddChromaVectorStore`, keep their names and their namespace, `Microsoft.Extensions.DependencyInjection`.

## History

ChromaDotNet.VectorData was a [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client). From 0.4.0 it had the same code as `CommunityToolkit.VectorData.Chroma`, which the AI Community Toolkit accepted in [CommunityToolkit/AI#58](https://github.com/CommunityToolkit/AI/pull/58). The provider is developed there now.

It started as a copy, under the MIT license, of the Qdrant provider in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI), and was then adapted to Chroma. The releases and their notes are on [GitHub](https://github.com/ChromaDotNet/ChromaDB.VectorData/releases).

> This is a community project. It is not affiliated with or endorsed by Chroma.

Website: [chromadotnet.org](https://chromadotnet.org)
