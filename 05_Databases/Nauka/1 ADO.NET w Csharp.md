### ADO.NET w C#

ADO.NET to niskopoziomowe API do komunikacji z bazami danych. Daje pełną kontrolę nad SQL i połączeniami — to fundament na którym zbudowane są ORMy jak EF Core i Dapper.

---

### 1. Połączenie — SqlConnection

csharp

```csharp
using System.Data;
using Microsoft.Data.SqlClient;  // dotnet add package Microsoft.Data.SqlClient

// Connection String — format
string connStr = "Server=localhost;Database=Sklep;Integrated Security=True;TrustServerCertificate=True;";

// Lub z hasłem
string connStrHaslo = "Server=sql.firma.pl,1433;Database=Sklep;" +
                      "User Id=app_user;Password=s3cur3_pass;" +
                      "TrustServerCertificate=True;Application Name=Sklep.API;";

// Connection String Builder — bezpieczne budowanie (bez injection)
var builder = new SqlConnectionStringBuilder
{
    DataSource         = "localhost",
    InitialCatalog     = "Sklep",
    IntegratedSecurity = true,
    TrustServerCertificate = true,
    ApplicationName    = "MojaSklep",
    ConnectTimeout     = 30,
    CommandTimeout     = 60,   // nie ma bezpośrednio, ale przydatny kontekst
    Encrypt            = true
};
Console.WriteLine(builder.ConnectionString);

// Otwieranie i zamykanie połączenia
await using var conn = new SqlConnection(builder.ConnectionString);

Console.WriteLine($"Stan przed Open: {conn.State}");  // Closed
await conn.OpenAsync();
Console.WriteLine($"Stan po Open: {conn.State}");      // Open
// using → conn.Dispose() → conn.Close() automatycznie

// Connection pooling — .NET zarządza pulą automatycznie!
// Każde "nowe" SqlConnection to tak naprawdę połączenie z puli
// Open() pobiera z puli, Close()/Dispose() zwraca do puli
// Pula konfigurowana przez connection string:
string connZPula = "Server=...;Min Pool Size=5;Max Pool Size=100;" +
                   "Connection Lifetime=300;Pooling=true";
```

---

### 2. SqlCommand — wykonywanie zapytań

csharp

```csharp
await using var conn2 = new SqlConnection(connStr);
await conn2.OpenAsync();

// Tworzenie SqlCommand
using var cmd = new SqlCommand("SELECT COUNT(*) FROM Produkty", conn2);

// Lub przez conn.CreateCommand() — lepsza praktyka!
using var cmd2 = conn2.CreateCommand();
cmd2.CommandText = "SELECT COUNT(*) FROM Produkty";
cmd2.CommandType  = CommandType.Text;      // SQL text (domyślne)
cmd2.CommandTimeout = 30;                  // sekundy, 0 = bez limitu

// ExecuteScalar — pojedyncza wartość (pierwsza kolumna, pierwszy wiersz)
object? wynik = await cmd.ExecuteScalarAsync();
int liczba = wynik != null ? (int)wynik : 0;
Console.WriteLine($"Produktów: {liczba}");

// ExecuteNonQuery — INSERT, UPDATE, DELETE, DDL — zwraca liczbę wierszy
using var cmdInsert = conn2.CreateCommand();
cmdInsert.CommandText = """
    INSERT INTO Produkty (Nazwa, Cena, Kategoria, StanMagazynu)
    VALUES (@Nazwa, @Cena, @Kategoria, @Stan)
    """;
cmdInsert.Parameters.AddWithValue("@Nazwa",     "Laptop");
cmdInsert.Parameters.AddWithValue("@Cena",      3500m);
cmdInsert.Parameters.AddWithValue("@Kategoria", "IT");
cmdInsert.Parameters.AddWithValue("@Stan",      10);

int wierszy = await cmdInsert.ExecuteNonQueryAsync();
Console.WriteLine($"Wstawiono {wierszy} wiersz(y)");

// Pobranie ID nowo wstawionego rekordu
cmdInsert.CommandText = """
    INSERT INTO Produkty (Nazwa, Cena, Kategoria)
    VALUES (@Nazwa, @Cena, @Kategoria);
    SELECT SCOPE_IDENTITY();
    """;
object? noweId = await cmdInsert.ExecuteScalarAsync();
int id = Convert.ToInt32(noweId);
Console.WriteLine($"Nowe ID: {id}");

// Stored Procedure
using var cmdSP = conn2.CreateCommand();
cmdSP.CommandText = "dbo.PobierzProduktPoId";
cmdSP.CommandType = CommandType.StoredProcedure;
cmdSP.Parameters.AddWithValue("@Id", 1);
```

---

### 3. Parametry — ochrona przed SQL Injection

csharp

```csharp
// SQL INJECTION — NIGDY tak nie rób!
string nazwaNiebezpieczna = "'; DROP TABLE Produkty; --";
string zlySQL = $"SELECT * FROM Produkty WHERE Nazwa = '{nazwaNiebezpieczna}'";
// Zapytanie staje się: SELECT * FROM Produkty WHERE Nazwa = ''; DROP TABLE Produkty; --'
// KATASTROFA!

// ZAWSZE używaj parametrów!
await using var conn3 = new SqlConnection(connStr);
await conn3.OpenAsync();

// Sposób 1 — AddWithValue (wygodne, ale może mieć problemy z typem)
using var cmd3 = conn3.CreateCommand();
cmd3.CommandText = "SELECT * FROM Produkty WHERE Nazwa = @Nazwa AND Cena > @MinCena";
cmd3.Parameters.AddWithValue("@Nazwa",    "Laptop");
cmd3.Parameters.AddWithValue("@MinCena",  1000m);

// Sposób 2 — SqlParameter z jawnym typem (ZALECANE — precyzyjniejsze)
using var cmd4 = conn3.CreateCommand();
cmd4.CommandText = """
    SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu
    FROM Produkty
    WHERE Kategoria = @Kategoria
      AND Cena BETWEEN @MinCena AND @MaxCena
      AND StanMagazynu >= @MinStan
    ORDER BY Cena DESC
    """;

cmd4.Parameters.Add(new SqlParameter("@Kategoria", SqlDbType.NVarChar, 100)
    { Value = "IT" });
cmd4.Parameters.Add(new SqlParameter("@MinCena", SqlDbType.Decimal)
    { Value = 500m, Precision = 18, Scale = 2 });
cmd4.Parameters.Add(new SqlParameter("@MaxCena", SqlDbType.Decimal)
    { Value = 5000m, Precision = 18, Scale = 2 });
cmd4.Parameters.Add(new SqlParameter("@MinStan", SqlDbType.Int)
    { Value = 1 });

// Sposób 3 — Add z przeciążeniem (też dobry)
cmd4.Parameters.Add("@Kategoria", SqlDbType.NVarChar, 100).Value = "IT";

// Parametry OUTPUT — pobieranie wartości z SP
using var cmdOut = conn3.CreateCommand();
cmdOut.CommandText = "dbo.AktualizujStan";
cmdOut.CommandType = CommandType.StoredProcedure;

cmdOut.Parameters.Add("@Id",       SqlDbType.Int).Value       = 1;
cmdOut.Parameters.Add("@Zmiana",   SqlDbType.Int).Value       = -5;

var paramNowyStan = cmdOut.Parameters.Add("@NowyStan", SqlDbType.Int);
paramNowyStan.Direction = ParameterDirection.Output;

var paramStatus = cmdOut.Parameters.Add("@Status", SqlDbType.NVarChar, 50);
paramStatus.Direction = ParameterDirection.Output;

await cmdOut.ExecuteNonQueryAsync();

Console.WriteLine($"Nowy stan: {paramNowyStan.Value}");
Console.WriteLine($"Status: {paramStatus.Value}");

// NULL w parametrach — PUŁAPKA
string? opcjonalnyOpis = null;
cmd4.Parameters.Add("@Opis", SqlDbType.NVarChar, 500).Value =
    (object?)opcjonalnyOpis ?? DBNull.Value;  // null → DBNull.Value!
// Bez tego: SqlException "parameter has no default value"
```

---

### 4. SqlDataReader — odczyt wyników

csharp

```csharp
await using var conn4 = new SqlConnection(connStr);
await conn4.OpenAsync();

using var cmd5 = conn4.CreateCommand();
cmd5.CommandText = """
    SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu, DataDodania, Aktywny, Opis
    FROM Produkty
    WHERE Kategoria = @Kat
    ORDER BY Cena DESC
    """;
cmd5.Parameters.Add("@Kat", SqlDbType.NVarChar, 50).Value = "IT";

using var reader = await cmd5.ExecuteReaderAsync(
    CommandBehavior.SequentialAccess |    // czytanie kolumn po kolei (wydajność)
    CommandBehavior.SingleResult);        // tylko jeden result set

while (await reader.ReadAsync())
{
    // Odczyt po indeksie (szybszy)
    int    id    = reader.GetInt32(0);
    string nazwa = reader.GetString(1);
    decimal cena = reader.GetDecimal(2);

    // Odczyt po nazwie (czytelniejszy)
    string kat   = reader.GetString("Kategoria");
    int    stan  = reader.GetInt32("StanMagazynu");

    // Odczyt z null check
    DateTime? data = reader.IsDBNull("DataDodania")
        ? null
        : reader.GetDateTime("DataDodania");

    bool aktywny = !reader.IsDBNull("Aktywny") && reader.GetBoolean("Aktywny");

    string? opis = reader.IsDBNull("Opis") ? null : reader.GetString("Opis");

    Console.WriteLine($"[{id}] {nazwa}: {cena:C} | {kat} | stan: {stan}");
}

// Ordinals — pre-compute dla wydajności w dużych pętlach
// Zamiast GetString("Nazwa") przy każdej iteracji — O(1) vs O(n lookupów)
int ordId    = reader.GetOrdinal("Id");
int ordNazwa = reader.GetOrdinal("Nazwa");
int ordCena  = reader.GetOrdinal("Cena");

while (await reader.ReadAsync())
{
    int    idV    = reader.GetInt32(ordId);
    string nazwaV = reader.GetString(ordNazwa);
    decimal cenaV = reader.GetDecimal(ordCena);
}

// Wiele result sets — gdy SP zwraca kilka SELECT
using var cmdMulti = conn4.CreateCommand();
cmdMulti.CommandText = """
    SELECT COUNT(*) AS Liczba FROM Produkty;
    SELECT TOP 5 Nazwa, Cena FROM Produkty ORDER BY Cena DESC;
    """;

using var multiReader = await cmdMulti.ExecuteReaderAsync();

// Pierwszy result set
if (await multiReader.ReadAsync())
    Console.WriteLine($"Liczba: {multiReader.GetInt32(0)}");

// Następny result set
await multiReader.NextResultAsync();

Console.WriteLine("Top 5 najdroższych:");
while (await multiReader.ReadAsync())
    Console.WriteLine($"  {multiReader.GetString(0)}: {multiReader.GetDecimal(1):C}");

// GetFieldValue<T> — generyczne, nowoczesne API
while (await reader.ReadAsync())
{
    var nazwaG = reader.GetFieldValue<string>("Nazwa");
    var cenaG  = reader.GetFieldValue<decimal>("Cena");
    var opisG  = await reader.GetFieldValueAsync<string?>("Opis");
}
```

---

### 5. Transakcje

csharp

```csharp
await using var conn5 = new SqlConnection(connStr);
await conn5.OpenAsync();

// Podstawowa transakcja
SqlTransaction? trx = null;
try
{
    trx = conn5.BeginTransaction(
        IsolationLevel.ReadCommitted);  // poziom izolacji

    using var cmd6 = conn5.CreateCommand();
    cmd6.Transaction = trx;  // WAŻNE — przypisz transakcję do command!

    // Operacja 1 — pobierz aktualny stan
    cmd6.CommandText = "SELECT StanMagazynu FROM Produkty WHERE Id = @Id";
    cmd6.Parameters.Add("@Id", SqlDbType.Int).Value = 1;
    int stanAktualny = (int)(await cmd6.ExecuteScalarAsync() ?? 0);

    if (stanAktualny < 5)
        throw new InvalidOperationException("Niewystarczający stan magazynowy");

    // Operacja 2 — zmniejsz stan
    cmd6.CommandText = """
        UPDATE Produkty
        SET StanMagazynu = StanMagazynu - @Ilosc
        WHERE Id = @Id AND StanMagazynu >= @Ilosc
        """;
    cmd6.Parameters.Clear();
    cmd6.Parameters.Add("@Id",    SqlDbType.Int).Value = 1;
    cmd6.Parameters.Add("@Ilosc", SqlDbType.Int).Value = 3;
    int zaktualizowanych = await cmd6.ExecuteNonQueryAsync();

    if (zaktualizowanych == 0)
        throw new InvalidOperationException("Nie udało się zarezerwować towaru");

    // Operacja 3 — utwórz zamówienie
    cmd6.CommandText = """
        INSERT INTO Zamowienia (ProduktId, Ilosc, Status, DataZlozenia)
        VALUES (@ProdId, @Ilosc, 'Nowe', GETUTCDATE());
        SELECT SCOPE_IDENTITY();
        """;
    cmd6.Parameters.Clear();
    cmd6.Parameters.Add("@ProdId", SqlDbType.Int).Value = 1;
    cmd6.Parameters.Add("@Ilosc",  SqlDbType.Int).Value = 3;
    object? zamId = await cmd6.ExecuteScalarAsync();

    // Wszystko OK — zatwierdź
    await trx.CommitAsync();
    Console.WriteLine($"Zamówienie #{Convert.ToInt32(zamId)} złożone!");
}
catch (Exception ex)
{
    Console.WriteLine($"Błąd: {ex.Message} — wycofuję...");
    if (trx != null)
        await trx.RollbackAsync();
    throw;
}
finally
{
    trx?.Dispose();
}

// Savepoints — częściowe wycofanie
await using var conn6 = new SqlConnection(connStr);
await conn6.OpenAsync();
await using var trx2 = conn6.BeginTransaction();

try
{
    using var cmdSP = conn6.CreateCommand();
    cmdSP.Transaction = trx2;

    // Operacja 1
    cmdSP.CommandText = "UPDATE Konta SET Saldo = Saldo - 100 WHERE Id = 1";
    await cmdSP.ExecuteNonQueryAsync();

    // Savepoint przed ryzykowną operacją
    await trx2.SaveAsync("przed_bonusem");

    try
    {
        // Ryzykowna operacja
        cmdSP.CommandText = "UPDATE Konta SET Saldo = Saldo + 100 WHERE Id = 99";
        await cmdSP.ExecuteNonQueryAsync();
    }
    catch
    {
        // Wróć tylko do savepoint — nie cała transakcja!
        await trx2.RollbackAsync("przed_bonusem");
        Console.WriteLine("Bonus nieudany — cofnięto tylko tę część");
    }

    await trx2.CommitAsync();
}
catch
{
    await trx2.RollbackAsync();
    throw;
}
```

---

### 6. Wzorce i Repository

csharp

```csharp
// Repozytorium oparte na ADO.NET

public record Produkt(
    int Id,
    string Nazwa,
    decimal Cena,
    string Kategoria,
    int StanMagazynu);

public interface IProduktRepository
{
    Task<IReadOnlyList<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool> AktualizujAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool> UsunAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Produkt>> SzukajAsync(string? kategoria, decimal? minCena,
        decimal? maxCena, CancellationToken ct = default);
}

public class AdoNetProduktRepository : IProduktRepository
{
    private readonly string _connStr;

    public AdoNetProduktRepository(string connStr) => _connStr = connStr;

    private SqlConnection OtworzPolaczenie() => new(_connStr);

    // Pomocnik — mapowanie rekordu na obiekt
    private static Produkt MapujProdukt(SqlDataReader r) => new(
        r.GetInt32("Id"),
        r.GetString("Nazwa"),
        r.GetDecimal("Cena"),
        r.GetString("Kategoria"),
        r.GetInt32("StanMagazynu"));

    public async Task<IReadOnlyList<Produkt>> PobierzWszystkieAsync(
        CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu FROM Produkty ORDER BY Nazwa";

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var wyniki = new List<Produkt>();
        while (await reader.ReadAsync(ct))
            wyniki.Add(MapujProdukt(reader));

        return wyniki.AsReadOnly();
    }

    public async Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu
            FROM Produkty WHERE Id = @Id
            """;
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        using var reader = await cmd.ExecuteReaderAsync(
            CommandBehavior.SingleRow, ct);

        return await reader.ReadAsync(ct) ? MapujProdukt(reader) : null;
    }

    public async Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Produkty (Nazwa, Cena, Kategoria, StanMagazynu)
            VALUES (@Nazwa, @Cena, @Kategoria, @Stan);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        cmd.Parameters.Add("@Nazwa",     SqlDbType.NVarChar, 200).Value = produkt.Nazwa;
        cmd.Parameters.Add("@Cena",      SqlDbType.Decimal)
            { Value = produkt.Cena, Precision = 18, Scale = 2 };
        cmd.Parameters.Add("@Kategoria", SqlDbType.NVarChar, 100).Value = produkt.Kategoria;
        cmd.Parameters.Add("@Stan",      SqlDbType.Int).Value           = produkt.StanMagazynu;

        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<bool> AktualizujAsync(Produkt produkt, CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Produkty
            SET Nazwa = @Nazwa, Cena = @Cena,
                Kategoria = @Kategoria, StanMagazynu = @Stan
            WHERE Id = @Id
            """;

        cmd.Parameters.Add("@Id",        SqlDbType.Int).Value           = produkt.Id;
        cmd.Parameters.Add("@Nazwa",     SqlDbType.NVarChar, 200).Value = produkt.Nazwa;
        cmd.Parameters.Add("@Cena",      SqlDbType.Decimal)
            { Value = produkt.Cena, Precision = 18, Scale = 2 };
        cmd.Parameters.Add("@Kategoria", SqlDbType.NVarChar, 100).Value = produkt.Kategoria;
        cmd.Parameters.Add("@Stan",      SqlDbType.Int).Value           = produkt.StanMagazynu;

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Produkty WHERE Id = @Id";
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<Produkt>> SzukajAsync(
        string? kategoria, decimal? minCena, decimal? maxCena,
        CancellationToken ct = default)
    {
        await using var conn = OtworzPolaczenie();
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();

        // Dynamiczne zapytanie — budowane warunkowo
        var sb = new System.Text.StringBuilder("""
            SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu
            FROM Produkty
            WHERE 1 = 1
            """);

        if (kategoria != null)
        {
            sb.AppendLine(" AND Kategoria = @Kategoria");
            cmd.Parameters.Add("@Kategoria", SqlDbType.NVarChar, 100).Value = kategoria;
        }
        if (minCena.HasValue)
        {
            sb.AppendLine(" AND Cena >= @MinCena");
            cmd.Parameters.Add("@MinCena", SqlDbType.Decimal)
                { Value = minCena.Value, Precision = 18, Scale = 2 };
        }
        if (maxCena.HasValue)
        {
            sb.AppendLine(" AND Cena <= @MaxCena");
            cmd.Parameters.Add("@MaxCena", SqlDbType.Decimal)
                { Value = maxCena.Value, Precision = 18, Scale = 2 };
        }

        sb.AppendLine(" ORDER BY Cena");
        cmd.CommandText = sb.ToString();

        using var reader = await cmd.ExecuteReaderAsync(ct);
        var wyniki = new List<Produkt>();
        while (await reader.ReadAsync(ct))
            wyniki.Add(MapujProdukt(reader));

        return wyniki.AsReadOnly();
    }
}

// Użycie
var repo = new AdoNetProduktRepository(connStr);

var wszystkie = await repo.PobierzWszystkieAsync();
Console.WriteLine($"Produktów: {wszystkie.Count}");

var nowyId = await repo.DodajAsync(
    new Produkt(0, "Monitor", 1500m, "IT", 20));
Console.WriteLine($"Dodano z ID: {nowyId}");

var produkt2 = await repo.PobierzPoIdAsync(nowyId);
Console.WriteLine($"Pobrano: {produkt2?.Nazwa}");

var itoweDoPolowy = await repo.SzukajAsync("IT", null, 2000m);
Console.WriteLine($"IT do 2000zł: {itoweDoPolowy.Count} szt.");
```

---

### Typowe pytania rekrutacyjne

**"Czym jest SQL Injection i jak ADO.NET przed nim chroni?"** SQL Injection to atak gdzie złośliwe dane wstrzykują kod SQL. `SELECT * FROM Users WHERE name = '" + input + "'"` — gdy input = `' OR '1'='1` zwraca wszystkich użytkowników. Parametryzowane zapytania ADO.NET (`@Nazwa`) wysyłają wartość jako dane, nie jako część SQL — serwer bazy oddziela kod od danych. Wartość `'; DROP TABLE Users; --` jest traktowana dosłownie jako string, nie jako SQL.

**"Dlaczego `DBNull.Value` zamiast `null` dla parametrów?"** ADO.NET rozróżnia C# `null` (brak wartości w parametrze — parametr niezainicjalizowany) od SQL `NULL` (null w bazie danych). Przekazanie C# `null` do `SqlParameter.Value` skutkuje błędem "parameter has no default value". `DBNull.Value` to singleton reprezentujący SQL NULL. Wzorzec: `cmd.Parameters["@Opis"].Value = (object?)opis ?? DBNull.Value`.

**"Jakie są poziomy izolacji transakcji i kiedy co używać?"** `ReadUncommitted` — najszybszy, dirty reads możliwe (widzisz niezatwierdzone dane innych transakcji). `ReadCommitted` — domyślny SQL Server, brak dirty reads, phantom reads możliwe. `RepeatableRead` — te same dane przy ponownym odczycie, phantom reads możliwe. `Serializable` — najwolniejszy, pełna izolacja. `Snapshot` — optymistyczna współbieżność, wersjonowanie wierszy, dobre dla read-heavy.

**"Co to `CommandBehavior.CloseConnection`?"** Gdy przekazujesz `SqlDataReader` poza metodę (nie możesz zamknąć połączenia wewnątrz), `CloseConnection` automatycznie zamknie połączenie gdy reader zostanie Disposed. `CommandBehavior.SingleRow` — optymalizacja gdy oczekujesz jednego wiersza. `CommandBehavior.SequentialAccess` — dla dużych danych (BLOB) — kolumny muszą być czytane w kolejności, ale zużywa mniej pamięci.