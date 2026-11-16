using SkyCD.Native.Sqlite2;
using var source = File.OpenRead(args[0]);
using var database = new Sqlite2Database(source);
if (Sqlite2Database.EngineVersion != "2.8.17" || database.ReadTable("cds").Count != 2)
    throw new InvalidOperationException("The packaged native engine did not read the fixture.");
var notices = Path.Combine(AppContext.BaseDirectory, "sqlite2-licenses", "NOTICE.txt");
if (!File.Exists(notices)) throw new InvalidOperationException("Native notices were not deployed.");
Console.WriteLine($"Package smoke test passed: SQLite {Sqlite2Database.EngineVersion}");
