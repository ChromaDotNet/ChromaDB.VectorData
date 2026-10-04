// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using ChromaDB.VectorData;
using Qdrant.Client;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register <see cref="ChromaVectorStore"/> and <see cref="ChromaCollection{TKey, TRecord}"/>  instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class ChromaServiceCollectionExtensions
{
    private const string DynamicCodeMessage = "This method is incompatible with NativeAOT, consult the documentation for adding collections in a way that's compatible with NativeAOT.";
    private const string UnreferencedCodeMessage = "This method is incompatible with trimming, consult the documentation for adding collections in a way that's compatible with NativeAOT.";

    /// <summary>
    /// Registers a <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="QdrantClient"/> returned by <paramref name="clientProvider"/>
    /// or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaVectorStore(IServiceCollection, object?, Func{IServiceProvider, QdrantClient}, Func{IServiceProvider, ChromaVectorStoreOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, QdrantClient>? clientProvider = default,
        Func<IServiceProvider, ChromaVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedChromaVectorStore(services, serviceKey: null, clientProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="QdrantClient"/> returned by <paramref name="clientProvider"/> or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="clientProvider">The <see cref="QdrantClient"/> provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="ChromaVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, QdrantClient>? clientProvider = default,
        Func<IServiceProvider, ChromaVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNull(services);

        services.Add(new ServiceDescriptor(typeof(ChromaVectorStore), serviceKey, (sp, _) =>
        {
            var client = clientProvider is null ? sp.GetRequiredService<QdrantClient>() : clientProvider(sp);
            var options = GetStoreOptions(sp, optionsProvider);

            // The client was restored from the DI container, so we do not own it.
            return new ChromaVectorStore(client, ownsClient: false, options);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaVectorStore>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="QdrantClient"/> created with <paramref name="host"/>, <paramref name="port"/>,
    /// <paramref name="https"/> and <paramref name="https"/>.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaVectorStore(IServiceCollection, object?, Func{IServiceProvider, QdrantClient}, Func{IServiceProvider, ChromaVectorStoreOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        string host,
        int port = 6334,
        bool https = true,
        string? apiKey = default,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedChromaVectorStore(services, serviceKey: null, host, port, https, apiKey, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="QdrantClient"/> created with <paramref name="host"/>, <paramref name="port"/>,
    /// <paramref name="https"/> and <paramref name="https"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to. Defaults to 6334.</param>
    /// <param name="https">Whether to encrypt the connection using HTTPS. Defaults to <c>true</c>.</param>
    /// <param name="apiKey">The API key to use.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        string host,
        int port = 6334,
        bool https = true,
        string? apiKey = default,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNullOrWhitespace(host);

        return AddKeyedChromaVectorStore(services, serviceKey, _ => new QdrantClient(host, port, https, apiKey), sp => options!, lifetime);
    }

    /// <summary>
    /// Registers a <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="QdrantClient"/> returned by <paramref name="clientProvider"/> or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, QdrantClient>? clientProvider = default,
        Func<IServiceProvider, ChromaCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey: null, name, clientProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="QdrantClient"/> returned by <paramref name="clientProvider"/> or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="clientProvider">The <see cref="QdrantClient"/> provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="ChromaCollection{TKey, TRecord}"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        Func<IServiceProvider, QdrantClient>? clientProvider = default,
        Func<IServiceProvider, ChromaCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNull(services);
        Throw.IfNullOrWhitespace(name);

        services.Add(new ServiceDescriptor(typeof(ChromaCollection<TKey, TRecord>), serviceKey, (sp, _) =>
        {
            var client = clientProvider is null ? sp.GetRequiredService<QdrantClient>() : clientProvider(sp);
            var options = GetCollectionOptions(sp, optionsProvider);

            // The client was restored from the DI container, so we do not own it.
            return new ChromaCollection<TKey, TRecord>(client, name, ownsClient: false, options);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStoreCollection<TKey, TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        services.Add(new ServiceDescriptor(typeof(IVectorSearchable<TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        services.Add(new ServiceDescriptor(typeof(IKeywordHybridSearchable<TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="QdrantClient"/> created with <paramref name="host"/>, <paramref name="port"/>,
    /// <paramref name="https"/> and <paramref name="https"/>.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaCollection{TKey, TRecord}(IServiceCollection, object?, string, string, int, bool, string?, ChromaCollectionOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        string host,
        int port = 6334,
        bool https = true,
        string? apiKey = default,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey: null, name, host, port, https, apiKey, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="QdrantClient"/> created with <paramref name="host"/>, <paramref name="port"/>,
    /// <paramref name="https"/> and <paramref name="https"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to. Defaults to 6334.</param>
    /// <param name="https">Whether to encrypt the connection using HTTPS. Defaults to <c>true</c>.</param>
    /// <param name="apiKey">The API key to use.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaCollection{TKey, TRecord}"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        string host,
        int port = 6334,
        bool https = true,
        string? apiKey = default,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNullOrWhitespace(host);

        return AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey, name, _ => new QdrantClient(host, port, https, apiKey), sp => options!, lifetime);
    }

    private static ChromaVectorStoreOptions? GetStoreOptions(IServiceProvider sp, Func<IServiceProvider, ChromaVectorStoreOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
        {
            return options; // The user has provided everything, there is nothing to change.
        }

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new(options) { EmbeddingGenerator = embeddingGenerator }; // Create a brand new copy in order to avoid modifying the original options.
    }

    private static ChromaCollectionOptions? GetCollectionOptions(IServiceProvider sp, Func<IServiceProvider, ChromaCollectionOptions?>? optionsProvider)
    {
        var options = optionsProvider?.Invoke(sp);
        if (options?.EmbeddingGenerator is not null)
        {
            return options; // The user has provided everything, there is nothing to change.
        }

        var embeddingGenerator = sp.GetService<IEmbeddingGenerator>();
        return embeddingGenerator is null
            ? options // There is nothing to change.
            : new(options) { EmbeddingGenerator = embeddingGenerator }; // Create a brand new copy in order to avoid modifying the original options.
    }
}
