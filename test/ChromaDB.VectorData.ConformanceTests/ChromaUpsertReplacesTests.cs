// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// An upsert of an existing record merges its metadata in Chroma: the provider deletes the values that are now null or empty,
/// so the record is replaced, also for the filters.
/// </summary>
public sealed class ChromaUpsertReplacesTests : IAsyncLifetime
{
    private const string CollectionName = "upsert-replaces";

    private VectorStoreCollection<string, Item> _collection = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Item>(CollectionName);
        await this._collection.EnsureCollectionDeletedAsync();
        await this._collection.EnsureCollectionExistsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await this._collection.EnsureCollectionDeletedAsync();
        this._collection.Dispose();
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task UpsertAsync_with_null_values_replaces_the_old_ones()
    {
        await this._collection.UpsertAsync(new Item { Key = "u", Category = "A", Rating = 5, Tags = ["red"], Text = "first text", Vector = new float[] { 1, 0 } });
        await this._collection.UpsertAsync(new Item { Key = "u", Category = null, Rating = null, Tags = null, Text = null, Vector = new float[] { 0, 1 } });

        var item = await this._collection.GetAsync("u");
        Assert.NotNull(item);
        Assert.Null(item.Category);
        Assert.Null(item.Rating);
        Assert.Null(item.Tags);
        Assert.Null(item.Text);

        Assert.Empty(await this._collection.GetAsync(i => i.Category == "A", top: 10).ToListAsync());
        Assert.Empty(await this._collection.GetAsync(i => i.Rating == 5, top: 10).ToListAsync());
        Assert.Empty(await this._collection.GetAsync(i => i.Tags!.Contains("red"), top: 10).ToListAsync());
        Assert.Empty(await this._collection.GetAsync(i => i.Text!.Contains("first"), top: 10).ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_with_an_empty_list_removes_the_old_elements()
    {
        await this._collection.UpsertAsync(new Item { Key = "u", Tags = ["red", "blue"], Vector = new float[] { 1, 0 } });
        await this._collection.UpsertAsync(new Item { Key = "u", Tags = [], Vector = new float[] { 1, 0 } });

        Assert.Null((await this._collection.GetAsync("u"))!.Tags);
        Assert.Empty(await this._collection.GetAsync(i => i.Tags!.Contains("red"), top: 10).ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_with_new_values_replaces_the_old_ones()
    {
        await this._collection.UpsertAsync(new Item { Key = "u", Category = "A", Tags = ["red"], Text = "first text", Vector = new float[] { 1, 0 } });
        await this._collection.UpsertAsync(new Item { Key = "u", Category = "B", Tags = ["blue"], Text = "second text", Vector = new float[] { 1, 0 } });

        var item = await this._collection.GetAsync("u");
        Assert.Equal("B", item!.Category);
        Assert.Equal(["blue"], item.Tags);
        Assert.Equal("second text", item.Text);
        Assert.Empty(await this._collection.GetAsync(i => i.Text!.Contains("first"), top: 10).ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_with_a_null_text_deletes_its_BM25_vector()
    {
        // Only Chroma Cloud has BM25 indexes: the vector of the old text would keep matching its words in hybrid search.
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, "BM25 indexes need Chroma Cloud.");

        using var collection = ChromaTestStore.Instance.CreateCollectionWithBm25Indexes<Item>(CollectionName + "-bm25", new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(Item.Key), typeof(string)),
                new VectorStoreDataProperty(nameof(Item.Text), typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreVectorProperty(nameof(Item.Vector), typeof(ReadOnlyMemory<float>), 2),
            ]
        });
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        try
        {
            await collection.UpsertAsync(new Item { Key = "u", Text = "pool and spa", Vector = new float[] { 1, 0 } });
            Assert.True(await HasMetadataKeyAsync("Text_bm25"));

            await collection.UpsertAsync(new Item { Key = "u", Text = null, Vector = new float[] { 1, 0 } });
            Assert.False(await HasMetadataKeyAsync("Text_bm25"));
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }

        async Task<bool> HasMetadataKeyAsync(string key)
        {
            var client = new ChromaClient(ChromaTestStore.Instance.ChromaOptions, ChromaTestStore.Instance.HttpClient);
            var chromaCollection = await client.GetCollectionAsync(CollectionName + "-bm25");
            var entry = await client.GetCollectionClient(chromaCollection).GetAsync("u", include: ChromaGetInclude.Metadatas);
            return entry?.Metadata?.ContainsKey(key) is true;
        }
    }

    [Fact]
    public async Task Many_properties_with_one_value_fit_the_limits_of_Chroma_Cloud()
    {
        // Chroma Cloud takes at most 32 metadata keys per record, and counts the null ones: a new record gets no null, and a
        // record that exists gets one only for the keys it has.
        var definition = new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(string)),
                .. Enumerable.Range(0, 33).Select(i => new VectorStoreDataProperty($"P{i:00}", typeof(string))),
                new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 2),
            ]
        };
        using var collection = ChromaTestStore.Instance.DefaultVectorStore.GetDynamicCollection(CollectionName + "-many", definition);
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        try
        {
            await collection.UpsertAsync(new Dictionary<string, object?> { ["Key"] = "u", ["P00"] = "first", ["Vector"] = new ReadOnlyMemory<float>([1, 0]) });
            await collection.UpsertAsync(new Dictionary<string, object?> { ["Key"] = "u", ["P01"] = "second", ["Vector"] = new ReadOnlyMemory<float>([1, 0]) });

            var item = await collection.GetAsync("u");
            Assert.NotNull(item);
            Assert.Null(item["P00"]);
            Assert.Equal("second", item["P01"]);
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    [Fact]
    public async Task A_null_full_text_property_sends_no_key_for_its_BM25_vector()
    {
        // On Chroma Cloud a metadata key has at most 36 bytes: the BM25 vector of a property with a long name never fits,
        // and a null value sends nothing for it.
        Assert.SkipUnless(ChromaTestStore.IsChromaCloud, "BM25 indexes need Chroma Cloud.");

        using var collection = ChromaTestStore.Instance.CreateCollectionWithBm25Indexes<LongNameItem>(CollectionName + "-long-name", new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(LongNameItem.Key), typeof(string)),
                new VectorStoreDataProperty(nameof(LongNameItem.Text), typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreDataProperty(nameof(LongNameItem.ProductDescriptionInMarkdownText), typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreVectorProperty(nameof(LongNameItem.Vector), typeof(ReadOnlyMemory<float>), 2),
            ]
        });
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        try
        {
            await collection.UpsertAsync(new LongNameItem { Key = "u", Text = "pool and spa", Vector = new float[] { 1, 0 } });
            await collection.UpsertAsync(new LongNameItem { Key = "u", Text = "gym", Vector = new float[] { 1, 0 } });

            Assert.Equal("gym", (await collection.GetAsync("u"))?.Text);
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    public sealed class LongNameItem
    {
        public string Key { get; set; } = "";
        public string? Text { get; set; }
        public string? ProductDescriptionInMarkdownText { get; set; }
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    public sealed class Item
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreData(IsIndexed = true)]
        public string? Category { get; set; }

        [VectorStoreData(IsIndexed = true)]
        public int? Rating { get; set; }

        [VectorStoreData(IsIndexed = true)]
        public List<string>? Tags { get; set; }

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Text { get; set; }

        [VectorStoreVector(2)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
