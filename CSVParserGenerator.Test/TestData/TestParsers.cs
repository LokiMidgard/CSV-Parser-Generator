// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Parser;

namespace CSVParserGenerator.Test.TestData;

internal partial class TestParsers {
    [CSVParser(
        nameof(TestItem.Id),
        nameof(TestItem.Name),
        nameof(TestItem.Timestamp),
        nameof(TestItem.TimeSpan))]
    public static partial List<TestItem> ParseTestItemNoHeader(
        ReadOnlySpan<char> raw1,
        Options<char> option);

    [CSVParser(
        nameof(TestItem.Id),
        nameof(TestItem.Name),
        nameof(TestItem.Timestamp),
        nameof(TestItem.TimeSpan),
        HasHeader = true)]
    public static partial List<TestItem> ParseTestItemWithHeader(
        ReadOnlySpan<char> raw,
        Options<char> option);

    [CSVParser(
        nameof(TestTypeItem<int>.Value))]
    public static partial List<TestTypeItem<int>> ParseTestTypeItemInt(
        ReadOnlySpan<char> raw,
        Options<char> option);

    [CSVParser(
        nameof(TestBoolItem.Value))]
    public static partial List<TestBoolItem> ParseTestBoolItem(
        ReadOnlySpan<char> raw,
        Options<char> option);
}
