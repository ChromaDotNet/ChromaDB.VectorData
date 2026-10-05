// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq.Expressions;
using ChromaDB.Client;
using Microsoft.Extensions.VectorData;
using Testcontainers.Chroma;
using VectorData.ConformanceTests.Support;

namespace ChromaDB.VectorData.ConformanceTests.Support;

#pragma warning disable CA1001 // Type owns disposable fields but is not disposable

internal sealed class ChromaTestStore : TestStore
{
    // CHROMA_TEST_URI runs the tests against a server already running, like Chroma Cloud, with the same variables
    // as the tests of ChromaDotNet.Client: CHROMA_TEST_TOKEN goes in X-Chroma-Token, CHROMA_TEST_TENANT and
    // CHROMA_TEST_DATABASE are used as they are, CHROMA_TEST_MAX_BATCH_SIZE sets the size of the batches, which the client
    // chooses by itself on Chroma Cloud.
    // Declared before Instance, which reads it when it is created.
    private static readonly string? s_testUri = Variable("CHROMA_TEST_URI");

    public static ChromaTestStore Instance { get; } = new();

    // Chroma indexes vectors with HNSW only
    public override string DefaultIndexKind => IndexKind.Hnsw;

    // Otherwise CHROMA_IMAGE runs the tests against another Chroma release, e.g. chromadb/chroma:1.5.0.
    private readonly ChromaContainer? _container = s_testUri is null
        ? new ChromaBuilder(Variable("CHROMA_IMAGE") ?? "chromadb/chroma:1.5.9").Build()
        : null;

    private HttpClient? _httpClient;

    // On a server already running, the collections that exist before the tests, so that only the ones the tests create are deleted.
    private HashSet<string>? _collectionsBefore;

    /// <summary>
    /// Chroma normalizes the vectors of cosine collections, so the vectors it returns
    /// can differ from the upserted ones in the last digits; we can only check that
    /// a vector was returned.
    /// </summary>
    public override bool VectorsComparable => false;

    public ChromaConfigurationOptions ChromaOptions { get; private set; } = null!;

    public HttpClient HttpClient => this._httpClient ?? throw new InvalidOperationException("Not initialized");

    /// <summary>
    /// Whether the tests run against Chroma Cloud, the only Chroma with the Search API and the sparse vector indexes that hybrid search needs.
    /// </summary>
    public static bool IsChromaCloud => s_testUri is not null && new Uri(s_testUri).Host.EndsWith(".trychroma.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a collection that creates a BM25 index for each string property with full-text indexing.
    /// </summary>
    public ChromaCollection<string, TRecord> CreateCollectionWithBm25Indexes<TRecord>(string name, VectorStoreCollectionDefinition definition)
        where TRecord : class
        => new(new ChromaClient(this.ChromaOptions, this.HttpClient), name, new() { Definition = definition, CreateBm25Indexes = true });

    public ChromaVectorStore GetVectorStore(ChromaVectorStoreOptions options)
        => new(this.ChromaOptions,
            this.HttpClient,
            ownsClient: false, // The client is shared, it's not owned by the vector store.
            new()
            {
                EmbeddingGenerator = options.EmbeddingGenerator
            });

    /// <summary>
    /// When the records don't appear in the vector search, also tells how many of them a filtered get returns:
    /// a write that didn't reach Chroma and a vector search that misses records Chroma holds fail the same way otherwise.
    /// </summary>
    public override async Task WaitForDataAsync<TKey, TRecord>(
        VectorStoreCollection<TKey, TRecord> collection,
        int recordCount,
        Expression<Func<TRecord, bool>>? filter = null,
        Expression<Func<TRecord, object?>>? vectorProperty = null,
        int? vectorSize = null,
        object? dummyVector = null)
    {
        try
        {
            await base.WaitForDataAsync(collection, recordCount, filter, vectorProperty, vectorSize, dummyVector);
        }
        catch (InvalidOperationException exception)
        {
            var stored = await collection.GetAsync(filter ?? (_ => true), top: recordCount + 10).CountAsync();
            throw new InvalidOperationException($"{exception.Message} A filtered get returns {stored} of the {recordCount} records.", exception);
        }
    }

    private ChromaTestStore()
    {
    }

    protected override async Task StartAsync()
    {
        if (this._container is not null)
        {
            await this._container.StartAsync();
            this.ChromaOptions = new ChromaConfigurationOptions(this._container.GetConnectionString());
        }
        else
        {
            this.ChromaOptions = new ChromaConfigurationOptions(s_testUri!, Variable("CHROMA_TEST_TENANT"), Variable("CHROMA_TEST_DATABASE"));
            if (Variable("CHROMA_TEST_TOKEN") is { } token)
            {
                this.ChromaOptions = this.ChromaOptions.WithChromaToken(token, ChromaTokenTransportHeader.XChromaToken);
            }

            if (int.TryParse(Variable("CHROMA_TEST_MAX_BATCH_SIZE"), out var maxBatchSize))
            {
                this.ChromaOptions = this.ChromaOptions.WithBatchSplitting(maxBatchSize);
            }
        }

        this._httpClient = new HttpClient();
        // The vector store does not own a ChromaClient it is given; GetVectorStore covers the constructor with options.
        this.DefaultVectorStore = new ChromaVectorStore(new ChromaClient(this.ChromaOptions, this._httpClient));

        if (this._container is null)
        {
            var collections = await new ChromaClient(this.ChromaOptions, this._httpClient).ListCollectionsAsync();
            this._collectionsBefore = collections.Select(collection => collection.Name).ToHashSet();
        }
    }

    protected override async Task StopAsync()
    {
        if (this._collectionsBefore is not null)
        {
            var client = new ChromaClient(this.ChromaOptions, this.HttpClient);
            foreach (var collection in await client.ListCollectionsAsync())
            {
                if (!this._collectionsBefore.Contains(collection.Name))
                {
                    await client.DeleteCollectionAsync(collection.Name);
                }
            }
        }

        this._httpClient?.Dispose();

        if (this._container is not null)
        {
            await this._container.StopAsync();
        }
    }

    private static string? Variable(string name)
        => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
