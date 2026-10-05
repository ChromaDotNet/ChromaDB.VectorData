// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// Chroma metadata has no date type: dates are stored as strings, so the values must come back as written, and filters must
/// compare them as == does in C#. Numbers and long texts have their own rules in Chroma.
/// </summary>
public sealed class ChromaMetadataValueTests : IAsyncLifetime
{
    private const string CollectionName = "metadata-values";

    private VectorStoreCollection<string, Record> _collection = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>(CollectionName);
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
    public async Task String_that_looks_like_a_date_and_DateTimeOffset_round_trip()
    {
        var record = new Record
        {
            Key = "a",
            Text = "2026-10-04",
            Opened = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.FromHours(2)),
            Vector = new float[] { 1, 2, 3 }
        };

        await this._collection.UpsertAsync(record);
        var read = await this._collection.GetAsync("a");

        // A DateTimeOffset is stored in UTC: the same instant comes back, with offset zero.
        Assert.NotNull(read);
        Assert.Equal("2026-10-04", read.Text);
        Assert.Equal(record.Opened, read.Opened);
        Assert.Equal(TimeSpan.Zero, read.Opened.Offset);
    }

    [Fact]
    public async Task Empty_list_comes_back_as_null()
    {
        await this._collection.UpsertAsync(new Record { Key = "a", Tags = [], Vector = new float[] { 1, 2, 3 } });
        var read = await this._collection.GetAsync("a");

        Assert.NotNull(read);
        Assert.Null(read.Tags);
    }

    [Fact]
    public async Task Equality_on_a_DateTimeOffset_compares_instants()
    {
        var opened = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.FromHours(2));
        await this._collection.UpsertAsync(new Record { Key = "a", Opened = opened, Vector = new float[] { 1, 2, 3 } });

        var sameInstantInUtc = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);
        Assert.Equal("a", Assert.Single(await this._collection.GetAsync(r => r.Opened == sameInstantInUtc, top: 10).ToListAsync()).Key);
        Assert.Equal("a", Assert.Single(await this._collection.GetAsync(r => r.Opened == opened, top: 10).ToListAsync()).Key);
        Assert.Empty(await this._collection.GetAsync(r => r.Opened != sameInstantInUtc, top: 10).ToListAsync());
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Equality_on_a_DateTime_compares_ticks_whatever_the_kind(DateTimeKind filterKind)
    {
        await this._collection.UpsertAsync(new Record { Key = "a", Updated = new DateTime(2026, 10, 5, 11, 0, 0, DateTimeKind.Utc), Vector = new float[] { 1, 2, 3 } });

        var sameTicks = new DateTime(2026, 10, 5, 11, 0, 0, filterKind);
        Assert.Equal("a", Assert.Single(await this._collection.GetAsync(r => r.Updated == sameTicks, top: 10).ToListAsync()).Key);
    }

    [Fact]
    public async Task A_double_with_an_integer_value_compares_as_a_double()
    {
        // ChromaDotNet.Client writes 2.0 with its decimal part: written as 2, Chroma would store an integer, which $lt 2.2 and
        // $contains 1.0 do not find.
        await this._collection.UpsertAsync(new Record { Key = "a", D = 2.0, Ds = [1.0, 2.5], Vector = new float[] { 1, 2, 3 } });

        Assert.Equal("a", Assert.Single(await this._collection.GetAsync(r => r.D < 2.2, top: 10).ToListAsync()).Key);
        Assert.Equal("a", Assert.Single(await this._collection.GetAsync(r => r.Ds!.Contains(1.0), top: 10).ToListAsync()).Key);
    }

    [Fact]
    public async Task A_full_text_longer_than_a_metadata_value_stays_in_the_document()
    {
        // Chroma Cloud takes at most 8,182 bytes per metadata value, and 16,384 per document.
        using var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, LongTextRecord>(CollectionName + "-long-text");
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();
        try
        {
            var text = string.Concat(Enumerable.Repeat("A page of Markdown. ", 500)) + "needle";
            await collection.UpsertAsync(new LongTextRecord { Key = "a", Text = text, Vector = new float[] { 1, 2, 3 } });

            Assert.Equal(text, (await collection.GetAsync("a"))?.Text);
            Assert.Equal("a", Assert.Single(await collection.GetAsync(r => r.Text!.Contains("needle"), top: 10).ToListAsync()).Key);
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
        public DateTime Updated { get; set; }

        [VectorStoreData]
        public double D { get; set; }

        [VectorStoreData]
        public List<double>? Ds { get; set; }

        [VectorStoreData]
        public List<string>? Tags { get; set; }

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    public sealed class LongTextRecord
    {
        [VectorStoreKey]
        public string Key { get; set; } = "";

        [VectorStoreData(IsFullTextIndexed = true)]
        public string? Text { get; set; }

        [VectorStoreVector(3)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
