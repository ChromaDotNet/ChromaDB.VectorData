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
    /// The suffix of the metadata key of the BM25 index of a property: the index of <c>Text</c> is on <c>Text_bm25</c>.
    /// </summary>
    private const string Bm25KeySuffix = "_bm25";

    /// <summary>
    /// Maps the vector property and the BM25 indexes to the definition of the Chroma collection.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="vectorProperty">The vector property.</param>
    /// <param name="bm25Properties">The properties to create a BM25 index for.</param>
    /// <param name="documentProperty">The property stored as the document, whose BM25 index comes from the document, or <see langword="null"/>.</param>
    /// <returns>The definition to create the collection with.</returns>
    /// <exception cref="NotSupportedException">Thrown if the property has options that Chroma does not support.</exception>
    public static ChromaCollectionDefinition MapCollectionDefinition(string name, VectorPropertyModel vectorProperty, IReadOnlyList<DataPropertyModel> bm25Properties, DataPropertyModel? documentProperty = null)
    {
        if (vectorProperty.IndexKind is not null and not IndexKind.Hnsw)
        {
            throw new NotSupportedException($"Index kind '{vectorProperty.IndexKind}' for {nameof(VectorStoreVectorProperty)} '{vectorProperty.ModelName}' is not supported by the Chroma VectorStore.");
        }

        ChromaCollectionSchema? schema = null;
        foreach (var property in bm25Properties)
        {
            // bm25: true makes Chroma apply the inverse document frequency, and the client computes the vectors from the text of the property.
            // The document holds the text of the property stored as the document also when it is too long for the metadata.
            var sourceKey = property == documentProperty ? ChromaSearchKeys.Document : property.StorageName;
            schema = (schema ?? new()).WithSparseVectorIndex(GetBm25Key(property), sourceKey, bm25: true, ChromaEmbeddingFunctionReference.ChromaBm25());
        }

        return new(name) { Configuration = new() { Space = GetSpace(vectorProperty) }, Schema = schema };
    }

    /// <summary>
    /// Get the string properties with full-text indexing, the ones a BM25 index can be created for.
    /// </summary>
    /// <param name="model">The model of the collection.</param>
    public static List<DataPropertyModel> GetBm25Properties(CollectionModel model)
        => model.DataProperties.Where(property => property.IsFullTextIndexed && property.Type == typeof(string)).ToList();

    /// <summary>
    /// Get the metadata key of the BM25 index the provider creates for the given property.
    /// </summary>
    /// <param name="property">The property.</param>
    public static string GetBm25Key(DataPropertyModel property)
        => property.StorageName + Bm25KeySuffix;

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
