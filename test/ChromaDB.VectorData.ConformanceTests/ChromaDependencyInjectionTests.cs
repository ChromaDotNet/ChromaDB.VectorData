// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VectorData.ConformanceTests;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaDependencyInjectionTests
    : DependencyInjectionTests<ChromaVectorStore, ChromaCollection<string, DependencyInjectionTests<string>.Record>, string, DependencyInjectionTests<string>.Record>
{
    private const string Uri = "http://localhost:8000";

    protected override void PopulateConfiguration(ConfigurationManager configuration, object? serviceKey = null)
        => configuration.AddInMemoryCollection(
        [
            new(CreateConfigKey("Chroma", serviceKey, "Uri"), Uri),
        ]);

    private static string UriProvider(IServiceProvider sp, object? serviceKey = null)
        => sp.GetRequiredService<IConfiguration>().GetRequiredSection(CreateConfigKey("Chroma", serviceKey, "Uri")).Value!;

    public override IEnumerable<Func<IServiceCollection, object?, string, ServiceLifetime, IServiceCollection>> CollectionDelegates
    {
        get
        {
            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services
                    .AddSingleton(new ChromaConfigurationOptions(Uri))
                    .AddChromaCollection<string, Record>(name, lifetime: lifetime)
                : services
                    .AddSingleton(new ChromaConfigurationOptions(Uri))
                    .AddKeyedChromaCollection<string, Record>(serviceKey, name, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<string, Record>(name, Uri, lifetime: lifetime)
                : services.AddKeyedChromaCollection<string, Record>(serviceKey, name, Uri, lifetime: lifetime);

            yield return (services, serviceKey, name, lifetime) => serviceKey is null
                ? services.AddChromaCollection<string, Record>(
                    name, sp => new ChromaConfigurationOptions(UriProvider(sp)), lifetime: lifetime)
                : services.AddKeyedChromaCollection<string, Record>(
                    serviceKey, name, sp => new ChromaConfigurationOptions(UriProvider(sp, serviceKey)), lifetime: lifetime);
        }
    }

    public override IEnumerable<Func<IServiceCollection, object?, ServiceLifetime, IServiceCollection>> StoreDelegates
    {
        get
        {
            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services.AddChromaVectorStore(Uri, lifetime: lifetime)
                : services.AddKeyedChromaVectorStore(serviceKey, Uri, lifetime: lifetime);

            yield return (services, serviceKey, lifetime) => serviceKey is null
                ? services
                    .AddSingleton(new ChromaConfigurationOptions(Uri))
                    .AddChromaVectorStore(lifetime: lifetime)
                : services
                    .AddSingleton(new ChromaConfigurationOptions(Uri))
                    .AddKeyedChromaVectorStore(serviceKey, lifetime: lifetime);
        }
    }

    [Fact]
    public void UriCantBeNullOrEmpty()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddChromaVectorStore(uri: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaVectorStore(serviceKey: "notNull", uri: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddChromaCollection<string, Record>(
            name: "notNull", uri: null!));
        Assert.Throws<ArgumentException>(() => services.AddChromaCollection<string, Record>(
            name: "notNull", uri: ""));
        Assert.Throws<ArgumentNullException>(() => services.AddKeyedChromaCollection<string, Record>(
            serviceKey: "notNull", name: "notNull", uri: null!));
        Assert.Throws<ArgumentException>(() => services.AddKeyedChromaCollection<string, Record>(
            serviceKey: "notNull", name: "notNull", uri: ""));
    }
}
