// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.VectorData;
using Qdrant.Client.Grpc;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaCollectionSearchMapping"/> class.
/// </summary>
public class ChromaCollectionSearchMappingTests
{
    [Fact]
    public void MapScoredPointToVectorSearchResultMapsResults()
    {
        var responseVector = VectorOutput.Parser.ParseJson("{ \"data\": [1, 2, 3] }");

        // Arrange.
        var scoredPoint = new ScoredPoint
        {
            Id = 1,
            Payload = { ["storage_DataField"] = "data 1" },
            Vectors = new VectorsOutput() { Vector = responseVector },
            Score = 0.5f
        };

        var model = new ChromaModelBuilder(hasNamedVectors: false)
            .Build(
                typeof(DataModel),
                typeof(ulong),
                new()
                {
                    Properties =
                    [
                        new VectorStoreKeyProperty("Id", typeof(ulong)),
                        new VectorStoreDataProperty("DataField", typeof(string)) { StorageName = "storage_DataField" },
                        new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 10),
                    ]
                },
                defaultEmbeddingGenerator: null);

        var mapper = new ChromaMapper<DataModel>(model, hasNamedVectors: false);

        // Act.
        var actual = ChromaCollectionSearchMapping.MapScoredPointToVectorSearchResult<DataModel>(scoredPoint, mapper, true, "Qdrant", "myvectorstore", "mycollection", "query");

        // Assert.
        Assert.Equal(1ul, actual.Record.Id);
        Assert.Equal("data 1", actual.Record.DataField);
        Assert.Equal(new float[] { 1, 2, 3 }, actual.Record.Embedding.ToArray());
        Assert.Equal(0.5f, actual.Score);
    }

    public class DataModel
    {
        public ulong Id { get; init; }

        public string? DataField { get; init; }

        public ReadOnlyMemory<float> Embedding { get; init; }
    }
}
