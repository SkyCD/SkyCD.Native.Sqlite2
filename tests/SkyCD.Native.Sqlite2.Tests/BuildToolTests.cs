using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class BuildToolTests
{
    [Theory]
    [InlineData("Public Domain", 0)]
    [InlineData("GPL-3.0-only", 1)]
    public void LicenseException_DoesNotPermitBlockedLicenses(string license, int expected)
    {
        InTemporaryDirectory(root =>
        {
            var sbom = Path.Combine(root, "sbom.json");
            var policy = Path.Combine(root, "policy.json");
            var report = Path.Combine(root, "report.json");
            File.WriteAllText(sbom, JsonSerializer.Serialize(new { components = new[] { new { name = "SQLite2", version = "2.8.17", licenses = new[] { new { license = new { name = license } } } } } }));
            File.WriteAllText(policy, """{"allowedLicenseIds":["MIT"],"blockedLicenseIds":["GPL-3.0-only"],"allowedUnknownLicensePackages":["SQLite2"],"failOnUnknownLicense":true}""");
            Assert.Equal(expected, LicenseChecker.Run(sbom, policy, report));
            using var result = JsonDocument.Parse(File.ReadAllText(report));
            Assert.Equal(expected, result.RootElement.GetProperty("NonCompliantCount").GetInt32());
        });
    }

    [Fact]
    public void ReadmeTemplate_UsesSourceAndPackageVersions()
    {
        InTemporaryDirectory(root =>
        {
            File.WriteAllText(Path.Combine(root, "native-source.json"), """{"version":"2.8.17","platforms":{"win-arm64":"Windows ARM64"}}""");
            Directory.CreateDirectory(Path.Combine(root, "src/SkyCD.Native.Sqlite2"));
            File.WriteAllText(Path.Combine(root, "PackageDescription.template.txt"), "Supports {{DOTNET_VERSIONS_TEXT}} on {{SUPPORTED_PLATFORMS}}");
            File.WriteAllText(Path.Combine(root, "README.template.md"), "Badge {{SQLITE_VERSION}} / {{PACKAGE_VERSION}} / {{DOTNET_VERSIONS}}\n# Library");
            ReadmeGenerator.Run(root, "0.1.0-ci.7", "net8.0;net11.0");
            Assert.Equal("Badge 2.8.17 / 0.1.0-ci.7 / 8.0%20%7C%2011.0\n# Library", File.ReadAllText(Path.Combine(root, "README.md")));
            Assert.Equal("Supports 8.0, 11.0 on Windows ARM64", XDocument.Load(Path.Combine(root, "src/SkyCD.Native.Sqlite2/PackageMetadata.g.props")).Descendants("Description").Single().Value);
        });
    }

    private static void InTemporaryDirectory(Action<string> run)
    {
        var root = Path.Combine(Path.GetTempPath(), "sqlite2-tool-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { run(root); }
        finally { Directory.Delete(root, true); }
    }
}
