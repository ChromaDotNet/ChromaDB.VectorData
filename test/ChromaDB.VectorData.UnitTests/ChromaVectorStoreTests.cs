// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

    private readonly Mock<MockableChromaClient> _qdrantClientMock;

    private readonly CancellationToken _testCancellationToken = new(false);

    public ChromaVectorStoreTests()
    {
        this._qdrantClientMock = new Mock<MockableChromaClient>(MockBehavior.Strict);
    }

    [Fact]
    public void GetCollectionReturnsCollection()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._qdrantClientMock.Object);

        // Act.
        var actual = sut.GetCollection<ulong, SinglePropsModel<ulong>>(TestCollectionName);

        // Assert.
        Assert.NotNull(actual);
        Assert.IsType<ChromaCollection<ulong, SinglePropsModel<ulong>>>(actual);
    }

    [Fact]
    public void GetCollectionThrowsForInvalidKeyType()
    {
        // Arrange.
        using var sut = new ChromaVectorStore(this._qdrantClientMock.Object);

        // Act & Assert.
        Assert.Throws<NotSupportedException>(() => sut.GetCollection<string, SinglePropsModel<string>>(TestCollectionName));
    }

    [Fact]
    public async Task ListCollectionNamesCallsSDKAsync()
    {
        // Arrange.
        this._qdrantClientMock
            .Setup(x => x.ListCollectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "collection1", "collection2" });
        using var sut = new ChromaVectorStore(this._qdrantClientMock.Object);

        // Act.
        var collectionNames = sut.ListCollectionNamesAsync(this._testCancellationToken);

        // Assert.
        var collectionNamesList = await collectionNames.ToListAsync();
        Assert.Equal(new[] { "collection1", "collection2" }, collectionNamesList);
    }

    public sealed class SinglePropsModel<TKey>
    {
        [VectorStoreKey]
        public required TKey Key { get; set; }

        [VectorStoreData]
        public string Data { get; set; } = string.Empty;

        [VectorStoreVector(dimensions: 4)]
        public ReadOnlyMemory<float>? Vector { get; set; }

        public string? NotAnnotated { get; set; }
    }
}
