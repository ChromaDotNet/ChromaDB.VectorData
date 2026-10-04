// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
