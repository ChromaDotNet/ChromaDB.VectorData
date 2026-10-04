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
        ChromaCollectionDefinition? definition = null;
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken))
            .Callback<ChromaCollectionDefinition, CancellationToken>((d, _) => definition = d)
            .ReturnsAsync(this._chromaCollection);

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        Assert.Equal(TestCollectionName, definition!.Name);
        Assert.Equal(ChromaSpace.InnerProduct, definition.Configuration?.Space);
    }

    [Fact]
    public async Task EnsureCollectionExistsAcceptsAFullTextIndexedPropertyAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, FullTextHotel>();
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken))
            .ReturnsAsync(this._chromaCollection);

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        this._chromaClientMock.Verify(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken), Times.Once);
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
            .Setup(x => x.QueryAsync(this._chromaCollection, It.IsAny<ReadOnlyMemory<float>>(), 3, null, null, ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances, this._testCancellationToken))
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
    public async Task SearchSendsTheKeysOfTheFilterAsIdsAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.QueryAsync(
                this._chromaCollection,
                It.IsAny<ReadOnlyMemory<float>>(),
                2,
                null,
                It.Is<List<string>>(ids => ids.SequenceEqual(new[] { "h1", "h2" })),
                ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances,
                this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionQueryEntry("h1") { Distance = 0.1f }]);

        // Act.
        var results = await sut.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), top: 2, new() { Filter = h => new[] { "h1", "h2" }.Contains(h.HotelId) }, this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Equal("h1", Assert.Single(results).Record.HotelId);
    }

    [Fact]
    public async Task GetSendsTheKeysOfTheFilterAsIdsAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.GetAsync(
                this._chromaCollection,
                It.Is<List<string>>(ids => ids.SequenceEqual(new[] { "h1" })),
                It.IsNotNull<ChromaWhereOperator>(),
                5,
                0,
                ChromaGetInclude.Metadatas,
                this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionEntry("h1")]);

        // Act.
        var results = await sut.GetAsync(h => h.HotelId == "h1" && h.Parking, top: 5, cancellationToken: this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Equal("h1", Assert.Single(results).HotelId);
    }

    [Fact]
    public async Task SearchWithAFilterThatMatchesNoRecordSendsNoRequestAsync()
    {
        // Arrange: the strict mock fails on any request that was not set up.
        using var sut = this.CreateCollection<string, Hotel<string>>();

        // Act.
        var results = await sut.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), top: 2, new() { Filter = h => new string[0].Contains(h.HotelName) }, this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetWithAFilterThatMatchesNoRecordSendsNoRequestAsync()
    {
        // Arrange: the strict mock fails on any request that was not set up.
        using var sut = this.CreateCollection<string, Hotel<string>>();

        // Act.
        var results = await sut.GetAsync(h => h.HotelId == "h1" && h.HotelId == "h2", top: 5, cancellationToken: this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Empty(results);
    }

    [Fact]
    public async Task EnsureCollectionExistsThrowsForAnExistingCollectionWithAnotherSpaceAsync()
    {
        // Arrange: a collection created elsewhere with the default space of Chroma, l2, and a model with cosine.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid(), Metadata = new() { ["hnsw:space"] = "l2" } });

        // Act and assert.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnsureCollectionExistsAsync(this._testCancellationToken));
        Assert.Contains("'L2'", exception.Message);
        Assert.Contains("'Cosine'", exception.Message);
    }

    [Fact]
    public async Task SearchThrowsForAnExistingCollectionWithAnotherSpaceAsync()
    {
        // Arrange: the model uses the dot product, the collection cosine.
        using var sut = new ChromaCollection<string, DotProductHotel>(() => this._chromaClientMock.Object, "othercollection", null);
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync("othercollection", this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection("othercollection") { Id = Guid.NewGuid(), Metadata = new() { ["hnsw:space"] = "cosine" } });

        // Act and assert.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), top: 1, cancellationToken: this._testCancellationToken).ToListAsync());
    }

    [Fact]
    public async Task EnsureCollectionExistsAcceptsAnExistingCollectionWithTheSameSpaceAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, DotProductHotel>();
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid(), Metadata = new() { ["hnsw:space"] = "ip" } });

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);
    }

    [Fact]
    public async Task LooksTheCollectionUpAgainWhenChromaNoLongerFindsItAsync()
    {
        // Arrange: the collection was deleted and created again elsewhere, so it has a new id.
        var recreatedCollection = new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid() };
        using var sut = new ChromaCollection<string, Hotel<string>>(() => this._chromaClientMock.Object, "recreatedcollection", null);
        this._chromaClientMock
            .SetupSequence(x => x.GetCollectionAsync("recreatedcollection", this._testCancellationToken))
            .ReturnsAsync(this._chromaCollection)
            .ReturnsAsync(recreatedCollection);
        this._chromaClientMock
            .Setup(x => x.DeleteAsync(this._chromaCollection, It.IsAny<List<string>>(), this._testCancellationToken))
            .Returns(Task.CompletedTask);
        this._chromaClientMock
            .Setup(x => x.GetAsync(this._chromaCollection, It.IsAny<List<string>>(), null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Collection does not exist.") { StatusCode = System.Net.HttpStatusCode.NotFound, ErrorType = "NotFoundError" });
        this._chromaClientMock
            .Setup(x => x.GetAsync(recreatedCollection, It.IsAny<List<string>>(), null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionEntry("h1")]);

        // Act: the delete keeps the id, the get finds that it no longer exists.
        await sut.DeleteAsync("h0", this._testCancellationToken);
        var record = await sut.GetAsync("h1", cancellationToken: this._testCancellationToken);

        // Assert.
        Assert.Equal("h1", record?.HotelId);
        this._chromaClientMock.Verify(x => x.GetCollectionAsync("recreatedcollection", this._testCancellationToken), Times.Exactly(2));
    }

    [Fact]
    public async Task ThrowsWhenTheCollectionIsMissingOnTheFirstLookupAsync()
    {
        // Arrange: without a kept id there is nothing to look up again.
        using var sut = new ChromaCollection<string, Hotel<string>>(() => this._chromaClientMock.Object, "missingcollection", null);
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync("missingcollection", this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Collection does not exist.") { StatusCode = System.Net.HttpStatusCode.NotFound, ErrorType = "NotFoundError" });

        // Act and assert.
        await Assert.ThrowsAsync<VectorStoreException>(() => sut.GetAsync("h1", cancellationToken: this._testCancellationToken));
        this._chromaClientMock.Verify(x => x.GetCollectionAsync("missingcollection", this._testCancellationToken), Times.Once);
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
