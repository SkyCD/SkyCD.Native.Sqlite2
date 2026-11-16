using System;
using System.IO;
using System.Text.Json;
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
            File.WriteAllText(Path.Combine(root, "native-source.json"), """{"version":"2.8.17"}""");
            File.WriteAllText(Path.Combine(root, "README.template.md"), "Badge {{SQLITE_VERSION}} / {{PACKAGE_VERSION}}\n# Library");
            ReadmeGenerator.Run(root, "0.1.0-ci.7");
            Assert.Equal("Badge 2.8.17 / 0.1.0-ci.7\n# Library", File.ReadAllText(Path.Combine(root, "README.md")));
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
