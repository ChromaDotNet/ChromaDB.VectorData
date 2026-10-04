// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using ChromaDB.Client;
using Microsoft.Extensions.VectorData.ProviderServices;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaFilterTranslator"/> class.
/// </summary>
public class ChromaFilterTranslatorTests
{
    private static readonly CollectionModel s_model
        = new ChromaModelBuilder().Build(typeof(Hotel<string>), typeof(string), definition: null, defaultEmbeddingGenerator: null);

    [Fact]
    public void TranslatesEquality()
        => Assert.Equal("""{"HotelName":{"$eq":"Grand"}}""", Translate(h => h.HotelName == "Grand"));

    [Fact]
    public void TranslatesAComparisonWithTheConstantOnTheLeft()
        => Assert.Equal("""{"Price":{"$lt":100}}""", Translate(h => 100 > h.Price));

    [Fact]
    public void TranslatesAndAndOr()
        => Assert.Equal(
            """{"$or":[{"$and":[{"Rating":{"$gte":4}},{"Parking":{"$eq":true}}]},{"HotelName":{"$ne":"Grand"}}]}""",
            Translate(h => h.Rating >= 4 && h.Parking || h.HotelName != "Grand"));

    [Fact]
    public void PushesANegationDownWithDeMorganLaws()
        => Assert.Equal(
            """{"$or":[{"Rating":{"$lt":4}},{"Parking":{"$eq":false}}]}""",
            Translate(h => !(h.Rating >= 4 && h.Parking)));

    [Fact]
    public void TranslatesContainsOverAnInlineArray()
        => Assert.Equal("""{"HotelName":{"$nin":["a","b"]}}""", Translate(h => !new[] { "a", "b" }.Contains(h.HotelName)));

    [Fact]
    public void TranslatesTrueToNoFilter()
        => Assert.Null(new ChromaFilterTranslator().Translate((Expression<Func<Hotel<string>, bool>>)(h => true), s_model));

    [Fact]
    public void ThrowsForNull()
        => Assert.Throws<NotSupportedException>(() => Translate(h => h.HotelName == null));

    [Fact]
    public void ThrowsForAComparisonOnAString()
        => Assert.Throws<NotSupportedException>(() => Translate(h => string.Compare(h.HotelName, "b", StringComparison.Ordinal) > 0));

    [Fact]
    public void ThrowsForContainsOverAnArrayProperty()
        => Assert.Throws<NotSupportedException>(() => Translate(h => h.Tags!.Contains("pool")));

    private static string Translate(Expression<Func<Hotel<string>, bool>> filter)
    {
        var where = new ChromaFilterTranslator().Translate(filter, s_model);
        Assert.NotNull(where);

        // The where clause sent to Chroma; ChromaDotNet.Client builds it internally.
        var toWhere = typeof(ChromaWhereOperator).GetMethod("ToWhere", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return JsonSerializer.Serialize(toWhere.Invoke(where, null));
    }
}
