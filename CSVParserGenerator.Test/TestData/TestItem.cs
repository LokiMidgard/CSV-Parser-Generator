﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CSVParserGenerator.Test.TestData;

public class TestItem {
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime? Timestamp { get; set; }
    public TimeSpan? TimeSpan { get; set; }
}