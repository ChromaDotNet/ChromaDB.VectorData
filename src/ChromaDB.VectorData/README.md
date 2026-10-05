# ChromaDotNet.VectorData

[Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client).

> This is a community project. It is not affiliated with or endorsed by Chroma.

## Quick start

1. Run Chroma with Docker:

```bash
docker run -d --name chroma -p 8000:8000 chromadb/chroma:1.5.9
```

2. Install the NuGet package:

```bash
dotnet add package ChromaDotNet.VectorData
```

3. Store and search records:

```csharp
using ChromaDB.Client;
using ChromaDB.VectorData;
using Microsoft.Extensions.VectorData;

using var httpClient = new HttpClient();
var client = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), httpClient);
using var vectorStore = new ChromaVectorStore(client);

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

    [VectorStoreData(IsFullTextIndexed = true)]
    public string? Name { get; set; }

    [VectorStoreData]
    public int Rating { get; set; }

    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

With dependency injection, the vector store takes the `ChromaClient` of the container, like the singleton that [ChromaDotNet.Client.DependencyInjection](https://www.nuget.org/packages/ChromaDotNet.Client.DependencyInjection) registers with an `HttpClient` from `IHttpClientFactory`:

```csharp
services.AddChromaClient(_ => new ChromaConfigurationOptions("http://localhost:8000"));
services.AddChromaVectorStore();
```

Or it creates its own client, from a URI or from the options of the client, like those of Chroma Cloud:

```csharp
services.AddChromaVectorStore("http://localhost:8000");

services.AddChromaVectorStore(new ChromaConfigurationOptions("https://api.trychroma.com", tenant: "<tenant>", database: "<database>")
    .WithChromaToken("<api key>"));
```

## Chroma Cloud

Connect with the options of the client: the API key goes in the `X-Chroma-Token` header, with the tenant and the database of the Chroma Cloud dashboard. Chroma Cloud reads and writes at most 300 records per request: on its addresses the client writes and reads in batches of 300 by itself. A search returns at most 300 results, `top` plus `Skip` included.

## Hybrid search

On Chroma Cloud, `HybridSearchAsync` searches with a vector and keywords together: it fuses the ranks of the vector search and of a BM25 search of the keywords in a full-text indexed `string` property. The BM25 search needs a BM25 index, a sparse vector index of Chroma, on the text of the property. With `CreateBm25Indexes`, creating the collection creates one for each full-text indexed `string` property, like `Name` of `Hotel` above, and the client computes the BM25 vectors of the records as it writes them:

```csharp
var collection = new ChromaCollection<string, Hotel>(client, "hotels-hybrid", new() { CreateBm25Indexes = true });
await collection.EnsureCollectionExistsAsync();

var results = collection.HybridSearchAsync(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, ["pool", "spa"], top: 5);
```

`ChromaVectorStoreOptions` has the same option for the collections of a vector store. Only with the option a collection answers `IKeywordHybridSearchable` from `GetService`, which the `TextSearchStore` of Semantic Kernel asks for to choose hybrid search over vector search. A collection created by another client of Chroma, like the Python one, works too when it has a `chroma_bm25` index on the text of the property, or on the documents for the property stored as the document. A record without any of the keywords gets nothing from the BM25 search, as in a keyword search. A single Chroma server has neither the Search API nor sparse vector indexes.

## Supported

- Keys: `string` and `Guid`.
- One vector per record: `ReadOnlyMemory<float>`, `Embedding<float>` or `float[]`, or any type with an embedding generator.
- Data properties: `string`, `int`, `long`, `double`, `float`, `bool`, `DateTime`, `DateTimeOffset`, `DateOnly`, and arrays or lists of these, stored as Chroma metadata; dates are stored as ISO 8601 strings.
- Distance functions: `CosineSimilarity` (the default), `CosineDistance`, `DotProductSimilarity`, `NegativeDotProductSimilarity`, `EuclideanDistance` and `EuclideanSquaredDistance`, with the HNSW index.
- An existing collection must use the space of the distance function, as a collection created by another Chroma client without a space uses l2: otherwise the provider throws, rather than turning the distances of another space into scores.
- Filters: `==` and `!=`, `<`, `<=`, `>` and `>=` on numbers, `&&`, `||`, `!`, `Contains` over an inline list or an array property, and `Any` with `Contains` over an inline list.
- Filters on the key: `==` and `Contains` over a list of keys, joined to the other conditions with `&&`; Chroma looks the records up by id.
- Full-text: the only full-text indexed `string` property is also stored as the Chroma document, where other Chroma clients store their text. `Contains` on it filters the text with `where_document`, joined to the other conditions with `&&`, and a record that has its text in the document only reads it into that property.
- NativeAOT and trimming: the dynamic collection, from `GetDynamicCollection` with a `VectorStoreCollectionDefinition`, works without reflection; `ChromaCollection<TKey, TRecord>` maps the properties of the record type by reflection.

## Limitations

- Chroma metadata has no null values: a null property is not stored, and filtering on null is not supported.
- Chroma does not store empty lists: an empty array or list is not stored, and comes back as null.
- `GetAsync` with a filter does not support ordering.
- Comparisons work on numbers only.
- Array properties need Chroma 1.5.0 or later.
- Hybrid search needs Chroma Cloud.
- Only one property can be the document: with more full-text indexed string properties, none is.

The provider runs the Microsoft.Extensions.VectorData conformance tests against Chroma 1.5.0, 1.5.9 and the latest release, and passes them on Chroma Cloud.
