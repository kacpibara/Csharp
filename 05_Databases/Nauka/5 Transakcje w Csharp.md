### Transakcje w C# — ADO.NET i EF Core

---

### 1. ACID — fundamenty transakcji

csharp

```csharp
// ACID = Atomicity, Consistency, Isolation, Durability

// ATOMICITY — "wszystko albo nic"
// Jeśli jedna operacja się nie powiedzie — całość jest cofana
public async Task PrzykladAtomowosciAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction();

    try
    {
        // Operacja 1: pobierz z konta A
        using var cmd1 = conn.CreateCommand();
        cmd1.Transaction = trx;
        cmd1.CommandText = "UPDATE Konta SET Saldo = Saldo - 1000 WHERE Id = 1";
        await cmd1.ExecuteNonQueryAsync();

        // Operacja 2: wpłać na konto B
        using var cmd2 = conn.CreateCommand();
        cmd2.Transaction = trx;
        cmd2.CommandText = "UPDATE Konta SET Saldo = Saldo + 1000 WHERE Id = 2";
        await cmd2.ExecuteNonQueryAsync();

        // OBE operacje lub ŻADNA — atomowość
        await trx.CommitAsync();
        Console.WriteLine("Przelew wykonany");
    }
    catch
    {
        await trx.RollbackAsync();  // cofnij obie operacje
        Console.WriteLine("Przelew cofnięty");
        throw;
    }
}

// CONSISTENCY — transakcja pozostawia bazę w spójnym stanie
// Constraints, triggery, reguły biznesowe muszą być spełnione

// ISOLATION — transakcje nie widzą nawzajem swoich niezatwierdzonych zmian
// Stopień izolacji konfigurowalny (patrz sekcja 2)

// DURABILITY — zatwierdzone transakcje przeżywają awarie systemu
// Realizowane przez WAL (Write-Ahead Log) w bazie danych
```

---

### 2. Poziomy izolacji — szczegółowo

csharp

```csharp
// SQL Server poziomy izolacji — od najmniej do najbardziej restrykcyjnego

// READ UNCOMMITTED — dirty reads możliwe, najszybszy
// Widzisz niezatwierdzone zmiany innych transakcji!
public async Task ReadUncommittedAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.ReadUncommitted);

    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;
    // Może odczytać dane które inny przelew jeszcze nie zatwierdził!
    // Ryzyko: czytasz -1000 z konta które za chwilę będzie cofnięte
    cmd.CommandText = "SELECT Saldo FROM Konta WHERE Id = 1";
    var saldo = await cmd.ExecuteScalarAsync();

    await trx.CommitAsync();
}

// READ COMMITTED — domyślny SQL Server
// Brak dirty reads, ale non-repeatable reads możliwe
public async Task ReadCommittedAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;

    // Odczyt 1 — saldo = 5000
    cmd.CommandText = "SELECT Saldo FROM Konta WHERE Id = 1";
    var saldo1 = (decimal)(await cmd.ExecuteScalarAsync())!;

    // Między odczytami inna transakcja mogła zmienić saldo!
    await Task.Delay(100);  // symulacja czasu

    // Odczyt 2 — saldo może być już inne!  (non-repeatable read)
    cmd.CommandText = "SELECT Saldo FROM Konta WHERE Id = 1";
    var saldo2 = (decimal)(await cmd.ExecuteScalarAsync())!;

    Console.WriteLine($"Saldo1: {saldo1}, Saldo2: {saldo2}");
    // Mogą być różne! To non-repeatable read

    await trx.CommitAsync();
}

// REPEATABLE READ — ten sam wiersz zawsze ten sam w ramach transakcji
// Brak dirty reads, brak non-repeatable reads, ale phantom reads możliwe
public async Task RepeatableReadAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.RepeatableRead);

    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;

    // Odczyt 1 — saldo = 5000, wiersz zablokowany dla innych modyfikacji
    cmd.CommandText = "SELECT Saldo FROM Konta WHERE Id = 1";
    var saldo1 = await cmd.ExecuteScalarAsync();

    await Task.Delay(100);

    // Odczyt 2 — GWARANTOWANE to samo saldo (blokada)
    var saldo2 = await cmd.ExecuteScalarAsync();

    // ALE: nowe wiersze mogą pojawić się! (phantom reads)
    // SELECT COUNT(*) FROM Konta WHERE Saldo > 1000
    // może zwrócić różne wartości jeśli nowe konta zostały dodane

    await trx.CommitAsync();
}

// SERIALIZABLE — najwyższy poziom, brak wszystkich anomalii
// Bardzo wolny — pełne blokady zakresowe
public async Task SerializableAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.Serializable);

    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;

    // Pełna izolacja — żadnych anomalii
    // Ale: duże ryzyko deadlocków i bardzo niska współbieżność
    cmd.CommandText = "SELECT SUM(Saldo) FROM Konta WHERE Typ = 'Oszczednosciowe'";
    var suma = await cmd.ExecuteScalarAsync();

    await trx.CommitAsync();
}

// SNAPSHOT — optymistyczna współbieżność, wersjonowanie wierszy
// SQL Server specifik — wymaga: ALTER DATABASE Sklep SET ALLOW_SNAPSHOT_ISOLATION ON
public async Task SnapshotAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.Snapshot);

    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;

    // Czytasz snapshot z momentu BEGIN TRANSACTION
    // Inne transakcje nie blokują odczytu!
    // Przy commit — sprawdza czy ktoś zmienił dane (conflict detection)
    cmd.CommandText = "SELECT * FROM Konta WHERE Id = 1";
    using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    var saldo = reader.GetDecimal(reader.GetOrdinal("Saldo"));
    reader.Close();

    await trx.CommitAsync();
}

// Tabela porównawcza anomalii:
// ┌──────────────────┬──────────┬──────────────┬─────────────────┬──────────────┐
// │ Poziom           │ Dirty    │ Non-repeatable│ Phantom         │ Wydajność    │
// │                  │ Read     │ Read          │ Read            │              │
// ├──────────────────┼──────────┼──────────────┼─────────────────┼──────────────┤
// │ ReadUncommitted  │ ✗ możliwe│ ✗ możliwe    │ ✗ możliwe       │ ⭐⭐⭐⭐⭐ │
// │ ReadCommitted    │ ✓ brak   │ ✗ możliwe    │ ✗ możliwe       │ ⭐⭐⭐⭐   │
// │ RepeatableRead   │ ✓ brak   │ ✓ brak       │ ✗ możliwe       │ ⭐⭐⭐     │
// │ Serializable     │ ✓ brak   │ ✓ brak       │ ✓ brak          │ ⭐         │
// │ Snapshot         │ ✓ brak   │ ✓ brak       │ ✓ brak          │ ⭐⭐⭐⭐   │
// └──────────────────┴──────────┴──────────────┴─────────────────┴──────────────┘
```

---

### 3. Optimistic Concurrency — EF Core

csharp

```csharp
// Optimistic = "zakładam że nikt inny nie zmieni moich danych"
// Sprawdzenie konfliktu przy ZAPISIE — nie blokuj przy odczycie

// Sposób 1 — ConcurrencyToken (timestamp / rowversion)
public class Produkt
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";
    public decimal Cena { get; set; }
    public int StanMagazynu { get; set; }

    // SQL Server ROWVERSION — automatycznie aktualizowany przy każdej zmianie
    [Timestamp]  // atrybut lub fluent: .IsRowVersion()
    public byte[] RowVersion { get; set; } = null!;
}

// Konfiguracja przez Fluent API
modelBuilder.Entity<Produkt>()
    .Property(p => p.RowVersion)
    .IsRowVersion()                 // automatyczny timestamp
    .IsConcurrencyToken();          // używany do wykrywania konfliktów

// Scenariusz konfliktu
public async Task OptimisticConflictAsync()
{
    // Użytkownik A i B pobierają ten sam produkt
    await using var ctxA = new SklepContext(options);
    await using var ctxB = new SklepContext(options);

    var produktA = await ctxA.Produkty.FindAsync(1);
    var produktB = await ctxB.Produkty.FindAsync(1);

    // A zmienia cenę
    produktA!.Cena = 3600m;

    // B zmienia stan (niezależnie)
    produktB!.StanMagazynu = 15;

    // A zapisuje pierwszy — sukces
    await ctxA.SaveChangesAsync();

    // B próbuje zapisać — KONFLIKT! RowVersion się zmienił!
    try
    {
        await ctxB.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        Console.WriteLine("Wykryto konflikt współbieżności!");

        foreach (var entry in ex.Entries)
        {
            var dbValues = await entry.GetDatabaseValuesAsync();  // aktualne wartości w DB
            var clientValues = entry.CurrentValues;               // wartości klienta B
            var originalValues = entry.OriginalValues;            // wartości gdy pobierał B

            Console.WriteLine($"Encja: {entry.Entity.GetType().Name}");

            // Pokaż różnice
            foreach (var prop in dbValues!.Properties)
            {
                var db     = dbValues[prop];
                var client = clientValues[prop];
                var orig   = originalValues[prop];

                if (!Equals(db, orig))
                    Console.WriteLine($"  {prop.Name}: oryginalne={orig}, klient={client}, DB={db}");
            }
        }
    }
}

// Strategie rozwiązywania konfliktów
public async Task RozwiezKonfliktAsync(int produktId, string nowaOpis)
{
    const int maxProb = 3;

    for (int proba = 0; proba < maxProb; proba++)
    {
        await using var ctx = new SklepContext(options);
        var produkt = await ctx.Produkty.FindAsync(produktId);
        if (produkt == null) return;

        produkt.Nazwa = nowaOpis;

        try
        {
            await ctx.SaveChangesAsync();
            Console.WriteLine($"Zapisano po {proba + 1} próbach");
            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (proba == maxProb - 1) throw;

            // Odśwież dane z bazy i spróbuj ponownie
            foreach (var entry in ex.Entries)
            {
                var dbVals = await entry.GetDatabaseValuesAsync();
                if (dbVals == null)
                    throw new InvalidOperationException("Rekord usunięty przez inną transakcję");

                // Strategia: "Database Wins" — nadpisz lokalne wartości
                entry.OriginalValues.SetValues(dbVals);

                // Strategia: "Client Wins" — zachowaj lokalne zmiany
                // entry.CurrentValues.SetValues(clientValues);
                // entry.OriginalValues.SetValues(dbVals);

                // Strategia: "Merge" — połącz zmiany
                // Tu: zachowaj Cena z DB, ale Nazwa z klienta
                var dbCena = (decimal)dbVals["Cena"]!;
                entry.Property("Cena").CurrentValue = dbCena;   // weź z DB
                // Nazwa pozostaje z klienta — już ustawiona
            }
        }
    }
}

// Sposób 2 — ConcurrencyToken na konkretnym polu
public class Konto
{
    public int     Id        { get; set; }
    public decimal Saldo     { get; set; }
    public string  Wlasciciel{ get; set; } = "";

    // Jeśli Saldo zmienione między odczytem a zapisem — DbUpdateConcurrencyException
    [ConcurrencyCheck]
    public decimal SaldoSnapshot { get; set; }  // kopiujesz Saldo przy odczycie
}
```

---

### 4. Pessimistic Concurrency — blokady

csharp

```csharp
// Pessimistic = "zakładam że ktoś zmieni dane — zablokuj od razu"
// EF Core NIE ma natywnego wsparcia — używasz surowego SQL z hintem blokady

// SELECT ... WITH (UPDLOCK) — pobierz z blokadą do aktualizacji
public async Task<decimal> PobierzISaldoZBlokadaAsync(int kontoId,
    SqlTransaction trx, SqlConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.Transaction = trx;
    // UPDLOCK — blokuje wiersz dla innych UPDATE, pozwala innym SELECT
    // ROWLOCK — blokada na poziomie wiersza (nie strony/tabeli)
    cmd.CommandText = """
        SELECT Saldo FROM Konta WITH (UPDLOCK, ROWLOCK)
        WHERE Id = @Id
        """;
    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = kontoId;

    return (decimal)(await cmd.ExecuteScalarAsync())!;
}

// EF Core z pesymistyczną blokadą przez FromSqlRaw
public async Task EFPessimisticAsync(int kontoId)
{
    await using var ctx = new SklepContext(options);
    await using var trx = await ctx.Database.BeginTransactionAsync(
        IsolationLevel.RepeatableRead);

    try
    {
        // Pobierz z blokadą (SQL hint)
        var konto = await ctx.Konta
            .FromSqlRaw("SELECT * FROM Konta WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", kontoId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Konto nie istnieje");

        // Żaden inny wątek nie może teraz zmodyfikować tego wiersza
        await Task.Delay(100);  // symulacja logiki biznesowej

        konto.Saldo -= 500m;
        await ctx.SaveChangesAsync();

        await trx.CommitAsync();
    }
    catch
    {
        await trx.RollbackAsync();
        throw;
    }
}

// Deadlock — klasyczny scenariusz z pesymistyczną blokadą
// Wątek A blokuje wiersz 1, chce wiersz 2
// Wątek B blokuje wiersz 2, chce wiersz 1
// → DEADLOCK — SQL Server wykrywa i przerywa jedną z transakcji

// Obsługa deadlocka — retry
public async Task<T> ZRetryNaDeadlockAsync<T>(
    Func<Task<T>> operacja, int maxProb = 3)
{
    for (int proba = 1; proba <= maxProb; proba++)
    {
        try
        {
            return await operacja();
        }
        catch (SqlException ex) when (ex.Number == 1205)  // 1205 = deadlock
        {
            if (proba == maxProb) throw;
            var opoznienie = TimeSpan.FromMilliseconds(proba * 100);
            Console.WriteLine($"Deadlock (próba {proba}) — retry za {opoznienie.TotalMs}ms");
            await Task.Delay(opoznienie);
        }
    }
    throw new InvalidOperationException("Nie powinno tu dotrzeć");
}
```

---

### 5. Optimistic vs Pessimistic — kiedy co

csharp

```csharp
// Optymistyczna — dla niskiej częstości konfliktów
// Pesymistyczna — gdy konflikty częste lub kosztowne

// ┌───────────────────────┬──────────────────────┬───────────────────────┐
// │ Cecha                 │ Optimistic           │ Pessimistic           │
// ├───────────────────────┼──────────────────────┼───────────────────────┤
// │ Blokada               │ Przy zapisie         │ Przy odczycie         │
// │ Konflikty             │ Wykryte później       │ Zapobiegane z góry    │
// │ Throughput            │ Wysoki               │ Niższy                │
// │ Deadlock              │ Brak                 │ Możliwy               │
// │ Latencja              │ Niska                │ Wyższa (czekanie)     │
// │ Idealny dla           │ Read-heavy, web API  │ Finansowe, rezerwacje  │
// └───────────────────────┴──────────────────────┴───────────────────────┘

// OPTIMISTIC — typowe scenariusze
// - Aktualizacja profilu użytkownika (rzadkie konflikty)
// - CMS — edycja artykułów (jeden edytor na raz)
// - Konfiguracja systemu

// PESSIMISTIC — typowe scenariusze
// - Rezerwacja miejsc (lot, kino, hotel — nie chcesz oversell)
// - Transakcje bankowe — przelew ze sprawdzeniem salda
// - Aukcje — ostatnia cena, countdown

// Przykład rezerwacji z pesymistyczną blokadą
public async Task<bool> ZarezerwujMiejsceAsync(int lotuId, int numerMiejsca,
    int klientId)
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction(IsolationLevel.Serializable);

    try
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trx;

        // Sprawdź dostępność Z BLOKADĄ — nikt inny nie zarezerwuje
        cmd.CommandText = """
            SELECT Status FROM MiejscaLotu
            WITH (UPDLOCK, ROWLOCK)
            WHERE LotuId = @LotuId AND NumerMiejsca = @Miejsce
            """;
        cmd.Parameters.Add("@LotuId",  SqlDbType.Int).Value = lotuId;
        cmd.Parameters.Add("@Miejsce", SqlDbType.Int).Value = numerMiejsca;

        string? status = (string?)await cmd.ExecuteScalarAsync();

        if (status != "Dostepne")
        {
            await trx.RollbackAsync();
            return false;  // zajęte
        }

        // Zarezerwuj
        cmd.CommandText = """
            UPDATE MiejscaLotu
            SET Status = 'Zarezerwowane', KlientId = @KlientId,
                DataRezerwacji = GETUTCDATE()
            WHERE LotuId = @LotuId AND NumerMiejsca = @Miejsce
            """;
        cmd.Parameters.Add("@KlientId", SqlDbType.Int).Value = klientId;
        await cmd.ExecuteNonQueryAsync();

        await trx.CommitAsync();
        return true;
    }
    catch
    {
        await trx.RollbackAsync();
        throw;
    }
}
```

---

### 6. EF Core — transakcje zaawansowane

csharp

```csharp
// Ambient Transaction — udostępnij jedną transakcję między kontekstami
using System.Transactions;

public async Task AmbientTransactionAsync()
{
    // TransactionScope — automatyczna transakcja dla wszystkiego wewnątrz
    using var scope = new TransactionScope(
        TransactionScopeOption.Required,
        new TransactionOptions
        {
            IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
            Timeout        = TimeSpan.FromSeconds(30)
        },
        TransactionScopeAsyncFlowOption.Enabled  // WYMAGANE dla async!
    );

    await using var ctx1 = new SklepContext(options);
    await using var ctx2 = new SklepContext(options);

    // Oba konteksty w tej samej transakcji!
    ctx1.Produkty.Add(new Produkt { Nazwa = "Test1", Cena = 100m, KategoriaId = 1 });
    await ctx1.SaveChangesAsync();

    ctx2.Klienci.Add(new Klient { Imie = "Test", Email = "t@t.pl" });
    await ctx2.SaveChangesAsync();

    // Zatwierdź obie operacje naraz
    scope.Complete();
}  // Brak Complete() = rollback!

// Savepoints — częściowe cofanie
public async Task SavepointsAsync(int klientId)
{
    await using var ctx = new SklepContext(options);
    await using var trx = await ctx.Database.BeginTransactionAsync();

    try
    {
        // Operacja 1 — zawsze
        ctx.Zamowienia.Add(new Zamowienie
        {
            KlientId = klientId,
            Status   = "Nowe",
            Wartosc  = 100m
        });
        await ctx.SaveChangesAsync();

        // Savepoint przed opcjonalną operacją
        await trx.CreateSavepointAsync("przed_bonusem");

        try
        {
            // Operacja 2 — może się nie udać
            var klient = await ctx.Klienci.FindAsync(klientId);
            klient!.LiczbaZamowien++;  // hipotetyczne pole
            await ctx.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Cofnij tylko do savepoint — zamówienie nadal istnieje
            await trx.RollbackToSavepointAsync("przed_bonusem");
            ctx.ChangeTracker.Clear();  // wyczyść tracker po rollback
        }

        await trx.CommitAsync();
    }
    catch
    {
        await trx.RollbackAsync();
        throw;
    }
}

// Dzielenie transakcji między EF Core i ADO.NET
public async Task EfCoreIAdoNetTransakcjaAsync()
{
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();
    await using var trx = conn.BeginTransaction();

    // EF Core używa tej samej transakcji
    await using var ctx = new SklepContext(
        new DbContextOptionsBuilder<SklepContext>()
            .UseSqlServer(conn)
            .Options);

    // Przekaż zewnętrzną transakcję do EF Core
    await ctx.Database.UseTransactionAsync(trx);

    try
    {
        // EF Core operacja
        ctx.Kategorie.Add(new Kategoria { Nazwa = "Nowa" });
        await ctx.SaveChangesAsync();

        // ADO.NET operacja w tej samej transakcji
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trx;
        cmd.CommandText = "INSERT INTO AuditLog (Akcja, Czas) VALUES ('DodanieKategorii', GETUTCDATE())";
        await cmd.ExecuteNonQueryAsync();

        trx.Commit();
    }
    catch
    {
        await trx.RollbackAsync();
        throw;
    }
}
```

---

### 7. Praktyczny przykład — przelew bankowy

csharp

```csharp
// Kompletny przykład z wszystkimi wzorcami

public class BankSerwis
{
    private readonly string _connStr;
    private readonly SklepContext _ctx;

    public BankSerwis(string connStr, SklepContext ctx)
    {
        _connStr = connStr;
        _ctx = ctx;
    }

    // Przelew z pesymistyczną blokadą (ADO.NET)
    public async Task<WynikPrzelewu> WykonajPrzelewAdoNetAsync(
        int kontoZ, int kontoNa, decimal kwota, CancellationToken ct = default)
    {
        if (kwota <= 0)
            throw new ArgumentException("Kwota musi być > 0");

        return await ZRetryNaDeadlockAsync(async () =>
        {
            await using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);
            await using var trx = conn.BeginTransaction(IsolationLevel.RepeatableRead);

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trx;

                // Blokuj OBA konta (zawsze w tej samej kolejności — zapobiega deadlockom!)
                int idMin = Math.Min(kontoZ, kontoNa);
                int idMax = Math.Max(kontoZ, kontoNa);

                cmd.CommandText = $"""
                    SELECT Id, Saldo, Wlasciciel, CzyAktywne
                    FROM Konta WITH (UPDLOCK, ROWLOCK)
                    WHERE Id IN ({idMin}, {idMax})
                    ORDER BY Id
                    """;

                var konta = new Dictionary<int, (decimal Saldo, string Wlasciciel, bool Aktywne)>();
                using (var reader = await cmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        konta[reader.GetInt32(0)] = (
                            reader.GetDecimal(1),
                            reader.GetString(2),
                            reader.GetBoolean(3));
                    }
                }

                if (!konta.ContainsKey(kontoZ)) throw new InvalidOperationException($"Konto #{kontoZ} nie istnieje");
                if (!konta.ContainsKey(kontoNa)) throw new InvalidOperationException($"Konto #{kontoNa} nie istnieje");
                if (!konta[kontoZ].Aktywne) throw new InvalidOperationException("Konto źródłowe nieaktywne");
                if (!konta[kontoNa].Aktywne) throw new InvalidOperationException("Konto docelowe nieaktywne");
                if (konta[kontoZ].Saldo < kwota) throw new InvalidOperationException(
                    $"Niewystarczające saldo: {konta[kontoZ].Saldo:C}, potrzeba {kwota:C}");

                // Wykonaj przelew
                cmd.CommandText = "UPDATE Konta SET Saldo = Saldo - @Kwota WHERE Id = @Id";
                cmd.Parameters.Clear();
                cmd.Parameters.Add("@Kwota", SqlDbType.Decimal).Value = kwota;
                cmd.Parameters.Add("@Id",    SqlDbType.Int).Value     = kontoZ;
                await cmd.ExecuteNonQueryAsync(ct);

                cmd.Parameters["@Id"].Value = kontoNa;
                cmd.CommandText = "UPDATE Konta SET Saldo = Saldo + @Kwota WHERE Id = @Id";
                await cmd.ExecuteNonQueryAsync(ct);

                // Audit log
                cmd.CommandText = """
                    INSERT INTO TransakcjeBankowe (KontoZ, KontoNa, Kwota, Status, Czas)
                    VALUES (@KontoZ, @KontoNa, @Kwota2, 'Wykonany', GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;
                cmd.Parameters.Clear();
                cmd.Parameters.Add("@KontoZ",  SqlDbType.Int).Value     = kontoZ;
                cmd.Parameters.Add("@KontoNa", SqlDbType.Int).Value     = kontoNa;
                cmd.Parameters.Add("@Kwota2",  SqlDbType.Decimal).Value = kwota;
                int transakcjaId = (int)(await cmd.ExecuteScalarAsync(ct))!;

                await trx.CommitAsync(ct);

                return new WynikPrzelewu(true, transakcjaId,
                    $"Przelew {kwota:C} z konta {konta[kontoZ].Wlasciciel} " +
                    $"na {konta[kontoNa].Wlasciciel} wykonany");
            }
            catch (InvalidOperationException ex)
            {
                await trx.RollbackAsync();
                return new WynikPrzelewu(false, 0, ex.Message);
            }
            catch
            {
                await trx.RollbackAsync();
                throw;
            }
        }, ct: ct);
    }

    // Wersja EF Core z optymistyczną blokadą
    public async Task<WynikPrzelewu> WykonajPrzelewEFCoreAsync(
        int kontoZ, int kontoNa, decimal kwota, CancellationToken ct = default)
    {
        const int maxProb = 3;

        for (int proba = 1; proba <= maxProb; proba++)
        {
            try
            {
                await using var trx = await _ctx.Database
                    .BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

                var konta = await _ctx.Konta
                    .Where(k => k.Id == kontoZ || k.Id == kontoNa)
                    .ToListAsync(ct);

                var kZ = konta.First(k => k.Id == kontoZ);
                var kN = konta.First(k => k.Id == kontoNa);

                if (kZ.Saldo < kwota)
                    return new WynikPrzelewu(false, 0, "Niewystarczające saldo");

                kZ.Saldo -= kwota;
                kN.Saldo += kwota;

                _ctx.TransakcjeBankowe.Add(new TransakcjaBankowa
                {
                    KontoZrodloweId   = kontoZ,
                    KontoDoceloweId   = kontoNa,
                    Kwota             = kwota,
                    Status            = "Wykonany",
                    Czas              = DateTime.UtcNow
                });

                await _ctx.SaveChangesAsync(ct);  // RowVersion sprawdzany tutaj!
                await trx.CommitAsync(ct);

                return new WynikPrzelewu(true, 0, $"Przelew {kwota:C} wykonany");
            }
            catch (DbUpdateConcurrencyException) when (proba < maxProb)
            {
                _ctx.ChangeTracker.Clear();
                await Task.Delay(100 * proba, ct);
                Console.WriteLine($"Konflikt współbieżności — próba {proba + 1}");
            }
        }

        return new WynikPrzelewu(false, 0, "Konflikt współbieżności — spróbuj ponownie");
    }

    private async Task<T> ZRetryNaDeadlockAsync<T>(
        Func<Task<T>> operacja,
        int maxProb = 3,
        CancellationToken ct = default)
    {
        for (int proba = 1; proba <= maxProb; proba++)
        {
            try { return await operacja(); }
            catch (SqlException ex) when (ex.Number == 1205 && proba < maxProb)
            {
                await Task.Delay(100 * proba, ct);
            }
        }
        return await operacja();
    }
}

public record WynikPrzelewu(bool Sukces, int TransakcjaId, string Komunikat);
public class Konto { public int Id { get; set; } public decimal Saldo { get; set; }
    public string Wlasciciel { get; set; } = ""; public bool CzyAktywne { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = null!; }
public class TransakcjaBankowa { public int Id { get; set; } public int KontoZrodloweId { get; set; }
    public int KontoDoceloweId { get; set; } public decimal Kwota { get; set; }
    public string Status { get; set; } = ""; public DateTime Czas { get; set; } }

// Demonstracja
var serwis = new BankSerwis(connStr, ctx);

// Przelew ADO.NET (pesymistyczny)
var wynik1 = await serwis.WykonajPrzelewAdoNetAsync(1, 2, 500m);
Console.WriteLine($"ADO.NET: {wynik1.Sukces} — {wynik1.Komunikat}");

// Przelew EF Core (optymistyczny)
var wynik2 = await serwis.WykonajPrzelewEFCoreAsync(1, 2, 250m);
Console.WriteLine($"EF Core: {wynik2.Sukces} — {wynik2.Komunikat}");
```

---

### Typowe pytania rekrutacyjne

**"Co to ACID i dlaczego jest ważne?"** Atomicity — transakcja to niepodzielna jednostka, albo wszystko albo nic. Consistency — baza przechodzi ze spójnego stanu do spójnego. Isolation — równoległe transakcje nie widzą nawzajem niezatwierdzonych zmian. Durability — zatwierdzone zmiany przeżywają awarie (WAL log). Bez ACID: przelew bankowy mógłby odjąć z konta A ale nie dodać do B przy awarii.

**"Jaka różnica między optymistyczną a pesymistyczną współbieżnością?"** Optymistyczna zakłada że konflikty są rzadkie — nie blokuje przy odczycie, wykrywa konflikt przy zapisie (RowVersion/Timestamp). Pesymistyczna zakłada częste konflikty — blokuje wiersz przy odczycie (UPDLOCK), inne transakcje czekają. Optymistyczna: wyższy throughput, możliwy retry. Pesymistyczna: gwarancja że zapis się uda, ryzyko deadlocka.

**"Jak zapobiegać deadlockom?"** Zawsze blokuj zasoby w tej samej kolejności (np. zawsze mniejszy Id najpierw). Trzymaj transakcje krótkie — minimalizuj czas blokady. Używaj odpowiedniego poziomu izolacji — nie używaj Serializable gdy ReadCommitted wystarczy. Retry pattern dla deadlocka (SqlException.Number == 1205). Index coverage — złe plany wykonania = blokady na poziomie strony zamiast wiersza.

**"Co to phantom reads i jak zapobiec?"** Phantom read — w jednej transakcji dwa identyczne SELECT zwracają różne zestawy wierszy bo inna transakcja dodała/usunęła wiersze. Przykład: liczysz zamówienia (10), inna transakcja dodaje zamówienie, liczysz ponownie (11). Zapobieganie: IsolationLevel.Serializable (blokady zakresowe) lub Snapshot isolation (MVCC — czytasz snapshot z BEGIN TRANSACTION).