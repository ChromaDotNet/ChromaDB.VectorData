// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChromaDB.Client;
using ChromaDB.Client.Models;
using ChromaDB.VectorData;
using Moq;
using Xunit;

namespace Chroma.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaVectorStore"/> class.
/// </summary>
public class ChromaVectorStoreTests
{
    private const string TestCollectionName = "testcollection";

    private readonly Mock<ChromaClient> _chromaClientMock = new(MockBehavior.Strict);

    // A token that is not the default one, so that the strict mocks also check that it reaches the client.
    private readonly CancellationToken _testCancellationToken = TestContext.Current.CancellationToken;

    public ChromaVectorStoreTests()
    {
        this._chromaClientMock
            .Setup(x => x.Options)
            .Returns(new ChromaConfigurationOptions("http://localhost:8000"));
        var collectionClientMock = new Mock<ChromaCollectionClient>(MockBehavior.Strict);
        collectionClientMock
            .Setup(x => x.WithMetadataValues(ChromaMetadataValues.Exact))
            .Returns(collectionClientMock.Object);
        this._chromaClientMock
            .Setup(x => x.GetCollectionClient(It.IsAny<string>()))
            .Returns(collectionClientMock.Object);
    }

    [Fact]
    public void GetCollectionReturnsChromaCollection()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object, ownsClient: false);

        // Act.
        using var actual = sut.GetCollection<string, ChromaHotel<string>>(TestCollectionName);

        // Assert.
        Assert.IsType<ChromaCollection<string, ChromaHotel<string>>>(actual);
    }

    [Fact]
    public void GetCollectionThrowsForInvalidKeyType()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object, ownsClient: false);

        // Act & Assert.
        Assert.Throws<NotSupportedException>(() => sut.GetCollection<int, ChromaHotel<int>>(TestCollectionName));
    }

    [Fact]
    public async Task ListCollectionNamesCallsClientAsync()
    {
        // Arrange.
        this._chromaClientMock
            .Setup(x => x.ListCollectionsAsync(null, null, this._testCancellationToken))
            .ReturnsAsync([new ChromaCollection("collection1"), new ChromaCollection("collection2")]);
        using var sut = new ChromaVectorStore(this._chromaClientMock.Object, ownsClient: false);

        // Act.
        var collectionNames = await sut.ListCollectionNamesAsync(this._testCancellationToken).ToListAsync();

        // Assert.
        Assert.Equal(["collection1", "collection2"], collectionNames);
    }
}
