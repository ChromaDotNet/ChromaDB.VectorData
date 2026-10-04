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
    private readonly ChromaConfigurationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ChromaClient _chromaClient;
    private readonly bool _ownsClient;
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
        _options = options.WithMetadataValues(ChromaMetadataValues.Exact);
        _httpClient = httpClient;
        _chromaClient = new ChromaClient(_options, httpClient);
        _ownsClient = ownsClient;
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

    public void Dispose()
    {
        if (_ownsClient)
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                _httpClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Check if a collection exists.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var collections = await _chromaClient.ListCollections(cancellationToken: cancellationToken).ConfigureAwait(false);
        return collections.Any(collection => collection.Name == collectionName);
    }

    /// <summary>
    /// Get a collection, creating it with the given metadata if it does not exist.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="metadata">The metadata of the collection, used only when it is created.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<ChromaCollection> GetOrCreateCollectionAsync(string collectionName, Dictionary<string, object>? metadata, CancellationToken cancellationToken = default)
        => _chromaClient.GetOrCreateCollection(collectionName, metadata, cancellationToken: cancellationToken);

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
    /// <param name="include">The fields to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public virtual Task<List<ChromaCollectionQueryEntry>> QueryAsync(
        ChromaCollection collection,
        ReadOnlyMemory<float> queryEmbedding,
        int nResults,
        ChromaWhereOperator? where,
        ChromaQueryInclude include,
        CancellationToken cancellationToken = default)
        => GetCollectionClient(collection).Query(queryEmbedding, nResults, where, whereDocument: null, include, cancellationToken);

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
        if (_ownsClient)
        {
            Interlocked.Increment(ref _referenceCount);
        }

        return this;
    }

    private ChromaCollectionClient GetCollectionClient(ChromaCollection collection)
        => new(collection, _options, _httpClient);
}
