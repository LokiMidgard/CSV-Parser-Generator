// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;

namespace CSVParserGenerator.Test.TestData;

public static class TestDataSource {
    public static string LoadCsv(string fileName) {
        var asmLocation = Path.GetDirectoryName(typeof(TestDataSource).Assembly.GetAssemblyLocation())!;
        var testFilePath = Path.Combine(asmLocation, "TestData", fileName);
        return File.ReadAllText(testFilePath);
    }
}
