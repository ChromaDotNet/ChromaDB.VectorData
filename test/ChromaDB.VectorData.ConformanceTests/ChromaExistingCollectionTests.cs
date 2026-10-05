// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// A collection created by another tool keeps its space, like the l2 that Chroma uses by default.
/// </summary>
public sealed class ChromaExistingCollectionTests : IAsyncLifetime
{
    private const string CollectionName = "existing-l2";

    private ChromaClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._client = new ChromaClient(ChromaTestStore.Instance.ChromaOptions, ChromaTestStore.Instance.HttpClient);
        if (await this._client.CollectionExistsAsync(CollectionName))
        {
            await this._client.DeleteCollectionAsync(CollectionName);
        }

        // Created without a space, as Chroma clients without a distance setting do.
        var collection = await this._client.CreateCollectionAsync(CollectionName);
        await this._client.GetCollectionClient(collection).AddAsync(["a"], [new float[] { 3, 4 }]);
    }

    public async ValueTask DisposeAsync()
    {
        await this._client.DeleteCollectionAsync(CollectionName);
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task A_collection_with_another_space_throws()
    {
        using var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, CosineRecord>(CollectionName);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await collection.SearchAsync(new float[] { 0, 0 }, top: 1).ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => collection.EnsureCollectionExistsAsync());
    }

    [Fact]
    public async Task A_collection_with_the_same_space_returns_its_scores()
    {
        using var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, EuclideanRecord>(CollectionName);

        var result = await collection.SearchAsync(new float[] { 0, 0 }, top: 1).SingleAsync();

        Assert.Equal("a", result.Record.Key);
        Assert.Equal(5, result.Score!.Value, precision: 4);
    }

    public sealed class CosineRecord
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreVector(2)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    public sealed class EuclideanRecord
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreVector(2, DistanceFunction = DistanceFunction.EuclideanDistance)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
