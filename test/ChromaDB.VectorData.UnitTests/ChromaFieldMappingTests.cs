// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ChromaDB.VectorData.UnitTests;

/// <summary>
/// Contains tests for the <see cref="ChromaFieldMapping"/> class.
/// </summary>
public class ChromaFieldMappingTests
{
    [Fact]
    public void ToMetadataValueKeepsScalars()
    {
        Assert.Equal("foo", ChromaFieldMapping.ToMetadataValue("foo"));
        Assert.Equal(5, ChromaFieldMapping.ToMetadataValue(5));
        Assert.Equal(5L, ChromaFieldMapping.ToMetadataValue(5L));
        Assert.Equal(1.5, ChromaFieldMapping.ToMetadataValue(1.5));
        Assert.Equal(true, ChromaFieldMapping.ToMetadataValue(true));
        Assert.Null(ChromaFieldMapping.ToMetadataValue(null));
    }

    [Fact]
    public void ToMetadataValueWritesDatesAsRoundTripStrings()
    {
        var date = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.FromHours(2));

        Assert.Equal("2026-10-04T12:30:00.0000000+02:00", ChromaFieldMapping.ToMetadataValue(date));
    }

    [Fact]
    public void ToMetadataValueWritesArraysAsLists()
        => Assert.Equal(new List<object?> { "a", "b" }, ChromaFieldMapping.ToMetadataValue(new[] { "a", "b" }));

    [Theory]
    [InlineData(typeof(int), 5)]
    [InlineData(typeof(long), 5L)]
    [InlineData(typeof(double), 5d)]
    [InlineData(typeof(float), 5f)]
    public void FromMetadataValueConvertsWholeNumbers(Type targetType, object expected)
        => Assert.Equal(expected, ChromaFieldMapping.FromMetadataValue(5L, targetType));

    [Fact]
    public void FromMetadataValueReadsNullableTypes()
        => Assert.Equal(5, ChromaFieldMapping.FromMetadataValue(5L, typeof(int?)));

    [Fact]
    public void FromMetadataValueReadsDateStrings()
    {
        var expected = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.FromHours(2));

        Assert.Equal(expected, ChromaFieldMapping.FromMetadataValue("2026-10-04T12:30:00.0000000+02:00", typeof(DateTimeOffset)));
    }

    [Fact]
    public void FromMetadataValueReadsADateTimeIntoAString()
    {
        // ChromaDotNet.Client reads metadata strings that look like dates as DateTime.
        var dateTime = new DateTime(2026, 10, 4, 12, 30, 0, DateTimeKind.Utc);

        Assert.Equal(dateTime.ToString("O", CultureInfo.InvariantCulture), ChromaFieldMapping.FromMetadataValue(dateTime, typeof(string)));
    }

    [Fact]
    public void FromMetadataValueReadsArrays()
    {
        using var strings = JsonDocument.Parse("""["a","b"]""");
        using var ints = JsonDocument.Parse("[1,2]");

        Assert.Equal(new List<string> { "a", "b" }, ChromaFieldMapping.FromMetadataValue(strings.RootElement, typeof(List<string>)));
        Assert.Equal(new[] { 1, 2 }, ChromaFieldMapping.FromMetadataValue(ints.RootElement, typeof(int[])));
    }

    [Fact]
    public void FromMetadataValueThrowsForAMismatchedType()
        => Assert.Throws<InvalidOperationException>(() => ChromaFieldMapping.FromMetadataValue("foo", typeof(int)));
}
