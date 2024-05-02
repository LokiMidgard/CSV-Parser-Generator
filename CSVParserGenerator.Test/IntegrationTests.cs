// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using CSVParserGenerator.Test.TestData;
using Parser;

namespace CSVParserGenerator.Test;

public class IntegrationTests {
    private static readonly Options<char> _defaultOptions = new() {
        Culture = CultureInfo.InvariantCulture,
    };

    [Fact]
    public void TestWithoutHeader() {
        var data = TestDataSource.LoadCsv("data-without-header.csv");
        var items = TestParsers.ParseTestItemNoHeader(data, _defaultOptions);
        Assert.Collection(
            items,
            item => {
                Assert.Equal(1, item.Id);
                Assert.Equal("name", item.Name);
                Assert.Equal(new DateTime(2023, 12, 31), item.Timestamp);
                Assert.Equal(new TimeSpan(0, 0, 0, 1, 250), item.TimeSpan);
            });
    }
    [Fact]
    public void TestNoColomnsSpecified() {
        var data = TestDataSource.LoadCsv("data-without-header.csv");
        var items = TestParsers.ParseTestItemNoHeader(data, _defaultOptions);
        Assert.Collection(
            items,
            item => {
                Assert.Equal(1, item.Id);
                Assert.Equal("name", item.Name);
                Assert.Equal(new DateTime(2023, 12, 31), item.Timestamp);
                Assert.Equal(new TimeSpan(0, 0, 0, 1, 250), item.TimeSpan);
            });
    }

    [Fact]
    public void TestWithHeader() {
        var data = TestDataSource.LoadCsv("data-with-header.csv");
        var items = TestParsers.ParseTestItemWithHeader(data, _defaultOptions);
        Assert.Collection(
            items,
            item => {
                Assert.Equal(1, item.Id);
                Assert.Equal("name", item.Name);
                Assert.Equal(new DateTime(2023, 12, 31, 9, 0, 0), item.Timestamp);
                Assert.Equal(new TimeSpan(0, 0, 0, 1, 250), item.TimeSpan);
            });
    }

    [Fact]
    public void TestGenericTypeInt() {
        var data = "1" + Environment.NewLine;
        var items = TestParsers.ParseTestTypeItemInt(data, _defaultOptions);
        Assert.Collection(
            items,
            item => Assert.Equal(1, item.Value));
    }

    [Fact]
    public void TestTypeBoolean() {
        var data = "true" + Environment.NewLine;
        var items = TestParsers.ParseTestBoolItem(data, _defaultOptions);
        Assert.Collection(
            items,
            item => Assert.True(item.Value));
    }

    [Fact]
    public void TestNoLineEnd() {
        var data = "1,\"name\",\"2023-12-31 09:00:00\",\"00:00:01.25\"";
        var items = TestParsers.ParseTestItemNoHeader(data, _defaultOptions);
        Assert.Collection(
             items,
             item => {
                 Assert.Equal(1, item.Id);
                 Assert.Equal("name", item.Name);
                 Assert.Equal(new DateTime(2023, 12, 31, 9, 0, 0), item.Timestamp);
                 Assert.Equal(new TimeSpan(0, 0, 0, 1, 250), item.TimeSpan);
             });
    }
}
