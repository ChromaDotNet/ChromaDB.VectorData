# ChromaDotNet.VectorData

[Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client).

> This is a community project. It is not affiliated with or endorsed by Chroma.

## Quick start

1. Run Chroma with Docker:

```bash
docker run -d --name chroma -p 8000:8000 chromadb/chroma
```

2. Install the NuGet package:

```bash
dotnet add package ChromaDotNet.VectorData --prerelease
```

3. Store and search records:

```csharp
using ChromaDB.Client;
using ChromaDB.VectorData;
using Microsoft.Extensions.VectorData;

using var vectorStore = new ChromaVectorStore(new ChromaConfigurationOptions("http://localhost:8000"), new HttpClient(), ownsClient: true);

var collection = vectorStore.GetCollection<string, Hotel>("hotels");
await collection.EnsureCollectionExistsAsync();

await collection.UpsertAsync(new Hotel { Id = "h1", Name = "Grand", Rating = 5, Embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f } });

await foreach (var result in collection.SearchAsync(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, top: 3, new() { Filter = h => h.Rating >= 4 }))
{
    Console.WriteLine($"{result.Record.Name}: {result.Score}");
}

public sealed class Hotel
{
    [VectorStoreKey]
    public string Id { get; set; } = "";

    [VectorStoreData]
    public string? Name { get; set; }

    [VectorStoreData]
    public int Rating { get; set; }

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

With dependency injection:

```csharp
services.AddChromaVectorStore("http://localhost:8000");
```

## Supported

- Keys: `string` and `Guid`.
- One vector per record: `ReadOnlyMemory<float>`, `Embedding<float>` or `float[]`, or any type with an embedding generator.
- Data properties: `string`, `int`, `long`, `double`, `float`, `bool`, `DateTime`, `DateTimeOffset`, `DateOnly`, and arrays or lists of these, stored as Chroma metadata; dates are stored as ISO 8601 strings.
- Distance functions: `CosineSimilarity` (the default), `CosineDistance`, `DotProductSimilarity`, `NegativeDotProductSimilarity`, `EuclideanDistance` and `EuclideanSquaredDistance`, with the HNSW index.
- Filters: `==` and `!=`, `<`, `<=`, `>` and `>=` on numbers, `&&`, `||`, `!`, `Contains` over an inline list or an array property, and `Any` with `Contains` over an inline list.

## Limitations

- Chroma metadata has no null values: a null property is not stored, and filtering on null is not supported.
- `GetAsync` with a filter does not support ordering.
- Comparisons work on numbers only.
- Hybrid search is not supported.

The provider runs the Microsoft.Extensions.VectorData conformance tests against Chroma 1.5.0, 1.5.9 and the latest release.
