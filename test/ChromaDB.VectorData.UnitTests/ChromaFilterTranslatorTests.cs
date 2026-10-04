// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Linq.Expressions;
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
    public void TranslatesContainsOverAnArrayProperty()
        => Assert.Equal("""{"Tags":{"$contains":"pool"}}""", Translate(h => h.Tags!.Contains("pool")));

    [Fact]
    public void TranslatesANegatedContainsOverAnArrayProperty()
        => Assert.Equal("""{"Tags":{"$not_contains":"pool"}}""", Translate(h => !h.Tags!.Contains("pool")));

    [Fact]
    public void TranslatesAnyWithContainsToAnOrOfContains()
        => Assert.Equal(
            """{"$or":[{"Tags":{"$contains":"pool"}},{"Tags":{"$contains":"spa"}}]}""",
            Translate(h => h.Tags!.Any(t => new[] { "pool", "spa" }.Contains(t))));

    [Fact]
    public void TranslatesANegatedAnyWithContainsToAnAndOfNotContains()
        => Assert.Equal(
            """{"$and":[{"Tags":{"$not_contains":"pool"}},{"Tags":{"$not_contains":"spa"}}]}""",
            Translate(h => !h.Tags!.Any(t => new[] { "pool", "spa" }.Contains(t))));

    [Fact]
    public void ThrowsForAnyOverAnEmptyArray()
        => Assert.Throws<NotSupportedException>(() => Translate(h => h.Tags!.Any(t => new string[0].Contains(t))));

    [Fact]
    public void TranslatesANegatedAnyOverAnEmptyArrayToMatchAll()
        => Assert.Null(new ChromaFilterTranslator().Translate((Expression<Func<Hotel<string>, bool>>)(h => !h.Tags!.Any(t => new string[0].Contains(t))), s_model));

    [Fact]
    public void ThrowsForContainsOverAnEmptyInlineArray()
        => Assert.Throws<NotSupportedException>(() => Translate(h => new string[0].Contains(h.HotelName)));

    [Fact]
    public void ThrowsForContainsOverAnEmptyCapturedList()
    {
        var names = new System.Collections.Generic.List<string>();

        Assert.Throws<NotSupportedException>(() => Translate(h => names.Contains(h.HotelName!)));
    }

    [Fact]
    public void TranslatesANegatedContainsOverAnEmptyInlineArrayToMatchAll()
        => Assert.Null(new ChromaFilterTranslator().Translate((Expression<Func<Hotel<string>, bool>>)(h => !new string[0].Contains(h.HotelName)), s_model));

    private static string Translate(Expression<Func<Hotel<string>, bool>> filter)
    {
        var where = new ChromaFilterTranslator().Translate(filter, s_model);
        Assert.NotNull(where);

        // The JSON that ChromaDotNet.Client sends in the where clause.
        return where.ToString()!;
    }
}
