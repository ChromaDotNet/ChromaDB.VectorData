// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ChromaDB.Client;
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
