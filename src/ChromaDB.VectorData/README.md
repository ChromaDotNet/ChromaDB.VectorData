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

A [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client), published from 0.1.0 to 0.4.4. It started as a copy of the Qdrant provider in [CommunityToolkit/AI](https://github.com/CommunityToolkit/AI), under the MIT license. The AI Community Toolkit accepted it in [CommunityToolkit/AI#58](https://github.com/CommunityToolkit/AI/pull/58). The releases and their notes are on [GitHub](https://github.com/ChromaDotNet/ChromaDB.VectorData/releases).

> This is a community project. It is not affiliated with or endorsed by Chroma.

Website: [chromadotnet.org](https://chromadotnet.org)
