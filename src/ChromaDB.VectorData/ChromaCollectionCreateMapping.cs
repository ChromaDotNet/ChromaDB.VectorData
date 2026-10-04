// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace ChromaDB.VectorData;

/// <summary>
/// Contains mapping helpers to use when creating a Chroma collection.
/// </summary>
internal static class ChromaCollectionCreateMapping
{
    /// <summary>The collection metadata key that sets the distance function of a Chroma collection.</summary>
    internal const string SpaceMetadataKey = "hnsw:space";

    /// <summary>
    /// Maps the vector property to the metadata of the Chroma collection.
    /// </summary>
    /// <param name="vectorProperty">The vector property.</param>
    /// <returns>The metadata to create the collection with.</returns>
    /// <exception cref="NotSupportedException">Thrown if the property has options that Chroma does not support.</exception>
    public static Dictionary<string, object> MapCollectionMetadata(VectorPropertyModel vectorProperty)
    {
        if (vectorProperty.IndexKind is not null and not IndexKind.Hnsw)
        {
            throw new NotSupportedException($"Index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreVectorProperty)} '{vectorProperty.ModelName}' is not supported by the Chroma VectorStore.");
        }

        return new() { [SpaceMetadataKey] = GetSpace(vectorProperty) };
    }

    /// <summary>
    /// Get the Chroma distance function, called space, for the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is cosine.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The Chroma space.</returns>
    /// <exception cref="NotSupportedException">Thrown if a distance function is chosen that Chroma does not support.</exception>
    public static string GetSpace(VectorPropertyModel vectorProperty)
        => vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity or DistanceFunction.CosineDistance or null => "cosine",
            DistanceFunction.DotProductSimilarity or DistanceFunction.NegativeDotProductSimilarity => "ip",
            DistanceFunction.EuclideanSquaredDistance or DistanceFunction.EuclideanDistance => "l2",

            _ => throw new NotSupportedException($"Distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreVectorProperty)} '{vectorProperty.ModelName}' is not supported by the Chroma VectorStore.")
        };
}
