using _05_Databases;

Console.WriteLine("=== 05_Databases ===\n");

// ═══════════════════════════════════════════════════════════════
// 1. ADO.NET
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("╔══════════════════════════════╗");
Console.WriteLine("║   1. ADO.NET                 ║");
Console.WriteLine("╚══════════════════════════════╝");

AdoNet.DemoPolaczenie();
await AdoNet.DemoKomendyAsync();
await AdoNet.DemoSqlDataReaderAsync();
await AdoNet.DemoTransakcjeAsync();
await AdoNet.DemoSavepointsAsync();
await AdoNet.DemoRepositoryAsync();

// ═══════════════════════════════════════════════════════════════
// 2. Dapper
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   2. Dapper                  ║");
Console.WriteLine("╚══════════════════════════════╝");

await DapperDemo.DemoZapytaniaAsync();
await DapperDemo.DemoDynamicAsync();
await DapperDemo.DemoParametryAsync();
await DapperDemo.DemoExecuteAsync();
await DapperDemo.DemoMultiMappingAsync();
await DapperDemo.DemoQueryMultipleAsync();
await DapperDemo.DemoTransakcjaAsync();
await DapperDemo.DemoDapperSklepAsync();

// ═══════════════════════════════════════════════════════════════
// 3. Entity Framework Core
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   3. Entity Framework Core   ║");
Console.WriteLine("╚══════════════════════════════╝");

await EFCore.DemoDbContextAsync();
await EFCore.DemoCRUDAsync();
await EFCore.DemoAktualizacjaAsync();
await EFCore.DemoUsuwanieAsync();
await EFCore.DemoChangeTrackerAsync();
await EFCore.DemoTransakcjaEFAsync();
EFCore.DemoMigracje();

// ═══════════════════════════════════════════════════════════════
// 4. Relacje w EF Core
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   4. Relacje w EF Core       ║");
Console.WriteLine("╚══════════════════════════════╝");

await EFRelacje.Demo1doNAsync();
await EFRelacje.DemoNdoNProstyAsync();
await EFRelacje.DemoNdoNJawnaKlasaAsync();
await EFRelacje.Demo1do1Async();
await EFRelacje.DemoEagerLoadingAsync();
await EFRelacje.DemoExplicitLoadingAsync();
await EFRelacje.DemoStrategieAsync();

// ═══════════════════════════════════════════════════════════════
// 5. Transakcje
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   5. Transakcje              ║");
Console.WriteLine("╚══════════════════════════════╝");

await Transakcje.DemoACIDAsync();
await Transakcje.DemoIzolacjaAsync();
await Transakcje.DemoOptymistycznaAsync();
await Transakcje.DemoPesymistycznaAsync();
await Transakcje.DemoSavepointsEFAsync();
await Transakcje.DemoAmbientAsync();
await Transakcje.DemoEFiAdoNetAsync();

// ═══════════════════════════════════════════════════════════════
// 6. Repository i Unit of Work
// ═══════════════════════════════════════════════════════════════
Console.WriteLine("\n╔══════════════════════════════╗");
Console.WriteLine("║   6. Repository i UoW        ║");
Console.WriteLine("╚══════════════════════════════╝");

await RepositoryUoW.DemoRepositoryAsync();
await RepositoryUoW.DemoUnitOfWorkAsync();
await RepositoryUoW.DemoSpecyfikacjeAsync();

Console.WriteLine("\n=== 05_Databases KOMPLETNY ===");
