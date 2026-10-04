// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChromaDB.Client;
using ChromaDB.Client.Models;
using Microsoft.Extensions.VectorData;
using Moq;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaCollection{TKey, TRecord}"/> class.
/// </summary>
public class ChromaCollectionTests
{
    private const string TestCollectionName = "testcollection";

    private static readonly Guid s_guidTestRecordKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<MockableChromaClient> _chromaClientMock = new(MockBehavior.Strict);
    private readonly ChromaCollection _chromaCollection = new(TestCollectionName) { Id = Guid.NewGuid() };
    private readonly CancellationToken _testCancellationToken = new(false);

    public ChromaCollectionTests()
    {
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync(TestCollectionName, this._testCancellationToken))
            .ReturnsAsync(this._chromaCollection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CollectionExistsReturnsCollectionStateAsync(bool expectedExists)
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.CollectionExistsAsync(TestCollectionName, this._testCancellationToken))
            .ReturnsAsync(expectedExists);

        // Act and assert.
        Assert.Equal(expectedExists, await sut.CollectionExistsAsync(this._testCancellationToken));
    }

    [Fact]
    public async Task EnsureCollectionExistsCreatesTheCollectionWithTheDistanceAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, DotProductHotel>();
        Dictionary<string, object>? metadata = null;
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(TestCollectionName, It.IsAny<Dictionary<string, object>?>(), this._testCancellationToken))
            .Callback<string, Dictionary<string, object>?, CancellationToken>((_, m, _) => metadata = m)
            .ReturnsAsync(this._chromaCollection);

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        Assert.Equal("ip", metadata!["hnsw:space"]);
    }

    [Fact]
    public async Task EnsureCollectionExistsThrowsForAFullTextIndexedPropertyAsync()
    {
        using var sut = this.CreateCollection<string, FullTextHotel>();

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.EnsureCollectionExistsAsync(this._testCancellationToken));
    }

    [Fact]
    public async Task UpsertSendsIdsEmbeddingsAndMetadataAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<Guid, Hotel<Guid>>();
        List<string>? ids = null;
        List<ReadOnlyMemory<float>>? embeddings = null;
        List<Dictionary<string, object>>? metadatas = null;
        this._chromaClientMock
            .Setup(x => x.UpsertAsync(this._chromaCollection, It.IsAny<List<string>>(), It.IsAny<List<ReadOnlyMemory<float>>>(), It.IsAny<List<Dictionary<string, object>>?>(), this._testCancellationToken))
            .Callback<ChromaCollection, List<string>, List<ReadOnlyMemory<float>>, List<Dictionary<string, object>>?, CancellationToken>((_, i, e, m, _) => (ids, embeddings, metadatas) = (i, e, m))
            .Returns(Task.CompletedTask);

        // Act.
        await sut.UpsertAsync(new Hotel<Guid> { HotelId = s_guidTestRecordKey, HotelName = "Grand", Embedding = new float[] { 1, 2, 3, 4 } }, this._testCancellationToken);

        // Assert.
        Assert.Equal(["11111111-1111-1111-1111-111111111111"], ids);
        Assert.Equal(new float[] { 1, 2, 3, 4 }, Assert.Single(embeddings!).ToArray());
        Assert.Equal("Grand", Assert.Single(metadatas!)["HotelName"]);
    }

    [Fact]
    public async Task GetReadsTheRecordsByIdAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.GetAsync(this._chromaCollection, new List<string> { "h1" }, null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionEntry("h1") { Metadata = new() { ["HotelName"] = "Grand" } }]);

        // Act.
        var hotel = await sut.GetAsync("h1", cancellationToken: this._testCancellationToken);

        // Assert.
        Assert.Equal("h1", hotel!.HotelId);
        Assert.Equal("Grand", hotel.HotelName);
    }

    [Fact]
    public async Task DeleteDeletesTheRecordsByIdAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.DeleteAsync(this._chromaCollection, new List<string> { "h1", "h2" }, this._testCancellationToken))
            .Returns(Task.CompletedTask);

        // Act.
        await sut.DeleteAsync(["h1", "h2"], this._testCancellationToken);

        // Assert.
        this._chromaClientMock.Verify(x => x.DeleteAsync(this._chromaCollection, new List<string> { "h1", "h2" }, this._testCancellationToken), Times.Once);
    }

    [Fact]
    public async Task SearchSkipsConvertsAndFiltersTheResultsAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.QueryAsync(this._chromaCollection, It.IsAny<ReadOnlyMemory<float>>(), 3, null, ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances, this._testCancellationToken))
            .ReturnsAsync(
            [
                new ChromaCollectionQueryEntry("skipped") { Distance = 0.1f },
                new ChromaCollectionQueryEntry("kept") { Distance = 0.2f },
                new ChromaCollectionQueryEntry("too far") { Distance = 0.6f },
            ]);

        // Act.
        var results = await sut.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), top: 2, new() { Skip = 1, ScoreThreshold = 0.5 }, this._testCancellationToken).ToListAsync();

        // Assert.
        var result = Assert.Single(results);
        Assert.Equal("kept", result.Record.HotelId);
        Assert.Equal(0.8, result.Score!.Value, precision: 6);
    }

    [Fact]
    public async Task GetWithOrderByThrowsAsync()
    {
        using var sut = this.CreateCollection<string, Hotel<string>>();

        await Assert.ThrowsAsync<NotSupportedException>(() => sut
            .GetAsync(h => h.Parking, top: 5, new() { OrderBy = o => o.Ascending(h => h.Price) }, this._testCancellationToken)
            .ToListAsync()
            .AsTask());
    }

    [Fact]
    public async Task WrapsChromaExceptionsInVectorStoreExceptionsAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.CollectionExistsAsync(TestCollectionName, this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Unexpected status code"));

        // Act.
        var exception = await Assert.ThrowsAsync<VectorStoreException>(() => sut.CollectionExistsAsync(this._testCancellationToken));

        // Assert.
        Assert.Equal("chroma", exception.VectorStoreSystemName);
        Assert.IsType<ChromaException>(exception.InnerException);
    }

    [Fact]
    public void RejectsUnsupportedKeyTypes()
        => Assert.Throws<NotSupportedException>(() => new ChromaCollection<int, Hotel<int>>(() => this._chromaClientMock.Object, TestCollectionName));

#pragma warning disable IL2026, IL3050 // The test models are not trimmed
    private ChromaCollection<TKey, TRecord> CreateCollection<TKey, TRecord>()
        where TKey : notnull
        where TRecord : class
        => new(() => this._chromaClientMock.Object, TestCollectionName);
#pragma warning restore IL2026, IL3050
}
