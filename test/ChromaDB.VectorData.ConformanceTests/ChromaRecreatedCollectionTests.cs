// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// A collection deleted and created again through another instance gets a new Chroma id.
/// </summary>
public sealed class ChromaRecreatedCollectionTests : IAsyncLifetime
{
    private const string CollectionName = "recreated";

    public async ValueTask InitializeAsync()
        => await ChromaTestStore.Instance.ReferenceCountingStartAsync();

    public async ValueTask DisposeAsync()
    {
        await ChromaTestStore.Instance.DefaultVectorStore.EnsureCollectionDeletedAsync(CollectionName);
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task A_collection_finds_its_records_after_it_is_created_again_elsewhere()
    {
        var store = ChromaTestStore.Instance.DefaultVectorStore;
        using var collection = store.GetCollection<string, Record>(CollectionName);
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        await collection.UpsertAsync(new Record { Key = "old", Vector = new float[] { 1, 0 } });

        // Another instance deletes the collection and creates it again.
        await store.EnsureCollectionDeletedAsync(CollectionName);
        using (var other = store.GetCollection<string, Record>(CollectionName))
        {
            await other.EnsureCollectionExistsAsync();
            await other.UpsertAsync(new Record { Key = "new", Vector = new float[] { 0, 1 } });
        }

        var record = await collection.GetAsync("new");

        Assert.Equal("new", record?.Key);
    }

    public sealed class Record
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreVector(2)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
