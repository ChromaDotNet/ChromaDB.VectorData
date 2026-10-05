// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.VectorData.ConformanceTests.Support;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

/// <summary>
/// With <c>WithBatchSplitting(maxBatchSize)</c> the reads go in pages of that size too, as Chroma Cloud returns at most
/// 300 records per request.
/// </summary>
public sealed class ChromaPagedReadTests : IAsyncLifetime
{
    private readonly CountingHandler _handler = new();
    private HttpClient _httpClient = null!;
    private ChromaVectorStore _store = null!;
    private VectorStoreCollection<string, Record> _collection = null!;

    public async ValueTask InitializeAsync()
    {
        await ChromaTestStore.Instance.ReferenceCountingStartAsync();

        this._httpClient = new HttpClient(this._handler);
        this._store = new ChromaVectorStore(new ChromaClient(ChromaTestStore.Instance.ChromaOptions.WithBatchSplitting(maxBatchSize: 2), this._httpClient));
        this._collection = this._store.GetCollection<string, Record>("paged-read");
        await this._collection.EnsureCollectionDeletedAsync();
        await this._collection.EnsureCollectionExistsAsync();
        await this._collection.UpsertAsync(Enumerable.Range(0, 5).Select(i => new Record { Key = $"r{i}", Rating = i, Vector = new float[] { 1, i } }));
    }

    public async ValueTask DisposeAsync()
    {
        await this._collection.EnsureCollectionDeletedAsync();
        this._collection.Dispose();
        this._store.Dispose();
        this._httpClient.Dispose();
        await ChromaTestStore.Instance.ReferenceCountingStopAsync();
    }

    [Fact]
    public async Task GetAsync_with_keys_reads_in_pages()
    {
        this._handler.GetRequests = 0;

        var records = await this._collection.GetAsync(["r0", "r1", "r2", "r3", "r4"]).ToListAsync();

        Assert.Equal(5, records.Count);
        Assert.Equal(3, this._handler.GetRequests);
    }

    [Fact]
    public async Task GetAsync_with_filter_reads_in_pages()
    {
        this._handler.GetRequests = 0;

        var records = await this._collection.GetAsync(r => r.Rating >= 0, top: 5).ToListAsync();

        Assert.Equal(["r0", "r1", "r2", "r3", "r4"], records.Select(r => r.Key).OrderBy(k => k));
        Assert.Equal(3, this._handler.GetRequests);
    }

    [Fact]
    public async Task GetAsync_with_filter_and_skip_stops_at_top()
    {
        this._handler.GetRequests = 0;

        var records = await this._collection.GetAsync(r => r.Rating >= 0, top: 3, new() { Skip = 1 }).ToListAsync();

        Assert.Equal(3, records.Count);
        Assert.Equal(2, this._handler.GetRequests);
    }

    // With the default store of the tests: Chroma Cloud refuses more than 300 records per request, and on its addresses the
    // client reads and writes in batches by itself.
    [Fact]
    public async Task More_records_than_a_page_of_Chroma_Cloud()
    {
        using var collection = ChromaTestStore.Instance.DefaultVectorStore.GetCollection<string, Record>("paged-read-301");
        await collection.EnsureCollectionDeletedAsync();
        await collection.EnsureCollectionExistsAsync();

        try
        {
            var keys = Enumerable.Range(0, 301).Select(i => $"r{i}").ToList();
            await collection.UpsertAsync(keys.Select((key, i) => new Record { Key = key, Rating = i, Vector = new float[] { 1, i } }));

            Assert.Equal(301, (await collection.GetAsync(keys).ToListAsync()).Count);
            Assert.Equal(301, (await collection.GetAsync(r => r.Rating >= 0, top: 301).ToListAsync()).Count);
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
        public int Rating { get; set; }

        [VectorStoreVector(2)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    private sealed class CountingHandler() : DelegatingHandler(new HttpClientHandler())
    {
        public int GetRequests { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/get", StringComparison.Ordinal))
            {
                this.GetRequests++;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
