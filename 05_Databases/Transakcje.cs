namespace _05_Databases;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

// ─── Encje (suffix TR) ───────────────────────────────────────────────────────

public class KategoriaTR
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";
}

public class ProduktTR
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public int     StanMagazynu { get; set; }
    public bool    Aktywny      { get; set; } = true;

    public int KategoriaTRId { get; set; }

    // Współbieżność optymistyczna — ręcznie inkrementowany Version
    // EF Core generuje: WHERE Id = @id AND Version = @originalVersion
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}

public class KontoTR
{
    public int     Id         { get; set; }
    public string  Wlasciciel { get; set; } = "";
    public decimal Saldo      { get; set; }
    public bool    CzyAktywne { get; set; } = true;

    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}

public class TransakcjaBankowaTR
{
    public int      Id               { get; set; }
    public int      KontoZrodloweId  { get; set; }
    public int      KontoDoceloweId  { get; set; }
    public decimal  Kwota            { get; set; }
    public string   Status           { get; set; } = "";
    public DateTime Czas             { get; set; } = DateTime.UtcNow;
}

// ─── DbContext ────────────────────────────────────────────────────────────────

public class TransakcjeContext : DbContext
{
    public DbSet<KategoriaTR>         Kategorie         { get; set; }
    public DbSet<ProduktTR>           Produkty          { get; set; }
    public DbSet<KontoTR>             Konta             { get; set; }
    public DbSet<TransakcjaBankowaTR> TransakcjeBankowe { get; set; }

    public TransakcjeContext(DbContextOptions<TransakcjeContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ProduktTR>().ToTable("ProduktyTR").HasKey(p => p.Id);
        mb.Entity<KontoTR>().ToTable("KontaTR").HasKey(k => k.Id);
        mb.Entity<KategoriaTR>().ToTable("KategorieTR").HasKey(k => k.Id);
        mb.Entity<TransakcjaBankowaTR>().ToTable("TransakcjeBankoweTR").HasKey(t => t.Id);
    }
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class Transakcje
{
    private static readonly SqliteConnection _conn =
        new SqliteConnection("Data Source=:memory:");
    private static bool _init;

    private static TransakcjeContext NowCtx()
    {
        if (!_init)
        {
            _conn.Open();
            using var seed = new TransakcjeContext(
                new DbContextOptionsBuilder<TransakcjeContext>().UseSqlite(_conn).Options);
            seed.Database.EnsureCreated();
            seed.Kategorie.AddRange(
                new KategoriaTR { Id = 1, Nazwa = "Elektronika" },
                new KategoriaTR { Id = 2, Nazwa = "Odzież" });
            seed.Produkty.AddRange(
                new ProduktTR { Id = 1, Nazwa = "Laptop",    Cena = 3499m, StanMagazynu = 10, KategoriaTRId = 1 },
                new ProduktTR { Id = 2, Nazwa = "Słuchawki", Cena =  299m, StanMagazynu = 25, KategoriaTRId = 1 },
                new ProduktTR { Id = 3, Nazwa = "Kurtka",    Cena =  199m, StanMagazynu = 15, KategoriaTRId = 2 });
            seed.Konta.AddRange(
                new KontoTR { Id = 1, Wlasciciel = "Anna Kowalska", Saldo = 5000m },
                new KontoTR { Id = 2, Wlasciciel = "Jan Nowak",     Saldo = 3000m });
            seed.SaveChanges();
            _init = true;
        }
        return new TransakcjeContext(
            new DbContextOptionsBuilder<TransakcjeContext>().UseSqlite(_conn).Options);
    }

    // 1. ACID — Atomicity demo (przelew bankowy)
    public static async Task DemoACIDAsync()
    {
        Console.WriteLine("\n--- Transakcje: ACID ---");

        // ACID = Atomicity, Consistency, Isolation, Durability
        // Atomicity — "wszystko albo nic"
        await using (var ctx = NowCtx())
        {
            var konta = await ctx.Konta.ToListAsync();
            Console.WriteLine($"Przed przelewem: Anna={konta[0].Saldo:C}, Jan={konta[1].Saldo:C}");
        }

        SqliteConnection conn = _conn; // współdzielone — nie dispose
        using SqliteTransaction trx = conn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Operacja 1 — odejmij z konta A
            using var cmd1 = conn.CreateCommand();
            cmd1.Transaction = trx;
            cmd1.CommandText = "UPDATE KontaTR SET Saldo = Saldo - 1000, Version = Version + 1 WHERE Id = 1";
            await cmd1.ExecuteNonQueryAsync();

            // Operacja 2 — dodaj do konta B
            using var cmd2 = conn.CreateCommand();
            cmd2.Transaction = trx;
            cmd2.CommandText = "UPDATE KontaTR SET Saldo = Saldo + 1000, Version = Version + 1 WHERE Id = 2";
            await cmd2.ExecuteNonQueryAsync();

            // OBE operacje lub ŻADNA — Atomicity
            await trx.CommitAsync();
            Console.WriteLine("Przelew 1000 PLN: ATOMIC COMMIT");
        }
        catch
        {
            await trx.RollbackAsync();
            Console.WriteLine("Przelew ROLLBACK — żadna zmiana nie weszła");
            throw;
        }

        await using (var ctx = NowCtx())
        {
            var konta = await ctx.Konta.ToListAsync();
            Console.WriteLine($"Po przelewie: Anna={konta[0].Saldo:C}, Jan={konta[1].Saldo:C}");
        }

        // CONSISTENCY — baza zawsze w spójnym stanie (constraints, reguły biznesowe)
        // ISOLATION   — transakcje nie widzą nawzajem niezatwierdzonych zmian (poziomy izolacji)
        // DURABILITY  — zatwierdzone zmiany przeżywają awarie (WAL — Write-Ahead Log)
        Console.WriteLine("ACID: Atomicity✓ | Consistency=constraints | Isolation=poziomy | Durability=WAL");
    }

    // 2. Poziomy izolacji — tabela anomalii + SQLite mapowanie
    public static async Task DemoIzolacjaAsync()
    {
        Console.WriteLine("\n--- Transakcje: Poziomy izolacji ---");

        // ┌──────────────────┬──────────┬──────────────┬─────────────┬───────────────┐
        // │ Poziom           │ Dirty    │Non-repeatable│ Phantom     │ SQLite odpow. │
        // │                  │ Read     │ Read         │ Read        │               │
        // ├──────────────────┼──────────┼──────────────┼─────────────┼───────────────┤
        // │ ReadUncommitted  │ ✗ możliwe│ ✗ możliwe    │ ✗ możliwe  │ DEFERRED      │
        // │ ReadCommitted    │ ✓ brak   │ ✗ możliwe    │ ✗ możliwe  │ DEFERRED      │
        // │ RepeatableRead   │ ✓ brak   │ ✓ brak       │ ✗ możliwe  │ IMMEDIATE     │
        // │ Serializable     │ ✓ brak   │ ✓ brak       │ ✓ brak     │ EXCLUSIVE     │
        // │ Snapshot (MVCC)  │ ✓ brak   │ ✓ brak       │ ✓ brak     │ brak (SQL Srv)│
        // └──────────────────┴──────────┴──────────────┴─────────────┴───────────────┘

        // SQLite obsługuje tylko DEFERRED i EXCLUSIVE przez Microsoft.Data.Sqlite
        // Poniżej demonstrujemy z komentarzami jak zachowuje się każdy poziom

        // READ UNCOMMITTED — widzisz niezatwierdzone zmiany innej transakcji (dirty reads)
        await using (var ctx = NowCtx())
        {
            // SQLite: BEGIN DEFERRED — nie blokuje; dirty reads nie są możliwe w SQLite
            await using var trx1 = await ctx.Database
                .BeginTransactionAsync(IsolationLevel.ReadUncommitted);

            var produkt = await ctx.Produkty.FindAsync(1);
            Console.WriteLine($"ReadUncommitted — Laptop cena: {produkt!.Cena:C}");
            await trx1.CommitAsync();
        }

        // SERIALIZABLE — pełna izolacja, najwolniejszy
        await using (var ctx = NowCtx())
        {
            // SQLite: BEGIN EXCLUSIVE — blokuje całą bazę dla innych transakcji
            await using var trx2 = await ctx.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var suma = await ctx.Produkty.SumAsync(p => (double)p.Cena);
            Console.WriteLine($"Serializable — suma cen: {suma:C} (żadna zmiana nie wejdzie podczas zapytania)");
            await trx2.CommitAsync();
        }

        // Praktyczne porównanie dla SQL Server:
        // ReadCommitted  — domyślny, dobre dla większości serwisów
        // RepeatableRead — gdy czytasz wielokrotnie i musisz mieć konsekwentne dane
        // Serializable   — finansowe obliczenia, raporty sumaryczne
        // Snapshot       — SQL Server MVCC: czytasz snapshot z BEGIN, bez blokad odczytu
        Console.WriteLine("SQL Server: ReadCommitted=domyślny, Snapshot=MVCC bez blokad odczytu");

        // Phantom reads — SELECT COUNT(*) może zwrócić różne wartości
        // gdy inna transakcja dodała wiersze między odczytami
        // Zapobieganie: Serializable lub Snapshot isolation
        Console.WriteLine("Phantom: COUNT może się zmienić gdy inna trx dodała wiersze → Serializable/Snapshot");
    }

    // 3. Optimistic Concurrency — ConcurrencyCheck + DbUpdateConcurrencyException
    public static async Task DemoOptymistycznaAsync()
    {
        Console.WriteLine("\n--- Transakcje: Optymistyczna współbieżność ---");

        // Symulacja: Użytkownik A i B pobierają ten sam produkt niezależnie
        await using var ctxA = NowCtx();
        await using var ctxB = NowCtx();

        // Obaj pobierają produkt (OriginalValue.Version = 0 lub aktualny)
        var produktA = await ctxA.Produkty.FindAsync(1);
        var produktB = await ctxB.Produkty.FindAsync(1);

        Console.WriteLine($"Obaj pobrali: {produktA!.Nazwa}, Version={produktA.Version}, Cena={produktA.Cena:C}");

        // A zmienia cenę i zapisuje pierwszy
        produktA.Cena    = 3299m;
        produktA.Version++;         // ręczna inkrementacja!
        await ctxA.SaveChangesAsync();
        Console.WriteLine($"A zapisał: Cena={produktA.Cena:C}, Version={produktA.Version}");
        // EF Core generuje: UPDATE ProduktyTR SET Cena=@cena, Version=@newVer
        //                   WHERE Id=@id AND Version=@originalVer

        // B próbuje zapisać — Version w DB już inny!
        produktB!.StanMagazynu = 5;
        produktB.Version++;
        try
        {
            await ctxB.SaveChangesAsync();
            Console.WriteLine("B zapisał (nie powinno się zdarzyć w konflikcie)");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Console.WriteLine("B: DbUpdateConcurrencyException — wykryto konflikt!");

            foreach (var entry in ex.Entries)
            {
                var dbValues     = await entry.GetDatabaseValuesAsync(); // aktualne w DB
                var clientValues = entry.CurrentValues;                  // wartości B
                var origValues   = entry.OriginalValues;                 // gdy B pobierał

                Console.WriteLine($"  Encja: {entry.Entity.GetType().Name}");
                foreach (var prop in dbValues!.Properties)
                {
                    var db   = dbValues[prop];
                    var client = clientValues[prop];
                    var orig = origValues[prop];
                    if (!Equals(db, orig))
                        Console.WriteLine($"  {prop.Name}: oryg={orig}, klient={client}, DB={db}");
                }
            }
        }

        // Strategia: retry z odświeżeniem danych
        const int maxProb = 3;
        for (int proba = 1; proba <= maxProb; proba++)
        {
            await using var ctx = NowCtx();
            var produkt = await ctx.Produkty.FindAsync(1);
            produkt!.StanMagazynu = 5;
            produkt.Version++;
            try
            {
                await ctx.SaveChangesAsync();
                Console.WriteLine($"Retry: zapisano po {proba} próbach (stan=5)");
                break;
            }
            catch (DbUpdateConcurrencyException ex) when (proba < maxProb)
            {
                foreach (var e in ex.Entries)
                {
                    var dbVals = await e.GetDatabaseValuesAsync();
                    e.OriginalValues.SetValues(dbVals!); // odśwież OriginalValues → spróbuj ponownie
                }
                Console.WriteLine($"Retry {proba}: konflikt, ponawiam...");
            }
        }

        // Kiedy optymistyczna:
        // ✅ Read-heavy, web API, rzadkie konflikty (profil, konfiguracja)
        // ❌ Nie dla: bankowości, rezerwacji — tam pesymistyczna
        Console.WriteLine("Optimistic: read-heavy, rzadkie konflikty → retry; Pessimistic: finansowe, rezerwacje");
    }

    // 4. Pessimistic Concurrency — blokady (SQLite: Serializable ≈ EXCLUSIVE)
    public static async Task DemoPesymistycznaAsync()
    {
        Console.WriteLine("\n--- Transakcje: Pesymistyczna współbieżność ---");

        // Pesymistyczna = zablokuj zasób przy ODCZYCIE; inni muszą czekać
        // SQL Server: SELECT ... WITH (UPDLOCK, ROWLOCK) — blokuje wiersz dla UPDATE
        // SQLite:     BEGIN EXCLUSIVE — blokuje całą bazę (granulacja pliku)

        // Rezerwacja miejsca — pesymistyczna gwarancja "nie oversell"
        async Task<bool> ZarezerwujAsync(int kontoId, decimal kwota)
        {
            // BEGIN EXCLUSIVE — żadna inna transakcja zapisu nie wejdzie
            using SqliteTransaction trx = _conn.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                using var cmdSprawdz = _conn.CreateCommand();
                cmdSprawdz.Transaction = trx;
                cmdSprawdz.CommandText = "SELECT Saldo, CzyAktywne FROM KontaTR WHERE Id = @Id";
                cmdSprawdz.Parameters.AddWithValue("@Id", kontoId);

                using var reader = await cmdSprawdz.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                { trx.Rollback(); return false; }

                decimal saldo     = (decimal)reader.GetDouble(0);
                bool    aktywne   = reader.GetBoolean(1);
                reader.Close();

                if (!aktywne || saldo < kwota) { trx.Rollback(); return false; }

                using var cmdUpd = _conn.CreateCommand();
                cmdUpd.Transaction = trx;
                cmdUpd.CommandText = "UPDATE KontaTR SET Saldo = Saldo - @K, Version = Version + 1 WHERE Id = @Id";
                cmdUpd.Parameters.AddWithValue("@K",  (double)kwota);
                cmdUpd.Parameters.AddWithValue("@Id", kontoId);
                await cmdUpd.ExecuteNonQueryAsync();

                trx.Commit();
                return true;
            }
            catch { trx.Rollback(); throw; }
        }

        bool ok = await ZarezerwujAsync(1, 500m);
        Console.WriteLine($"Pesymistyczna rezerwacja 500 PLN z konta 1: {ok}");

        // Deadlock — SQL Server: gdy wątki blokują zasoby w odwrotnej kolejności
        // Zapobieganie: zawsze blokuj w tej samej kolejności (np. mniejszy Id pierwszy)
        // SQL Server: SqlException.Number == 1205 (deadlock victim)
        // SQLite: SqliteException z SqliteErrorCode 5 (SQLITE_BUSY) — timeout zamiast deadlock

        // Retry na deadlock:
        // for (int p = 1; p <= 3; p++) {
        //     try { return await operacja(); }
        //     catch (SqlException ex) when (ex.Number == 1205 && p < 3)  // SQL Server
        //     catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && p < 3) // SQLite
        //     { await Task.Delay(100 * p); }
        // }

        await using (var ctx = NowCtx())
        {
            var konto = await ctx.Konta.FindAsync(1);
            Console.WriteLine($"Saldo po rezerwacji: {konto!.Saldo:C}");
        }
        Console.WriteLine("SQL Server UPDLOCK/ROWLOCK → granulacja wiersza; SQLite EXCLUSIVE → cały plik");
    }

    // 5. Savepoints EF Core — CreateSavepointAsync, RollbackToSavepointAsync
    public static async Task DemoSavepointsEFAsync()
    {
        Console.WriteLine("\n--- Transakcje: Savepoints EF Core ---");
        await using var ctx = NowCtx();

        await using var trx = await ctx.Database.BeginTransactionAsync();
        try
        {
            // Główna operacja — zawsze
            var nowyProdukt = new ProduktTR
                { Nazwa = "Tablet", Cena = 999m, StanMagazynu = 7, KategoriaTRId = 1 };
            ctx.Produkty.Add(nowyProdukt);
            await ctx.SaveChangesAsync();
            Console.WriteLine($"Główna operacja: dodano Tablet ID={nowyProdukt.Id}");

            // Savepoint przed opcjonalną operacją
            await trx.CreateSavepointAsync("przed_bonusem");
            Console.WriteLine("Savepoint 'przed_bonusem' ustawiony");

            try
            {
                // Opcjonalna operacja — może się nie udać
                var konto = await ctx.Konta.FindAsync(999); // nieistniejące konto
                if (konto == null)
                    throw new InvalidOperationException("Konto bonusowe nie istnieje");

                konto.Saldo += 100m;
                await ctx.SaveChangesAsync();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Opcjonalna operacja: {ex.Message}");
                // Cofnij TYLKO do savepoint — główna operacja nadal w transakcji
                await trx.RollbackToSavepointAsync("przed_bonusem");
                ctx.ChangeTracker.Clear(); // wyczyść tracker po rollback!
                Console.WriteLine("Rollback do savepoint — Tablet nadal istnieje w transakcji");
            }

            await trx.CommitAsync();
            Console.WriteLine($"Commit: Tablet ID={nowyProdukt.Id} zapisany (bonus cofnięty)");
            Console.WriteLine($"Produktów teraz: {await ctx.Produkty.CountAsync()}");
        }
        catch
        {
            await trx.RollbackAsync();
            throw;
        }
    }

    // 6. TransactionScope — ambient transaction (koordynacja wielu kontekstów)
    public static async Task DemoAmbientAsync()
    {
        Console.WriteLine("\n--- Transakcje: TransactionScope ---");

        // TransactionScope = "otoczka" — wszystko w jej zasięgu wchodzi do tej samej transakcji
        // WYMAGANE: TransactionScopeAsyncFlowOption.Enabled dla async/await!
        // OGRANICZENIE: SQLite EF Core nie obsługuje TransactionScope (brak DTC)
        // Niżej demonstracja API + zachowanie na produkcji (SQL Server)

        // --- Demonstracja struktury TransactionScope ---
        Console.WriteLine("TransactionScope API:");
        Console.WriteLine("  new TransactionScope(Required, options, AsyncFlowOption.Enabled)");
        Console.WriteLine("  {");
        Console.WriteLine("      ctx1.SaveChangesAsync(); // oba SaveChanges w jednej trx");
        Console.WriteLine("      ctx2.SaveChangesAsync();");
        Console.WriteLine("      scope.Complete();        // COMMIT; brak = ROLLBACK");
        Console.WriteLine("  }");
        Console.WriteLine("TransactionScopeAsyncFlowOption.Enabled — OBOWIĄZKOWE dla async/await!");

        // SQL Server: można koordynować wiele DbContextów/połączeń w jednym scope
        Console.WriteLine("SQL Server: wiele kontekstów, wiele połączeń → MSDTC lub single enlistment");
        Console.WriteLine("SQLite EF Core: nie obsługuje ambient transactions → użyj BeginTransactionAsync()");

        // Demonstracja ROLLBACK gdy brak Complete() — używamy ADO.NET (bez EF Core)
        // bo SQLite ADO.NET driver IGNORUJE TransactionScope (nie rzuca wyjątku)
        long katPrzed = await (NowCtx()).Kategorie.CountAsync();

        using (var scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled))
        {
            // Nie wywołujemy scope.Complete() → przy wyjściu z using: ROLLBACK
            Console.WriteLine("TransactionScope bez Complete() → ROLLBACK przy dispose");
            // scope.Complete() celowo pominięte
        }

        long katPo = await (NowCtx()).Kategorie.CountAsync();
        Console.WriteLine($"Kategorii przed: {katPrzed}, po nieukończonym scope: {katPo} (brak zmian)");
        Console.WriteLine("TransactionOptions: IsolationLevel, Timeout — konfiguracja całego scope");
        await Task.CompletedTask;
    }

    // 7. EF Core + ADO.NET w jednej transakcji — UseTransactionAsync
    public static async Task DemoEFiAdoNetAsync()
    {
        Console.WriteLine("\n--- Transakcje: EF Core + ADO.NET (współdzielona transakcja) ---");

        // Uruchom transakcję przez ADO.NET
        using SqliteTransaction adoTrx = _conn.BeginTransaction(IsolationLevel.Serializable);

        // EF Core przejmuje zewnętrzną transakcję
        await using var ctx = NowCtx();
        await ctx.Database.UseTransactionAsync(adoTrx);

        try
        {
            // Operacja EF Core w zewnętrznej transakcji
            ctx.Kategorie.Add(new KategoriaTR { Nazwa = "EF+ADO Kategoria" });
            await ctx.SaveChangesAsync(); // nie commituje — transakcja jest zewnętrzna!

            // Operacja ADO.NET w tej samej transakcji
            using var cmdLog = _conn.CreateCommand();
            cmdLog.Transaction = adoTrx;
            cmdLog.CommandText = "INSERT INTO KategorieTR (Nazwa) VALUES ('ADO.NET Kategoria')";
            await cmdLog.ExecuteNonQueryAsync();

            // Wspólny COMMIT — obie operacje naraz
            adoTrx.Commit();
            Console.WriteLine("EF+ADO.NET: COMMIT obu operacji w jednej transakcji");
        }
        catch
        {
            adoTrx.Rollback();
            Console.WriteLine("EF+ADO.NET: ROLLBACK");
            throw;
        }

        // Przypadek użycia: EF Core dla encji + ADO.NET dla audit logu / raportów
        // Bez UseTransactionAsync — dwie niezależne transakcje (bez gwarancji atomowości)
        await using var ctx2 = NowCtx();
        var kategorieNowe = await ctx2.Kategorie
            .Where(k => k.Nazwa.Contains("ADO") || k.Nazwa.Contains("EF+"))
            .ToListAsync();
        Console.WriteLine($"Kategorie zapisane przez EF+ADO.NET: {kategorieNowe.Count}");
        foreach (var k in kategorieNowe)
            Console.WriteLine($"  [{k.Id}] {k.Nazwa}");
    }
}
