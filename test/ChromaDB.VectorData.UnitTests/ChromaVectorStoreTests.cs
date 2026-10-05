// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ChromaDB.Client;
using ChromaDB.Client.Models;
using Microsoft.Extensions.VectorData;
using Moq;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaVectorStore"/> class.
/// </summary>
public class ChromaVectorStoreTests
{
    private const string TestCollectionName = "testcollection";

    private readonly Mock<MockableChromaClient> _chromaClientMock = new(MockBehavior.Strict);
    private readonly CancellationToken _testCancellationToken = new(false);

    [Fact]
    public void ReadsMetadataExactlyWithTheClientOfTheCaller()
    {
        // Arrange.
        using var httpClient = new HttpClient();
        var chromaClient = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), httpClient);

        // Act.
        using var sut = new ChromaVectorStore(chromaClient);
        var actual = Assert.IsType<ChromaClient>(sut.GetService(typeof(ChromaClient)));

        // Assert.
        Assert.Equal(ChromaMetadataValues.Exact, actual.Options.MetadataValues);
        Assert.Equal(ChromaMetadataValues.Inferred, chromaClient.Options.MetadataValues);
    }

    [Theory]
    [InlineData("hotels_db")]
    [InlineData(null)]
    public void MetadataNamesTheDatabaseOfTheClient(string? database)
    {
        // Arrange.
        using var httpClient = new HttpClient();
        var chromaClient = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000", defaultDatabase: database), httpClient);

        // Act.
        using var sut = new ChromaVectorStore(chromaClient);
        using var collection = sut.GetCollection<string, Hotel<string>>(TestCollectionName);

        // Assert.
        Assert.Equal(database, Assert.IsType<VectorStoreMetadata>(sut.GetService(typeof(VectorStoreMetadata))).VectorStoreName);
        Assert.Equal(database, Assert.IsType<VectorStoreCollectionMetadata>(collection.GetService(typeof(VectorStoreCollectionMetadata))).VectorStoreName);
    }

    [Fact]
    public async Task DisposeLeavesTheHttpClientOfTheCallerAsync()
    {
        // Arrange.
        var handler = new HttpMessageHandlerStub();
        using var httpClient = new HttpClient(handler);
        var chromaClient = new ChromaClient(new ChromaConfigurationOptions("http://localhost:8000"), httpClient);

        // Act.
        new ChromaVectorStore(chromaClient).Dispose();

        // Assert.
        using var response = await httpClient.GetAsync(new Uri("http://localhost:8000/api/v2/heartbeat"));
        Assert.NotNull(handler.RequestUri);
    }

    [Fact]
    public async Task DisposeDisposesAnOwnedHttpClientAsync()
    {
        // Arrange.
        var handler = new HttpMessageHandlerStub();
        using var httpClient = new HttpClient(handler);

        // Act.
        new ChromaVectorStore(new ChromaConfigurationOptions("http://localhost:8000"), httpClient, ownsClient: true).Dispose();

        // Assert.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => httpClient.GetAsync(new Uri("http://localhost:8000/api/v2/heartbeat")));
    }

    [Fact]
    public async Task CollectionsCreateTheBm25IndexesWithTheOptionOfTheStoreAsync()
    {
        // Arrange.
        var definitions = new List<ChromaCollectionDefinition>();
        this._chromaClientMock
            .Setup(x => x.GetOrCreateCollectionAsync(It.IsAny<ChromaCollectionDefinition>(), this._testCancellationToken))
            .Callback<ChromaCollectionDefinition, CancellationToken>((d, _) => definitions.Add(d))
            .ReturnsAsync(new ChromaCollection(TestCollectionName) { Id = Guid.NewGuid() });
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object, new() { CreateBm25Indexes = true });
        using var collection = sut.GetCollection<string, FullTextHotel>(TestCollectionName);
        using var dynamicCollection = sut.GetDynamicCollection(TestCollectionName, new()
        {
            Properties =
            [
                new VectorStoreKeyProperty("Key", typeof(string)),
                new VectorStoreDataProperty("Text", typeof(string)) { IsFullTextIndexed = true },
                new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 4),
            ]
        });

        // Act.
        await collection.EnsureCollectionExistsAsync(this._testCancellationToken);
        await dynamicCollection.EnsureCollectionExistsAsync(this._testCancellationToken);

        // Assert.
        Assert.Equal(2, definitions.Count);
        Assert.All(definitions, definition => Assert.NotNull(definition.Schema));
    }

    [Fact]
    public void CopiesOfTheOptionsKeepCreateBm25Indexes()
    {
        // The registrations for dependency injection copy the options to add the embedding generator of the container.
        Assert.True(new ChromaVectorStoreOptions(new ChromaVectorStoreOptions { CreateBm25Indexes = true }).CreateBm25Indexes);
        Assert.True(new ChromaCollectionOptions(new ChromaCollectionOptions { CreateBm25Indexes = true }).CreateBm25Indexes);
    }

    [Fact]
    public void GetCollectionReturnsChromaCollection()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object);

        // Act.
        var actual = sut.GetCollection<string, Hotel<string>>(TestCollectionName);

        // Assert.
        Assert.NotNull(actual);
        Assert.IsType<ChromaCollection<string, Hotel<string>>>(actual);
    }

    [Fact]
    public async Task ListCollectionNamesCallsClientAsync()
    {
        // Arrange.
        this._chromaClientMock
            .Setup(x => x.ListCollectionsAsync(this._testCancellationToken))
            .ReturnsAsync(["collection1", "collection2"]);
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object);

        // Act.
        var collectionNames = await sut.ListCollectionNamesAsync(this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Equal(["collection1", "collection2"], collectionNames);
    }

    [Fact]
    public void GetServiceReturnsTheMetadataAndTheStore()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object);

        // Act and assert.
        Assert.Equal("chroma", Assert.IsType<VectorStoreMetadata>(sut.GetService(typeof(VectorStoreMetadata))).VectorStoreSystemName);
        Assert.Same(sut, sut.GetService(typeof(ChromaVectorStore)));
    }
}
