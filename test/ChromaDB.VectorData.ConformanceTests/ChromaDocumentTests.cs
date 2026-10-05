// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// The single full-text indexed property is also stored as the Chroma document: Chroma searches its text, and other
/// Chroma clients store their text there.
/// </summary>
public sealed class ChromaDocumentTests : IAsyncLifetime
{
    private const string CollectionName = "documents";

    private ChromaClient _client = null!;
    private VectorStoreCollection<string, Record> _collection = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._client = new ChromaClient(ChromaTestStore.Instance.ChromaOptions, ChromaTestStore.Instance.HttpClient);
        this._collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>(CollectionName);
        await this._collection.EnsureCollectionDeletedAsync();
        await this._collection.EnsureCollectionExistsAsync();
        await this._collection.UpsertAsync(
        [
            new Record { Key = "a", Text = "A hotel with a pool and a spa", Rating = 5, Vector = new float[] { 1, 0, 0 } },
            new Record { Key = "b", Text = "A hotel with a gym", Rating = 4, Vector = new float[] { 0, 1, 0 } },
            new Record { Key = "c", Text = "A pool by the sea", Rating = 2, Vector = new float[] { 0, 0, 1 } },
        ]);
    }

    public async ValueTask DisposeAsync()
    {
        await this._collection.EnsureCollectionDeletedAsync();
        this._collection.Dispose();
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task The_full_text_property_is_stored_as_the_document()
    {
        var collection = await this._client.GetCollectionAsync(CollectionName);
        var entry = await this._client.GetCollectionClient(collection).GetAsync("a", include: ChromaGetInclude.Documents);

        Assert.Equal("A hotel with a pool and a spa", entry?.Document);
    }

    [Fact]
    public async Task GetAsync_with_Contains_on_the_text_and_another_condition()
    {
        var records = await this._collection.GetAsync(r => r.Text!.Contains("pool") && r.Rating >= 4, top: 10).ToListAsync();

        Assert.Equal("a", Assert.Single(records).Key);
    }

    [Fact]
    public async Task SearchAsync_with_a_negated_Contains_on_the_text()
    {
        var results = await this._collection.SearchAsync(new float[] { 1, 0, 0 }, top: 3, new() { Filter = r => !r.Text!.Contains("pool") }).ToListAsync();

        Assert.Equal("b", Assert.Single(results).Record.Key);
    }

    [Fact]
    public async Task A_record_written_by_another_client_reads_its_text_from_the_document()
    {
        var collection = await this._client.GetCollectionAsync(CollectionName);
        await this._client.GetCollectionClient(collection).UpsertAsync(["d"], [new float[] { 1, 1, 0 }], documents: ["Written by another Chroma client"]);

        var record = await this._collection.GetAsync("d");

        Assert.Equal("Written by another Chroma client", record?.Text);
    }

    [Fact]
    public async Task A_text_condition_inside_an_or_throws()
        => await Assert.ThrowsAsync<NotSupportedException>(async () => await this._collection.GetAsync(r => r.Text!.Contains("pool") || r.Rating >= 4, top: 10).ToListAsync());

    public sealed class Record
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Text { get; set; }

        [VectorStoreData]
        public int Rating { get; set; }

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
