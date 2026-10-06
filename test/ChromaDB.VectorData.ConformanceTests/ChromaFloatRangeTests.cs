// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// An embedding beyond the range of a float gives Chroma numbers it cannot write, which it sends as null: since
/// ChromaDotNet.Client 2.9.2 they read as NaN, and the other records of the answer are not lost.
/// </summary>
public sealed class ChromaFloatRangeTests : IAsyncLifetime
{
    private const string CollectionName = "float-range";

    public async ValueTask InitializeAsync() => await ChromaTestStore.Instance.ReferenceCountingStartAsync();

    public async ValueTask DisposeAsync() => await ChromaTestStore.Instance.ReferenceCountingStopAsync();

    [Fact]
    public async Task Search_returns_the_records_beside_one_whose_distance_is_beyond_the_range_of_a_float()
    {
        using var collection = await CreateCollectionAsync(DistanceFunction.EuclideanDistance);
        try
        {
            var results = await collection.SearchAsync(new ReadOnlyMemory<float>([1, 0, 0]), top: 5).ToListAsync();

            Assert.Equal(["near", "far", "big"], results.Select(r => r.Record.Key));
            Assert.Equal(0, results[0].Score);
            Assert.Equal(Math.Sqrt(2), results[1].Score!.Value, 5);
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    [Fact]
    public async Task Get_with_vectors_returns_a_record_whose_normalized_embedding_is_beyond_the_range_of_a_float()
    {
        // In a cosine collection Chroma normalizes the embedding, and the norm of this one is beyond the range of a float.
        using var collection = await CreateCollectionAsync(DistanceFunction.CosineSimilarity);
        try
        {
            var records = await collection.GetAsync(["near", "big"], new() { IncludeVectors = true }).ToListAsync();

            Assert.Equal(["big", "near"], records.Select(r => r.Key).OrderBy(key => key));
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    private static async Task<VectorStoreCollection<string, Item>> CreateCollectionAsync(string distanceFunction)
    {
        var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Item>(CollectionName, new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(Item.Key), typeof(string)),
                new VectorStoreVectorProperty(nameof(Item.Vector), typeof(ReadOnlyMemory<float>), 3) { DistanceFunction = distanceFunction },
            ]
        });
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        await collection.UpsertAsync(
        [
            new Item { Key = "near", Vector = new float[] { 1, 0, 0 } },
            new Item { Key = "far", Vector = new float[] { 0, 1, 0 } },
            new Item { Key = "big", Vector = new float[] { 3e38f, 3e38f, 3e38f } },
        ]);
        return collection;
    }

    public sealed class Item
    {
        public string Key { get; set; } = "";
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
