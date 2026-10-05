// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.VectorData;

namespace ChromaDB.VectorData;

/// <summary>
/// Options when creating a <see cref="ChromaCollection{TKey, TRecord}"/>.
/// </summary>
public sealed class ChromaCollectionOptions : VectorStoreCollectionOptions
{
    internal static readonly ChromaCollectionOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaCollectionOptions"/> class.
    /// </summary>
    public ChromaCollectionOptions()
    {
    }

    internal ChromaCollectionOptions(ChromaCollectionOptions? source) : base(source)
    {
        CreateBm25Indexes = source?.CreateBm25Indexes ?? false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether creating the collection also creates a BM25 index, a sparse vector index of Chroma,
    /// for each string property with full-text indexing, which hybrid search needs. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Only Chroma Cloud has sparse vector indexes: a single Chroma server rejects them. The index of a property is on the metadata
    /// key of the property followed by <c>_bm25</c>, and Chroma computes the BM25 vectors from its text. Hybrid search also works on a
    /// collection created elsewhere, like by the Python client of Chroma, with a BM25 index on the text of the property, or on the
    /// documents for the property stored as the document.
    /// </remarks>
    public bool CreateBm25Indexes { get; set; }
}
