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
        // IsFullTextIndexed is accepted and has no effect, like IsIndexed: Chroma indexes every metadata field for filtering,
        // and the provider has no hybrid search, the only operation that uses a full-text index.
        var definition = ChromaCollectionCreateMapping.MapCollectionDefinition(Name, _model.VectorProperty);

        var collection = await RunOperationAsync(
            "EnsureCollectionExists",
            () => _chromaClient.GetOrCreateCollectionAsync(definition, cancellationToken)).ConfigureAwait(false);

        // An existing collection keeps its space, which can differ from the one of the definition.
        _chromaCollection = VerifySpace(collection);
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

        var ids = keys.Select(key => ChromaFieldMapping.ToId(key)).ToList();
        if (ids.Count == 0)
        {
            yield break;
        }

        var includeVectors = options?.IncludeVectors ?? false;
        if (includeVectors && _model.EmbeddingGenerationRequired)
        {
            throw new NotSupportedException(VectorDataStrings.IncludeVectorsNotSupportedWithEmbeddingGeneration);
        }

        foreach (var page in GetPages(ids, _chromaClient.ReadPageSize))
        {
            var entries = await RunOperationAsync(
                OperationName,
                () => RunOnCollectionAsync(collection => _chromaClient.GetAsync(
                    collection,
                    page,
                    where: null,
                    whereDocument: null,
                    limit: null,
                    offset: null,
                    GetInclude(includeVectors),
                    cancellationToken), cancellationToken)).ConfigureAwait(false);

            foreach (var entry in entries)
            {
                yield return _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, entry.Document, includeVectors);
            }
        }
    }

    private static IEnumerable<List<string>> GetPages(List<string> ids, int? pageSize)
    {
        if (pageSize is not { } size || ids.Count <= size)
        {
            yield return ids;
            yield break;
        }

        for (var start = 0; start < ids.Count; start += size)
        {
            yield return ids.GetRange(start, Math.Min(size, ids.Count - start));
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

        var ids = keys.Select(key => ChromaFieldMapping.ToId(key)).ToList();
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return RunOperationAsync(
            DeleteName,
            () => RunOnCollectionAsync(collection => _chromaClient.DeleteAsync(
                collection,
                ids,
                cancellationToken), cancellationToken));
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
        var documents = new List<string>();
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
            documents.Add(storageRecord.Document!);
            hasMetadata |= storageRecord.Metadata is not null;
        }

        if (ids.Count == 0)
        {
            return;
        }

        await RunOperationAsync(
            UpsertName,
            () => RunOnCollectionAsync(collection => _chromaClient.UpsertAsync(
                collection,
                ids,
                embeddings,
                hasMetadata ? metadatas : null,
                _mapper.HasDocument ? documents : null,
                cancellationToken), cancellationToken)).ConfigureAwait(false);
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

        var filter = options.Filter is not null
            ? new ChromaFilterTranslator().Translate(options.Filter, _model)
            : ChromaFilter.All;
        if (filter.MatchesNothing)
        {
            yield break;
        }

        var include = ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances;
        if (_mapper.HasDocument)
        {
            include |= ChromaQueryInclude.Documents;
        }
        if (options.IncludeVectors)
        {
            include |= ChromaQueryInclude.Embeddings;
        }

        // Chroma has no offset in queries: ask for the skipped records too, and drop them here.
        var entries = await RunOperationAsync(
            "Query",
            () => RunOnCollectionAsync(collection => _chromaClient.QueryAsync(
                collection,
                vector,
                top + options.Skip,
                filter.Where,
                filter.WhereDocument,
                filter.Ids,
                include,
                cancellationToken), cancellationToken)).ConfigureAwait(false);

        foreach (var entry in entries.Skip(options.Skip))
        {
            var score = ChromaCollectionSearchMapping.ToScore(entry.Distance!.Value, vectorProperty.DistanceFunction);
            if (!ChromaCollectionSearchMapping.PassesThreshold(score, options.ScoreThreshold, vectorProperty.DistanceFunction))
            {
                continue;
            }

            yield return new VectorSearchResult<TRecord>(
                _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, entry.Document, options.IncludeVectors),
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

        var chromaFilter = new ChromaFilterTranslator().Translate(filter, _model);
        if (chromaFilter.MatchesNothing)
        {
            yield break;
        }

        // Read in pages of the read page size, if any: Chroma Cloud returns at most 300 records per request.
        var pageSize = _chromaClient.ReadPageSize ?? top;
        for (var read = 0; read < top;)
        {
            var limit = Math.Min(pageSize, top - read);
            var entries = await RunOperationAsync(
                "Get",
                () => RunOnCollectionAsync(collection => _chromaClient.GetAsync(
                    collection,
                    chromaFilter.Ids,
                    chromaFilter.Where,
                    chromaFilter.WhereDocument,
                    limit,
                    offset: options.Skip + read,
                    GetInclude(options.IncludeVectors),
                    cancellationToken), cancellationToken)).ConfigureAwait(false);

            foreach (var entry in entries)
            {
                yield return _mapper.MapFromStorageToDataModel(entry.Id, entry.Embeddings, entry.Metadata, entry.Document, options.IncludeVectors);
            }

            read += entries.Count;
            if (entries.Count < limit)
            {
                break;
            }
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

    private ChromaGetInclude GetInclude(bool includeVectors)
        => ChromaGetInclude.Metadatas
            | (includeVectors ? ChromaGetInclude.Embeddings : 0)
            | (_mapper.HasDocument ? ChromaGetInclude.Documents : 0);

    /// <summary>
    /// Get the Chroma collection, reading it the first time; record operations need its id.
    /// </summary>
    /// <summary>
    /// Run an operation on the Chroma collection. Its id is kept after the first lookup, and a collection deleted and created
    /// again elsewhere has a new id: when Chroma no longer finds the kept id, the collection is looked up again by name, once.
    /// </summary>
    private async Task<T> RunOnCollectionAsync<T>(Func<ChromaCollection, Task<T>> operation, CancellationToken cancellationToken)
    {
        var wasKept = _chromaCollection is not null;
        try
        {
            return await operation(await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (ChromaException exception) when (wasKept && IsCollectionNotFound(exception))
        {
            _chromaCollection = null;
            return await operation(await GetChromaCollectionAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }

    private Task RunOnCollectionAsync(Func<ChromaCollection, Task> operation, CancellationToken cancellationToken)
        => RunOnCollectionAsync<bool>(async collection =>
        {
            await operation(collection).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    private static bool IsCollectionNotFound(ChromaException exception)
        => exception.ErrorType == "NotFoundError" || exception.StatusCode == System.Net.HttpStatusCode.NotFound;

    private async Task<ChromaCollection> GetChromaCollectionAsync(CancellationToken cancellationToken)
        => _chromaCollection ??= VerifySpace(await _chromaClient.GetCollectionAsync(Name, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Check that the collection uses the space of the distance function of the vector property: the scores are computed from
    /// the distances that Chroma returns, so the distances of another space, like the l2 that Chroma uses by default, would give
    /// wrong scores. A collection whose space Chroma does not report is accepted.
    /// </summary>
    private ChromaCollection VerifySpace(ChromaCollection collection)
    {
        var expectedSpace = ChromaCollectionCreateMapping.GetSpace(_model.VectorProperty);

        return collection.Space is { } space && space != expectedSpace
            ? throw new InvalidOperationException(
                $"The Chroma collection '{Name}' uses the space '{space}', but the distance function '{_model.VectorProperty.DistanceFunction ?? DistanceFunction.CosineSimilarity}' " +
                $"of the vector property '{_model.VectorProperty.ModelName}' needs the space '{expectedSpace}'. " +
                $"Use a distance function of the space '{space}', or another collection.")
            : collection;
    }

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
