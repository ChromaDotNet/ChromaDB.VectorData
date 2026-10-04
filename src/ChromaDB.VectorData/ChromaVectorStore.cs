// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ChromaDB.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Shared.Diagnostics;

namespace ChromaDB.VectorData;

/// <summary>
/// Class for accessing the list of collections in a Chroma vector store.
/// </summary>
/// <remarks>
/// This class can be used with collections of any schema type, but requires you to provide schema information when getting a collection.
/// </remarks>
public sealed class ChromaVectorStore : VectorStore
{
    /// <summary>Metadata about vector store.</summary>
    private readonly VectorStoreMetadata _metadata;

    /// <summary>Chroma client that can be used to manage the collections and records in a Chroma store.</summary>
    private readonly MockableChromaClient _chromaClient;

    /// <summary>A general purpose definition that can be used to construct a collection when needing to proxy schema agnostic operations.</summary>
    private static readonly VectorStoreCollectionDefinition s_generalPurposeDefinition = new() { Properties = [new VectorStoreKeyProperty("Key", typeof(string)), new VectorStoreVectorProperty("Vector", typeof(ReadOnlyMemory<float>), 1)] };

    private readonly IEmbeddingGenerator? _embeddingGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaVectorStore"/> class.
    /// </summary>
    /// <param name="chromaOptions">The options used to connect to Chroma.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send the requests to Chroma.</param>
    /// <param name="ownsClient">A value indicating whether <paramref name="httpClient"/> is disposed after the vector store is disposed.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public ChromaVectorStore(ChromaConfigurationOptions chromaOptions, HttpClient httpClient, bool ownsClient, ChromaVectorStoreOptions? options = default)
        : this(new MockableChromaClient(chromaOptions, httpClient, ownsClient), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaVectorStore"/> class.
    /// </summary>
    /// <param name="chromaClient">The Chroma client, for example from the dependency injection container. The vector store does not dispose it.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    public ChromaVectorStore(ChromaClient chromaClient, ChromaVectorStoreOptions? options = default)
        : this(new MockableChromaClient(chromaClient), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaVectorStore"/> class.
    /// </summary>
    /// <param name="chromaClient">Chroma client that can be used to manage the collections and records in a Chroma store.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    internal ChromaVectorStore(MockableChromaClient chromaClient, ChromaVectorStoreOptions? options = default)
    {
        Throw.IfNull(chromaClient);

        _chromaClient = chromaClient;

        options ??= ChromaVectorStoreOptions.Default;
        _embeddingGenerator = options.EmbeddingGenerator;

        _metadata = new()
        {
            VectorStoreSystemName = ChromaConstants.VectorStoreSystemName,
            VectorStoreName = chromaClient.DatabaseName
        };
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _chromaClient.Dispose();
        base.Dispose(disposing);
    }

#pragma warning disable IDE0090 // Use 'new(...)'
    /// <inheritdoc />
    [RequiresDynamicCode("This overload of GetCollection() is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, call GetDynamicCollection() instead.")]
    [RequiresUnreferencedCode("This overload of GetCollection() is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, call GetDynamicCollection() instead.")]
#if NET
    public override ChromaCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
#else
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
#endif
        => typeof(TRecord) == typeof(Dictionary<string, object?>)
            ? throw new ArgumentException(VectorDataStrings.GetCollectionWithDictionaryNotSupported)
            : new ChromaCollection<TKey, TRecord>(_chromaClient.Share, name, new()
            {
                Definition = definition,
                EmbeddingGenerator = _embeddingGenerator
            });

    /// <inheritdoc />
#if NET
    public override ChromaDynamicCollection GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
#else
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
#endif
        => new ChromaDynamicCollection(_chromaClient.Share, name, new ChromaCollectionOptions()
        {
            Definition = definition,
            EmbeddingGenerator = _embeddingGenerator
        });
#pragma warning restore IDE0090

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collections = await VectorStoreErrorHandler.RunOperationAsync<IReadOnlyList<string>, HttpRequestException>(
            _metadata,
            "ListCollections",
            () => VectorStoreErrorHandler.RunOperationAsync<IReadOnlyList<string>, ChromaException>(
                _metadata,
                "ListCollections",
                () => _chromaClient.ListCollectionsAsync(cancellationToken))).ConfigureAwait(false);

        foreach (var collection in collections)
        {
            yield return collection;
        }
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var collection = GetDynamicCollection(name, s_generalPurposeDefinition);
        return collection.CollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        var collection = GetDynamicCollection(name, s_generalPurposeDefinition);
        return collection.EnsureCollectionDeletedAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Throw.IfNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreMetadata) ? _metadata :
            serviceType == typeof(ChromaClient) ? _chromaClient.ChromaClient :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }
}
