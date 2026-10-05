// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using Microsoft.Shared.Diagnostics;

namespace ChromaDB.VectorData;

/// <summary>
/// The <see cref="ChromaClient"/> of a vector store or of a collection, which reads metadata exactly. A vector store shares it with
/// the collections it returns, and the <see cref="HttpClient"/> it owns is disposed when the last of them is disposed.
/// </summary>
internal sealed class SharedChromaClient : IDisposable
{
    private readonly HttpClient? _ownedHttpClient;
    private int _referenceCount = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedChromaClient"/> class with a client of its own.
    /// </summary>
    /// <param name="options">The options used to connect to Chroma.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send the requests to Chroma.</param>
    /// <param name="ownsClient">A value indicating whether <paramref name="httpClient"/> is disposed with the last of the vector store and its collections.</param>
    public SharedChromaClient(ChromaConfigurationOptions options, HttpClient httpClient, bool ownsClient)
    {
        Throw.IfNull(options);
        Throw.IfNull(httpClient);

        // Strings in metadata stay strings, and lists come back as lists of values, not as JSON.
        Client = new ChromaClient(options.WithMetadataValues(ChromaMetadataValues.Exact), httpClient);
        _ownedHttpClient = ownsClient ? httpClient : null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedChromaClient"/> class with a client that the caller owns.
    /// </summary>
    /// <param name="chromaClient">The Chroma client, for example from the dependency injection container.</param>
    public SharedChromaClient(ChromaClient chromaClient)
    {
        Throw.IfNull(chromaClient);

        // A client with the same HttpClient and options, which reads metadata exactly also when the one of the caller does not.
        Client = chromaClient.WithMetadataValues(ChromaMetadataValues.Exact);
    }

    /// <summary>
    /// Gets the client, which reads metadata exactly.
    /// </summary>
    public ChromaClient Client { get; }

    /// <summary>
    /// Gets the database the client works in, or <see langword="null"/> for the default database of the server.
    /// </summary>
    public string? DatabaseName => Client.Options.Database;

    /// <summary>
    /// Gets this client for one more user, a collection of the vector store, which disposes it too.
    /// </summary>
    public SharedChromaClient Share()
    {
        if (_ownedHttpClient is not null)
        {
            Interlocked.Increment(ref _referenceCount);
        }

        return this;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownedHttpClient is not null && Interlocked.Decrement(ref _referenceCount) == 0)
        {
            _ownedHttpClient.Dispose();
        }
    }
}
