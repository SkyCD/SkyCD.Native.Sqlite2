using System.Text.Json;
using System.Text.RegularExpressions;

namespace SkyCD.Native.Sqlite2.Build;

internal static class LicenseChecker
{
    internal static int Run(string sbomPath, string policyPath, string reportPath)
    {
        using var sbom = JsonDocument.Parse(File.ReadAllText(sbomPath));
        using var policy = JsonDocument.Parse(File.ReadAllText(policyPath));
        HashSet<string> Set(string name) => policy.RootElement.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowed = Set("allowedLicenseIds"); var blocked = Set("blockedLicenseIds"); var exceptions = Set("allowedUnknownLicensePackages");
        var failUnknown = policy.RootElement.GetProperty("failOnUnknownLicense").GetBoolean();
        var results = new List<object>(); var failed = 0;
        foreach (var component in sbom.RootElement.GetProperty("components").EnumerateArray())
        {
            var licenses = new List<string>();
            if (component.TryGetProperty("licenses", out var entries)) foreach (var entry in entries.EnumerateArray())
            {
                if (entry.TryGetProperty("license", out var license))
                {
                    if (license.TryGetProperty("id", out var id)) licenses.Add(id.GetString()!);
                    else if (license.TryGetProperty("name", out var name)) licenses.Add(name.GetString()!);
                }
                else if (entry.TryGetProperty("expression", out var expression)) licenses.Add(expression.GetString()!);
            }
            var status = licenses.Count == 0 ? "Unknown" : "Compliant"; var reasons = new List<string>();
            if (licenses.Count == 0) reasons.Add("No license metadata.");
            foreach (var token in licenses.SelectMany(l => Regex.Split(l, @"\s+OR\s+|\s+AND\s+|\(|\)").Select(t => t.Trim()).Where(t => t.Length > 0)))
            {
                if (blocked.Contains(token)) { status = "Blocked"; reasons.Add($"Blocked license '{token}'."); }
                else if (!allowed.Contains(token)) { if (status == "Compliant") status = "Unknown"; reasons.Add($"Unknown/unapproved license '{token}'."); }
            }
            var componentName = component.GetProperty("name").GetString()!;
            if (status == "Unknown" && exceptions.Contains(componentName))
            {
                status = "CompliantByException";
                reasons = [$"Unknown license allowed by policy exception for package '{componentName}'."];
            }
            if (status == "Blocked" || (status == "Unknown" && failUnknown)) failed++;
            results.Add(new { Name = componentName, Version = component.GetProperty("version").GetString(), Licenses = licenses, Status = status, Reasons = reasons });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { GeneratedUtc = DateTimeOffset.UtcNow, ComponentsScanned = results.Count, NonCompliantCount = failed, FailOnUnknownLicense = failUnknown, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"License check: {results.Count} components, {failed} non-compliant.");
        return failed == 0 ? 0 : 1;
    }
}
