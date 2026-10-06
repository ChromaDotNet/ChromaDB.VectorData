[![ChromaDotNet](https://raw.githubusercontent.com/ChromaDotNet/.github/main/assets/logo-64.png)](https://chromadotnet.org)

# ChromaDotNet.VectorData

A [Chroma](https://www.trychroma.com/) provider for [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/ai/vector-stores/overview), built on [ChromaDotNet.Client](https://github.com/ChromaDotNet/ChromaDB.Client).

> This is a community project. It is not affiliated with or endorsed by Chroma.

Website: [chromadotnet.org](https://chromadotnet.org)

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

With dependency injection, the vector store takes the `ChromaClient` from the container. That can be the singleton that [ChromaDotNet.Client.DependencyInjection](https://www.nuget.org/packages/ChromaDotNet.Client.DependencyInjection) registers, which gets its `HttpClient` from `IHttpClientFactory`:

```csharp
// dotnet add package ChromaDotNet.Client.DependencyInjection
using ChromaDB.Client;
using ChromaDB.Client.DependencyInjection;

services.AddChromaClient(_ => new ChromaConfigurationOptions("http://localhost:8000"));
services.AddChromaVectorStore();
```

Or the vector store creates its own client, from a URI or from client options, like the ones for Chroma Cloud:

```csharp
services.AddChromaVectorStore("http://localhost:8000");

services.AddChromaVectorStore(new ChromaConfigurationOptions("https://api.trychroma.com", tenant: "<tenant>", database: "<database>")
    .WithChromaToken("<api key>"));
```

`AddChromaCollection<TKey, TRecord>(name, …)` registers one collection in the same three ways. `AddKeyedChromaVectorStore` and `AddKeyedChromaCollection` register them under a key.

With these registrations, the embedding generator is the `EmbeddingGenerator` set in `ChromaVectorStoreOptions` or `ChromaCollectionOptions`. If none is set, it is an `IEmbeddingGenerator` registered in the container.

Without dependency injection, `ChromaVectorStore` and `ChromaCollection` also take client options and an `HttpClient`. When you pass `ownsClient: true`, they dispose that `HttpClient`.

`GetService(typeof(ChromaClient))` on the vector store or on a collection returns the client it uses.

## Chroma Cloud

Connect with the client options. The API key goes in the `X-Chroma-Token` header. Set the tenant and the database shown in the Chroma Cloud dashboard.

Chroma Cloud reads and writes at most 300 records per request. On Chroma Cloud addresses, the client reads and writes in batches of 300 by itself. A search returns at most 300 results, counting both `top` and `Skip`.

Chroma Cloud also limits metadata and documents:

- A record has at most 32 metadata keys.
- A key has at most 36 bytes.
- A value has at most 8,182 bytes.
- A document has at most 16,384 bytes.

Each property with a value is a key. With `CreateBm25Indexes`, so is the BM25 vector of a property, named after the property with `_bm25` added. In that case, the storage names of full-text indexed properties can have at most 31 bytes.

If the text of the property stored as the document is longer than 8,182 bytes, it is stored in the document only. `Contains` finds it, but `==` does not.

## Hybrid search

On Chroma Cloud, `HybridSearchAsync` searches with a vector and keywords together:

```csharp
var collection = new ChromaCollection<string, Hotel>(client, "hotels-hybrid", new() { CreateBm25Indexes = true });
await collection.EnsureCollectionExistsAsync();

var results = collection.HybridSearchAsync(new float[] { 0.1f, 0.2f, 0.3f, 0.4f }, ["pool", "spa"], top: 5);
```

It fuses the ranks of two searches: the vector search, and a BM25 search of the keywords in a full-text indexed `string` property. The BM25 search needs a BM25 index on the text of the property. A BM25 index is a Chroma sparse vector index.

With `CreateBm25Indexes`, creating the collection creates a BM25 index for each full-text indexed `string` property, like `Name` in `Hotel` above. The client then computes the BM25 vectors of the records as it writes them. `ChromaVectorStoreOptions` has the same option for the collections of a vector store.

A collection answers `IKeywordHybridSearchable` from `GetService` only when the option is set and it has a full-text indexed `string` property. The `TextSearchStore` of Semantic Kernel asks for this interface to choose hybrid search over vector search. `AddChromaCollection` registers the collection as `IKeywordHybridSearchable<TRecord>` in any case, so resolve it from the container only when the option is set.

The score of a hybrid result is the reciprocal rank fusion score (k = 60) of the two searches. Higher is better, and the score is well below 1. `ScoreThreshold` applies to it.

A collection created by another Chroma client, like the Python one, works too when it has a `chroma_bm25` index on the text of the property. For the property stored as the document, the index goes on the documents.

A record without any of the keywords gets nothing from the BM25 search, as in a keyword search.

A single Chroma server has neither the Search API nor sparse vector indexes. On a single server, creating a collection with `CreateBm25Indexes` fails.

## Supported

- Keys: `string` and `Guid`.
- One vector per record: `ReadOnlyMemory<float>`, `Embedding<float>` or `float[]`, or any type with an embedding generator.
- Data properties, stored as Chroma metadata:
  - `string`, `int`, `long`, `double`, `float`, `bool`, `DateTime`, `DateTimeOffset` and `DateOnly` (.NET 8 and later)
  - their nullable forms
  - arrays or `List<T>` of the non-nullable ones
- Dates are stored as ISO 8601 strings. A `DateTimeOffset` is stored in UTC, so it comes back as the same instant with offset zero.
- Targets .NET 10, .NET 8, .NET Standard 2.0 and .NET Framework 4.6.2. NativeAOT needs .NET 8 or later.
- Distance functions: `CosineSimilarity` (the default), `CosineDistance`, `DotProductSimilarity`, `NegativeDotProductSimilarity`, `EuclideanDistance` and `EuclideanSquaredDistance`, with the HNSW index.
- An existing collection must use the space of the distance function. A collection created by another Chroma client without a space uses l2. If the space differs, the provider throws, rather than turning the distances of another space into scores.
- Filters:
  - `==` and `!=`
  - `<`, `<=`, `>` and `>=` on numbers
  - `&&`, `||` and `!`
  - `Contains` over an inline list or an array property
  - `Any` with `Contains` over an inline list
- Filters on the key: `==`, and `Contains` over a list of keys, joined to the other conditions with `&&`. Chroma looks the records up by id.
- Full-text: the only full-text indexed `string` property is also stored as the Chroma document. That is where other Chroma clients store their text.
  - `Contains` and `!Contains` on it filter the text with `where_document`, joined to the other conditions with `&&`.
  - When a record has its text in the document only, the provider reads it into that property.
  - A null text is stored as no document. For a record that has a document, it is stored as an empty one, since Chroma keeps the old document when it gets a null one.
  - An empty document reads back as null.
- NativeAOT and trimming: the dynamic collection works without reflection. You get it from `GetDynamicCollection` with a `VectorStoreCollectionDefinition`. `ChromaCollection<TKey, TRecord>` maps the properties of the record type by reflection. `AddChromaVectorStore` and `AddKeyedChromaVectorStore` work with trimming and NativeAOT. `AddChromaCollection` and `AddKeyedChromaCollection` map a record type, so they are marked as incompatible. There, register the vector store and get the collection from it with `GetDynamicCollection`.

## Limitations

- Chroma metadata has no null values. A null property is not stored, and filtering on null is not supported.
- Upserting a record that exists replaces it, so a value that is now null, or an empty list, is deleted. To do this when a record has null values, the provider first reads which keys the record has stored, and deletes only those.
- Chroma does not store empty lists. An empty array or list is not stored, and comes back as null.
- `GetAsync` with a filter does not support ordering.
- Comparisons work on numbers only.
- Dates are strings in Chroma. `==`, `!=` and `Contains` compare a `DateTimeOffset` as an instant. They compare a `DateTime` by its ticks, whatever its kind. For a `Local` one, they use the time zone of this machine.
- A `DateTimeOffset` that a version before 0.3.5 stored with another offset is found only with that offset.
- Array properties need Chroma 1.5.0 or later.
- Hybrid search needs Chroma Cloud.
- Only one property can be the document. With more than one full-text indexed string property, none is.

In CI, the provider runs the Microsoft.Extensions.VectorData conformance tests against Chroma 1.5.0, 1.5.9 and the latest release. On Chroma Cloud, the tests are run by hand, hybrid search included, and they pass.
