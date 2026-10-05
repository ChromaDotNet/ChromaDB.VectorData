// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using ChromaDB.Client;
using ChromaDB.VectorData;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.Shared.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to register <see cref="ChromaVectorStore"/> and <see cref="ChromaCollection{TKey, TRecord}"/> instances on an <see cref="IServiceCollection"/>.
/// </summary>
public static class ChromaServiceCollectionExtensions
{
    private const string DynamicCodeMessage = "This method is incompatible with NativeAOT, consult the documentation for adding collections in a way that's compatible with NativeAOT.";
    private const string UnreferencedCodeMessage = "This method is incompatible with trimming, consult the documentation for adding collections in a way that's compatible with NativeAOT.";

    /// <summary>
    /// Registers a <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="ChromaClient"/> returned by <paramref name="clientProvider"/>
    /// or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaVectorStore(IServiceCollection, object?, Func{IServiceProvider, ChromaClient}?, Func{IServiceProvider, ChromaVectorStoreOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, ChromaClient>? clientProvider = default,
        Func<IServiceProvider, ChromaVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedChromaVectorStore(services, serviceKey: null, clientProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// with <see cref="ChromaClient"/> returned by <paramref name="clientProvider"/>
    /// or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="clientProvider">The <see cref="ChromaClient"/> provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="ChromaVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, ChromaClient>? clientProvider = default,
        Func<IServiceProvider, ChromaVectorStoreOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNull(services);

        return AddKeyedChromaVectorStore(services, serviceKey, lifetime, (sp, options) =>
        {
            var client = clientProvider is null ? sp.GetRequiredService<ChromaClient>() : clientProvider(sp);

            // The client was restored from the DI container, so we do not own it.
            return new ChromaVectorStore(client, options);
        }, optionsProvider);
    }

    /// <summary>
    /// Registers a <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// that connects to the Chroma server at <paramref name="uri"/>.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaVectorStore(IServiceCollection, object?, string, ChromaVectorStoreOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        string uri,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedChromaVectorStore(services, serviceKey: null, uri, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// that connects to the Chroma server at <paramref name="uri"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="uri">The URI of the Chroma server, like <c>http://localhost:8000</c>.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        string uri,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNullOrWhitespace(uri);

        return AddKeyedChromaVectorStore(services, serviceKey, new ChromaConfigurationOptions(uri), options, lifetime);
    }

    /// <summary>
    /// Registers a <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// that connects to Chroma with <paramref name="chromaOptions"/>, like a token, a tenant and a database for Chroma Cloud.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaVectorStore(IServiceCollection, object?, ChromaConfigurationOptions, ChromaVectorStoreOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaVectorStore(
        this IServiceCollection services,
        ChromaConfigurationOptions chromaOptions,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddKeyedChromaVectorStore(services, serviceKey: null, chromaOptions, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaVectorStore"/> as <see cref="VectorStore"/>
    /// that connects to Chroma with <paramref name="chromaOptions"/>, like a token, a tenant and a database for Chroma Cloud.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaVectorStore"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the vector store.</param>
    /// <param name="chromaOptions">The options used to connect to Chroma.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaVectorStore"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaVectorStore(
        this IServiceCollection services,
        object? serviceKey,
        ChromaConfigurationOptions chromaOptions,
        ChromaVectorStoreOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        Throw.IfNull(services);
        Throw.IfNull(chromaOptions);

        // The store creates its own HttpClient, so it owns it.
        return AddKeyedChromaVectorStore(services, serviceKey, lifetime,
            (_, storeOptions) => new ChromaVectorStore(chromaOptions, new HttpClient(), ownsClient: true, storeOptions),
            _ => options!);
    }

    private static IServiceCollection AddKeyedChromaVectorStore(
        IServiceCollection services,
        object? serviceKey,
        ServiceLifetime lifetime,
        Func<IServiceProvider, ChromaVectorStoreOptions?, ChromaVectorStore> storeFactory,
        Func<IServiceProvider, ChromaVectorStoreOptions?>? optionsProvider)
    {
        services.Add(new ServiceDescriptor(typeof(ChromaVectorStore), serviceKey,
            (sp, _) => storeFactory(sp, GetStoreOptions(sp, optionsProvider)), lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStore), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaVectorStore>(key), lifetime));

        return services;
    }

    /// <summary>
    /// Registers a <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="ChromaClient"/> returned by <paramref name="clientProvider"/>
    /// or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaCollection{TKey, TRecord}(IServiceCollection, object?, string, Func{IServiceProvider, ChromaClient}?, Func{IServiceProvider, ChromaCollectionOptions}?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, ChromaClient>? clientProvider = default,
        Func<IServiceProvider, ChromaCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey: null, name, clientProvider, optionsProvider, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// with <see cref="ChromaClient"/> returned by <paramref name="clientProvider"/>
    /// or retrieved from the dependency injection container if <paramref name="clientProvider"/> was not provided.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="clientProvider">The <see cref="ChromaClient"/> provider.</param>
    /// <param name="optionsProvider">Options provider to further configure the <see cref="ChromaCollection{TKey, TRecord}"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        Func<IServiceProvider, ChromaClient>? clientProvider = default,
        Func<IServiceProvider, ChromaCollectionOptions>? optionsProvider = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNull(services);
        Throw.IfNullOrWhitespace(name);

        return AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey, lifetime, (sp, options) =>
        {
            var client = clientProvider is null ? sp.GetRequiredService<ChromaClient>() : clientProvider(sp);

            // The client was restored from the DI container, so we do not own it.
            return new ChromaCollection<TKey, TRecord>(client, name, options);
        }, optionsProvider);
    }

    /// <summary>
    /// Registers a <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// that connects to the Chroma server at <paramref name="uri"/>.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaCollection{TKey, TRecord}(IServiceCollection, object?, string, string, ChromaCollectionOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        string uri,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey: null, name, uri, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// that connects to the Chroma server at <paramref name="uri"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="uri">The URI of the Chroma server, like <c>http://localhost:8000</c>.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaCollection{TKey, TRecord}"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        string uri,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNullOrWhitespace(uri);

        return AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey, name, new ChromaConfigurationOptions(uri), options, lifetime);
    }

    /// <summary>
    /// Registers a <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// that connects to Chroma with <paramref name="chromaOptions"/>, like a token, a tenant and a database for Chroma Cloud.
    /// </summary>
    /// <inheritdoc cref="AddKeyedChromaCollection{TKey, TRecord}(IServiceCollection, object?, string, ChromaConfigurationOptions, ChromaCollectionOptions?, ServiceLifetime)"/>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        string name,
        ChromaConfigurationOptions chromaOptions,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
        => AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey: null, name, chromaOptions, options, lifetime);

    /// <summary>
    /// Registers a keyed <see cref="ChromaCollection{TKey, TRecord}"/> as <see cref="VectorStoreCollection{TKey, TRecord}"/>
    /// that connects to Chroma with <paramref name="chromaOptions"/>, like a token, a tenant and a database for Chroma Cloud.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the <see cref="ChromaCollection{TKey, TRecord}"/> on.</param>
    /// <param name="serviceKey">The key with which to associate the collection.</param>
    /// <param name="name">The name of the collection.</param>
    /// <param name="chromaOptions">The options used to connect to Chroma.</param>
    /// <param name="options">Options to further configure the <see cref="ChromaCollection{TKey, TRecord}"/>.</param>
    /// <param name="lifetime">The service lifetime for the store. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>Service collection.</returns>
    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    public static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        this IServiceCollection services,
        object? serviceKey,
        string name,
        ChromaConfigurationOptions chromaOptions,
        ChromaCollectionOptions? options = default,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TKey : notnull
        where TRecord : class
    {
        Throw.IfNull(services);
        Throw.IfNullOrWhitespace(name);
        Throw.IfNull(chromaOptions);

        // The collection creates its own HttpClient, so it owns it.
        return AddKeyedChromaCollection<TKey, TRecord>(services, serviceKey, lifetime,
            (_, collectionOptions) => new ChromaCollection<TKey, TRecord>(chromaOptions, new HttpClient(), name, ownsClient: true, collectionOptions),
            _ => options!);
    }

    [RequiresUnreferencedCode(DynamicCodeMessage)]
    [RequiresDynamicCode(UnreferencedCodeMessage)]
    private static IServiceCollection AddKeyedChromaCollection<TKey, TRecord>(
        IServiceCollection services,
        object? serviceKey,
        ServiceLifetime lifetime,
        Func<IServiceProvider, ChromaCollectionOptions?, ChromaCollection<TKey, TRecord>> collectionFactory,
        Func<IServiceProvider, ChromaCollectionOptions?>? optionsProvider)
        where TKey : notnull
        where TRecord : class
    {
        services.Add(new ServiceDescriptor(typeof(ChromaCollection<TKey, TRecord>), serviceKey,
            (sp, _) => collectionFactory(sp, GetCollectionOptions(sp, optionsProvider)), lifetime));

        services.Add(new ServiceDescriptor(typeof(VectorStoreCollection<TKey, TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        services.Add(new ServiceDescriptor(typeof(IVectorSearchable<TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        services.Add(new ServiceDescriptor(typeof(IKeywordHybridSearchable<TRecord>), serviceKey,
            static (sp, key) => sp.GetRequiredKeyedService<ChromaCollection<TKey, TRecord>>(key), lifetime));

        return services;
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
