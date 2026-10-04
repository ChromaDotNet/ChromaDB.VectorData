// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

        var actual = (DateTimeOffset)ChromaFieldMapping.FromMetadataValue("2026-10-04T12:30:00.0000000+02:00", typeof(DateTimeOffset))!;

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Offset, actual.Offset);
    }

    [Fact]
    public void FromMetadataValueKeepsAStringThatLooksLikeADate()
        => Assert.Equal("2026-10-04", ChromaFieldMapping.FromMetadataValue("2026-10-04", typeof(string)));

    [Fact]
    public void FromMetadataValueReadsArrays()
    {
        // ChromaDotNet.Client reads a list in metadata as a List<object> of string, long, double and bool.
        Assert.Equal(new List<string> { "a", "b" }, ChromaFieldMapping.FromMetadataValue(new List<object> { "a", "b" }, typeof(List<string>)));
        Assert.Equal(new[] { 1, 2 }, ChromaFieldMapping.FromMetadataValue(new List<object> { 1L, 2L }, typeof(int[])));
        Assert.Equal(new[] { 1.5, 2 }, ChromaFieldMapping.FromMetadataValue(new List<object> { 1.5, 2L }, typeof(double[])));
    }

    [Fact]
    public void FromMetadataValueThrowsForAMismatchedType()
        => Assert.Throws<InvalidOperationException>(() => ChromaFieldMapping.FromMetadataValue("foo", typeof(int)));
}
