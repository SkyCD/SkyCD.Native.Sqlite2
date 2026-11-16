using System.Text.Json;
using System.Text.RegularExpressions;

namespace SkyCD.Native.Sqlite2.Build;

internal static class SourcePreparer
{
    internal static async Task Run(string root, string work, string zig, JsonElement definition)
    {
        var upstream = Path.Combine(root, definition.GetProperty("path").GetString()!);
        if (!File.Exists(Path.Combine(upstream, "VERSION")))
            throw new BuildToolException("initialize the SQLite submodule with git submodule update --init");
        var commit = await ProcessRunner.Run("git", ["-C", upstream, "rev-parse", "HEAD"], true);
        if (commit != definition.GetProperty("commit").GetString())
            throw new BuildToolException("SQLite submodule does not match the pinned commit");
        await ProcessRunner.Run("git", ["-C", upstream, "diff", "--quiet", "HEAD", "--"]);
        var version = File.ReadAllText(Path.Combine(upstream, "VERSION")).Trim();
        if (version != definition.GetProperty("version").GetString())
            throw new BuildToolException("SQLite submodule version does not match its metadata");
        const string units = "attach auth btree btree_rb build copy date delete encode expr func hash insert main os pager pragma printf random select table tokenize update util vacuum vdbe vdbeaux where trigger";
        foreach (var unit in units.Split(' ')) File.Copy(Path.Combine(upstream, "src", unit + ".c"), Path.Combine(work, unit + ".c"));
        foreach (var header in Directory.GetFiles(Path.Combine(upstream, "src"), "*.h")) File.Copy(header, Path.Combine(work, Path.GetFileName(header)));
        File.WriteAllText(Path.Combine(work, "sqlite.h"), File.ReadAllText(Path.Combine(upstream, "src/sqlite.h.in")).Replace("--VERS--", version).Replace("--ENCODING--", "UTF8"));
        var opcodes = Regex.Matches(File.ReadAllText(Path.Combine(work, "vdbe.c")), @"^case (OP_\w+):", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();
        if (opcodes.Length == 0) throw new BuildToolException("upstream opcode definitions were not found");
        File.WriteAllText(Path.Combine(work, "opcodes.h"), string.Join("\n", opcodes.Select((name, i) => $"#define {name} {i + 1}")) + "\n");
        File.WriteAllText(Path.Combine(work, "opcodes.c"), "char *sqliteOpcodeNames[] = { \"???\",\n" + string.Join(",\n", opcodes.Select(name => "\"" + name[3..] + "\"")) + "\n};\n");
        var lemon = Path.Combine(work, OperatingSystem.IsWindows() ? "lemon.exe" : "lemon");
        var lemonSource = Path.Combine(work, "lemon.c");
        // Lemon predates Windows LLP64: its pointer arithmetic assumes unsigned long is pointer-sized.
        File.WriteAllText(lemonSource, "#include <stdint.h>\n" + File.ReadAllText(Path.Combine(upstream, "tool/lemon.c")).Replace("(unsigned long)", "(uintptr_t)"));
        await ProcessRunner.Run(zig, ["cc", "-std=gnu89", "-O2", "-fwrapv", "-Wno-deprecated-non-prototype", "-Wno-implicit-function-declaration", lemonSource, "-o", lemon]);
        File.Copy(Path.Combine(upstream, "tool/lempar.c"), Path.Combine(work, "lempar.c"));
        File.Copy(Path.Combine(upstream, "src/parse.y"), Path.Combine(work, "parse.y"));
        await ProcessRunner.Run(lemon, ["parse.y"], workingDirectory: work);
        File.Delete(lemon);
        File.Delete(lemonSource);
        File.Delete(Path.Combine(work, "lempar.c"));
    }
}
