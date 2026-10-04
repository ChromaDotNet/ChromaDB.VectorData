// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// The key of a record is its Chroma id, not a metadata field, so filters on it go to the ids of the request.
/// </summary>
public sealed class ChromaKeyFilterTests : IAsyncLifetime
{
    private VectorStoreCollection<string, Record> _collection = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>("key-filter");
        await this._collection.EnsureCollectionDeletedAsync();
        await this._collection.EnsureCollectionExistsAsync();
        await this._collection.UpsertAsync(
        [
            new Record { Key = "a", Rating = 5, Vector = new float[] { 1, 0, 0 } },
            new Record { Key = "b", Rating = 2, Vector = new float[] { 0, 1, 0 } },
            new Record { Key = "c", Rating = 4, Vector = new float[] { 0, 0, 1 } },
        ]);
    }

    public async ValueTask DisposeAsync()
    {
        await this._collection.EnsureCollectionDeletedAsync();
        this._collection.Dispose();
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task GetAsync_with_key_equality()
    {
        var records = await this._collection.GetAsync(r => r.Key == "b", top: 10).ToListAsync();

        Assert.Equal("b", Assert.Single(records).Key);
    }

    [Fact]
    public async Task GetAsync_with_keys_and_another_condition()
    {
        var keys = new List<string> { "a", "b" };

        var records = await this._collection.GetAsync(r => keys.Contains(r.Key) && r.Rating >= 4, top: 10).ToListAsync();

        Assert.Equal("a", Assert.Single(records).Key);
    }

    [Fact]
    public async Task SearchAsync_with_keys()
    {
        var results = await this._collection.SearchAsync(new float[] { 1, 0, 0 }, top: 3, new() { Filter = r => new[] { "b", "c" }.Contains(r.Key) }).ToListAsync();

        Assert.Equal(["b", "c"], results.Select(r => r.Record.Key).OrderBy(k => k));
    }

    [Fact]
    public async Task Contains_over_an_empty_list_returns_no_record()
    {
        var records = await this._collection.GetAsync(r => new int[0].Contains(r.Rating), top: 10).ToListAsync();
        var results = await this._collection.SearchAsync(new float[] { 1, 0, 0 }, top: 3, new() { Filter = r => new int[0].Contains(r.Rating) || r.Rating == 2 }).ToListAsync();

        Assert.Empty(records);
        Assert.Equal("b", Assert.Single(results).Record.Key);
    }

    [Fact]
    public async Task Key_inequality_throws()
        => await Assert.ThrowsAsync<NotSupportedException>(async () => await this._collection.GetAsync(r => r.Key != "a", top: 10).ToListAsync());

    public sealed class Record
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreData]
        public int Rating { get; set; }

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
