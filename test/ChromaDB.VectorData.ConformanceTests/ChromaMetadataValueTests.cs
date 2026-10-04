// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// Chroma metadata has no date type: dates are stored as strings, so the values must come back exactly as written.
/// </summary>
public sealed class ChromaMetadataValueTests : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
        => await ChromaTestStore.Instance.ReferenceCountingStartAsync();

    public async ValueTask DisposeAsync()
        => await ChromaTestStore.Instance.ReferenceCountingStopAsync();

    [Fact]
    public async Task String_that_looks_like_a_date_and_DateTimeOffset_round_trip()
    {
        var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>("metadata-values");
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();

        try
        {
            var record = new Record
            {
                Key = "a",
                Text = "2026-10-04",
                Opened = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.FromHours(2)),
                Vector = new float[] { 1, 2, 3 }
            };

            await collection.UpsertAsync(record);
            var read = await collection.GetAsync("a");

            Assert.NotNull(read);
            Assert.Equal("2026-10-04", read.Text);
            Assert.Equal(record.Opened, read.Opened);
            Assert.Equal(TimeSpan.FromHours(2), read.Opened.Offset);
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    [Fact]
    public async Task Empty_list_comes_back_as_null()
    {
        var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>("metadata-empty-list");
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();

        try
        {
            await collection.UpsertAsync(new Record { Key = "a", Tags = [], Vector = new float[] { 1, 2, 3 } });
            var read = await collection.GetAsync("a");

            Assert.NotNull(read);
            Assert.Null(read.Tags);
        }
        finally
        {
            await collection.EnsureCollectionDeletedAsync();
        }
    }

    public sealed class Record
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreData]
        public string Text { get; set; } = "";

        [VectorStoreData]
        public DateTimeOffset Opened { get; set; }

        [VectorStoreData]
        public List<string>? Tags { get; set; }

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
