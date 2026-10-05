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
    public void WritesNullPropertiesAsExplicitNulls()
    {
        // An upsert of an existing record merges its metadata in Chroma: an explicit null deletes the old value.
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());
        var hotel = new Hotel<string> { HotelId = "h1", HotelName = null, Rating = 4, Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var metadata = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null).Metadata!;

        // Assert.
        Assert.True(metadata.ContainsKey("HotelName"));
        Assert.Null(metadata["HotelName"]);
        Assert.Equal(4, metadata["Rating"]);
        Assert.Equal(0d, metadata["Price"]);
        Assert.Equal(false, metadata["Parking"]);
    }

    [Fact]
    public void WritesEmptyListsAsExplicitNulls()
    {
        // Arrange.
        var sut = new ChromaMapper<Hotel<string>>(BuildModel<string>());
        var hotel = new Hotel<string> { HotelId = "h1", Tags = [], Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var metadata = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null).Metadata!;

        // Assert.
        Assert.True(metadata.ContainsKey("Tags"));
        Assert.Null(metadata["Tags"]);
    }

    [Fact]
    public void WritesNoDocumentForANullFullTextProperty()
    {
        // Arrange: the collection empties the document of a record that exists.
        var sut = new ChromaMapper<FullTextHotel>(new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));
        var hotel = new FullTextHotel { HotelId = "h1", Description = null, Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var record = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null);

        // Assert.
        Assert.Null(record.Document);
        Assert.Null(record.Metadata!["Description"]);
    }

    [Theory]
    [InlineData(8182, true)]
    [InlineData(8183, false)]
    public void KeepsATextTooLongForTheMetadataInTheDocumentOnly(int bytes, bool inMetadata)
    {
        // Arrange: Chroma Cloud takes at most 8,182 bytes per metadata value, and 16,384 per document.
        var sut = new ChromaMapper<FullTextHotel>(new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));
        var text = new string('a', bytes - 2) + "é";
        var hotel = new FullTextHotel { HotelId = "h1", Description = text, Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var record = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null);

        // Assert: the text reads back from the document when the metadata does not have it.
        Assert.Equal(text, record.Document);
        Assert.Equal(inMetadata ? text : null, record.Metadata!["Description"]);
        Assert.Equal(text, sut.MapFromStorageToDataModel("h1", embedding: null, metadata: null, document: record.Document, includeVectors: false).Description);
    }

    [Fact]
    public void ReadsAnEmptyDocumentAsNull()
    {
        var sut = new ChromaMapper<FullTextHotel>(new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));

        var hotel = sut.MapFromStorageToDataModel("h1", embedding: null, metadata: null, document: "", includeVectors: false);

        Assert.Null(hotel.Description);
    }

    [Fact]
    public void WritesTheFullTextPropertyAsTheDocumentAndInTheMetadata()
    {
        // Arrange.
        var sut = new ChromaMapper<FullTextHotel>(new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));
        var hotel = new FullTextHotel { HotelId = "h1", Description = "A pool and a spa", Embedding = new float[] { 1, 2, 3, 4 } };

        // Act.
        var record = sut.MapFromDataToStorageModel(hotel, 0, generatedEmbeddings: null);

        // Assert.
        Assert.Equal("A pool and a spa", record.Document);
        Assert.Equal("A pool and a spa", record.Metadata!["Description"]);
    }

    [Fact]
    public void ReadsTheFullTextPropertyFromTheDocumentWhenTheMetadataLacksIt()
    {
        // Arrange: a record written by another Chroma client, with its text in the document only.
        var sut = new ChromaMapper<FullTextHotel>(new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));

        // Act.
        var hotel = sut.MapFromStorageToDataModel("h1", embedding: null, metadata: null, document: "A pool and a spa", includeVectors: false);

        // Assert.
        Assert.Equal("A pool and a spa", hotel.Description);
    }

    [Fact]
    public void WritesNoDocumentWithoutAFullTextProperty()
        => Assert.Null(new ChromaMapper<Hotel<string>>(BuildModel<string>())
            .MapFromDataToStorageModel(new Hotel<string> { HotelId = "h1", HotelName = "Grand", Embedding = new float[] { 1, 2, 3, 4 } }, 0, generatedEmbeddings: null)
            .Document);

    [Fact]
    public void WritesNoDocumentWithTwoFullTextProperties()
        => Assert.Null(new ChromaMapper<TwoFullTextHotel>(new ChromaModelBuilder().Build(typeof(TwoFullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null))
            .MapFromDataToStorageModel(new TwoFullTextHotel { HotelId = "h1", Description = "A pool", Review = "Great", Embedding = new float[] { 1, 2, 3, 4 } }, 0, generatedEmbeddings: null)
            .Document);

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
        var hotel = sut.MapFromStorageToDataModel("11111111-1111-1111-1111-111111111111", new float[] { 1, 2, 3, 4 }, metadata, document: null, includeVectors);

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
        var hotel = sut.MapFromStorageToDataModel("h1", embedding: null, new Dictionary<string, object> { ["Price"] = 10.0 }, document: null, includeVectors: false);

        // Assert.
        Assert.Null(hotel.HotelName);
        Assert.Null(hotel.Rating);
        Assert.Null(hotel.Tags);
        Assert.Equal(10.0, hotel.Price);
    }
}
