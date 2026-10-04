// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using ChromaDB.Client.Models;
using Microsoft.Shared.Diagnostics;

namespace ChromaDB.VectorData;

/// <summary>
/// Decorator class for <see cref="ChromaClient"/> and <see cref="ChromaCollectionClient"/> that exposes the required methods as virtual allowing for mocking in unit tests.
/// </summary>
internal class MockableChromaClient : IDisposable
{
    private readonly HttpClient? _ownedHttpClient;
    private readonly ChromaClient _chromaClient;
    private int _referenceCount = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockableChromaClient"/> class.
    /// </summary>
    /// <param name="options">The options used to connect to Chroma.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send the requests to Chroma.</param>
    /// <param name="ownsClient">A value indicating whether <paramref name="httpClient"/> is disposed when the vector store is disposed.</param>
    public MockableChromaClient(ChromaConfigurationOptions options, HttpClient httpClient, bool ownsClient = true)
    {
        Throw.IfNull(options);
        Throw.IfNull(httpClient);

        // Strings in metadata stay strings, and lists come back as lists of values, not as JSON.
        _chromaClient = new ChromaClient(options.WithMetadataValues(ChromaMetadataValues.Exact), httpClient);
        _ownedHttpClient = ownsClient ? httpClient : null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockableChromaClient"/> class with a client that the caller owns.
    /// </summary>
    /// <param name="chromaClient">The Chroma client, for example from the dependency injection container.</param>
    public MockableChromaClient(ChromaClient chromaClient)
    {
        Throw.IfNull(chromaClient);

        // A client with the same HttpClient and options, which reads metadata exactly.
        _chromaClient = chromaClient.WithMetadataValues(ChromaMetadataValues.Exact);
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Constructor for mocking purposes only.
    /// </summary>
    internal MockableChromaClient()
    {
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Gets the internal <see cref="ChromaClient"/> that this mockable instance wraps.
    /// </summary>
    public ChromaClient ChromaClient => _chromaClient;

    /// <summary>
    /// Gets the database the client works in, or <see langword="null"/> for the default database of the server.
    /// </summary>
    public string? DatabaseName => _chromaClient?.Options.Database;

    public void Dispose()
    {
        if (_ownedHttpClient is not null && Interlocked.Decrement(ref _referenceCount) == 0)
        {
            _ownedHttpClient.Dispose();
        }
    }

    /// <summary>
    /// Check if a collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
        => _chromaClient.CollectionExists(collectionName, cancellationToken: cancellationToken);

    /// <summary>
    /// Get a collection, creating it from the given definition if it does not exist.
    /// </summary>
    /// <param name="definition">The name and configuration of the collection; the configuration is used only when the collection is created.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<ChromaCollection> GetOrCreateCollectionAsync(ChromaCollectionDefinition definition, CancellationToken cancellationToken = default)
        => _chromaClient.GetOrCreateCollection(definition, cancellationToken: cancellationToken);

    /// <summary>
    /// Get a collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<ChromaCollection> GetCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        => _chromaClient.GetCollection(collectionName, cancellationToken: cancellationToken);

    /// <summary>
    /// Delete a collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
        => _chromaClient.DeleteCollection(collectionName, cancellationToken: cancellationToken);

    /// <summary>
    /// List the names of the collections.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _chromaClient.ListCollections(cancellationToken: cancellationToken).ConfigureAwait(false);
        return collections.Select(collection => collection.Name).ToList();
    }

    /// <summary>
    /// Get the records of a collection by their ids, by a filter, or both.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="ids">The ids of the records, or <see langword="null"/> for any record.</param>
    /// <param name="where">The metadata filter, or <see langword="null"/> for no filter.</param>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <param name="offset">The number of records to skip.</param>
    /// <param name="include">The fields to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<List<ChromaCollectionEntry>> GetAsync(
        ChromaCollection collection,
        List<string>? ids,
        ChromaWhereOperator? where,
        int? limit,
        int? offset,
        ChromaGetInclude include,
        CancellationToken cancellationToken = default)
        => GetCollectionClient(collection).Get(ids, where, whereDocument: null, limit, offset, include, cancellationToken);

    /// <summary>
    /// Find the records nearest to a vector.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="queryEmbedding">The vector to search for.</param>
    /// <param name="nResults">The number of records to return.</param>
    /// <param name="where">The metadata filter, or <see langword="null"/> for no filter.</param>
    /// <param name="ids">The ids the records must have, or <see langword="null"/> for any id.</param>
    /// <param name="include">The fields to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual async Task<List<ChromaCollectionQueryEntry>> QueryAsync(
        ChromaCollection collection,
        ReadOnlyMemory<float> queryEmbedding,
        int nResults,
        ChromaWhereOperator? where,
        List<string>? ids,
        ChromaQueryInclude include,
        CancellationToken cancellationToken = default)
    {
        var results = await GetCollectionClient(collection).Query(
            new ChromaQuery([queryEmbedding]) { NResults = nResults, Where = where, Ids = ids, Include = include },
            cancellationToken).ConfigureAwait(false);

        return results[0];
    }

    /// <summary>
    /// Insert or update records.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="ids">The ids of the records.</param>
    /// <param name="embeddings">The vectors of the records, in the same order as <paramref name="ids"/>.</param>
    /// <param name="metadatas">The metadata of the records, in the same order as <paramref name="ids"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task UpsertAsync(
        ChromaCollection collection,
        List<string> ids,
        List<ReadOnlyMemory<float>> embeddings,
        List<Dictionary<string, object>>? metadatas,
        CancellationToken cancellationToken = default)
        => GetCollectionClient(collection).Upsert(ids, embeddings, metadatas, documents: null, cancellationToken);

    /// <summary>
    /// Delete records by their ids.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="ids">The ids of the records.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task DeleteAsync(ChromaCollection collection, List<string> ids, CancellationToken cancellationToken = default)
        => GetCollectionClient(collection).Delete(ids, cancellationToken: cancellationToken);

    internal MockableChromaClient Share()
    {
        if (_ownedHttpClient is not null)
        {
            Interlocked.Increment(ref _referenceCount);
        }

        return this;
    }

    // The collection clients of one ChromaClient share what it keeps, like the server version.
    private ChromaCollectionClient GetCollectionClient(ChromaCollection collection)
        => _chromaClient.GetCollectionClient(collection);
}
