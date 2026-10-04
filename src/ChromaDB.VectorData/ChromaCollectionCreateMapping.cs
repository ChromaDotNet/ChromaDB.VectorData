// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.Client.Models;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;

namespace ChromaDB.VectorData;

/// <summary>
/// Contains mapping helpers to use when creating a Chroma collection.
/// </summary>
internal static class ChromaCollectionCreateMapping
{
    /// <summary>
    /// Maps the vector property to the definition of the Chroma collection.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="vectorProperty">The vector property.</param>
    /// <returns>The definition to create the collection with.</returns>
    /// <exception cref="NotSupportedException">Thrown if the property has options that Chroma does not support.</exception>
    public static ChromaCollectionDefinition MapCollectionDefinition(string name, VectorPropertyModel vectorProperty)
    {
        if (vectorProperty.IndexKind is not null and not IndexKind.Hnsw)
        {
            throw new NotSupportedException($"Index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreVectorProperty)} '{vectorProperty.ModelName}' is not supported by the Chroma VectorStore.");
        }

        return new(name) { Configuration = new() { Space = GetSpace(vectorProperty) } };
    }

    /// <summary>
    /// Get the Chroma distance function, called space, for the given <paramref name="vectorProperty"/>.
    /// If none is configured, the default is cosine.
    /// </summary>
    /// <param name="vectorProperty">The vector property definition.</param>
    /// <returns>The Chroma space.</returns>
    /// <exception cref="NotSupportedException">Thrown if a distance function is chosen that Chroma does not support.</exception>
    public static ChromaSpace GetSpace(VectorPropertyModel vectorProperty)
        => vectorProperty.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity or DistanceFunction.CosineDistance or null => ChromaSpace.Cosine,
            DistanceFunction.DotProductSimilarity or DistanceFunction.NegativeDotProductSimilarity => ChromaSpace.InnerProduct,
            DistanceFunction.EuclideanSquaredDistance or DistanceFunction.EuclideanDistance => ChromaSpace.L2,

            _ => throw new NotSupportedException($"Distance function '{vectorProperty.DistanceFunction}' for {nameof(VectorStoreVectorProperty)} '{vectorProperty.ModelName}' is not supported by the Chroma VectorStore.")
        };
}
