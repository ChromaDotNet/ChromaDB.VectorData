// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaCollectionCreateMapping"/> class.
/// </summary>
public class ChromaCollectionCreateMappingTests
{
    [Theory]
    [InlineData(null, "cosine")]
    [InlineData(DistanceFunction.CosineSimilarity, "cosine")]
    [InlineData(DistanceFunction.CosineDistance, "cosine")]
    [InlineData(DistanceFunction.DotProductSimilarity, "ip")]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, "ip")]
    [InlineData(DistanceFunction.EuclideanDistance, "l2")]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, "l2")]
    public void MapCollectionMetadataSetsTheSpace(string? distanceFunction, string expectedSpace)
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { DistanceFunction = distanceFunction };

        // Act.
        var metadata = ChromaCollectionCreateMapping.MapCollectionMetadata(vectorProperty);

        // Assert.
        Assert.Equal(expectedSpace, Assert.Single(metadata, m => m.Key == "hnsw:space").Value);
    }

    [Theory]
    [InlineData(DistanceFunction.ManhattanDistance)]
    [InlineData(DistanceFunction.HammingDistance)]
    public void MapCollectionMetadataThrowsForUnsupportedDistanceFunction(string distanceFunction)
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { DistanceFunction = distanceFunction };

        // Act and assert.
        Assert.Throws<NotSupportedException>(() => ChromaCollectionCreateMapping.MapCollectionMetadata(vectorProperty));
    }

    [Fact]
    public void MapCollectionMetadataThrowsForFlatIndex()
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { IndexKind = IndexKind.Flat };

        // Act and assert.
        Assert.Throws<NotSupportedException>(() => ChromaCollectionCreateMapping.MapCollectionMetadata(vectorProperty));
    }
}
