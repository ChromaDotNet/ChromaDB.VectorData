// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using ChromaDB.Client;
using ChromaDB.Client.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Shared.Diagnostics;

namespace ChromaDB.VectorData;

/// <summary>
/// Service for storing and retrieving vector records, that uses Chroma as the underlying storage.
/// </summary>
/// <typeparam name="TKey">The data type of the record key. Can be either <see cref="string"/> or <see cref="Guid"/>.</typeparam>
/// <typeparam name="TRecord">The data model to use for adding, updating and retrieving data from storage.</typeparam>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public class ChromaCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    /// <summary>Metadata about vector store record collection.</summary>
    private readonly VectorStoreCollectionMetadata _collectionMetadata;

    /// <summary>The default options for vector search.</summary>
    private static readonly VectorSearchOptions<TRecord> s_defaultVectorSearchOptions = new();

    /// <summary>The name of the upsert operation for telemetry purposes.</summary>
    private const string UpsertName = "Upsert";

    /// <summary>The name of the Delete operation for telemetry purposes.</summary>
    private const string DeleteName = "Delete";

    /// <summary>Chroma client that can be used to manage the collections and records in a Chroma store.</summary>
    private readonly MockableChromaClient _chromaClient;

    /// <summary>The model for this collection.</summary>
    private readonly CollectionModel _model;

    /// <summary>A mapper to use for converting between Chroma records and consumer models.</summary>
    private readonly ChromaMapper<TRecord> _mapper;

    /// <summary>The Chroma collection, once it has been read or created.</summary>
    private ChromaCollection? _chromaCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaCollection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="chromaOptions">The options used to connect to Chroma.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> used to send the requests to Chroma.</param>
    /// <param name="name">The name of the collection that this <see cref="ChromaCollection{TKey, TRecord}"/> will access.</param>
    /// <param name="ownsClient">A value indicating whether <paramref name="httpClient"/> is disposed when the collection is disposed.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="chromaOptions"/> or <paramref name="httpClient"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown for any misconfigured options.</exception>
    [RequiresDynamicCode("This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead.")]
    [RequiresUnreferencedCode("This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead")]
    public ChromaCollection(ChromaConfigurationOptions chromaOptions, HttpClient httpClient, string name, bool ownsClient, ChromaCollectionOptions? options = null)
        : this(() => new MockableChromaClient(chromaOptions, httpClient, ownsClient), name, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaCollection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="chromaClient">The Chroma client, for example from the dependency injection container. The collection does not dispose it.</param>
    /// <param name="name">The name of the collection that this <see cref="ChromaCollection{TKey, TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="chromaClient"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown for any misconfigured options.</exception>
    [RequiresDynamicCode("This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead.")]
    [RequiresUnreferencedCode("This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead")]
    public ChromaCollection(ChromaClient chromaClient, string name, ChromaCollectionOptions? options = null)
        : this(() => new MockableChromaClient(chromaClient), name, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChromaCollection{TKey, TRecord}"/> class.
    /// </summary>
    /// <param name="clientFactory">Chroma client factory.</param>
    /// <param name="name">The name of the collection that this <see cref="ChromaCollection{TKey, TRecord}"/> will access.</param>
    /// <param name="options">Optional configuration options for this class.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="clientFactory"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown for any misconfigured options.</exception>
    [RequiresDynamicCode("This constructor is incompatible with NativeAOT. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead.")]
    [RequiresUnreferencedCode("This constructor is incompatible with trimming. For dynamic mapping via Dictionary<string, object?>, instantiate ChromaDynamicCollection instead")]
    internal ChromaCollection(Func<MockableChromaClient> clientFactory, string name, ChromaCollectionOptions? options = null)
        : this(
            clientFactory,
            name,
            static options => typeof(TRecord) == typeof(Dictionary<string, object?>)
                ? throw new NotSupportedException(VectorDataStrings.NonDynamicCollectionWithDictionaryNotSupported(typeof(ChromaDynamicCollection)))
                : new ChromaModelBuilder().Build(typeof(TRecord), typeof(TKey), options.Definition, options.EmbeddingGenerator),
            options)
    {
    }

    internal ChromaCollection(Func<MockableChromaClient> clientFactory, string name, Func<ChromaCollectionOptions, CollectionModel> modelFactory, ChromaCollectionOptions? options)
    {
        // Verify.
        Throw.IfNull(clientFactory);
        Throw.IfNullOrWhitespace(name);

        if (typeof(TKey) != typeof(string) && typeof(TKey) != typeof(Guid) && typeof(TKey) != typeof(object))
        {
            throw new NotSupportedException("Only string and Guid keys are supported.");
        }

        options ??= ChromaCollectionOptions.Default;

        // Assign.
        Name = name;
        _model = modelFactory(options);
        _mapper = new ChromaMapper<TRecord>(_model);

        // The code above can throw, so we need to create the client after the model is built and verified.
        // In case an exception is thrown, we don't need to dispose any resources.
        _chromaClient = clientFactory();

        _collectionMetadata = new()
        {
            VectorStoreSystemName = ChromaConstants.VectorStoreSystemName,
            VectorStoreName = _chromaClient.DatabaseName,
            CollectionName = name
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _chromaClient.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        => RunOperationAsync(
            "CollectionExists",
            () => _chromaClient.CollectionExistsAsync(Name, cancellationToken));

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        // Chroma indexes every metadata field for filtering; full-text search is on documents only.
        if (_model.DataProperties.FirstOrDefault(p => p.IsFullTextIndexed) is { } fullTextProperty)
        {
            throw new NotSupportedException($"Property {nameof(VectorStoreDataProperty.IsFullTextIndexed)} on {nameof(VectorStoreDataProperty)} '{fullTextProperty.ModelName}' is set to true, but the Chroma VectorStore does not support full-text search on data properties.");
        }

        var definition = ChromaCollectionCreateMapping.MapCollectionDefinition(Name, _model.VectorProperty);

        _chromaCollection = await RunOperationAsync(
            "EnsureCollectionExists",
            () => _chromaClient.GetOrCreateCollectionAsync(definition, cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
        => RunOperationAsync("DeleteCollection",
            async () =>
            {
                _chromaCollection = null;

                if (await _chromaClient.CollectionExistsAsync(Name, cancellationToken).ConfigureAwait(false))
                {
                    await _chromaClient.DeleteCollectionAsync(Name, cancellationToken).ConfigureAwait(false);
                }
            });

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(key);

        var records = await GetAsync([key], options, cancellationToken).ToListAsync(cancellationToken).ConfigureAwait(false);
        return records.FirstOrDefault();
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string OperationName = "Get";

        Throw.IfNull(keys);

        var ids = keys.Select(key => ChromaMapper<TRecord>.ToId(key)).ToList();
        if (ids.Count == 0)
        {
            yield break;
        }

        var includeVectors = options?.IncludeVectors ?? false;
        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        var entries = await RunOperationAsync(
            OperationName,
            async () => await _chromaClient.GetAsync(
                await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false),
                ids,
                where: null,
                limit: null,
                offset: null,
                GetInclude(includeVectors),
                cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            yield return _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, includeVectors);
        }
    }

    /// <inheritdoc />
    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(key);

        return DeleteAsync([key], cancellationToken);
    }

    /// <inheritdoc />
    public override Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(keys);

        var ids = keys.Select(key => ChromaMapper<TRecord>.ToId(key)).ToList();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return RunOperationAsync(
            DeleteName,
            async () => await _chromaClient.DeleteAsync(
                await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false),
                ids,
                cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(record);

        await UpsertAsync([record], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        Throw.IfNull(records);

        GeneratedEmbeddings<Embedding<float>>?[]? generatedEmbeddings = null;

        var vectorProperty = _model.VectorProperty;
        if (!ChromaModelBuilder.IsVectorPropertyTypeValidCore(vectorProperty.Type, out _))
        {
            // The vector property's type isn't natively supported - we need to generate embeddings.
            Debug.Assert(vectorProperty.EmbeddingGenerator is not null);

            var recordsList = records is IReadOnlyList<TRecord> r ? r : records.ToList();
            if (recordsList.Count == 0)
            {
                return;
            }

            records = recordsList;
            generatedEmbeddings = [(GeneratedEmbeddings<Embedding<float>>)await vectorProperty.GenerateEmbeddingsAsync(records.Select(r => vectorProperty.GetValueAsObject(r)), cancellationToken).ConfigureAwait(false)];
        }

        // Create the Chroma records.
        var keyProperty = _model.KeyProperty;
        var ids = new List<string>();
        var embeddings = new List<ReadOnlyMemory<float>>();
        var metadatas = new List<Dictionary<string, object>>();
        var hasMetadata = false;
        var recordIndex = 0;
        foreach (var record in records)
        {
            if (keyProperty.IsAutoGenerated && keyProperty.GetValue<Guid>(record) == Guid.Empty)
            {
                keyProperty.SetValue(record, Guid.NewGuid());
            }

            var storageRecord = _mapper.MapFromDataToStorageModel(record, recordIndex++, generatedEmbeddings);
            ids.Add(storageRecord.Id);
            embeddings.Add(storageRecord.Embedding);
            metadatas.Add(storageRecord.Metadata!);
            hasMetadata |= storageRecord.Metadata is not null;
        }

        if (ids.Count == 0)
        {
            return;
        }

        await RunOperationAsync(
            UpsertName,
            async () => await _chromaClient.UpsertAsync(
                await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false),
                ids,
                embeddings,
                hasMetadata ? metadatas : null,
                cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    #region Search

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfNull(searchValue);
        Throw.IfLessThan(top, 1);

        options ??= s_defaultVectorSearchOptions;
        if (options.IncludeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        var vectorProperty = _model.GetVectorPropertyOrSingle(options);
        var vector = await GetSearchVectorAsync(searchValue, vectorProperty, cancellationToken).ConfigureAwait(false);

        var where = options.Filter is not null
            ? new ChromaFilterTranslator().Translate(options.Filter, _model)
            : null;

        var include = ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances;
        if (options.IncludeVectors)
        {
            include |= ChromaQueryInclude.Embeddings;
        }

        // Chroma has no offset in queries: ask for the skipped records too, and drop them here.
        var entries = await RunOperationAsync(
            "Query",
            async () => await _chromaClient.QueryAsync(
                await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false),
                vector,
                top + options.Skip,
                where,
                include,
                cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        foreach (var entry in entries.Skip(options.Skip))
        {
            var score = ChromaCollectionSearchMapping.ToScore(entry.Distance!.Value, vectorProperty.DistanceFunction);
            if (!ChromaCollectionSearchMapping.PassesThreshold(score, options.ScoreThreshold, vectorProperty.DistanceFunction))
            {
                continue;
            }

            yield return new VectorSearchResult<TRecord>(
                _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, options.IncludeVectors),
                score);
        }
    }

    private static async ValueTask<ReadOnlyMemory<float>> GetSearchVectorAsync<TInput>(TInput searchValue, VectorPropertyModel vectorProperty, CancellationToken cancellationToken)
        where TInput : notnull
        => searchValue switch
        {
            float[] array => array,
            ReadOnlyMemory<float> r => r,
            Embedding<float> e => e.Vector,
            _ when vectorProperty.EmbeddingGenerationDispatcher is not null
                => ((Embedding<float>)await vectorProperty.GenerateEmbeddingAsync(searchValue, cancellationToken).ConfigureAwait(false)).Vector,

            _ => vectorProperty.EmbeddingGenerator is null
                ? throw new NotSupportedException(VectorDataStrings.InvalidSearchInputAndNoEmbeddingGeneratorWasConfigured(searchValue.GetType(), ChromaModelBuilder.SupportedVectorTypes))
                : throw new InvalidOperationException(VectorDataStrings.IncompatibleEmbeddingGeneratorWasConfiguredForInputType(typeof(TInput), vectorProperty.EmbeddingGenerator.GetType()))
        };

    #endregion Search

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter, int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Throw.IfNull(filter);
        Throw.IfLessThan(top, 1);

        options ??= new();

        if (options.OrderBy?.Invoke(new()).Values is { Count: > 0 })
        {
            throw new NotSupportedException("Chroma does not support ordering.");
        }

        var where = new ChromaFilterTranslator().Translate(filter, _model);

        var entries = await RunOperationAsync(
            "Get",
            async () => await _chromaClient.GetAsync(
                await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false),
                ids: null,
                where,
                limit: top,
                offset: options.Skip,
                GetInclude(options.IncludeVectors),
                cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);

        foreach (var entry in entries)
        {
            yield return _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, options.IncludeVectors);
        }
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        Throw.IfNull(serviceType);

        return
            serviceKey is not null ? null :
            serviceType == typeof(VectorStoreCollectionMetadata) ? _collectionMetadata :
            serviceType == typeof(ChromaClient) ? _chromaClient.ChromaClient :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    private static ChromaGetInclude GetInclude(bool includeVectors)
        => includeVectors
            ? ChromaGetInclude.Metadatas | ChromaGetInclude.Embeddings
            : ChromaGetInclude.Metadatas;

    /// <summary>
    /// Get the Chroma collection, reading it the first time; record operations need its id.
    /// </summary>
    private async Task<ChromaCollection> GetChromaCollectionAsync(CancellationToken cancellationToken)
        => _chromaCollection ??= await _chromaClient.GetCollectionAsync(Name, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Run the given operation and wrap any <see cref="ChromaException"/> or <see cref="HttpRequestException"/> with <see cref="VectorStoreException"/>.
    /// </summary>
    /// <param name="operationName">The type of database operation being run.</param>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The result of the operation.</returns>
    private Task RunOperationAsync(string operationName, Func<Task> operation)
        => VectorStoreErrorHandler.RunOperationAsync<HttpRequestException>(
            _collectionMetadata,
            operationName,
            () => VectorStoreErrorHandler.RunOperationAsync<ChromaException>(_collectionMetadata, operationName, operation));

    /// <summary>
    /// Run the given operation and wrap any <see cref="ChromaException"/> or <see cref="HttpRequestException"/> with <see cref="VectorStoreException"/>.
    /// </summary>
    /// <typeparam name="T">The response type of the operation.</typeparam>
    /// <param name="operationName">The type of database operation being run.</param>
    /// <param name="operation">The operation to run.</param>
    /// <returns>The result of the operation.</returns>
    private Task<T> RunOperationAsync<T>(string operationName, Func<Task<T>> operation)
        => VectorStoreErrorHandler.RunOperationAsync<T, HttpRequestException>(
            _collectionMetadata,
            operationName,
            () => VectorStoreErrorHandler.RunOperationAsync<T, ChromaException>(_collectionMetadata, operationName, operation));
}
