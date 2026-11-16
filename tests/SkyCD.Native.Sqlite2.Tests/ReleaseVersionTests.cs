using System;
using SkyCD.Native.Sqlite2.Build;
using Xunit;

namespace SkyCD.Native.Sqlite2.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("", "2.8.17", "0.1.0.2817")]
    [InlineData("v0.1.0-ci.15", "2.8.17", "0.1.1.2817")]
    [InlineData("v0.1.1.2817", "2.8.17", "0.1.2.2817")]
    [InlineData("v0.1.2.2817|0.1.3.2817", "2.8.18", "0.1.4.2818")]
    [InlineData("v0.1.9.2817|v0.1.2.2817|v9.0.99", "2.8.17", "0.1.10.2817")]
    public void Next_UsesNextPatchAndSqliteRevision(string releases, string sqliteVersion, string expected)
        => Assert.Equal(expected, ReleaseVersion.Next(releases.Split('|', StringSplitOptions.RemoveEmptyEntries), sqliteVersion));

    [Theory]
    [InlineData("2.8.preview")]
    [InlineData("2.8")]
    [InlineData("99999999999.8.17")]
    public void Next_RejectsInvalidSqliteVersions(string version)
        => Assert.Throws<BuildToolException>(() => ReleaseVersion.Next([], version));
}
