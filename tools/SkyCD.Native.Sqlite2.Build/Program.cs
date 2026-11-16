using SkyCD.Native.Sqlite2.Build;

try
{
    if (args.Length == 0) throw new BuildToolException("choose native-build, verify-package, check-licenses, smoke-package, or generate-readme");
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 1; i < args.Length; i += 2)
    {
        if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal)) throw new BuildToolException("options require --name value pairs");
        options.Add(args[i], args[i + 1]);
    }
    string Required(string name) => options.TryGetValue(name, out var value) ? value : throw new BuildToolException($"missing {name}");
    var root = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(root, "SkyCD.Native.Sqlite2.slnx")))
        root = Directory.GetParent(root)?.FullName ?? throw new BuildToolException("run inside the native repository");
    switch (args[0])
    {
        case "native-build": await NativeBuilder.Run(root, Required("--zig"), options.GetValueOrDefault("--runtime"), options.GetValueOrDefault("--output-root")); break;
        case "verify-package": PackageVerifier.Run(Required("--package"), options.GetValueOrDefault("--version")); break;
        case "release-version": await ReleaseVersion.Run(root, Required("--output")); break;
        case "check-licenses": return LicenseChecker.Run(Required("--sbom"), Required("--policy"), Required("--report"));
        case "smoke-package": await PackageSmoke.Run(root, Required("--version"), Required("--rid"), Required("--feed"), options.GetValueOrDefault("--framework", "net10.0")); break;
        case "release-matrix": await ReleaseMatrix.Run(Required("--output")); break;
        case "retry-runners": await RunnerRetry.Run(Required("--repository"), Required("--run-id")); break;
        case "generate-readme": ReadmeGenerator.Run(root, Required("--version"), options.GetValueOrDefault("--frameworks")); break;
        default: throw new BuildToolException($"unknown command {args[0]}");
    }
    return 0;
}
catch (Exception exception) { Console.Error.WriteLine(exception.Message); return 1; }
