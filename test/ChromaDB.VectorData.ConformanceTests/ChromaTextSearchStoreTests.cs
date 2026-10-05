// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Data;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// The <c>TextSearchStore</c> of Semantic Kernel asks the collection for hybrid search through <c>GetService</c>, and uses it when it
/// gets it: with its default options it has to keep working where hybrid search cannot, searching by vector.
/// </summary>
public sealed class ChromaTextSearchStoreTests : IAsyncLifetime
{
    private const string CollectionName = "text-search-store";
    private const int Dimensions = 64;

    private ChromaVectorStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._store = new ChromaVectorStore(
            new ChromaClient(ChromaTestStore.Instance.ChromaOptions, ChromaTestStore.Instance.HttpClient),
            new() { EmbeddingGenerator = new WordEmbeddingGenerator(Dimensions) });
        await this._store.EnsureCollectionDeletedAsync(CollectionName);
    }

    public async ValueTask DisposeAsync()
    {
        await this._store.EnsureCollectionDeletedAsync(CollectionName);
        this._store.Dispose();
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task TextSearchStore_with_the_default_options_finds_the_documents()
    {
        using var textSearchStore = new TextSearchStore<string>(this._store, CollectionName, Dimensions, new TextSearchStoreOptions { SearchNamespace = "docs" });
        await textSearchStore.UpsertDocumentsAsync(
        [
            new TextSearchDocument { Namespaces = ["docs"], SourceId = "a", SourceName = "chroma.md", Text = "Chroma is a vector database for embeddings" },
            new TextSearchDocument { Namespaces = ["docs"], SourceId = "b", SourceName = "cats.md", Text = "Cats sleep most of the day" },
            new TextSearchDocument { Namespaces = ["other"], SourceId = "c", SourceName = "other.md", Text = "Chroma vector database embeddings in another namespace" },
        ]);

        // Chroma Cloud indexes asynchronously: search until the documents appear.
        List<TextSearchResult> results = [];
        for (var attempt = 0; attempt < 50 && results.Count == 0; attempt++)
        {
            var search = await textSearchStore.GetTextSearchResultsAsync("vector database embeddings");
            results = await search.Results.ToListAsync();
            if (results.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

        Assert.Equal("chroma.md", results[0].Name);
        Assert.DoesNotContain(results, result => result.Name == "other.md");
    }

    [Fact]
    public async Task TextSearchStore_finds_no_document_that_left_the_namespace()
    {
        // The document is replaced by its source id: without the namespace, a search in the namespace no longer finds it.
        using var textSearchStore = new TextSearchStore<string>(this._store, CollectionName, Dimensions, new TextSearchStoreOptions { SearchNamespace = "docs", UseSourceIdAsPrimaryKey = true });
        await textSearchStore.UpsertDocumentsAsync([new TextSearchDocument { Namespaces = ["docs"], SourceId = "a", SourceName = "chroma.md", Text = "Chroma is a vector database for embeddings" }]);
        Assert.NotEmpty(await SearchUntilAsync(textSearchStore, results => results.Count > 0));

        await textSearchStore.UpsertDocumentsAsync([new TextSearchDocument { Namespaces = [], SourceId = "a", SourceName = "chroma.md", Text = "Chroma is a vector database for embeddings" }]);
        Assert.Empty(await SearchUntilAsync(textSearchStore, results => results.Count == 0));
    }

    // Chroma Cloud indexes asynchronously: search until the condition holds, or the attempts end.
    private static async Task<List<TextSearchResult>> SearchUntilAsync(TextSearchStore<string> textSearchStore, Func<List<TextSearchResult>, bool> condition)
    {
        List<TextSearchResult> results = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var search = await textSearchStore.GetTextSearchResultsAsync("vector database embeddings");
            results = await search.Results.ToListAsync();
            if (condition(results))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        return results;
    }

    /// <summary>
    /// Deterministic embeddings: the words hashed into a fixed number of dimensions, so texts that share words are close.
    /// </summary>
    private sealed class WordEmbeddingGenerator(int dimensions) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(this.Embed)));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private Embedding<float> Embed(string text)
        {
            var vector = new float[dimensions];
            foreach (var word in text.ToLowerInvariant().Split([' ', ',', '.', '?', '!']).Where(word => word.Length > 2))
            {
                using var sha = SHA256.Create();
                vector[BitConverter.ToUInt32(sha.ComputeHash(Encoding.UTF8.GetBytes(word)), 0) % dimensions] += 1;
            }

            var norm = (float)Math.Sqrt(vector.Sum(value => value * value));
            return new(norm == 0 ? vector : vector.Select(value => value / norm).ToArray());
        }
    }
}
