### Dapper w C#

Dapper to **micro-ORM** — cienka warstwa nad ADO.NET która eliminuje boilerplate mapowania wyników na obiekty, zachowując pełną kontrolę nad SQL.

---

### 1. Setup i podstawy

csharp

```csharp
// dotnet add package Dapper
// dotnet add package Microsoft.Data.SqlClient

using Dapper;
using Microsoft.Data.SqlClient;

string connStr = "Server=localhost;Database=Sklep;Integrated Security=True;TrustServerCertificate=True;";

// Dapper rozszerza IDbConnection — działa z dowolnym providerem!
// SqlConnection, NpgsqlConnection (PostgreSQL), SQLiteConnection, MySqlConnection

// Modele
public record Produkt(
    int Id,
    string Nazwa,
    decimal Cena,
    string Kategoria,
    int StanMagazynu,
    bool Aktywny,
    DateTime DataDodania);

public record Klient(
    int Id,
    string Imie,
    string Nazwisko,
    string Email,
    DateTime DataRejestracji);

public record Zamowienie(
    int Id,
    int KlientId,
    DateTime DataZlozenia,
    decimal Wartosc,
    string Status);

// Pierwsze zapytanie — tak proste jak to możliwe
await using var conn = new SqlConnection(connStr);

// Dapper automatycznie:
// 1. Otwiera połączenie (jeśli zamknięte)
// 2. Wykonuje SQL
// 3. Mapuje wyniki na typ T
// 4. Zamyka (jeśli sam otworzył)

IEnumerable<Produkt> produkty = await conn.QueryAsync<Produkt>(
    "SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu, Aktywny, DataDodania FROM Produkty");

foreach (var p in produkty)
    Console.WriteLine($"{p.Id}: {p.Nazwa} — {p.Cena:C}");
```

---

### 2. Query — podstawowe odczyty

csharp

```csharp
await using var conn = new SqlConnection(connStr);

// QueryAsync<T> — wiele wierszy
IEnumerable<Produkt> wszystkie = await conn.QueryAsync<Produkt>(
    "SELECT * FROM Produkty ORDER BY Nazwa");

// QueryFirstAsync — pierwszy wiersz, wyjątek gdy brak
Produkt pierwszy = await conn.QueryFirstAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Id = @Id", new { Id = 1 });

// QueryFirstOrDefaultAsync — pierwszy lub null gdy brak
Produkt? mozeByc = await conn.QueryFirstOrDefaultAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Id = @Id", new { Id = 9999 });
Console.WriteLine(mozeByc?.Nazwa ?? "Brak produktu");

// QuerySingleAsync — dokładnie jeden, wyjątek gdy 0 lub >1
Produkt jedyny = await conn.QuerySingleAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Id = @Id", new { Id = 1 });

// QuerySingleOrDefaultAsync — jeden lub null, wyjątek gdy >1
Produkt? jedynyLubNull = await conn.QuerySingleOrDefaultAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Nazwa = @Nazwa", new { Nazwa = "Laptop" });

// Typy skalarne
int liczba = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM Produkty");
decimal maxCena = await conn.QuerySingleAsync<decimal>("SELECT MAX(Cena) FROM Produkty");
string? nazwaSerwera = await conn.QuerySingleAsync<string>("SELECT @@SERVERNAME");

// ExecuteScalarAsync — pojedyncza wartość (jak ADO.NET)
int noweId = await conn.ExecuteScalarAsync<int>("""
    INSERT INTO Produkty (Nazwa, Cena, Kategoria)
    VALUES (@Nazwa, @Cena, @Kategoria);
    SELECT CAST(SCOPE_IDENTITY() AS INT);
    """, new { Nazwa = "Mysz", Cena = 149.99m, Kategoria = "IT" });
Console.WriteLine($"Nowe ID: {noweId}");

// Mapowanie na typy dynamiczne — gdy nie znasz struktury
IEnumerable<dynamic> dynamiczne = await conn.QueryAsync(
    "SELECT Nazwa, Cena FROM Produkty WHERE Kategoria = @Kat",
    new { Kat = "IT" });

foreach (dynamic d in dynamiczne)
    Console.WriteLine($"{d.Nazwa}: {d.Cena}");

// Mapowanie na Dictionary
IEnumerable<IDictionary<string, object>> dicts = await conn.QueryAsync(
    "SELECT Id, Nazwa, Cena FROM Produkty") as IEnumerable<IDictionary<string, object>>
    ?? Enumerable.Empty<IDictionary<string, object>>();
```

---

### 3. Parametry — wszystkie sposoby

csharp

```csharp
await using var conn = new SqlConnection(connStr);

// 1. Anonymous object — najczęstszy sposób
var wynik1 = await conn.QueryAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Kategoria = @Kat AND Cena > @Min",
    new { Kat = "IT", Min = 500m });

// 2. DynamicParameters — gdy parametry budowane dynamicznie
var dp = new DynamicParameters();
dp.Add("@Nazwa",  "Laptop",  DbType.String,  size: 200);
dp.Add("@Cena",   3500m,     DbType.Decimal, precision: 18, scale: 2);
dp.Add("@Kat",    "IT",      DbType.String,  size: 100);

await conn.ExecuteAsync(
    "INSERT INTO Produkty (Nazwa, Cena, Kategoria) VALUES (@Nazwa, @Cena, @Kat)",
    dp);

// DynamicParameters z OUTPUT
var dpOut = new DynamicParameters(new { Id = 1, Zmiana = -5 });
dpOut.Add("@NowyStan", dbType: DbType.Int32, direction: ParameterDirection.Output);
dpOut.Add("@Status",   dbType: DbType.String, size: 50, direction: ParameterDirection.Output);

await conn.ExecuteAsync("dbo.AktualizujStan", dpOut,
    commandType: CommandType.StoredProcedure);

int nowyStan = dpOut.Get<int>("@NowyStan");
string status = dpOut.Get<string>("@Status");
Console.WriteLine($"Stan: {nowyStan}, Status: {status}");

// 3. Konkretny obiekt jako parametry
var nowyProdukt = new { Nazwa = "Monitor", Cena = 1500m, Kategoria = "IT", Stan = 10 };
await conn.ExecuteAsync(
    "INSERT INTO Produkty (Nazwa, Cena, Kategoria, StanMagazynu) VALUES (@Nazwa, @Cena, @Kategoria, @Stan)",
    nowyProdukt);

// Dapper użyje WSZYSTKICH właściwości obiektu jako parametrów
// Jeśli obiekt ma więcej pól niż SQL — OK (nieużywane ignorowane)

// 4. Kolekcja parametrów — IN clause
var ids = new[] { 1, 2, 3, 4, 5 };
var produktyWIds = await conn.QueryAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Id IN @Ids",
    new { Ids = ids });
// Dapper automatycznie rozwijaja tablicę:
// WHERE Id IN @Ids → WHERE Id IN (1, 2, 3, 4, 5)

// 5. NULL w parametrach
string? opcjonalnyOpis = null;
var dpNull = new DynamicParameters();
dpNull.Add("@Opis", opcjonalnyOpis, DbType.String);
// Dapper automatycznie konwertuje C# null → DBNull.Value!

// 6. Parametry z DbString — kontrola długości stringa
var dbString = new DbString { Value = "IT", Length = 100, IsAnsi = false };
var wynikStr = await conn.QueryAsync<Produkt>(
    "SELECT * FROM Produkty WHERE Kategoria = @Kat",
    new { Kat = dbString });
```

---

### 4. Execute — INSERT, UPDATE, DELETE

csharp

```csharp
await using var conn = new SqlConnection(connStr);

// ExecuteAsync — zwraca liczbę dotkniętych wierszy
int wierszy = await conn.ExecuteAsync(
    "UPDATE Produkty SET Cena = Cena * 1.1 WHERE Kategoria = @Kat",
    new { Kat = "IT" });
Console.WriteLine($"Zaktualizowano {wierszy} produktów");

// Bulk insert — lista obiektów jako parametry!
// Dapper wykona INSERT raz dla każdego elementu listy
var noweProdukty = new List<object>
{
    new { Nazwa = "Klawiatura", Cena = 249m, Kat = "IT",    Stan = 30 },
    new { Nazwa = "Biurko",     Cena = 899m, Kat = "Meble", Stan = 5  },
    new { Nazwa = "Lampa",      Cena = 149m, Kat = "Dom",   Stan = 20 },
};

int wstawiono = await conn.ExecuteAsync("""
    INSERT INTO Produkty (Nazwa, Cena, Kategoria, StanMagazynu)
    VALUES (@Nazwa, @Cena, @Kat, @Stan)
    """, noweProdukty);
Console.WriteLine($"Wstawiono {wstawiono} rekordów");

// Bulk update — lista obiektów
var aktualizacje = new[]
{
    new { Id = 1, Cena = 3600m },
    new { Id = 2, Cena = 165m  },
    new { Id = 3, Cena = 275m  },
};

await conn.ExecuteAsync(
    "UPDATE Produkty SET Cena = @Cena WHERE Id = @Id",
    aktualizacje);

// Stored Procedure
await conn.ExecuteAsync(
    "dbo.ArchiwizujStareZamowienia",
    new { DataGraniczna = DateTime.Now.AddMonths(-6) },
    commandType: CommandType.StoredProcedure);
```

---

### 5. Multi-mapping — JOIN na wiele typów

csharp

```csharp
// Multi-mapping — mapowanie JOIN-a na zagnieżdżone obiekty

public class ZamowienieZKlientem
{
    public int Id { get; set; }
    public DateTime DataZlozenia { get; set; }
    public decimal Wartosc { get; set; }
    public string Status { get; set; } = "";
    public Klient Klient { get; set; } = null!;  // zagnieżdżony obiekt
}

await using var conn = new SqlConnection(connStr);

// QueryAsync z splitOn — powiedz Dapperowi gdzie zaczyna się kolejny obiekt
var zamowienia = await conn.QueryAsync<ZamowienieZKlientem, Klient, ZamowienieZKlientem>(
    sql: """
        SELECT z.Id, z.DataZlozenia, z.Wartosc, z.Status,
               k.Id, k.Imie, k.Nazwisko, k.Email, k.DataRejestracji
        FROM Zamowienia z
        INNER JOIN Klienci k ON z.KlientId = k.Id
        ORDER BY z.DataZlozenia DESC
        """,
    map: (zamowienie, klient) =>          // funkcja łącząca
    {
        zamowienie.Klient = klient;
        return zamowienie;
    },
    splitOn: "Id"                         // kolumna gdzie zaczyna się Klient
                                          // UWAGA: musi być Id klienta, nie zamówienia!
);

foreach (var z in zamowienia)
    Console.WriteLine($"#{z.Id} | {z.Klient.Imie} {z.Klient.Nazwisko} | {z.Wartosc:C}");

// Trzy obiekty w JOINie
public class PozycjaZamowienia
{
    public int Id { get; set; }
    public int Ilosc { get; set; }
    public decimal CenaJednostkowa { get; set; }
    public Zamowienie Zamowienie { get; set; } = null!;
    public Produkt Produkt { get; set; } = null!;
}

var pozycje = await conn.QueryAsync<PozycjaZamowienia, Zamowienie, Produkt, PozycjaZamowienia>(
    sql: """
        SELECT pz.Id, pz.Ilosc, pz.CenaJednostkowa,
               z.Id, z.DataZlozenia, z.Wartosc, z.Status,
               p.Id, p.Nazwa, p.Cena, p.Kategoria, p.StanMagazynu, p.Aktywny, p.DataDodania
        FROM PozycjeZamowien pz
        INNER JOIN Zamowienia z ON pz.ZamowienieId = z.Id
        INNER JOIN Produkty p ON pz.ProduktId = p.Id
        """,
    map: (pozycja, zamowienie, produkt) =>
    {
        pozycja.Zamowienie = zamowienie;
        pozycja.Produkt    = produkt;
        return pozycja;
    },
    splitOn: "Id,Id");                    // dwa splitOn — oddzielone przecinkami

// Grupowanie po kluczu — jeden zamówienie z wieloma pozycjami
public class ZamowienieZPozycjami
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public List<Produkt> Produkty { get; set; } = new();
}

var lookup = new Dictionary<int, ZamowienieZPozycjami>();

await conn.QueryAsync<ZamowienieZPozycjami, Produkt, ZamowienieZPozycjami>(
    sql: """
        SELECT z.Id, z.Status,
               p.Id, p.Nazwa, p.Cena, p.Kategoria, p.StanMagazynu, p.Aktywny, p.DataDodania
        FROM Zamowienia z
        INNER JOIN PozycjeZamowien pz ON z.Id = pz.ZamowienieId
        INNER JOIN Produkty p ON pz.ProduktId = p.Id
        WHERE z.Id IN @Ids
        """,
    map: (zam, prod) =>
    {
        if (!lookup.TryGetValue(zam.Id, out var istniejace))
        {
            istniejace = zam;
            lookup[zam.Id] = istniejace;
        }
        istniejace.Produkty.Add(prod);
        return istniejace;
    },
    param: new { Ids = new[] { 1, 2, 3 } },
    splitOn: "Id");

var zamowieniaZProd = lookup.Values.ToList();
foreach (var z in zamowieniaZProd)
    Console.WriteLine($"Zamówienie #{z.Id}: {z.Produkty.Count} produktów");
```

---

### 6. Multi-result — wiele result setów

csharp

```csharp
await using var conn = new SqlConnection(connStr);

// QueryMultiple — wiele SELECT w jednym zapytaniu
using var multi = await conn.QueryMultipleAsync("""
    SELECT COUNT(*) FROM Produkty;
    SELECT COUNT(*) FROM Klienci WHERE Aktywny = 1;
    SELECT TOP 5 Id, Nazwa, Cena FROM Produkty ORDER BY Cena DESC;
    SELECT TOP 3 Id, Imie, Nazwisko FROM Klienci ORDER BY DataRejestracji DESC;
    """);

int liczbaProd  = await multi.ReadSingleAsync<int>();
int liczbaKli   = await multi.ReadSingleAsync<int>();
var topProdukty = await multi.ReadAsync<dynamic>();
var nowyKlienci = await multi.ReadAsync<dynamic>();

Console.WriteLine($"Produktów: {liczbaProd}, Klientów aktywnych: {liczbaKli}");
Console.WriteLine("Top 5 drogich:");
foreach (var p in topProdukty)
    Console.WriteLine($"  {p.Nazwa}: {p.Cena:C}");

// Przydatne dla raportów — jeden round-trip do bazy
using var raportMulti = await conn.QueryMultipleAsync(
    "dbo.PobierzRaportSprzedazy",
    new { DataOd = DateTime.Now.AddMonths(-1), DataDo = DateTime.Now },
    commandType: CommandType.StoredProcedure);

var podsumowanie = await raportMulti.ReadSingleAsync<dynamic>();
var topKlienci   = await raportMulti.ReadAsync<dynamic>();
var wgKategorii  = await raportMulti.ReadAsync<dynamic>();
var dziennyTrend = await raportMulti.ReadAsync<dynamic>();
```

---

### 7. Transakcje z Dapper

csharp

```csharp
await using var conn = new SqlConnection(connStr);
await conn.OpenAsync();

// Dapper używa transakcji przez opcjonalny parametr transaction
await using var trx = conn.BeginTransaction();

try
{
    // Przekaż transakcję do każdego zapytania!
    int klientId = await conn.ExecuteScalarAsync<int>("""
        INSERT INTO Klienci (Imie, Nazwisko, Email)
        VALUES (@Imie, @Nazwisko, @Email);
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """,
        new { Imie = "Jan", Nazwisko = "Kowalski", Email = "jan@test.pl" },
        trx);      // ← transakcja!

    int zamId = await conn.ExecuteScalarAsync<int>("""
        INSERT INTO Zamowienia (KlientId, Status, DataZlozenia)
        VALUES (@KlientId, 'Nowe', GETUTCDATE());
        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """,
        new { KlientId = klientId },
        trx);      // ← ta sama transakcja!

    await conn.ExecuteAsync("""
        INSERT INTO PozycjeZamowien (ZamowienieId, ProduktId, Ilosc, CenaJednostkowa)
        VALUES (@ZamId, @ProdId, @Ilosc, @Cena)
        """,
        new { ZamId = zamId, ProdId = 1, Ilosc = 2, Cena = 3500m },
        trx);

    await conn.ExecuteAsync(
        "UPDATE Produkty SET StanMagazynu = StanMagazynu - @Ilosc WHERE Id = @Id",
        new { Ilosc = 2, Id = 1 },
        trx);

    await trx.CommitAsync();
    Console.WriteLine($"Klient #{klientId}, Zamówienie #{zamId} — sukces!");
}
catch (Exception ex)
{
    await trx.RollbackAsync();
    Console.WriteLine($"Wycofano: {ex.Message}");
    throw;
}
```

---

### 8. Dapper vs ADO.NET — porównanie

csharp

```csharp
// Ten sam scenariusz — porównanie kodu

// ============ ADO.NET ============
public async Task<List<Produkt>> ADO_PobierzProdukty(string kat)
{
    var wyniki = new List<Produkt>();
    await using var conn = new SqlConnection(connStr);
    await conn.OpenAsync();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu, Aktywny, DataDodania FROM Produkty WHERE Kategoria = @Kat";
    cmd.Parameters.Add("@Kat", SqlDbType.NVarChar, 100).Value = kat;

    using var reader = await cmd.ExecuteReaderAsync();
    int ordId      = reader.GetOrdinal("Id");
    int ordNazwa   = reader.GetOrdinal("Nazwa");
    int ordCena    = reader.GetOrdinal("Cena");
    int ordKat     = reader.GetOrdinal("Kategoria");
    int ordStan    = reader.GetOrdinal("StanMagazynu");
    int ordAktywny = reader.GetOrdinal("Aktywny");
    int ordData    = reader.GetOrdinal("DataDodania");

    while (await reader.ReadAsync())
    {
        wyniki.Add(new Produkt(
            reader.GetInt32(ordId),
            reader.GetString(ordNazwa),
            reader.GetDecimal(ordCena),
            reader.GetString(ordKat),
            reader.GetInt32(ordStan),
            reader.GetBoolean(ordAktywny),
            reader.GetDateTime(ordData)));
    }
    return wyniki;
}

// ============ DAPPER ============
public async Task<List<Produkt>> Dapper_PobierzProdukty(string kat)
{
    await using var conn = new SqlConnection(connStr);
    return (await conn.QueryAsync<Produkt>(
        "SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu, Aktywny, DataDodania FROM Produkty WHERE Kategoria = @Kat",
        new { Kat = kat })).ToList();
}

// PORÓWNANIE:
// ┌────────────────────────┬───────────┬──────────────┐
// │ Cecha                  │ ADO.NET   │ Dapper       │
// ├────────────────────────┼───────────┼──────────────┤
// │ Linie kodu (powyżej)   │ ~25       │ ~5           │
// │ Mapowanie ręczne       │ TAK       │ NIE          │
// │ Pełna kontrola SQL     │ TAK       │ TAK          │
// │ Wydajność              │ ★★★★★   │ ★★★★☆      │
// │ Bulk operations        │ SqlBulk   │ Ograniczone  │
// │ Streaming dużych danych│ TAK       │ Ograniczone  │
// │ OUTPUT parametry       │ TAK       │ DynamicParams│
// │ Nauka                  │ Trudna    │ Łatwa        │
// └────────────────────────┴───────────┴──────────────┘
```

---

### 9. Praktyczny przykład — repozytorium z Dapper

csharp

```csharp
// Kompletne repozytorium — sprzedaż

public class DapperSklep
{
    private readonly string _connStr;

    public DapperSklep(string connStr) => _connStr = connStr;

    private SqlConnection Conn() => new(_connStr);

    // --- PRODUKTY ---
    public async Task<IReadOnlyList<Produkt>> PobierzProduktyAsync(
        string? kategoria = null,
        decimal? minCena  = null,
        decimal? maxCena  = null,
        bool tylkoAktywne = true)
    {
        var warunki = new List<string>();
        var dp = new DynamicParameters();

        if (tylkoAktywne) warunki.Add("Aktywny = 1");

        if (kategoria != null)
        {
            warunki.Add("Kategoria = @Kategoria");
            dp.Add("@Kategoria", kategoria);
        }
        if (minCena.HasValue)
        {
            warunki.Add("Cena >= @MinCena");
            dp.Add("@MinCena", minCena.Value);
        }
        if (maxCena.HasValue)
        {
            warunki.Add("Cena <= @MaxCena");
            dp.Add("@MaxCena", maxCena.Value);
        }

        string where = warunki.Any()
            ? "WHERE " + string.Join(" AND ", warunki)
            : "";

        string sql = $"""
            SELECT Id, Nazwa, Cena, Kategoria, StanMagazynu, Aktywny, DataDodania
            FROM Produkty
            {where}
            ORDER BY Nazwa
            """;

        await using var conn = Conn();
        return (await conn.QueryAsync<Produkt>(sql, dp)).ToList().AsReadOnly();
    }

    // --- ZAMÓWIENIA ---
    public async Task<int> ZlozZamowienieAsync(
        int klientId,
        IEnumerable<(int ProduktId, int Ilosc)> pozycje)
    {
        await using var conn = Conn();
        await conn.OpenAsync();
        await using var trx = conn.BeginTransaction();

        try
        {
            // Sprawdź dostępność wszystkich produktów
            var prodIds = pozycje.Select(p => p.ProduktId).ToArray();
            var dostepne = await conn.QueryAsync<dynamic>(
                "SELECT Id, StanMagazynu, Cena FROM Produkty WHERE Id IN @Ids AND Aktywny = 1",
                new { Ids = prodIds }, trx);

            var dostepneDict = dostepne.ToDictionary(
                d => (int)d.Id,
                d => new { Stan = (int)d.StanMagazynu, Cena = (decimal)d.Cena });

            foreach (var (prodId, ilosc) in pozycje)
            {
                if (!dostepneDict.TryGetValue(prodId, out var info))
                    throw new InvalidOperationException($"Produkt {prodId} niedostępny");
                if (info.Stan < ilosc)
                    throw new InvalidOperationException($"Niewystarczający stan dla produktu {prodId}: mamy {info.Stan}, potrzeba {ilosc}");
            }

            // Utwórz zamówienie
            decimal wartoscCalkowita = pozycje.Sum(p =>
                dostepneDict[p.ProduktId].Cena * p.Ilosc);

            int zamId = await conn.ExecuteScalarAsync<int>("""
                INSERT INTO Zamowienia (KlientId, Wartosc, Status, DataZlozenia)
                VALUES (@KlientId, @Wartosc, 'Nowe', GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """,
                new { KlientId = klientId, Wartosc = wartoscCalkowita },
                trx);

            // Dodaj pozycje i zaktualizuj stany
            foreach (var (prodId, ilosc) in pozycje)
            {
                decimal cenaJedn = dostepneDict[prodId].Cena;

                await conn.ExecuteAsync("""
                    INSERT INTO PozycjeZamowien (ZamowienieId, ProduktId, Ilosc, CenaJednostkowa)
                    VALUES (@ZamId, @ProdId, @Ilosc, @Cena)
                    """,
                    new { ZamId = zamId, ProdId = prodId, Ilosc = ilosc, Cena = cenaJedn },
                    trx);

                await conn.ExecuteAsync(
                    "UPDATE Produkty SET StanMagazynu = StanMagazynu - @Ilosc WHERE Id = @Id",
                    new { Ilosc = ilosc, Id = prodId },
                    trx);
            }

            await trx.CommitAsync();
            Console.WriteLine($"Zamówienie #{zamId} złożone — wartość: {wartoscCalkowita:C}");
            return zamId;
        }
        catch
        {
            await trx.RollbackAsync();
            throw;
        }
    }

    // --- RAPORT ---
    public async Task<dynamic> PobierzRaportAsync(DateTime dataOd, DateTime dataDo)
    {
        await using var conn = Conn();

        using var multi = await conn.QueryMultipleAsync("""
            -- Ogólne statystyki
            SELECT
                COUNT(*)             AS LiczbaZamowien,
                ISNULL(SUM(Wartosc), 0)  AS SumaSprzedazy,
                ISNULL(AVG(Wartosc), 0)  AS SrednieZamowienie,
                COUNT(DISTINCT KlientId) AS UnikalniKlienci
            FROM Zamowienia
            WHERE DataZlozenia BETWEEN @Od AND @Do;

            -- Top 5 produktów
            SELECT TOP 5
                p.Nazwa,
                SUM(pz.Ilosc)                   AS SprzedanoSzt,
                SUM(pz.Ilosc * pz.CenaJednostkowa) AS Przychod
            FROM PozycjeZamowien pz
            INNER JOIN Produkty p ON pz.ProduktId = p.Id
            INNER JOIN Zamowienia z ON pz.ZamowienieId = z.Id
            WHERE z.DataZlozenia BETWEEN @Od AND @Do
            GROUP BY p.Id, p.Nazwa
            ORDER BY Przychod DESC;

            -- Sprzedaż wg kategorii
            SELECT
                p.Kategoria,
                COUNT(DISTINCT z.Id)             AS LiczbaZamowien,
                SUM(pz.Ilosc)                    AS LacznaIlosc,
                SUM(pz.Ilosc * pz.CenaJednostkowa) AS Przychod
            FROM PozycjeZamowien pz
            INNER JOIN Produkty p ON pz.ProduktId = p.Id
            INNER JOIN Zamowienia z ON pz.ZamowienieId = z.Id
            WHERE z.DataZlozenia BETWEEN @Od AND @Do
            GROUP BY p.Kategoria
            ORDER BY Przychod DESC;
            """,
            new { Od = dataOd, Do = dataDo });

        var ogolne    = await multi.ReadSingleAsync<dynamic>();
        var topProd   = await multi.ReadAsync<dynamic>();
        var wgKat     = await multi.ReadAsync<dynamic>();

        Console.WriteLine($"\n=== Raport {dataOd:dd.MM} — {dataDo:dd.MM.yyyy} ===");
        Console.WriteLine($"Zamówień: {ogolne.LiczbaZamowien}, Sprzedaż: {ogolne.SumaSprzedazy:C}");

        Console.WriteLine("\nTop produkty:");
        foreach (var p in topProd)
            Console.WriteLine($"  {p.Nazwa}: {p.SprzedanoSzt} szt., {p.Przychod:C}");

        Console.WriteLine("\nWg kategorii:");
        foreach (var k in wgKat)
            Console.WriteLine($"  {k.Kategoria}: {k.Przychod:C}");

        return ogolne;
    }
}

// Demonstracja
var sklep = new DapperSklep(connStr);

// Szukaj produktów
var produktyIT = await sklep.PobierzProduktyAsync(kategoria: "IT", minCena: 200m);
Console.WriteLine($"IT powyżej 200zł: {produktyIT.Count} produktów");

// Złóż zamówienie
int zamId = await sklep.ZlozZamowienieAsync(
    klientId: 1,
    pozycje: new[] { (ProduktId: 1, Ilosc: 1), (ProduktId: 2, Ilosc: 3) });

// Raport
await sklep.PobierzRaportAsync(
    DateTime.Now.AddDays(-30),
    DateTime.Now);
```

---

### Typowe pytania rekrutacyjne

**"Czym Dapper różni się od ADO.NET i EF Core?"** ADO.NET — najniższy poziom, pełna kontrola, dużo kodu mapowania. Dapper — cienka warstwa nad ADO.NET, eliminuje mapowanie zachowując pełną kontrolę SQL, wydajność zbliżona do ADO.NET. EF Core — pełny ORM, generuje SQL automatycznie, abstrakcja bazy danych, change tracking, migracje — największy narzut ale najmniej kodu. Wybór: ADO.NET gdy potrzebujesz maksymalnej wydajności i kontroli, Dapper gdy chcesz pisać SQL ale bez boilerplate mapowania, EF Core dla standardowego CRUD.

**"Jak Dapper mapuje wyniki na obiekty?"** Przez reflection i convention-over-configuration. Dapper dopasowuje nazwy kolumn do właściwości/pól obiektu (case-insensitive). Mapowanie jest cachowane — drugi raz ten sam typ mapowany bez reflection overhead. Możesz dostosować mapowanie przez `SqlMapper.SetTypeMap<T>()` lub `CustomPropertyTypeMap`. Dla `record` — Dapper używa konstruktora gdy właściwości są init-only.

**"Co to `splitOn` w multi-mapping?"** Dapper czyta wyniki jako jeden płaski wiersz. `splitOn` mówi gdzie kończy się jeden obiekt a zaczyna następny — domyślnie `"Id"`. Gdy kolumna o tej nazwie się pojawi w wynikach, Dapper zaczyna mapować nowy obiekt. W SQL kolejność kolumn musi odpowiadać kolejności typów w metodzie. Dla wielu splitOn: `splitOn: "Id,Id"` — każde `Id` oznacza nowy obiekt.

**"Kiedy Dapper a kiedy EF Core?"** Dapper gdy: piszesz złożone zapytania SQL z JOINami, agregacjami, window functions — EF Core miałby problem z wygenerowaniem optymalnego SQL. Gdy masz legacy bazę z procedurami składowanymi. Gdy wydajność jest krytyczna i każde ms się liczy. EF Core gdy: standardowy CRUD bez skomplikowanego SQL, chcesz migracje i code-first, zależy Ci na change tracking i lazy loading, zespół nie zna SQL dobrze.