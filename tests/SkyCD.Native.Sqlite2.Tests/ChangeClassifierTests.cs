using System;
using System.IO;
using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class ChangeClassifierTests
{
    [Theory]
    [InlineData("README.template.md", true)]
    [InlineData("README.md|README.template.md|docs/usage.md", true)]
    [InlineData("README.md|src/Sqlite2Database.cs", false)]
    [InlineData(".github/workflows/ci.yml", false)]
    [InlineData("native-source.json", false)]
    [InlineData("docs/policy.json", false)]
    [InlineData("README.template.md.cs", false)]
    public void DocumentationChanges_SkipCodeTestsOnlyWhenEveryFileIsDocumentation(string paths, bool expected)
        => Assert.Equal(expected, ChangeClassifier.IsDocsOnly(paths.Split('|')));

    [Fact]
    public void PublishedFrameworks_PreservesTheReleasedTargetsForDocumentationUpdates()
    {
        var root = Path.Combine(Path.GetTempPath(), "sqlite2-readme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "README.md"), "[![.NET](https://img.shields.io/static/v1?label=.NET&message=8.0%20%7C%2011.0&color=blue)](https://example.com)");
            Assert.Equal("net8.0;net11.0", ReadmeGenerator.PublishedFrameworks(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
