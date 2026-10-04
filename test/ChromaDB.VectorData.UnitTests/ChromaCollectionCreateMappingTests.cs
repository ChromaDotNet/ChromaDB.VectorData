// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ChromaDB.Client;
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
    [InlineData(null, ChromaSpace.Cosine)]
    [InlineData(DistanceFunction.CosineSimilarity, ChromaSpace.Cosine)]
    [InlineData(DistanceFunction.CosineDistance, ChromaSpace.Cosine)]
    [InlineData(DistanceFunction.DotProductSimilarity, ChromaSpace.InnerProduct)]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, ChromaSpace.InnerProduct)]
    [InlineData(DistanceFunction.EuclideanDistance, ChromaSpace.L2)]
    [InlineData(DistanceFunction.EuclideanSquaredDistance, ChromaSpace.L2)]
    public void MapCollectionDefinitionSetsTheSpace(string? distanceFunction, ChromaSpace expectedSpace)
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { DistanceFunction = distanceFunction };

        // Act.
        var definition = ChromaCollectionCreateMapping.MapCollectionDefinition("hotels", vectorProperty);

        // Assert.
        Assert.Equal("hotels", definition.Name);
        Assert.Equal(expectedSpace, definition.Configuration?.Space);
    }

    [Theory]
    [InlineData(DistanceFunction.ManhattanDistance)]
    [InlineData(DistanceFunction.HammingDistance)]
    public void MapCollectionDefinitionThrowsForUnsupportedDistanceFunction(string distanceFunction)
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { DistanceFunction = distanceFunction };

        // Act and assert.
        Assert.Throws<NotSupportedException>(() => ChromaCollectionCreateMapping.MapCollectionDefinition("hotels", vectorProperty));
    }

    [Fact]
    public void MapCollectionDefinitionThrowsForFlatIndex()
    {
        // Arrange.
        var vectorProperty = new VectorPropertyModel("Vector", typeof(ReadOnlyMemory<float>)) { IndexKind = IndexKind.Flat };

        // Act and assert.
        Assert.Throws<NotSupportedException>(() => ChromaCollectionCreateMapping.MapCollectionDefinition("hotels", vectorProperty));
    }
}
