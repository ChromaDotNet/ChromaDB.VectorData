// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ChromaDB.VectorData.ConformanceTests.Support;
using VectorData.ConformanceTests;
using VectorData.ConformanceTests.Support;
using Xunit;

namespace ChromaDB.VectorData.ConformanceTests;

public class ChromaFilterTests(ChromaFilterTests.Fixture fixture)
    : FilterTests<string>(fixture), IClassFixture<ChromaFilterTests.Fixture>
{
    #region Null checking

    // Chroma metadata has no null values, and Chroma has no filter for a missing field
    public override Task Equal_with_null_reference_type()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Equal_with_null_reference_type());

    public override Task Equal_with_null_captured()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Equal_with_null_captured());

    public override Task NotEqual_with_null_reference_type()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.NotEqual_with_null_reference_type());

    public override Task NotEqual_with_null_captured()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.NotEqual_with_null_captured());

    public override Task Equal_int_property_with_null_nullable_int()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Equal_int_property_with_null_nullable_int());

    #endregion

    #region Contains over array fields

    // Chroma filters arrays with $contains, which ChromaDotNet.Client does not support yet
    public override Task Contains_over_field_string_array()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Contains_over_field_string_array());

    public override Task Contains_over_field_string_List()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Contains_over_field_string_List());

    public override Task Contains_with_Enumerable_Contains()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Contains_with_Enumerable_Contains());

    public override Task Contains_with_MemoryExtensions_Contains()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Contains_with_MemoryExtensions_Contains());

    public override Task Contains_with_MemoryExtensions_Contains_with_null_comparer()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Contains_with_MemoryExtensions_Contains_with_null_comparer());

    public override Task Any_with_Contains_over_inline_string_array()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Any_with_Contains_over_inline_string_array());

    public override Task Any_with_Contains_over_captured_string_array()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Any_with_Contains_over_captured_string_array());

    public override Task Any_with_Contains_over_captured_string_list()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Any_with_Contains_over_captured_string_list());

    public override Task Any_over_List_with_Contains_over_captured_string_array()
        => Assert.ThrowsAsync<NotSupportedException>(() => base.Any_over_List_with_Contains_over_captured_string_array());

    #endregion

    public new class Fixture : FilterTests<string>.Fixture
    {
        public override TestStore TestStore => ChromaTestStore.Instance;
    }
}
