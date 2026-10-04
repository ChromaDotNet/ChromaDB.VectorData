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
        HasNamedVectors = source?.HasNamedVectors ?? Default.HasNamedVectors;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the vectors in the store are named and multiple vectors are supported, or whether there is just a single unnamed vector per qdrant point.
    /// Defaults to single vector per point.
    /// </summary>
    public bool HasNamedVectors { get; set; }
}
