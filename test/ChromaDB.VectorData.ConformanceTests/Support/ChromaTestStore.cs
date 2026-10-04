// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    // CHROMA_TEST_DATABASE are used as they are, CHROMA_TEST_MAX_BATCH_SIZE splits the writes.
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

    public ChromaVectorStore GetVectorStore(ChromaVectorStoreOptions options)
        => new(this.ChromaOptions,
            this.HttpClient,
            ownsClient: false, // The client is shared, it's not owned by the vector store.
            new()
            {
                EmbeddingGenerator = options.EmbeddingGenerator
            });

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
            var collections = await new ChromaClient(this.ChromaOptions, this._httpClient).ListCollections();
            this._collectionsBefore = collections.Select(collection => collection.Name).ToHashSet();
        }
    }

    protected override async Task StopAsync()
    {
        if (this._collectionsBefore is not null)
        {
            var client = new ChromaClient(this.ChromaOptions, this.HttpClient);
            foreach (var collection in await client.ListCollections())
            {
                if (!this._collectionsBefore.Contains(collection.Name))
                {
                    await client.DeleteCollection(collection.Name);
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
