// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaMapper{TRecord}"/> class.
/// </summary>
public class ChromaMapperTests
{
    private static CollectionModel BuildModel<TKey>()
        => new ChromaModelBuilder().Build(typeof(Hotel<TKey>), typeof(TKey), definition: null, defaultEmbeddingGenerator: null);

    [Fact]
    public void MapsAGuidKeyToItsStringForm()
    {
        // Arrange.
        var key = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sut = new ChromaMapper<Hotel<Guid>>(BuildModel<Guid>());
        var hotel = new Hotel<Guid> { HotelId = key, Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var storageRecord = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null);

        // Assert.
        Assert.Equal("11111111-1111-1111-1111-111111111111", storageRecord.Id);
        Assert.Equal(new float[] { 1, 2, 3, 4 }, storageRecord.Embedding.ToArray());
    }

    [Fact]
    public void LeavesNullPropertiesOutOfTheMetadata()
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());
        var hotel = new Hotel<string> { HotelId = "h1", HotelName = null, Rating = 4, Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var metadata = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null).Metadata!;

        // Assert.
        Assert.False(metadata.ContainsKey("HotelName"));
        Assert.Equal(4, metadata["Rating"]);
        Assert.Equal(0d, metadata["Price"]);
        Assert.Equal(false, metadata["Parking"]);
    }

    [Fact]
    public void LeavesEmptyListsOutOfTheMetadata()
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());
        var hotel = new Hotel<string> { HotelId = "h1", Tags = [], Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var metadata = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null).Metadata!;

        // Assert.
        Assert.False(metadata.ContainsKey("Tags"));
    }

    [Fact]
    public void ThrowsWhenTheVectorIsMissing()
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());
        var hotel = new Hotel<string> { HotelId = "h1" };

        // Act and assert.
        Assert.Throws<InvalidOperationException>(() => sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapsAChromaRecordToTheDataModel(bool includeVectors)
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<Guid>>(BuildModel<Guid>());
        var metadata = new Dictionary<string, object> { ["HotelName"] = "Grand", ["Rating"] = 5L, ["Price"] = 120.5, ["Parking"] = true };

        // Act.
        var hotel = sut.MapFromStorageToDataModel("11111111-1111-1111-1111-111111111111", new float[] { 1, 2, 3, 4 }, metadata, includeVectors);

        // Assert.
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), hotel.HotelId);
        Assert.Equal("Grand", hotel.HotelName);
        Assert.Equal(5, hotel.Rating);
        Assert.Equal(120.5, hotel.Price);
        Assert.True(hotel.Parking);
        Assert.Equal(includeVectors, hotel.Embedding.HasValue);
    }

    [Fact]
    public void ReadsAMissingMetadataValueAsNull()
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());

        // Act.
        var hotel = sut.MapFromStorageToDataModel("h1", embedding: null, new Dictionary<string, object> { ["Price"] = 10.0 }, includeVectors: false);

        // Assert.
        Assert.Null(hotel.HotelName);
        Assert.Null(hotel.Rating);
        Assert.Null(hotel.Tags);
        Assert.Equal(10.0, hotel.Price);
    }
}
