namespace _05_Databases;

using Microsoft.Data.Sqlite;
using System.Data;

// ─── Modele (suffix AN) ──────────────────────────────────────────────────────

public class KategoriaAN
{
    public long   Id    { get; set; }
    public string Nazwa { get; set; } = "";
}

public class ProduktAN
{
    public long   Id             { get; set; }
    public string Nazwa          { get; set; } = "";
    public double Cena           { get; set; }
    public int    StanMagazynu   { get; set; }
    public bool   Aktywny        { get; set; } = true;
    public long   KategoriaId    { get; set; }
    public string KategoriaNazwa { get; set; } = "";
}

// ─── Repository Pattern ───────────────────────────────────────────────────────

public interface IProduktRepositoryAN
{
    Task<IReadOnlyList<ProduktAN>> PobierzWszystkieAsync();
    Task<ProduktAN?>               PobierzPoIdAsync(long id);
    Task<long>                     DodajAsync(ProduktAN produkt);
    Task<bool>                     AktualizujAsync(ProduktAN produkt);
    Task<bool>                     UsunAsync(long id);
    Task<IReadOnlyList<ProduktAN>> SzukajAsync(string? fraza, double? minCena, double? maxCena);
}

public class AdoNetProduktRepository : IProduktRepositoryAN
{
    private readonly SqliteConnection _conn;

    public AdoNetProduktRepository(SqliteConnection conn) => _conn = conn;

    public async Task<IReadOnlyList<ProduktAN>> PobierzWszystkieAsync()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny,
                   p.KategoriaId, k.Nazwa AS KategoriaNazwa
            FROM Produkty p
            JOIN Kategorie k ON p.KategoriaId = k.Id
            ORDER BY p.Nazwa
            """;
        var wyniki = new List<ProduktAN>();
        using var reader = await cmd.ExecuteReaderAsync();
        int oId   = reader.GetOrdinal("Id");
        int oNaz  = reader.GetOrdinal("Nazwa");
        int oCena = reader.GetOrdinal("Cena");
        int oStan = reader.GetOrdinal("StanMagazynu");
        int oAkt  = reader.GetOrdinal("Aktywny");
        int oKatId  = reader.GetOrdinal("KategoriaId");
        int oKatNaz = reader.GetOrdinal("KategoriaNazwa");
        while (await reader.ReadAsync())
        {
            wyniki.Add(new ProduktAN
            {
                Id             = reader.GetInt64(oId),
                Nazwa          = reader.GetString(oNaz),
                Cena           = reader.GetDouble(oCena),
                StanMagazynu   = reader.GetInt32(oStan),
                Aktywny        = reader.GetBoolean(oAkt),
                KategoriaId    = reader.GetInt64(oKatId),
                KategoriaNazwa = reader.GetString(oKatNaz)
            });
        }
        return wyniki;
    }

    public async Task<ProduktAN?> PobierzPoIdAsync(long id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny,
                   p.KategoriaId, k.Nazwa AS KategoriaNazwa
            FROM Produkty p
            JOIN Kategorie k ON p.KategoriaId = k.Id
            WHERE p.Id = @Id
            """;
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new ProduktAN
        {
            Id             = reader.GetInt64(0),
            Nazwa          = reader.GetString(1),
            Cena           = reader.GetDouble(2),
            StanMagazynu   = reader.GetInt32(3),
            Aktywny        = reader.GetBoolean(4),
            KategoriaId    = reader.GetInt64(5),
            KategoriaNazwa = reader.GetString(6)
        };
    }

    public async Task<long> DodajAsync(ProduktAN produkt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, Aktywny, KategoriaId)
            VALUES (@Nazwa, @Cena, @Stan, @Akt, @KatId);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@Nazwa", produkt.Nazwa);
        cmd.Parameters.AddWithValue("@Cena",  produkt.Cena);
        cmd.Parameters.AddWithValue("@Stan",  produkt.StanMagazynu);
        cmd.Parameters.AddWithValue("@Akt",   produkt.Aktywny ? 1 : 0);
        cmd.Parameters.AddWithValue("@KatId", produkt.KategoriaId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<bool> AktualizujAsync(ProduktAN produkt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Produkty
            SET Nazwa = @Nazwa, Cena = @Cena, StanMagazynu = @Stan, Aktywny = @Akt
            WHERE Id = @Id
            """;
        cmd.Parameters.AddWithValue("@Nazwa", produkt.Nazwa);
        cmd.Parameters.AddWithValue("@Cena",  produkt.Cena);
        cmd.Parameters.AddWithValue("@Stan",  produkt.StanMagazynu);
        cmd.Parameters.AddWithValue("@Akt",   produkt.Aktywny ? 1 : 0);
        cmd.Parameters.AddWithValue("@Id",    produkt.Id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UsunAsync(long id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Produkty WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<IReadOnlyList<ProduktAN>> SzukajAsync(
        string? fraza, double? minCena, double? maxCena)
    {
        using var cmd = _conn.CreateCommand();
        var warunki = new List<string> { "p.Aktywny = 1" };
        if (!string.IsNullOrWhiteSpace(fraza))
        {
            warunki.Add("p.Nazwa LIKE @Fraza");
            cmd.Parameters.AddWithValue("@Fraza", $"%{fraza}%");
        }
        if (minCena.HasValue)
        {
            warunki.Add("p.Cena >= @MinCena");
            cmd.Parameters.AddWithValue("@MinCena", minCena.Value);
        }
        if (maxCena.HasValue)
        {
            warunki.Add("p.Cena <= @MaxCena");
            cmd.Parameters.AddWithValue("@MaxCena", maxCena.Value);
        }
        cmd.CommandText = $"""
            SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny,
                   p.KategoriaId, k.Nazwa AS KategoriaNazwa
            FROM Produkty p
            JOIN Kategorie k ON p.KategoriaId = k.Id
            WHERE {string.Join(" AND ", warunki)}
            ORDER BY p.Cena
            """;
        var wyniki = new List<ProduktAN>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            wyniki.Add(new ProduktAN
            {
                Id = reader.GetInt64(0), Nazwa = reader.GetString(1),
                Cena = reader.GetDouble(2), StanMagazynu = reader.GetInt32(3),
                Aktywny = reader.GetBoolean(4), KategoriaId = reader.GetInt64(5),
                KategoriaNazwa = reader.GetString(6)
            });
        return wyniki;
    }
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class AdoNet
{
    // Statyczne połączenie — in-memory baza żyje tak długo jak połączenie
    internal static readonly SqliteConnection Polaczenie =
        new SqliteConnection("Data Source=:memory:");

    private static bool _init;

    internal static void EnsureInit()
    {
        if (_init) return;
        Polaczenie.Open();
        using var cmd = Polaczenie.CreateCommand();
        cmd.CommandText = """
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
                Email    TEXT NOT NULL UNIQUE
            );
            CREATE TABLE Konta (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Wlasciciel TEXT NOT NULL,
                Saldo      REAL NOT NULL DEFAULT 0
            );
            CREATE TABLE AuditLog (
                Id    INTEGER PRIMARY KEY AUTOINCREMENT,
                Akcja TEXT NOT NULL,
                Czas  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            INSERT INTO Kategorie (Nazwa) VALUES ('Elektronika'), ('Odzież'), ('Narzędzia');
            INSERT INTO Produkty  (Nazwa, Cena, StanMagazynu, KategoriaId)
                VALUES ('Laptop',    3499.99, 10, 1),
                       ('Słuchawki',  299.99, 25, 1),
                       ('Kurtka',     199.99, 15, 2),
                       ('Wiertarka',  599.99,  8, 3);
            INSERT INTO Klienci (Imie, Nazwisko, Email)
                VALUES ('Anna', 'Kowalska', 'anna@email.pl'),
                       ('Jan',  'Nowak',    'jan@email.pl');
            INSERT INTO Konta (Wlasciciel, Saldo)
                VALUES ('Anna Kowalska', 5000.0),
                       ('Jan Nowak',     3000.0);
            """;
        cmd.ExecuteNonQuery();
        _init = true;
    }

    // 1. Połączenie i ConnectionStringBuilder
    public static void DemoPolaczenie()
    {
        Console.WriteLine("\n--- ADO.NET: Połączenie ---");
        EnsureInit();

        // ConnectionStringBuilder — bezpieczne budowanie connection string
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = "sklep.db",        // plik lub :memory:
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Default
        };
        Console.WriteLine($"ConnectionString: {builder.ConnectionString}");

        // Właściwości otwartego połączenia
        Console.WriteLine($"State:         {Polaczenie.State}");
        Console.WriteLine($"Database:      {Polaczenie.Database}");
        Console.WriteLine($"DataSource:    {Polaczenie.DataSource}");
        Console.WriteLine($"ServerVersion: {Polaczenie.ServerVersion}");

        // Connection Pooling: SQL Server — MinPoolSize, MaxPoolSize, Pooling=true
        // SQLite   — każdy plik = własny menadżer blokad, pooling mniej istotny
        // In-memory — JEDNO połączenie trzymaj otwarte! Zamknięcie = utrata danych
        Console.WriteLine("Uwaga: in-memory SQLite → trzymaj połączenie otwarte przez cały czas życia bazy");
    }

    // 2. ExecuteScalar, ExecuteNonQuery, parametryzacja (ochrona przed SQL Injection)
    public static async Task DemoKomendyAsync()
    {
        Console.WriteLine("\n--- ADO.NET: Komendy ---");
        EnsureInit();

        // ExecuteScalarAsync — pojedyncza wartość
        using var cmdCount = Polaczenie.CreateCommand();
        cmdCount.CommandText = "SELECT COUNT(*) FROM Produkty";
        long liczba = (long)(await cmdCount.ExecuteScalarAsync())!;
        Console.WriteLine($"Produktów: {liczba}");

        // INSERT z parametrami — parametry zapobiegają SQL Injection
        using var cmdIns = Polaczenie.CreateCommand();
        cmdIns.CommandText = """
            INSERT INTO Produkty (Nazwa, Cena, StanMagazynu, Aktywny, KategoriaId)
            VALUES (@Nazwa, @Cena, @Stan, @Akt, @KatId);
            SELECT last_insert_rowid();
            """;
        // Sposób 1: AddWithValue (wygodny, ale wymaga poprawnego typowania)
        cmdIns.Parameters.AddWithValue("@Nazwa", "Tablet Pro");
        cmdIns.Parameters.AddWithValue("@Cena",  1299.99);
        cmdIns.Parameters.AddWithValue("@Stan",  5);
        cmdIns.Parameters.AddWithValue("@Akt",   1);
        cmdIns.Parameters.AddWithValue("@KatId", 1L);
        long noweId = (long)(await cmdIns.ExecuteScalarAsync())!;
        Console.WriteLine($"Dodano ID={noweId}");

        // Sposób 2: SqliteParameter z jawnym typem
        using var cmdUpd = Polaczenie.CreateCommand();
        cmdUpd.CommandText = "UPDATE Produkty SET Cena = @Cena WHERE Id = @Id";
        cmdUpd.Parameters.Add(new SqliteParameter("@Cena", SqliteType.Real) { Value = 1199.99 });
        cmdUpd.Parameters.Add(new SqliteParameter("@Id",   SqliteType.Integer) { Value = noweId });
        int rows = await cmdUpd.ExecuteNonQueryAsync();
        Console.WriteLine($"Zaktualizowano wierszy: {rows}");

        // ExecuteNonQueryAsync — DELETE
        using var cmdDel = Polaczenie.CreateCommand();
        cmdDel.CommandText = "DELETE FROM Produkty WHERE Id = @Id";
        cmdDel.Parameters.AddWithValue("@Id", noweId);
        await cmdDel.ExecuteNonQueryAsync();

        // SQL Injection — NIGDY nie konkatenuj danych użytkownika do SQL!
        // string sql = $"SELECT * FROM Produkty WHERE Nazwa = '{wejscie}'"; // BUG!
        // Używaj parametrów: cmd.Parameters.AddWithValue("@Nazwa", wejscie);
        Console.WriteLine("Parametry @Param chronią przed SQL Injection (vs konkatenacja)");
    }

    // 3. SqlDataReader — ordinals, null checks, multiple result sets, GetFieldValue<T>
    public static async Task DemoSqlDataReaderAsync()
    {
        Console.WriteLine("\n--- ADO.NET: SqlDataReader ---");
        EnsureInit();

        using var cmd = Polaczenie.CreateCommand();
        // Dwa result sets w jednym zapytaniu
        cmd.CommandText = """
            SELECT p.Id, p.Nazwa, p.Cena, p.StanMagazynu, p.Aktywny,
                   p.KategoriaId, k.Nazwa AS KategoriaNazwa
            FROM Produkty p
            JOIN Kategorie k ON p.KategoriaId = k.Id
            ORDER BY p.Cena DESC;

            SELECT COUNT(*) AS IloscProduktow, AVG(Cena) AS SredniaCena, MAX(Cena) AS MaxCena
            FROM Produkty WHERE Aktywny = 1;
            """;

        using var reader = await cmd.ExecuteReaderAsync();

        // Ordinals — GetOrdinal() raz przed pętlą, potem używaj indeksu (wydajność)
        int oId    = reader.GetOrdinal("Id");
        int oNaz   = reader.GetOrdinal("Nazwa");
        int oCena  = reader.GetOrdinal("Cena");
        int oStan  = reader.GetOrdinal("StanMagazynu");
        int oAkt   = reader.GetOrdinal("Aktywny");
        int oKatNaz = reader.GetOrdinal("KategoriaNazwa");

        Console.WriteLine("Produkty (wg ceny malejąco):");
        while (await reader.ReadAsync())
        {
            long   id    = reader.GetInt64(oId);
            string nazwa = reader.GetString(oNaz);
            double cena  = reader.GetDouble(oCena);
            // IsDBNull — sprawdzaj zanim odczytasz nullable kolumny
            int    stan  = reader.IsDBNull(oStan) ? 0 : reader.GetInt32(oStan);
            bool   akt   = reader.GetBoolean(oAkt);
            // GetFieldValue<T> — generyczna wersja typowanych GetXxx
            string katNaz = reader.GetFieldValue<string>(oKatNaz);
            Console.WriteLine($"  [{id}] {nazwa,-12} {cena,8:C} stan={stan} " +
                             $"akt={akt} kat={katNaz}");
        }

        // NextResult — przejście do drugiego result set
        await reader.NextResultAsync();
        if (await reader.ReadAsync())
        {
            long   il    = reader.GetInt64(0);
            double sr    = reader.GetDouble(1);
            double max   = reader.GetDouble(2);
            Console.WriteLine($"Raport: {il} produktów | średnia {sr:C} | max {max:C}");
        }
    }

    // 4. Transakcje — BeginTransaction, IsolationLevel, CommitAsync, RollbackAsync
    public static async Task DemoTransakcjeAsync()
    {
        Console.WriteLine("\n--- ADO.NET: Transakcje ---");
        EnsureInit();

        // Odczyt sald przed
        async Task<(double a, double b)> PobierzSaldaAsync()
        {
            using var c = Polaczenie.CreateCommand();
            c.CommandText = "SELECT Saldo FROM Konta WHERE Id IN (1,2) ORDER BY Id";
            using var r = await c.ExecuteReaderAsync();
            await r.ReadAsync(); double sa = r.GetDouble(0);
            await r.ReadAsync(); double sb = r.GetDouble(0);
            return (sa, sb);
        }

        var (przed1, przed2) = await PobierzSaldaAsync();
        Console.WriteLine($"Salda przed: Anna={przed1:C}, Jan={przed2:C}");

        // Przelew bankowy — atomowość (Atomicity z ACID)
        // SQLite IsolationLevel: ReadUncommitted→DEFERRED, Serializable→EXCLUSIVE
        using SqliteTransaction trx = Polaczenie.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            using var cmd1 = Polaczenie.CreateCommand();
            cmd1.Transaction = trx;
            cmd1.CommandText = "UPDATE Konta SET Saldo = Saldo - @K WHERE Id = 1";
            cmd1.Parameters.AddWithValue("@K", 1000.0);
            await cmd1.ExecuteNonQueryAsync();

            using var cmd2 = Polaczenie.CreateCommand();
            cmd2.Transaction = trx;
            cmd2.CommandText = "UPDATE Konta SET Saldo = Saldo + @K WHERE Id = 2";
            cmd2.Parameters.AddWithValue("@K", 1000.0);
            await cmd2.ExecuteNonQueryAsync();

            await trx.CommitAsync(); // OBE operacje naraz
            Console.WriteLine("Przelew 1000 PLN: COMMITTED");
        }
        catch
        {
            await trx.RollbackAsync(); // ŻADNA operacja — atomowość
            Console.WriteLine("Przelew ROLLED BACK");
            throw;
        }

        var (po1, po2) = await PobierzSaldaAsync();
        Console.WriteLine($"Salda po:    Anna={po1:C}, Jan={po2:C}");

        // IsolationLevel porównanie (SQL Server vs SQLite):
        // ReadUncommitted — dirty reads możliwe | SQLite: DEFERRED
        // ReadCommitted   — domyślny SQL Server | SQLite: DEFERRED
        // RepeatableRead  — brak non-repeatable | SQLite: IMMEDIATE (BEGIN IMMEDIATE)
        // Serializable    — pełna izolacja       | SQLite: EXCLUSIVE
        // Snapshot        — MVCC (SQL Server)    | SQLite: brak odpowiednika
        Console.WriteLine("IsolationLevels: SQLite EXCLUSIVE ≈ Serializable (blokuje całą bazę)");
    }

    // 5. Savepoints — częściowe cofanie transakcji
    public static async Task DemoSavepointsAsync()
    {
        Console.WriteLine("\n--- ADO.NET: Savepoints ---");
        EnsureInit();

        using SqliteTransaction trx = Polaczenie.BeginTransaction(IsolationLevel.Serializable);

        using var cmdLog1 = Polaczenie.CreateCommand();
        cmdLog1.Transaction = trx;
        cmdLog1.CommandText = "INSERT INTO AuditLog (Akcja) VALUES ('Start transakcji')";
        await cmdLog1.ExecuteNonQueryAsync();

        // Savepoint — punkt powrotu bez cofania całej transakcji
        await trx.SaveAsync("przed_ryzykiem");
        Console.WriteLine("Savepoint 'przed_ryzykiem' ustawiony");

        try
        {
            using var cmdRyz = Polaczenie.CreateCommand();
            cmdRyz.Transaction = trx;
            // Próba wstawienia duplikatu emaila — UNIQUE constraint fail
            cmdRyz.CommandText = "INSERT INTO Klienci (Imie, Nazwisko, Email) " +
                                  "VALUES ('Test', 'X', 'anna@email.pl')";
            await cmdRyz.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex)
        {
            Console.WriteLine($"Błąd: {ex.SqliteErrorCode} — rollback DO savepoint");
            await trx.RollbackAsync("przed_ryzykiem"); // cofnij TYLKO do savepoint
        }

        // Główna transakcja nadal aktywna
        using var cmdLog2 = Polaczenie.CreateCommand();
        cmdLog2.Transaction = trx;
        cmdLog2.CommandText = "INSERT INTO AuditLog (Akcja) VALUES ('Koniec transakcji')";
        await cmdLog2.ExecuteNonQueryAsync();

        await trx.CommitAsync();

        using var cmdAudit = Polaczenie.CreateCommand();
        cmdAudit.CommandText = "SELECT Akcja FROM AuditLog ORDER BY Id";
        using var r = await cmdAudit.ExecuteReaderAsync();
        Console.WriteLine("AuditLog po transakcji:");
        while (await r.ReadAsync())
            Console.WriteLine($"  > {r.GetString(0)}");
    }

    // 6. Repository Pattern — pełna implementacja CRUD + SzukajAsync
    public static async Task DemoRepositoryAsync()
    {
        Console.WriteLine("\n--- ADO.NET: Repository Pattern ---");
        EnsureInit();

        IProduktRepositoryAN repo = new AdoNetProduktRepository(Polaczenie);

        // PobierzWszystkieAsync
        var wszystkie = await repo.PobierzWszystkieAsync();
        Console.WriteLine($"Produktów w bazie: {wszystkie.Count}");
        foreach (var p in wszystkie)
            Console.WriteLine($"  [{p.Id}] {p.Nazwa,-12} {p.Cena,8:F2} PLN | {p.KategoriaNazwa}");

        // DodajAsync — zwraca nowe ID (last_insert_rowid())
        var nowy = new ProduktAN { Nazwa = "Klawiatura", Cena = 149.99, StanMagazynu = 20, KategoriaId = 1 };
        long newId = await repo.DodajAsync(nowy);
        Console.WriteLine($"Dodano: {nowy.Nazwa} ID={newId}");

        // PobierzPoIdAsync
        var pobrany = await repo.PobierzPoIdAsync(newId);
        Console.WriteLine($"Pobrano: {pobrany?.Nazwa} z kategorii '{pobrany?.KategoriaNazwa}'");

        // SzukajAsync — dynamiczne WHERE z parametrami
        var tanie = await repo.SzukajAsync(null, null, 350.0);
        Console.WriteLine($"Do 350 PLN: {tanie.Count} produktów");

        var elektronika = await repo.SzukajAsync("a", null, null);
        Console.WriteLine($"Zawierające 'a': {elektronika.Count} produktów");

        // AktualizujAsync
        pobrany!.Cena = 129.99;
        bool zaktualizowany = await repo.AktualizujAsync(pobrany);
        Console.WriteLine($"Aktualizacja: {zaktualizowany} (nowa cena: {pobrany.Cena:F2} PLN)");

        // UsunAsync
        bool usuniety = await repo.UsunAsync(newId);
        Console.WriteLine($"Usunięto ID={newId}: {usuniety}");
        Console.WriteLine($"Pozostało: {(await repo.PobierzWszystkieAsync()).Count} produktów");
    }
}
