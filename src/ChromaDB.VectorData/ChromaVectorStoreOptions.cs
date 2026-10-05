// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.AI;

namespace ChromaDB.VectorData;

/// <summary>
/// Options when creating a <see cref="ChromaVectorStore"/>.
/// </summary>
public sealed class ChromaVectorStoreOptions
{
    internal static readonly ChromaVectorStoreOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaVectorStoreOptions"/> class.
    /// </summary>
    public ChromaVectorStoreOptions()
    {
    }

    internal ChromaVectorStoreOptions(ChromaVectorStoreOptions? source)
    {
        EmbeddingGenerator = source?.EmbeddingGenerator;
        CreateBm25Indexes = source?.CreateBm25Indexes ?? false;
    }

    /// <summary>
    /// Gets or sets the default embedding generator to use when generating vectors embeddings with this vector store.
    /// </summary>
    public IEmbeddingGenerator? EmbeddingGenerator { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the collections of this vector store, when created, also create a BM25 index for each
    /// string property with full-text indexing, which hybrid search needs. Only Chroma Cloud has these indexes. The default is
    /// <see langword="false"/>.
    /// </summary>
    /// <seealso cref="ChromaCollectionOptions.CreateBm25Indexes"/>
    public bool CreateBm25Indexes { get; set; }
}
