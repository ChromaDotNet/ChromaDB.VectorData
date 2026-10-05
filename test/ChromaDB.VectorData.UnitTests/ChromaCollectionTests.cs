// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    private readonly Mock<ChromaClient> _chromaClientMock = new(MockBehavior.Strict);
    private readonly Mock<ChromaCollectionClient> _collectionClientMock = new(MockBehavior.Strict);
    private readonly ChromaCollection _chromaCollection = new(TestCollectionName) { Id = Guid.NewGuid() };
    private readonly CancellationToken _testCancellationToken = new(false);

    public ChromaCollectionTests()
    {
        // The collection reads metadata exactly with a client of its own: here the same mock.
        this._chromaClientMock
            .Setup(x => x.WithMetadataValues(ChromaMetadataValues.Exact))
            .Returns(this._chromaClientMock.Object);
        this._chromaClientMock
            .Setup(x => x.Options)
            .Returns(new ChromaConfigurationOptions("http://localhost:8000"));
        this._chromaClientMock
            .Setup(x => x.GetCollectionClient(It.IsAny<ChromaCollection>()))
            .Returns(this._collectionClientMock.Object);
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync(TestCollectionName, null, null, this._testCancellationToken))
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
            .Setup(x => x.CollectionExistsAsync(TestCollectionName, null, null, this._testCancellationToken))
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
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken))
            .Callback<ChromaCollectionDefinition, string?, string?, CancellationToken>((d, _, _, _) => definition = d)
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
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken))
            .ReturnsAsync(this._chromaCollection);

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        this._chromaClientMock.Verify(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureCollectionExistsCreatesTheBm25IndexesOnlyWithTheOptionAsync(bool createBm25Indexes)
    {
        // Arrange.
        using var sut = new ChromaCollection<string, FullTextHotel>(this._chromaClientMock.Object, TestCollectionName, new ChromaCollectionOptions { CreateBm25Indexes = createBm25Indexes });
        ChromaCollectionDefinition? definition = null;
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken))
            .Callback<ChromaCollectionDefinition, string?, string?, CancellationToken>((d, _, _, _) => definition = d)
            .ReturnsAsync(this._chromaCollection);

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        Assert.Equal(createBm25Indexes, definition!.Schema is not null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetServiceOffersHybridSearchOnlyWithTheBm25Indexes(bool createBm25Indexes)
    {
        // The TextSearchStore of Semantic Kernel searches with keywords when the collection offers hybrid search.
        using var sut = new ChromaCollection<string, FullTextHotel>(this._chromaClientMock.Object, TestCollectionName, new ChromaCollectionOptions { CreateBm25Indexes = createBm25Indexes });

        Assert.Equal(createBm25Indexes, sut.GetService(typeof(IKeywordHybridSearchable<FullTextHotel>)) is not null);
        Assert.Same(sut, sut.GetService(typeof(VectorStoreCollection<string, FullTextHotel>)));
    }

    [Fact]
    public void GetServiceDoesNotOfferHybridSearchWithoutAFullTextProperty()
    {
        using var sut = new ChromaCollection<string, Hotel<string>>(this._chromaClientMock.Object, TestCollectionName, new ChromaCollectionOptions { CreateBm25Indexes = true });

        Assert.Null(sut.GetService(typeof(IKeywordHybridSearchable<Hotel<string>>)));
    }

    [Fact]
    public void ThrowsWhenAPropertyUsesTheKeyOfABm25Index()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ChromaCollection<string, Bm25KeyClashHotel>(this._chromaClientMock.Object, TestCollectionName, new ChromaCollectionOptions { CreateBm25Indexes = true }));

        Assert.Contains("'Description_bm25'", exception.Message);
        Assert.Contains("'Other'", exception.Message);
    }

    [Fact]
    public void AcceptsAPropertyWithTheKeyOfABm25IndexWithoutTheOption()
    {
        using var sut = new ChromaCollection<string, Bm25KeyClashHotel>(this._chromaClientMock.Object, TestCollectionName, null);
    }

    [Fact]
    public async Task HybridSearchSendsTheRrfOfTheVectorAndBm25SearchesAsync()
    {
        // Arrange: the collection has the BM25 index the provider creates.
        using var sut = this.CreateHybridCollection<FullTextHotel>(Bm25Index("Description_bm25", "Description"));
        ChromaSearch? search = null;
        this._collectionClientMock
            .Setup(x => x.SearchAsync(It.IsAny<ChromaSearch>(), null, this._testCancellationToken))
            .Callback<ChromaSearch, ChromaReadLevel?, CancellationToken>((s, _, _) => search = s)
            .ReturnsAsync(
            [
                new ChromaSearchEntry("h1") { Score = -0.032f, Document = "A pool and a spa", Metadata = new Dictionary<string, object> { ["Description"] = "A pool and a spa", ["Rating"] = 5L } },
                new ChromaSearchEntry("h2") { Score = -0.016f, Document = "A gym", Metadata = new Dictionary<string, object> { ["Description"] = "A gym", ["Rating"] = 4L } },
            ]);

        // Act.
        var results = await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool", "spa"], top: 2, new() { Skip = 1, Filter = h => h.Rating >= 4, ScoreThreshold = 0.02 }, this._testCancellationToken).ToListAsync();

        // Assert: the RRF of the two searches, each among the top + skip records it ranks first, the others with the last rank;
        // the BM25 search counts only for the records with a keyword, at a distance below 1.
        Assert.Equal(
            """{"$mul":[{"$val":-1},{"$sum":["""
            + """{"$div":{"left":{"$val":1},"right":{"$sum":[{"$val":60},{"$knn":{"query":[1,2,3,4],"key":"#embedding","limit":3,"default":3,"return_rank":true}}]}}},"""
            + """{"$div":{"left":{"$min":[{"$val":1},{"$mul":[{"$sub":{"left":{"$val":1},"right":{"$knn":{"query":"pool spa","key":"Description_bm25","limit":3,"default":1}}}},{"$val":1000000}]}]},"right":"""
            + """{"$sum":[{"$val":60},{"$knn":{"query":"pool spa","key":"Description_bm25","limit":3,"default":3,"return_rank":true}}]}}}]}]}""",
            search!.Rank!.ToString());
        Assert.Equal(2, search.Limit);
        Assert.Equal(1, search.Offset);
        Assert.Equal("""{"Rating":{"$gte":4}}""", search.Where!.ToString());
        Assert.Equal([ChromaSearchKeys.Metadata, ChromaSearchKeys.Score, ChromaSearchKeys.Document], search.Select);

        // The score is the RRF score, the opposite of what Chroma returns, and the threshold applies to it.
        var result = Assert.Single(results);
        Assert.Equal("h1", result.Record.HotelId);
        Assert.Equal("A pool and a spa", result.Record.Description);
        Assert.Equal(0.032, result.Score!.Value, precision: 6);
    }

    [Theory]
    [InlineData("Description")]
    [InlineData("#document")]
    public async Task UpsertDeletesTheSparseVectorOfANullTextAsync(string sourceKey)
    {
        // Arrange: the client computes no vector for a record without its text, and Chroma merges the metadata of an existing record.
        using var sut = this.CreateHybridCollection<FullTextHotel>(Bm25Index("Description_bm25", sourceKey));
        IReadOnlyList<IReadOnlyDictionary<string, object>>? metadatas = null;
        this._collectionClientMock
            .Setup(x => x.UpsertAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<ReadOnlyMemory<float>>>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object>>?>(), It.IsAny<IReadOnlyList<string>?>(), this._testCancellationToken))
            .Callback<IReadOnlyList<string>, IReadOnlyList<ReadOnlyMemory<float>>, IReadOnlyList<IReadOnlyDictionary<string, object>>?, IReadOnlyList<string>?, CancellationToken>((_, _, m, _, _) => metadatas = m)
            .Returns(Task.CompletedTask);

        // Act.
        await sut.UpsertAsync(
        [
            new FullTextHotel { HotelId = "h1", Description = null, Embedding = new float[] { 1, 2, 3, 4 } },
            new FullTextHotel { HotelId = "h2", Description = "A pool", Embedding = new float[] { 1, 2, 3, 4 } },
        ], this._testCancellationToken);

        // Assert: an explicit null deletes the old vector of the record without text; the client computes the other one.
        Assert.True(metadatas![0].ContainsKey("Description_bm25"));
        Assert.Null(metadatas[0]["Description_bm25"]);
        Assert.False(metadatas[1].ContainsKey("Description_bm25"));
    }

    [Fact]
    public async Task HybridSearchUsesABm25IndexOnTheDocumentsForTheDocumentPropertyAsync()
    {
        // Arrange: a collection created by the Python client of Chroma, with the index on the documents.
        using var sut = this.CreateHybridCollection<FullTextHotel>(Bm25Index("sparse_embedding", "#document"));
        ChromaSearch? search = null;
        this._collectionClientMock
            .Setup(x => x.SearchAsync(It.IsAny<ChromaSearch>(), null, this._testCancellationToken))
            .Callback<ChromaSearch, ChromaReadLevel?, CancellationToken>((s, _, _) => search = s)
            .ReturnsAsync([]);

        // Act.
        await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool"], top: 1, cancellationToken: this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Contains("\"key\":\"sparse_embedding\"", search!.Rank!.ToString());
    }

    [Fact]
    public async Task HybridSearchUsesTheIndexOfTheChosenPropertyAsync()
    {
        // Arrange.
        using var sut = this.CreateHybridCollection<TwoFullTextHotel>(Bm25Index("Description_bm25", "Description"), Bm25Index("Review_bm25", "Review"));
        ChromaSearch? search = null;
        this._collectionClientMock
            .Setup(x => x.SearchAsync(It.IsAny<ChromaSearch>(), null, this._testCancellationToken))
            .Callback<ChromaSearch, ChromaReadLevel?, CancellationToken>((s, _, _) => search = s)
            .ReturnsAsync([]);

        // Act.
        await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["great"], top: 1, new() { AdditionalProperty = h => h.Review }, this._testCancellationToken).ToListAsync();

        // Assert: two full-text properties, so neither is stored as the document.
        Assert.Contains("\"key\":\"Review_bm25\"", search!.Rank!.ToString());
        Assert.Equal([ChromaSearchKeys.Metadata, ChromaSearchKeys.Score], search.Select);
    }

    [Fact]
    public async Task HybridSearchThrowsWithoutABm25IndexAsync()
    {
        // Arrange: the strict mock fails on any search.
        using var sut = this.CreateHybridCollection<FullTextHotel>();

        // Act and assert.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool"], top: 1, cancellationToken: this._testCancellationToken).ToListAsync());
        Assert.Contains(nameof(ChromaCollectionOptions.CreateBm25Indexes), exception.Message);
    }

    [Fact]
    public async Task HybridSearchThrowsForAnIndexWithAnotherFunctionAsync()
    {
        // Arrange: the client computes the vectors of the keywords only with chroma_bm25.
        using var sut = this.CreateHybridCollection<FullTextHotel>(Bm25Index("Description_splade", "Description", "prithivida_splade"));

        // Act and assert.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool"], top: 1, cancellationToken: this._testCancellationToken).ToListAsync());
    }

    [Fact]
    public async Task HybridSearchThrowsWithoutAChosenPropertyAmongTwoAsync()
    {
        using var sut = this.CreateHybridCollection<TwoFullTextHotel>(Bm25Index("Description_bm25", "Description"), Bm25Index("Review_bm25", "Review"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool"], top: 1, cancellationToken: this._testCancellationToken).ToListAsync());
    }

    [Fact]
    public async Task HybridSearchWithAFilterThatMatchesNoRecordSendsNoRequestAsync()
    {
        // Arrange: the strict mock fails on any request that was not set up.
        using var sut = this.CreateHybridCollection<FullTextHotel>(Bm25Index("Description_bm25", "Description"));

        // Act.
        var results = await sut.HybridSearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), ["pool"], top: 1, new() { Filter = h => h.HotelId == "h1" && h.HotelId == "h2" }, this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Empty(results);
    }

    [Fact]
    public async Task UpsertSendsIdsEmbeddingsAndMetadataAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<Guid, Hotel<Guid>>();
        IReadOnlyList<string>? ids = null;
        IReadOnlyList<ReadOnlyMemory<float>>? embeddings = null;
        IReadOnlyList<IReadOnlyDictionary<string, object>>? metadatas = null;
        this._collectionClientMock
            .Setup(x => x.UpsertAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<ReadOnlyMemory<float>>>(), It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object>>?>(), It.IsAny<IReadOnlyList<string>?>(), this._testCancellationToken))
            .Callback<IReadOnlyList<string>, IReadOnlyList<ReadOnlyMemory<float>>, IReadOnlyList<IReadOnlyDictionary<string, object>>?, IReadOnlyList<string>?, CancellationToken>((i, e, m, _, _) => (ids, embeddings, metadatas) = (i, e, m))
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
        this._collectionClientMock
            .Setup(x => x.GetAsync(new List<string> { "h1" }, null, null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionEntry("h1") { Metadata = new Dictionary<string, object> { ["HotelName"] = "Grand" } }]);

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
        this._collectionClientMock
            .Setup(x => x.DeleteAsync(new List<string> { "h1", "h2" }, null, null, this._testCancellationToken))
            .Returns(Task.CompletedTask);

        // Act.
        await sut.DeleteAsync(["h1", "h2"], this._testCancellationToken);

        // Assert.
        this._collectionClientMock.Verify(x => x.DeleteAsync(new List<string> { "h1", "h2" }, null, null, this._testCancellationToken), Times.Once);
    }

    [Fact]
    public async Task SearchSkipsConvertsAndFiltersTheResultsAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, Hotel<string>>();
        this._collectionClientMock
            .Setup(x => x.QueryAsync(It.IsAny<ReadOnlyMemory<float>>(), 3, null, null, ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances, null, this._testCancellationToken))
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
        this._collectionClientMock
            .Setup(x => x.QueryAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                2,
                null,
                null,
                ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances,
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "h1", "h2" })),
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
        this._collectionClientMock
            .Setup(x => x.GetAsync(
                It.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "h1" })),
                It.IsNotNull<ChromaWhereOperator>(),
                null,
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
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid(), Metadata = new Dictionary<string, object> { ["hnsw:space"] = "l2" } });

        // Act and assert.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnsureCollectionExistsAsync(this._testCancellationToken));
        Assert.Contains("'L2'", exception.Message);
        Assert.Contains("'Cosine'", exception.Message);
    }

    [Fact]
    public async Task SearchThrowsForAnExistingCollectionWithAnotherSpaceAsync()
    {
        // Arrange: the model uses the dot product, the collection cosine.
        using var sut = new ChromaCollection<string, DotProductHotel>(this._chromaClientMock.Object, "othercollection", null);
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync("othercollection", null, null, this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection("othercollection") { Id = Guid.NewGuid(), Metadata = new Dictionary<string, object> { ["hnsw:space"] = "cosine" } });

        // Act and assert.
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.SearchAsync(new ReadOnlyMemory<float>([1, 2, 3, 4]), top: 1, cancellationToken: this._testCancellationToken).ToListAsync());
    }

    [Fact]
    public async Task EnsureCollectionExistsAcceptsAnExistingCollectionWithTheSameSpaceAsync()
    {
        // Arrange.
        using var sut = this.CreateCollection<string, DotProductHotel>();
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), null, null, this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid(), Metadata = new Dictionary<string, object> { ["hnsw:space"] = "ip" } });

        // Act.
        await sut.EnsureCollectionExistsAsync(this._testCancellationToken);
    }

    [Fact]
    public async Task LooksTheCollectionUpAgainWhenChromaNoLongerFindsItAsync()
    {
        // Arrange: the collection was deleted and created again elsewhere, so it has a new id.
        var recreatedCollection = new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid() };
        using var sut = new ChromaCollection<string, Hotel<string>>(this._chromaClientMock.Object, "recreatedcollection", null);
        var recreatedCollectionClientMock = new Mock<ChromaCollectionClient>(MockBehavior.Strict);
        this._chromaClientMock
            .SetupSequence(x => x.GetCollectionAsync("recreatedcollection", null, null, this._testCancellationToken))
            .ReturnsAsync(this._chromaCollection)
            .ReturnsAsync(recreatedCollection);
        this._chromaClientMock
            .Setup(x => x.GetCollectionClient(recreatedCollection))
            .Returns(recreatedCollectionClientMock.Object);
        this._collectionClientMock
            .Setup(x => x.DeleteAsync(It.IsAny<IReadOnlyList<string>>(), null, null, this._testCancellationToken))
            .Returns(Task.CompletedTask);
        this._collectionClientMock
            .Setup(x => x.GetAsync(It.IsAny<IReadOnlyList<string>>(), null, null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Collection does not exist.") { StatusCode = System.Net.HttpStatusCode.NotFound, ErrorType = "NotFoundError" });
        recreatedCollectionClientMock
            .Setup(x => x.GetAsync(It.IsAny<IReadOnlyList<string>>(), null, null, null, null, ChromaGetInclude.Metadatas, this._testCancellationToken))
            .ReturnsAsync([new ChromaCollectionEntry("h1")]);

        // Act: the delete keeps the id, the get finds that it no longer exists.
        await sut.DeleteAsync("h0", this._testCancellationToken);
        var record = await sut.GetAsync("h1", cancellationToken: this._testCancellationToken);

        // Assert.
        Assert.Equal("h1", record?.HotelId);
        this._chromaClientMock.Verify(x => x.GetCollectionAsync("recreatedcollection", null, null, this._testCancellationToken), Times.Exactly(2));
    }

    [Fact]
    public async Task ThrowsWhenTheCollectionIsMissingOnTheFirstLookupAsync()
    {
        // Arrange: without a kept id there is nothing to look up again.
        using var sut = new ChromaCollection<string, Hotel<string>>(this._chromaClientMock.Object, "missingcollection", null);
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync("missingcollection", null, null, this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Collection does not exist.") { StatusCode = System.Net.HttpStatusCode.NotFound, ErrorType = "NotFoundError" });

        // Act and assert.
        await Assert.ThrowsAsync<VectorStoreException>(() => sut.GetAsync("h1", cancellationToken: this._testCancellationToken));
        this._chromaClientMock.Verify(x => x.GetCollectionAsync("missingcollection", null, null, this._testCancellationToken), Times.Once);
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
            .Setup(x => x.CollectionExistsAsync(TestCollectionName, null, null, this._testCancellationToken))
            .ThrowsAsync(new ChromaException("Unexpected status code"));

        // Act.
        var exception = await Assert.ThrowsAsync<VectorStoreException>(() => sut.CollectionExistsAsync(this._testCancellationToken));

        // Assert.
        Assert.Equal("chroma", exception.VectorStoreSystemName);
        Assert.IsType<ChromaException>(exception.InnerException);
    }

    [Fact]
    public void RejectsUnsupportedKeyTypes()
        => Assert.Throws<NotSupportedException>(() => new ChromaCollection<int, Hotel<int>>(this._chromaClientMock.Object, TestCollectionName));

#pragma warning disable IL2026, IL3050 // The test models are not trimmed
    private ChromaCollection<TKey, TRecord> CreateCollection<TKey, TRecord>()
        where TKey : notnull
        where TRecord : class
        => new(this._chromaClientMock.Object, TestCollectionName);

    private ChromaCollection<string, TRecord> CreateHybridCollection<TRecord>(params string[] indexes)
        where TRecord : class
    {
        var schema = JsonDocument.Parse("{\"keys\":{" + string.Join(",", indexes) + "}}").RootElement.Clone();
        this._chromaClientMock
            .Setup(x => x.GetCollectionAsync("hybridcollection", null, null, this._testCancellationToken))
            .ReturnsAsync(new ChromaCollection("hybridcollection") { Id = Guid.NewGuid(), SchemaJson = schema });

        return new(this._chromaClientMock.Object, "hybridcollection", null);
    }
#pragma warning restore IL2026, IL3050

    // A sparse vector index as Chroma Cloud returns it in the schema of a collection.
    private static string Bm25Index(string key, string sourceKey, string function = "chroma_bm25")
        => "\"" + key + "\":{\"sparse_vector\":{\"sparse_vector_index\":{\"enabled\":true,\"config\":{\"source_key\":\"" + sourceKey + "\",\"bm25\":true,"
            + "\"embedding_function\":{\"type\":\"known\",\"name\":\"" + function + "\",\"config\":{\"k\":1.2,\"b\":0.75,\"avg_doc_length\":256,\"token_max_length\":40}}}}}}";
}
