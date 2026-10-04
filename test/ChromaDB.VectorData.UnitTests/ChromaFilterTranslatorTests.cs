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
        => AssertMatchesAll(TranslateFilter(h => true));

    [Fact]
    public void TranslatesFalseToNoRecord()
        => Assert.True(TranslateFilter(h => false).MatchesNothing);

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
    public void TranslatesAnyOverAnEmptyArrayToNoRecord()
        => Assert.True(TranslateFilter(h => h.Tags!.Any(t => new string[0].Contains(t))).MatchesNothing);

    [Fact]
    public void TranslatesANegatedAnyOverAnEmptyArrayToMatchAll()
        => AssertMatchesAll(TranslateFilter(h => !h.Tags!.Any(t => new string[0].Contains(t))));

    [Fact]
    public void TranslatesContainsOverAnEmptyInlineArrayToNoRecord()
        => Assert.True(TranslateFilter(h => new string[0].Contains(h.HotelName)).MatchesNothing);

    [Fact]
    public void TranslatesContainsOverAnEmptyCapturedListToNoRecord()
    {
        var names = new System.Collections.Generic.List<string>();

        Assert.True(TranslateFilter(h => names.Contains(h.HotelName!)).MatchesNothing);
    }

    [Fact]
    public void TranslatesANegatedContainsOverAnEmptyInlineArrayToMatchAll()
        => AssertMatchesAll(TranslateFilter(h => !new string[0].Contains(h.HotelName)));

    [Fact]
    public void DropsAnOrBranchThatMatchesNoRecord()
        => Assert.Equal("""{"Rating":{"$gte":4}}""", Translate(h => new string[0].Contains(h.HotelName) || h.Rating >= 4));

    [Fact]
    public void TranslatesAnAndWithABranchThatMatchesNoRecordToNoRecord()
        => Assert.True(TranslateFilter(h => new string[0].Contains(h.HotelName) && h.Rating >= 4).MatchesNothing);

    [Fact]
    public void TranslatesKeyEqualityToIds()
    {
        var filter = TranslateFilter(h => h.HotelId == "h1");

        Assert.Equal(["h1"], filter.Ids);
        Assert.Null(filter.Where);
        Assert.False(filter.MatchesNothing);
    }

    [Fact]
    public void TranslatesKeyEqualityWithTheConstantOnTheLeftToIds()
        => Assert.Equal(["h1"], TranslateFilter(h => "h1" == h.HotelId).Ids);

    [Fact]
    public void TranslatesContainsOverAListOfKeysToIds()
    {
        var keys = new System.Collections.Generic.List<string> { "h1", "h2", "h1" };

        Assert.Equal(["h1", "h2"], TranslateFilter(h => keys.Contains(h.HotelId)).Ids);
    }

    [Fact]
    public void TranslatesAKeyConditionJoinedWithAndToIdsAndAWhereClause()
    {
        var filter = TranslateFilter(h => h.Rating >= 4 && new[] { "h1", "h2" }.Contains(h.HotelId));

        Assert.Equal(["h1", "h2"], filter.Ids);
        Assert.Equal("""{"Rating":{"$gte":4}}""", filter.Where!.ToString());
    }

    [Fact]
    public void IntersectsTwoKeyConditions()
        => Assert.Equal(["h2"], TranslateFilter(h => new[] { "h1", "h2" }.Contains(h.HotelId) && h.HotelId == "h2").Ids);

    [Fact]
    public void TranslatesDisjointKeyConditionsToNoRecord()
        => Assert.True(TranslateFilter(h => h.HotelId == "h1" && h.HotelId == "h2").MatchesNothing);

    [Fact]
    public void TranslatesContainsOverAnEmptyListOfKeysToNoRecord()
        => Assert.True(TranslateFilter(h => new string[0].Contains(h.HotelId)).MatchesNothing);

    [Fact]
    public void TranslatesAGuidKeyToItsId()
    {
        var key = new Guid("11111111-2222-3333-4444-555555555555");
        var model = new ChromaModelBuilder().Build(typeof(Hotel<Guid>), typeof(Guid), definition: null, defaultEmbeddingGenerator: null);

        var filter = new ChromaFilterTranslator().Translate((Expression<Func<Hotel<Guid>, bool>>)(h => h.HotelId == key), model);

        Assert.Equal(["11111111-2222-3333-4444-555555555555"], filter.Ids);
    }

    [Fact]
    public void ThrowsForAKeyInequality()
        => Assert.Throws<NotSupportedException>(() => TranslateFilter(h => h.HotelId != "h1"));

    [Fact]
    public void ThrowsForAKeyConditionInsideAnOr()
        => Assert.Throws<NotSupportedException>(() => TranslateFilter(h => h.HotelId == "h1" || h.Rating >= 4));

    [Fact]
    public void ThrowsForANegatedKeyCondition()
        => Assert.Throws<NotSupportedException>(() => TranslateFilter(h => !(h.HotelId == "h1")));

    [Fact]
    public void TranslatesContainsOnTheFullTextPropertyToWhereDocument()
    {
        var filter = TranslateFullText(h => h.Description!.Contains("pool"));

        Assert.Equal("""{"$contains":"pool"}""", filter.WhereDocument!.ToString());
        Assert.Null(filter.Where);
    }

    [Fact]
    public void TranslatesANegatedContainsOnTheFullTextPropertyToNotContains()
        => Assert.Equal("""{"$not_contains":"pool"}""", TranslateFullText(h => !h.Description!.Contains("pool")).WhereDocument!.ToString());

    [Fact]
    public void JoinsTextConditionsAndOtherConditionsWithAnd()
    {
        var filter = TranslateFullText(h => h.Description!.Contains("pool") && h.Rating >= 4 && h.Description.Contains("spa") && h.HotelId == "h1");

        Assert.Equal("""{"$and":[{"$contains":"pool"},{"$contains":"spa"}]}""", filter.WhereDocument!.ToString());
        Assert.Equal("""{"Rating":{"$gte":4}}""", filter.Where!.ToString());
        Assert.Equal(["h1"], filter.Ids);
    }

    [Fact]
    public void ThrowsForATextConditionInsideAnOr()
        => Assert.Throws<NotSupportedException>(() => TranslateFullText(h => h.Description!.Contains("pool") || h.Rating >= 4));

    [Fact]
    public void ThrowsForContainsOnAStringPropertyThatIsNotTheDocument()
        => Assert.Throws<NotSupportedException>(() => TranslateFilter(h => h.HotelName!.Contains("Grand")));

    private static ChromaFilter TranslateFullText(Expression<Func<FullTextHotel, bool>> filter)
        => new ChromaFilterTranslator().Translate(filter, new ChromaModelBuilder().Build(typeof(FullTextHotel), typeof(string), definition: null, defaultEmbeddingGenerator: null));

    private static string Translate(Expression<Func<Hotel<string>, bool>> filter)
    {
        var chromaFilter = TranslateFilter(filter);
        Assert.Null(chromaFilter.Ids);
        Assert.NotNull(chromaFilter.Where);

        // The JSON that ChromaDotNet.Client sends in the where clause.
        return chromaFilter.Where.ToString()!;
    }

    private static ChromaFilter TranslateFilter(Expression<Func<Hotel<string>, bool>> filter)
        => new ChromaFilterTranslator().Translate(filter, s_model);

    private static void AssertMatchesAll(ChromaFilter filter)
    {
        Assert.Null(filter.Where);
        Assert.Null(filter.Ids);
        Assert.False(filter.MatchesNothing);
    }
}
