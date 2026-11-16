using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace SkyCD.Native.Sqlite2.Build;

internal static class NativeBuilder
{
    internal static async Task Run(string root, string zig, string? runtimes, string? output)
    {
        using var definition = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-source.json")));
        var source = definition.RootElement;
        if (await ProcessRunner.Run(zig, ["version"], true) != source.GetProperty("zigVersion").GetString())
            throw new BuildToolException("compiler does not match the pinned Zig version");
        var native = Path.Combine(root, "src/SkyCD.Native.Sqlite2/native");
        var outputRoot = Path.GetFullPath(output ?? Path.Combine(native, "runtimes"));
        var work = Path.Combine(Path.GetTempPath(), "skycd-sqlite2-build-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var archive = Path.Combine(work, "source.zip");
            using (var http = new HttpClient())
            using (var stream = await http.GetStreamAsync(source.GetProperty("url").GetString()!))
            using (var target = File.Create(archive))
                await stream.CopyToAsync(target);
            using (var file = File.OpenRead(archive))
                if (Convert.ToHexStringLower(await SHA256.HashDataAsync(file)) != source.GetProperty("sha256").GetString())
                    throw new BuildToolException("upstream source archive checksum mismatch");
            ZipFile.ExtractToDirectory(archive, work);
            File.WriteAllText(Path.Combine(work, "config.h"), "#define SQLITE_PTR_SZ 8\n");
            var osHeader = Path.Combine(work, "os.h");
            var osText = File.ReadAllText(osHeader);
            var first = osText.IndexOf("# if defined(_MSC_VER) || defined(__BORLANDC__)", StringComparison.Ordinal);
            var last = first < 0 ? -1 : osText.IndexOf("# define SQLITE_TEMPNAME_SIZE (MAX_PATH+50)", first, StringComparison.Ordinal);
            if (first < 0 || last < 0) throw new BuildToolException("unexpected upstream Windows header");
            File.WriteAllText(osHeader, osText[..first] + "# include <sys/types.h>\n" + osText[last..]);
            PatchBtree(Path.Combine(work, "btree.c"));
            File.Copy(Path.Combine(native, "WindowsCrtCompat.c"), Path.Combine(work, "skycd_crt_compat.c"));
            var sources = Directory.GetFiles(work, "*.c").Where(p => Path.GetFileName(p) is not ("shell.c" or "tclsqlite.c")).Order(StringComparer.Ordinal).ToArray();
            var exports = Path.Combine(work, "exports.def");
            File.WriteAllText(exports, "EXPORTS\nsqlite_open\nsqlite_close\nsqlite_compile\nsqlite_step\nsqlite_finalize\nsqlite_freemem\nsqlite_set_authorizer\nsqlite_progress_handler\nsqlite_libversion\n");
            foreach (var rid in (runtimes ?? "win-x64,linux-x64,osx-x64,osx-arm64").Split(','))
            {
                var target = rid switch
                {
                    "win-x64" => "x86_64-windows-gnu",
                    "linux-x64" => "x86_64-linux-gnu.2.17",
                    "osx-x64" => "x86_64-macos.10.13",
                    "osx-arm64" => "aarch64-macos.11.0",
                    _ => throw new BuildToolException($"unsupported runtime {rid}")
                };
                var filename = rid.StartsWith("win-", StringComparison.Ordinal) ? "skycd_sqlite2.dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "libskycd_sqlite2.dylib" : "libskycd_sqlite2.so";
                var binaryDirectory = Path.Combine(work, rid);
                var destination = Path.Combine(outputRoot, rid, "native");
                Directory.CreateDirectory(binaryDirectory);
                Directory.CreateDirectory(destination);
                var arguments = new List<string> { "cc", "-target", target, "-std=gnu89", "-O2", "-g0", "-s", "-shared", "-fPIC", "-DTHREADSAFE=1", "-D_FILE_OFFSET_BITS=64", "-DSQLITE_UTF8=1", "-Wno-int-conversion", "-Wno-incompatible-pointer-types", "-Wno-pointer-to-int-cast", "-I", work };
                arguments.AddRange(sources);
                if (rid.StartsWith("win-", StringComparison.Ordinal)) arguments.Add(exports);
                if (rid.StartsWith("osx-", StringComparison.Ordinal)) arguments.Add("-Wl,-install_name,@rpath/libskycd_sqlite2.dylib");
                if (Environment.GetEnvironmentVariable("SKYCD_NATIVE_LINK_AUDIT") == "1") arguments.Add("-v");
                arguments.AddRange(["-o", Path.Combine(binaryDirectory, filename)]);
                await ProcessRunner.Run(zig, arguments);
                File.Copy(Path.Combine(binaryDirectory, filename), Path.Combine(destination, filename), true);
                Console.WriteLine($"Built {rid}");
            }
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories))
            {
                using var stream = File.OpenRead(file);
                hashes["runtimes/" + Path.GetRelativePath(outputRoot, file).Replace(Path.DirectorySeparatorChar, '/')] = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
            }
            File.WriteAllText(Path.Combine(native, "sha256.json"), JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        finally
        {
            var temp = Path.GetFullPath(Path.GetTempPath());
            if (!Path.GetFullPath(work).StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(work).StartsWith("skycd-sqlite2-build-", StringComparison.Ordinal))
                throw new BuildToolException("invalid temporary cleanup directory");
            Directory.Delete(work, true);
        }
    }

    private static void PatchBtree(string path)
    {
        var text = File.ReadAllText(path);
        var function = text.IndexOf("static int initPage(", StringComparison.Ordinal);
        var position = function < 0 ? -1 : text.IndexOf("  if( pPage->pParent ){", function, StringComparison.Ordinal);
        if (position < 0) throw new BuildToolException("unexpected upstream initPage layout");
        const string guard = """
              {
                MemPage *ancestor = pParent;
                int levels = 0;
                while( ancestor ){
                  if( ancestor==pPage || ++levels>64 ) return SQLITE_CORRUPT;
                  ancestor = ancestor->pParent;
                }
              }
            """;
        text = text.Insert(position, guard + "\n");
        text = text.Replace("    assert( pPage->pParent==pParent );", "    if( pPage->pParent!=pParent ) return SQLITE_CORRUPT;");
        text = text.Replace("    pPage->apCell[pPage->nCell++] = pCell;", "    if( pPage->nCell>=MX_CELL ) goto page_format_error;\n    pPage->apCell[pPage->nCell++] = pCell;");
        File.WriteAllText(path, text);
    }
}
