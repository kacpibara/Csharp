namespace _05_Databases;

using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Text;

// ─── Modele (suffix DP) ──────────────────────────────────────────────────────

public class KategoriaDP
{
    public long   Id    { get; set; }
    public string Nazwa { get; set; } = "";
}

public class ProduktDP
{
    public long       Id           { get; set; }
    public string     Nazwa        { get; set; } = "";
    public double     Cena         { get; set; }
    public int        StanMagazynu { get; set; }
    public bool       Aktywny      { get; set; }
    public long       KategoriaId  { get; set; }
    public KategoriaDP? Kategoria  { get; set; }
}

public class KlientDP
{
    public long   Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Email    { get; set; } = "";
}

public class ZamowienieDP
{
    public long             Id            { get; set; }
    public long             KlientId      { get; set; }
    public string           Status        { get; set; } = "Nowe";
    public double           SumaCalkowita { get; set; }
    public string           Data          { get; set; } = "";
    public KlientDP?        Klient        { get; set; }
    public List<PozycjaDP>  Pozycje       { get; set; } = new();
}

public class PozycjaDP
{
    public long      Id           { get; set; }
    public long      ZamowienieId { get; set; }
    public long      ProduktId    { get; set; }
    public int       Ilosc        { get; set; }
    public double    CenaWChwili  { get; set; }
    public string    ProduktNazwa { get; set; } = "";
}

public record RaportDP(long LiczbaZamowien, double SumaSprzedazy, long LiczbaKlientow);

// ─── DapperSklep — serwis demonstracyjny ─────────────────────────────────────

public class DapperSklep
{
    private readonly SqliteConnection _conn;

    public DapperSklep(SqliteConnection conn) => _conn = conn;

    // Dynamiczne WHERE — bezpieczne budowanie zapytania
    public async Task<IEnumerable<ProduktDP>> PobierzProduktyAsync(
        string? fraza = null, double? minCena = null, double? maxCena = null)
    {
        var sql    = new StringBuilder("SELECT * FROM Produkty WHERE Aktywny = 1");
        var param  = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(fraza))
        { sql.Append(" AND Nazwa LIKE @Fraza"); param.Add("@Fraza", $"%{fraza}%"); }
        if (minCena.HasValue)
        { sql.Append(" AND Cena >= @MinCena");  param.Add("@MinCena", minCena.Value); }
        if (maxCena.HasValue)
        { sql.Append(" AND Cena <= @MaxCena");  param.Add("@MaxCena", maxCena.Value); }

        sql.Append(" ORDER BY Nazwa");
        return await _conn.QueryAsync<ProduktDP>(sql.ToString(), param);
    }

    // Transakcja ze słownikiem zwracanym przez QueryMultiple
    public async Task<long> ZlozZamowienieAsync(
        long klientId, IEnumerable<(long ProduktId, int Ilosc)> pozycje)
    {
        using var trx = _conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Pobierz ceny produktów
            var ids      = pozycje.Select(p => p.ProduktId).ToArray();
            var produkty = (await _conn.QueryAsync<ProduktDP>(
                "SELECT * FROM Produkty WHERE Id IN @Ids AND Aktywny = 1",
                new { Ids = ids }, trx)).ToDictionary(p => p.Id);

            double suma = pozycje.Sum(p =>
                produkty.TryGetValue(p.ProduktId, out var prod) ? prod.Cena * p.Ilosc : 0);

            // Wstaw zamówienie
            long zamId = await _conn.ExecuteScalarAsync<long>("""
                INSERT INTO Zamowienia (KlientId, Status, SumaCalkowita, Data)
                VALUES (@KlientId, 'Nowe', @Suma, datetime('now'));
                SELECT last_insert_rowid();
                """,
                new { KlientId = klientId, Suma = suma }, trx);

            // Wstaw pozycje — bulk Execute
            var pozycjeEncje = pozycje.Select(p => new
            {
                ZamowienieId = zamId,
                p.ProduktId,
                p.Ilosc,
                CenaWChwili  = produkty.TryGetValue(p.ProduktId, out var pr) ? pr.Cena : 0.0
            }).ToList();

            await _conn.ExecuteAsync("""
                INSERT INTO PozycjeZamowien (ZamowienieId, ProduktId, Ilosc, CenaWChwili)
                VALUES (@ZamowienieId, @ProduktId, @Ilosc, @CenaWChwili)
                """, pozycjeEncje, trx);

            trx.Commit();
            return zamId;
        }
        catch { trx.Rollback(); throw; }
    }

    // QueryMultiple — wiele result setów w jednym round-trip
    public async Task<(IEnumerable<ProduktDP> Produkty, RaportDP Raport)>
        PobierzRaportAsync()
    {
        using var grid = await _conn.QueryMultipleAsync("""
            SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny, p.KategoriaId
            FROM Produkty p WHERE p.Aktywny = 1 ORDER BY p.Cena DESC;

            SELECT COUNT(*) AS LiczbaZamowien, COALESCE(SUM(SumaCalkowita),0) AS SumaSprzedazy,
                   COUNT(DISTINCT KlientId) AS LiczbaKlientow
            FROM Zamowienia WHERE Status != 'Anulowane';
            """);

        var produkty = await grid.ReadAsync<ProduktDP>();
        var raport   = await grid.ReadFirstAsync<RaportDP>();
        return (produkty, raport);
    }
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class DapperDemo
{
    private static readonly SqliteConnection _conn =
        new SqliteConnection("Data Source=:memory:");

    private static bool _init;

    private static void EnsureInit()
    {
        if (_init) return;
        _conn.Open();
        _conn.Execute("""
            CREATE TABLE Kategorie (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Nazwa TEXT NOT NULL
            );
            CREATE TABLE Produkty (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Nazwa        TEXT    NOT NULL,
                Cena         REAL    NOT NULL,
                StanMagazynu INTEGER NOT NULL DEFAULT 0,
                Aktywny      INTEGER NOT NULL DEFAULT 1,
                KategoriaId  INTEGER NOT NULL REFERENCES Kategorie(Id)
            );
            CREATE TABLE Klienci (
                Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                Imie     TEXT NOT NULL,
                Nazwisko TEXT NOT NULL,
                Email    TEXT NOT NULL
            );
            CREATE TABLE Zamowienia (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                KlientId      INTEGER NOT NULL REFERENCES Klienci(Id),
                Status        TEXT NOT NULL DEFAULT 'Nowe',
                SumaCalkowita REAL NOT NULL DEFAULT 0,
                Data          TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE PozycjeZamowien (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                ZamowienieId INTEGER NOT NULL REFERENCES Zamowienia(Id),
                ProduktId    INTEGER NOT NULL REFERENCES Produkty(Id),
                Ilosc        INTEGER NOT NULL,
                CenaWChwili  REAL    NOT NULL
            );
            INSERT INTO Kategorie (Nazwa) VALUES ('Elektronika'), ('Odzież'), ('Narzędzia');
            INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, KategoriaId)
                VALUES ('Laptop',    3499.99, 10, 1),
                       ('Słuchawki',  299.99, 25, 1),
                       ('Kurtka',     199.99, 15, 2),
                       ('Wiertarka',  599.99,  8, 3),
                       ('Monitor',   1299.99,  5, 1);
            INSERT INTO Klienci (Imie, Nazwisko, Email)
                VALUES ('Anna', 'Kowalska', 'anna@email.pl'),
                       ('Jan',  'Nowak',    'jan@email.pl');
            """);
        _init = true;
    }

    // 1. Warianty QueryAsync
    public static async Task DemoZapytaniaAsync()
    {
        Console.WriteLine("\n--- Dapper: Warianty QueryAsync ---");
        EnsureInit();

        // QueryAsync<T> — lista wyników
        var wszystkie = await _conn.QueryAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Aktywny = 1 ORDER BY Cena");
        Console.WriteLine($"QueryAsync: {wszystkie.Count()} produktów");

        // QueryFirstAsync<T> — pierwszy lub wyjątek gdy brak
        var pierwszyLP = await _conn.QueryFirstAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Cena < @Max ORDER BY Cena",
            new { Max = 400.0 });
        Console.WriteLine($"QueryFirstAsync: {pierwszyLP.Nazwa} ({pierwszyLP.Cena:C})");

        // QueryFirstOrDefaultAsync<T> — pierwszy lub null
        var nieIstniejacy = await _conn.QueryFirstOrDefaultAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Id = 999");
        Console.WriteLine($"QueryFirstOrDefault (id=999): {nieIstniejacy?.Nazwa ?? "null"}");

        // QuerySingleAsync<T> — dokładnie 1 wynik lub wyjątek
        var laptop = await _conn.QuerySingleAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Nazwa = @Nazwa",
            new { Nazwa = "Laptop" });
        Console.WriteLine($"QuerySingle: {laptop.Nazwa} ID={laptop.Id}");

        // QuerySingleOrDefaultAsync<T> — 0 lub 1 wynik lub wyjątek (>1)
        var monitor = await _conn.QuerySingleOrDefaultAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Nazwa = 'Monitor'");
        Console.WriteLine($"QuerySingleOrDefault: {monitor?.Nazwa}");

        // Scalar — pojedyncza wartość
        long count   = await _conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Produkty");
        double srednia = await _conn.ExecuteScalarAsync<double>("SELECT AVG(Cena) FROM Produkty");
        Console.WriteLine($"Scalar COUNT={count}, AVG={srednia:C}");
    }

    // 2. Typy dynamiczne i IDictionary
    public static async Task DemoDynamicAsync()
    {
        Console.WriteLine("\n--- Dapper: Dynamic Types ---");
        EnsureInit();

        // dynamic — Dapper zwraca DapperRow implementujący IDictionary
        var dynamicProdukty = await _conn.QueryAsync(
            "SELECT Id, Nazwa, Cena FROM Produkty ORDER BY Id LIMIT 3");
        foreach (dynamic p in dynamicProdukty)
            Console.WriteLine($"  dynamic: Id={p.Id}, Nazwa={p.Nazwa}, Cena={p.Cena}");

        // IDictionary<string, object> — DapperRow implementuje ten interfejs; rzutujemy dynamic
        var dictRows = await _conn.QueryAsync(
            "SELECT Id, Nazwa, Cena FROM Produkty ORDER BY Id LIMIT 2");
        foreach (IDictionary<string, object> dict in dictRows)
        {
            Console.Write("  dict: ");
            foreach (var kv in dict)
                Console.Write($"{kv.Key}={kv.Value} ");
            Console.WriteLine();
        }
    }

    // 3. Parametry: anonymous, DynamicParameters, IN clause, NULL
    public static async Task DemoParametryAsync()
    {
        Console.WriteLine("\n--- Dapper: Parametry ---");
        EnsureInit();

        // Anonymous object — najprościej
        var anonParam = await _conn.QueryAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE KategoriaId = @KatId AND Cena < @Max",
            new { KatId = 1L, Max = 2000.0 });
        Console.WriteLine($"Anonymous params: {anonParam.Count()} produktów elektronicznych <2000 PLN");

        // DynamicParameters — więcej kontroli (typ, rozmiar, kierunek)
        var dp = new DynamicParameters();
        dp.Add("@Fraza", "%a%",   DbType.String);
        dp.Add("@MinStan", 5,     DbType.Int32);
        var dynResult = await _conn.QueryAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Nazwa LIKE @Fraza AND StanMagazynu >= @MinStan",
            dp);
        Console.WriteLine($"DynamicParameters: {dynResult.Count()} produktów na 'a' z stanem>=5");

        // IN clause — Dapper automatycznie rozszerza tablicę do (1,2,3)
        var ids = new long[] { 1, 2, 3 };
        var inResult = await _conn.QueryAsync<ProduktDP>(
            "SELECT * FROM Produkty WHERE Id IN @Ids",
            new { Ids = ids });
        Console.WriteLine($"IN clause (ids=[1,2,3]): {inResult.Count()} produktów");

        // Wartości NULL — DynamicParameters lub DBNull.Value
        var dpNull = new DynamicParameters();
        dpNull.Add("@Stan",  value: null, dbType: DbType.Int32); // NULL zamiast 0
        dpNull.Add("@Nazwa", "TestNullStanu", DbType.String);
        await _conn.ExecuteAsync(
            "INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, KategoriaId) " +
            "VALUES (@Nazwa, 0.0, COALESCE(@Stan, 0), 1)", dpNull);
        Console.WriteLine("NULL param: dodano produkt ze stanem NULL→COALESCE→0");

        // Posprzątaj test
        await _conn.ExecuteAsync("DELETE FROM Produkty WHERE Nazwa = 'TestNullStanu'");
    }

    // 4. ExecuteAsync — INSERT, UPDATE, DELETE; bulk
    public static async Task DemoExecuteAsync()
    {
        Console.WriteLine("\n--- Dapper: ExecuteAsync ---");
        EnsureInit();

        // ExecuteAsync — zwraca liczbę zmodyfikowanych wierszy
        int rows = await _conn.ExecuteAsync(
            "INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, KategoriaId) " +
            "VALUES (@Nazwa, @Cena, @Stan, @KatId)",
            new { Nazwa = "Router WiFi", Cena = 249.99, Stan = 12, KatId = 1L });
        Console.WriteLine($"ExecuteAsync INSERT: {rows} wiersz");

        // ExecuteScalarAsync — INSERT + zwróć ID
        long newId = await _conn.ExecuteScalarAsync<long>(
            "INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, KategoriaId) " +
            "VALUES (@Nazwa, @Cena, @Stan, @KatId); SELECT last_insert_rowid();",
            new { Nazwa = "Powerbank", Cena = 89.99, Stan = 30, KatId = 1L });
        Console.WriteLine($"ExecuteScalar INSERT+ID: newId={newId}");

        // Bulk INSERT — lista obiektów; Dapper wywoła INSERT wielokrotnie
        var noweKategorie = new[]
        {
            new { Nazwa = "Sport" },
            new { Nazwa = "Książki" }
        };
        int bulkRows = await _conn.ExecuteAsync(
            "INSERT INTO Kategorie (Nazwa) VALUES (@Nazwa)", noweKategorie);
        Console.WriteLine($"Bulk INSERT: {bulkRows} kategorii dodanych");

        // UPDATE
        int updated = await _conn.ExecuteAsync(
            "UPDATE Produkty SET Cena = Cena * 0.9 WHERE KategoriaId = 1");
        Console.WriteLine($"UPDATE -10% dla elektroniki: {updated} produktów");

        // Posprzątaj
        await _conn.ExecuteAsync(
            "DELETE FROM Produkty WHERE Id IN (@Id1, @Id2)",
            new { Id1 = newId - 1, Id2 = newId });
        await _conn.ExecuteAsync("DELETE FROM Kategorie WHERE Nazwa IN ('Sport','Książki')");
    }

    // 5. Multi-mapping — JOIN z rozbiciem na wiele typów
    public static async Task DemoMultiMappingAsync()
    {
        Console.WriteLine("\n--- Dapper: Multi-Mapping ---");
        EnsureInit();

        // QueryAsync<T1, T2, TReturn> z splitOn
        // Dapper dzieli kolumny na dwa typy przy kolumnie "Id" (drugie wystąpienie)
        var produktyZKat = await _conn.QueryAsync<ProduktDP, KategoriaDP, ProduktDP>(
            sql: """
                SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny, p.KategoriaId,
                       k.Id, k.Nazwa
                FROM Produkty p
                JOIN Kategorie k ON p.KategoriaId = k.Id
                ORDER BY p.Id
                """,
            map: (produkt, kategoria) =>
            {
                produkt.Kategoria = kategoria;
                return produkt;
            },
            splitOn: "Id"); // drugi "Id" zaczyna KategoriaDP

        Console.WriteLine("Multi-mapping JOIN Produkt + Kategoria:");
        foreach (var p in produktyZKat)
            Console.WriteLine($"  {p.Nazwa,-12} | Kat: {p.Kategoria?.Nazwa}");

        // Lookup pattern — 1:N grupowanie w pamięci
        // Dodaj testowe zamówienie do grupowania
        await _conn.ExecuteAsync("""
            INSERT INTO Zamowienia (KlientId, Status, SumaCalkowita)
                VALUES (1, 'Nowe', 3799.98);
            INSERT INTO PozycjeZamowien (ZamowienieId, ProduktId, Ilosc, CenaWChwili)
                VALUES (last_insert_rowid(), 1, 1, 3499.99),
                       (last_insert_rowid() - 0, 2, 1,  299.99);
            """);

        var lookup = new Dictionary<long, ZamowienieDP>();
        await _conn.QueryAsync<ZamowienieDP, KlientDP, PozycjaDP, ZamowienieDP>(
            sql: """
                SELECT z.Id, z.KlientId, z.Status, z.SumaCalkowita, z.Data,
                       k.Id, k.Imie, k.Nazwisko, k.Email,
                       pz.Id, pz.ZamowienieId, pz.ProduktId, pz.Ilosc, pz.CenaWChwili
                FROM Zamowienia z
                JOIN Klienci k ON z.KlientId = k.Id
                JOIN PozycjeZamowien pz ON pz.ZamowienieId = z.Id
                ORDER BY z.Id
                """,
            map: (zam, klient, pozycja) =>
            {
                if (!lookup.TryGetValue(zam.Id, out var existing))
                {
                    existing = zam;
                    existing.Klient = klient;
                    lookup[zam.Id] = existing;
                }
                existing.Pozycje.Add(pozycja);
                return existing;
            },
            splitOn: "Id,Id");

        Console.WriteLine("Lookup pattern (1:N — jedno zamówienie, wiele pozycji):");
        foreach (var z in lookup.Values)
        {
            Console.WriteLine($"  Zam#{z.Id} | {z.Klient?.Imie} | {z.SumaCalkowita:C}");
            foreach (var p in z.Pozycje)
                Console.WriteLine($"    PozId={p.Id} | ProdId={p.ProduktId} | {p.Ilosc}× {p.CenaWChwili:C}");
        }
    }

    // 6. QueryMultipleAsync — wiele result setów
    public static async Task DemoQueryMultipleAsync()
    {
        Console.WriteLine("\n--- Dapper: QueryMultipleAsync ---");
        EnsureInit();

        using var grid = await _conn.QueryMultipleAsync("""
            SELECT * FROM Produkty WHERE Aktywny = 1 ORDER BY Cena LIMIT 3;
            SELECT COUNT(*) FROM Produkty;
            SELECT Id, Nazwa FROM Kategorie ORDER BY Id;
            """);

        // ReadAsync — lista z pierwszego result set
        var produkty = await grid.ReadAsync<ProduktDP>();
        Console.WriteLine($"Produkty (top 3 wg ceny): {string.Join(", ", produkty.Select(p => p.Nazwa))}");

        // ReadFirstAsync — pierwsza wartość z drugiego result set
        long totalProd = await grid.ReadFirstAsync<long>();
        Console.WriteLine($"Łącznie produktów: {totalProd}");

        // ReadAsync z mapowaniem trzeciego result set
        var kategorie = await grid.ReadAsync<KategoriaDP>();
        Console.WriteLine($"Kategorie: {string.Join(", ", kategorie.Select(k => k.Nazwa))}");
    }

    // 7. Transakcja Dapper — przekazanie trx do każdego wywołania
    public static async Task DemoTransakcjaAsync()
    {
        Console.WriteLine("\n--- Dapper: Transakcja ---");
        EnsureInit();

        using var trx = _conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Każde wywołanie Dapper dostaje transakcję jako parametr
            long nowyKlientId = await _conn.ExecuteScalarAsync<long>(
                "INSERT INTO Klienci (Imie, Nazwisko, Email) " +
                "VALUES (@I, @N, @E); SELECT last_insert_rowid();",
                new { I = "Piotr", N = "Wiśniewski", E = "piotr@email.pl" },
                trx);

            long noweZamId = await _conn.ExecuteScalarAsync<long>(
                "INSERT INTO Zamowienia (KlientId, Status, SumaCalkowita) " +
                "VALUES (@KId, 'Nowe', @Suma); SELECT last_insert_rowid();",
                new { KId = nowyKlientId, Suma = 599.99 },
                trx);

            await _conn.ExecuteAsync(
                "INSERT INTO PozycjeZamowien (ZamowienieId, ProduktId, Ilosc, CenaWChwili) " +
                "VALUES (@ZId, 4, 1, 599.99)",
                new { ZId = noweZamId },
                trx);

            trx.Commit();
            Console.WriteLine($"Transakcja OK: klient={nowyKlientId}, zamówienie={noweZamId}");

            // Weryfikacja
            var klientZam = await _conn.QueryFirstAsync<dynamic>(
                "SELECT k.Imie, z.SumaCalkowita FROM Klienci k " +
                "JOIN Zamowienia z ON z.KlientId = k.Id WHERE k.Id = @Id",
                new { Id = nowyKlientId });
            Console.WriteLine($"Weryfikacja: {klientZam.Imie}, zamówienie={klientZam.SumaCalkowita:C}");
        }
        catch
        {
            trx.Rollback();
            Console.WriteLine("Transakcja ROLLED BACK");
            throw;
        }
    }

    // 8. DapperSklep — kompletny serwis
    public static async Task DemoDapperSklepAsync()
    {
        Console.WriteLine("\n--- Dapper: DapperSklep ---");
        EnsureInit();

        var sklep = new DapperSklep(_conn);

        // PobierzProduktyAsync z filtrowaniem
        var elektro = await sklep.PobierzProduktyAsync(maxCena: 500.0);
        Console.WriteLine($"PobierzProdukty (max 500 PLN): {elektro.Count()} produktów");

        var zFraza = await sklep.PobierzProduktyAsync("a");
        Console.WriteLine($"PobierzProdukty (fraza='a'): {string.Join(", ", zFraza.Select(p => p.Nazwa))}");

        // ZlozZamowienieAsync — transakcja wewnątrz serwisu
        long zamId = await sklep.ZlozZamowienieAsync(2L, new[]
        {
            (ProduktId: 2L, Ilosc: 2),
            (ProduktId: 3L, Ilosc: 1)
        });
        Console.WriteLine($"ZlozZamowienie: zamówienie ID={zamId}");

        // PobierzRaportAsync — QueryMultipleAsync
        var (produkty, raport) = await sklep.PobierzRaportAsync();
        Console.WriteLine($"Raport: zamówień={raport.LiczbaZamowien}, " +
                         $"sprzedaż={raport.SumaSprzedazy:C}, klientów={raport.LiczbaKlientow}");
        Console.WriteLine($"Aktywnych produktów: {produkty.Count()}");

        // ADO.NET vs Dapper porównanie:
        // ADO.NET : SqlDataReader, ręczne mapowanie, pełna kontrola, verbose
        // Dapper  : auto-mapowanie przez reflection, QueryAsync<T>, niemal tak szybki jak ADO.NET
        // EF Core : pełny ORM, LINQ, Change Tracker, migracje, ale narzut abstrakcji
        Console.WriteLine("ADO.NET: pełna kontrola | Dapper: mapowanie+SQL | EF Core: pełny ORM");
    }
}
