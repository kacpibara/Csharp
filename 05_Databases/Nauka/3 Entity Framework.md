### Entity Framework Core od podstaw

EF Core to **ORM (Object-Relational Mapper)** — tłumaczy obiekty C# na tabele SQL i odwrotnie. Zamiast pisać SQL ręcznie, operujesz na zwykłych klasach C#.

---

### 1. Setup i pierwsze modele

csharp

```csharp
// dotnet add package Microsoft.EntityFrameworkCore.SqlServer
// dotnet add package Microsoft.EntityFrameworkCore.Tools
// dotnet add package Microsoft.EntityFrameworkCore.Design

using Microsoft.EntityFrameworkCore;

// --- MODELE (Entity Classes) ---
// Zwykłe klasy C# — EF Core mapuje je na tabele

public class Klient
{
    public int    Id         { get; set; }        // PK — konwencja: Id lub KlientId
    public string Imie       { get; set; } = "";
    public string Nazwisko   { get; set; } = "";
    public string Email      { get; set; } = "";
    public DateTime DataRej  { get; set; } = DateTime.UtcNow;
    public bool   Aktywny    { get; set; } = true;

    // Navigation property — relacja 1:N
    public ICollection<Zamowienie> Zamowienia { get; set; } = new List<Zamowienie>();
}

public class Kategoria
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<Produkt> Produkty { get; set; } = new List<Produkt>();
}

public class Produkt
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public int     StanMagazynu { get; set; }
    public bool    Aktywny      { get; set; } = true;
    public DateTime DataDodania { get; set; } = DateTime.UtcNow;

    // Foreign Key + Navigation property
    public int       KategoriaId { get; set; }
    public Kategoria Kategoria   { get; set; } = null!;

    // Relacja N:N z Zamowienie przez tabelę łączącą
    public ICollection<PozycjaZamowienia> Pozycje { get; set; } = new List<PozycjaZamowienia>();
}

public class Zamowienie
{
    public int      Id           { get; set; }
    public DateTime DataZlozenia { get; set; } = DateTime.UtcNow;
    public string   Status       { get; set; } = "Nowe";
    public decimal  SumaCalkowita{ get; set; }

    public int    KlientId { get; set; }
    public Klient Klient   { get; set; } = null!;

    public ICollection<PozycjaZamowienia> Pozycje { get; set; } = new List<PozycjaZamowienia>();
}

public class PozycjaZamowienia  // tabela łącząca Zamowienie ↔ Produkt
{
    public int Id              { get; set; }
    public int ZamowienieId    { get; set; }
    public int ProduktId       { get; set; }
    public int     Ilosc       { get; set; }
    public decimal CenaW_Chwili{ get; set; }  // snapshot ceny w momencie zakupu

    public Zamowienie Zamowienie { get; set; } = null!;
    public Produkt    Produkt    { get; set; } = null!;
}
```

---

### 2. DbContext — serce EF Core

csharp

```csharp
// DbContext = brama do bazy danych + unit of work
public class SklepDbContext : DbContext
{
    // DbSet<T> = tabela w bazie
    public DbSet<Klient>           Klienci           { get; set; }
    public DbSet<Kategoria>        Kategorie          { get; set; }
    public DbSet<Produkt>          Produkty           { get; set; }
    public DbSet<Zamowienie>       Zamowienia         { get; set; }
    public DbSet<PozycjaZamowienia>PozycjeZamowien    { get; set; }

    // Konstruktor dla DI (Dependency Injection)
    public SklepDbContext(DbContextOptions<SklepDbContext> options)
        : base(options) { }

    // Konfiguracja przez Fluent API — ZALECANE nad atrybutami
    protected override void OnModelCreating(ModelBuilder mb)
    {
        // --- KLIENT ---
        mb.Entity<Klient>(e =>
        {
            e.ToTable("Klienci");               // nazwa tabeli
            e.HasKey(k => k.Id);                // klucz główny

            e.Property(k => k.Imie)
                .IsRequired()
                .HasMaxLength(100);

            e.Property(k => k.Nazwisko)
                .IsRequired()
                .HasMaxLength(150);

            e.Property(k => k.Email)
                .IsRequired()
                .HasMaxLength(200);

            e.HasIndex(k => k.Email)
                .IsUnique();                    // unikalny indeks

            e.Property(k => k.DataRej)
                .HasDefaultValueSql("GETUTCDATE()");  // wartość domyślna SQL
        });

        // --- PRODUKT ---
        mb.Entity<Produkt>(e =>
        {
            e.ToTable("Produkty");

            e.Property(p => p.Nazwa)
                .IsRequired()
                .HasMaxLength(200);

            e.Property(p => p.Cena)
                .HasPrecision(18, 2);           // precyzja decimal

            e.Property(p => p.DataDodania)
                .HasDefaultValueSql("GETUTCDATE()");

            // Relacja N:1 z Kategoria
            e.HasOne(p => p.Kategoria)
                .WithMany(k => k.Produkty)
                .HasForeignKey(p => p.KategoriaId)
                .OnDelete(DeleteBehavior.Restrict);  // nie usuwaj kaskadowo
        });

        // --- ZAMOWIENIE ---
        mb.Entity<Zamowienie>(e =>
        {
            e.Property(z => z.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Nowe");

            e.Property(z => z.SumaCalkowita)
                .HasPrecision(18, 2);

            e.HasOne(z => z.Klient)
                .WithMany(k => k.Zamowienia)
                .HasForeignKey(z => z.KlientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- POZYCJA ZAMOWIENIA ---
        mb.Entity<PozycjaZamowienia>(e =>
        {
            e.Property(p => p.CenaW_Chwili).HasPrecision(18, 2);

            e.HasOne(p => p.Zamowienie)
                .WithMany(z => z.Pozycje)
                .HasForeignKey(p => p.ZamowienieId);

            e.HasOne(p => p.Produkt)
                .WithMany(p => p.Pozycje)
                .HasForeignKey(p => p.ProduktId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- DANE STARTOWE (seed) ---
        mb.Entity<Kategoria>().HasData(
            new Kategoria { Id = 1, Nazwa = "IT" },
            new Kategoria { Id = 2, Nazwa = "Meble" },
            new Kategoria { Id = 3, Nazwa = "Dom" });
    }
}
```

---

### 3. Rejestracja w DI i konfiguracja

csharp

```csharp
// Program.cs — ASP.NET Core
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja DbContext
builder.Services.AddDbContext<SklepDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Sklep"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(60);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        })
    .EnableSensitiveDataLogging()    // SQL z wartościami (tylko dev!)
    .LogTo(Console.WriteLine,        // loguj SQL do konsoli
        Microsoft.Extensions.Logging.LogLevel.Information));

// appsettings.json
// {
//   "ConnectionStrings": {
//     "Sklep": "Server=localhost;Database=Sklep;Integrated Security=True;TrustServerCertificate=True"
//   }
// }

// Dla testów / console app — bez DI
var options = new DbContextOptionsBuilder<SklepDbContext>()
    .UseSqlServer("Server=...;Database=Sklep;...")
    .Options;

using var ctx = new SklepDbContext(options);
```

---

### 4. Migracje — Code First

csharp

```csharp
// Migracje = wersjonowanie schematu bazy danych
// EF Core śledzi zmiany w modelach i generuje SQL do aktualizacji bazy

// --- KOMENDY CLI ---

// 1. Dodaj pierwszą migrację
// dotnet ef migrations add PoczatkowaStruktura

// Generuje pliki:
// Migrations/20240315_PoczatkowaStruktura.cs
// Migrations/20240315_PoczatkowaStruktura.Designer.cs
// Migrations/SklepDbContextModelSnapshot.cs

// 2. Zastosuj migracje (utwórz/zaktualizuj bazę)
// dotnet ef database update

// 3. Dodaj kolejną migrację po zmianie modeli
// dotnet ef migrations add DodajKolumneOpis

// 4. Cofnij do konkretnej migracji
// dotnet ef database update PoczatkowaStruktura

// 5. Usuń ostatnią migrację (jeszcze nie zastosowaną)
// dotnet ef migrations remove

// 6. Wygeneruj SQL bez stosowania
// dotnet ef migrations script

// Jak wygląda wygenerowana migracja:
public partial class PoczatkowaStruktura : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Klienci",
            columns: table => new
            {
                Id       = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Imie     = table.Column<string>(maxLength: 100, nullable: false),
                Nazwisko = table.Column<string>(maxLength: 150, nullable: false),
                Email    = table.Column<string>(maxLength: 200, nullable: false),
                DataRej  = table.Column<DateTime>(nullable: false,
                    defaultValueSql: "GETUTCDATE()"),
                Aktywny  = table.Column<bool>(nullable: false, defaultValue: true)
            },
            constraints: t => t.PrimaryKey("PK_Klienci", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Klienci_Email",
            table: "Klienci",
            column: "Email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Klienci");
    }
}

// Stosowanie migracji z kodu (np. przy starcie aplikacji)
public static async Task ZastosujMigracjeAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<SklepDbContext>();

    // Sprawdź czy są oczekujące migracje
    var pending = await ctx.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        Console.WriteLine($"Stosuję {pending.Count()} migrację/migracje...");
        await ctx.Database.MigrateAsync();
        Console.WriteLine("Migracje zastosowane");
    }
}
```

---

### 5. CRUD — Create, Read, Update, Delete

csharp

```csharp
// Zakładamy że mamy DbContext przez DI
public class ProduktSerwis
{
    private readonly SklepDbContext _ctx;

    public ProduktSerwis(SklepDbContext ctx) => _ctx = ctx;

    // ==================== CREATE ====================

    public async Task<Produkt> DodajProduktAsync(
        string nazwa, decimal cena, int kategoriaId, int stan = 0)
    {
        var produkt = new Produkt
        {
            Nazwa        = nazwa,
            Cena         = cena,
            KategoriaId  = kategoriaId,
            StanMagazynu = stan
        };

        _ctx.Produkty.Add(produkt);       // śledzenie jako Added
        await _ctx.SaveChangesAsync();     // INSERT SQL wykonany tutaj

        Console.WriteLine($"Dodano produkt #{produkt.Id}: {produkt.Nazwa}");
        return produkt;
    }

    // Dodaj wiele naraz — batch
    public async Task<int> DodajWieleProduktowAsync(
        IEnumerable<Produkt> produkty)
    {
        await _ctx.Produkty.AddRangeAsync(produkty);
        return await _ctx.SaveChangesAsync();
        // EF Core generuje jeden batch INSERT (zależnie od providera)
    }

    // ==================== READ ====================

    // Wszystkie — uwaga na tracking!
    public async Task<List<Produkt>> PobierzWszystkieAsync()
    {
        return await _ctx.Produkty
            .AsNoTracking()            // bez śledzenia — szybsze dla odczytu!
            .OrderBy(p => p.Nazwa)
            .ToListAsync();
    }

    // Po ID
    public async Task<Produkt?> PobierzPoIdAsync(int id)
    {
        // FindAsync — najpierw szuka w cache, potem baza
        return await _ctx.Produkty.FindAsync(id);
    }

    // Z filtrowaniem
    public async Task<List<Produkt>> SzukajAsync(
        string? kategoria = null,
        decimal? minCena  = null,
        decimal? maxCena  = null,
        bool tylkoAktywne = true)
    {
        var query = _ctx.Produkty
            .Include(p => p.Kategoria)   // eager loading — dołącz Kategoria
            .AsNoTracking()
            .AsQueryable();

        if (tylkoAktywne)
            query = query.Where(p => p.Aktywny);

        if (kategoria != null)
            query = query.Where(p => p.Kategoria.Nazwa == kategoria);

        if (minCena.HasValue)
            query = query.Where(p => p.Cena >= minCena.Value);

        if (maxCena.HasValue)
            query = query.Where(p => p.Cena <= maxCena.Value);

        return await query
            .OrderBy(p => p.Kategoria.Nazwa)
            .ThenBy(p => p.Cena)
            .ToListAsync();
    }

    // Projekcja — pobierz tylko potrzebne pola
    public async Task<List<ProduktDto>> PobierzDtoAsync()
    {
        return await _ctx.Produkty
            .AsNoTracking()
            .Where(p => p.Aktywny)
            .Select(p => new ProduktDto(    // EF tłumaczy na SELECT z konkretnymi kolumnami
                p.Id,
                p.Nazwa,
                p.Cena,
                p.Kategoria.Nazwa,          // JOIN automatyczny
                p.StanMagazynu))
            .ToListAsync();
    }

    // ==================== UPDATE ====================

    // Podejście 1 — Track and Save
    public async Task<bool> AktualizujCeneAsync(int id, decimal nowaCena)
    {
        var produkt = await _ctx.Produkty.FindAsync(id);
        if (produkt == null) return false;

        produkt.Cena = nowaCena;           // zmiana śledzona przez Change Tracker

        await _ctx.SaveChangesAsync();     // UPDATE tylko zmienionych kolumn
        return true;
    }

    // Podejście 2 — Attach and Mark Modified (bez pobierania z bazy)
    public async Task AktualizujBezPobieraniaAsync(Produkt produkt)
    {
        _ctx.Produkty.Attach(produkt);             // podłącz jako Unchanged
        _ctx.Entry(produkt).State = EntityState.Modified;  // oznacz jako Modified

        await _ctx.SaveChangesAsync();             // UPDATE wszystkich kolumn!
    }

    // Podejście 3 — Update konkretnych właściwości
    public async Task<bool> AktualizujNazweICeneAsync(int id, string nazwa, decimal cena)
    {
        var produkt = await _ctx.Produkty.FindAsync(id);
        if (produkt == null) return false;

        _ctx.Entry(produkt).Property(p => p.Nazwa).IsModified = true;
        _ctx.Entry(produkt).Property(p => p.Cena).IsModified  = true;

        produkt.Nazwa = nazwa;
        produkt.Cena  = cena;

        await _ctx.SaveChangesAsync();  // UPDATE tylko Nazwa i Cena!
        return true;
    }

    // ExecuteUpdateAsync — bez pobierania (EF Core 7+) — najwydajniejszy!
    public async Task<int> PodwyzkaCenKategoriiAsync(string kategoria, decimal procent)
    {
        return await _ctx.Produkty
            .Where(p => p.Kategoria.Nazwa == kategoria && p.Aktywny)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Cena, p => p.Cena * (1 + procent / 100)));
        // Generuje: UPDATE Produkty SET Cena = Cena * 1.1 WHERE ...
        // Bez pobierania danych do C#!
    }

    // ==================== DELETE ====================

    // Podejście 1 — Find and Remove
    public async Task<bool> UsunAsync(int id)
    {
        var produkt = await _ctx.Produkty.FindAsync(id);
        if (produkt == null) return false;

        _ctx.Produkty.Remove(produkt);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // Podejście 2 — Soft delete (logiczne usunięcie)
    public async Task<bool> SoftUsunAsync(int id)
    {
        var produkt = await _ctx.Produkty.FindAsync(id);
        if (produkt == null) return false;

        produkt.Aktywny = false;
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ExecuteDeleteAsync — bez pobierania (EF Core 7+)
    public async Task<int> UsunNiedostepneAsync()
    {
        return await _ctx.Produkty
            .Where(p => !p.Aktywny && p.StanMagazynu == 0)
            .ExecuteDeleteAsync();
        // Generuje: DELETE FROM Produkty WHERE Aktywny = 0 AND StanMagazynu = 0
    }
}

public record ProduktDto(int Id, string Nazwa, decimal Cena,
    string Kategoria, int Stan);
```

---

### 6. SaveChanges i Change Tracker

csharp

```csharp
// Change Tracker — EF Core śledzi wszystkie pobrane obiekty
// Przy SaveChanges generuje minimalne SQL (tylko to co się zmieniło)

await using var ctx = new SklepDbContext(options);

var produkt = await ctx.Produkty.FindAsync(1);

// Sprawdź stan encji
var wpis = ctx.Entry(produkt!);
Console.WriteLine(wpis.State);  // Unchanged

produkt!.Cena = 9999m;
Console.WriteLine(ctx.Entry(produkt).State);  // Modified

// Oryginalna wartość
var oryginalnaValue = ctx.Entry(produkt)
    .Property(p => p.Cena)
    .OriginalValue;  // np. 3500m

// Aktualna wartość
var aktualnaValue = ctx.Entry(produkt)
    .Property(p => p.Cena)
    .CurrentValue;   // 9999m

// SaveChanges — wyślij wszystkie zmiany do bazy
int zmienionych = await ctx.SaveChangesAsync();
Console.WriteLine($"Zmieniono {zmienionych} rekord(ów)");

// SaveChanges z wyjątkiem — całe Unit of Work rollback
var nowy = new Produkt { Nazwa = "Test", Cena = 100m, KategoriaId = 1 };
ctx.Produkty.Add(nowy);

var istniejacy = await ctx.Produkty.FindAsync(2);
istniejacy!.Cena = 200m;

// Oba zapytania w jednej transakcji!
await ctx.SaveChangesAsync();

// Odrzuć zmiany bez zapisu
ctx.ChangeTracker.Clear();          // wyczyść wszystkie śledzone obiekty
// lub dla konkretnego:
ctx.Entry(produkt).Reload();        // odświeżz z bazy, porzuć lokalne zmiany
```

---

### 7. Transakcje w EF Core

csharp

```csharp
await using var ctx = new SklepDbContext(options);

// Transakcja jawna — gdy SaveChanges nie wystarczy
await using var trx = await ctx.Database.BeginTransactionAsync();

try
{
    // Operacja 1 — dodaj zamówienie
    var zamowienie = new Zamowienie
    {
        KlientId     = 1,
        Status       = "Nowe",
        SumaCalkowita = 0m
    };
    ctx.Zamowienia.Add(zamowienie);
    await ctx.SaveChangesAsync();  // insert, mamy Id

    // Operacja 2 — dodaj pozycje
    decimal suma = 0;
    var pozycje = new List<PozycjaZamowienia>
    {
        new() { ZamowienieId = zamowienie.Id, ProduktId = 1, Ilosc = 2, CenaW_Chwili = 3500m },
        new() { ZamowienieId = zamowienie.Id, ProduktId = 2, Ilosc = 1, CenaW_Chwili = 150m  }
    };
    ctx.PozycjeZamowien.AddRange(pozycje);
    suma = pozycje.Sum(p => p.Ilosc * p.CenaW_Chwili);

    // Operacja 3 — zaktualizuj sumę
    zamowienie.SumaCalkowita = suma;

    // Operacja 4 — zmniejsz stany
    await ctx.Produkty
        .Where(p => p.Id == 1)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.StanMagazynu,
            p => p.StanMagazynu - 2));

    await ctx.SaveChangesAsync();
    await trx.CommitAsync();

    Console.WriteLine($"Zamówienie #{zamowienie.Id}: {suma:C}");
}
catch (Exception ex)
{
    await trx.RollbackAsync();
    Console.WriteLine($"Rollback: {ex.Message}");
    throw;
}

// Wykonanie surowego SQL
var wynik = await ctx.Database.ExecuteSqlRawAsync(
    "UPDATE Produkty SET StanMagazynu = 0 WHERE DataDodania < {0}",
    DateTime.Now.AddYears(-5));

// Lub z interpolowanym SQL (bezpieczne — parametryzowane)
var dataGraniczna = DateTime.Now.AddYears(-5);
await ctx.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE Produkty SET StanMagazynu = 0 WHERE DataDodania < {dataGraniczna}");
```

---

### 8. Praktyczny przykład — pełny serwis

csharp

```csharp
// Kompletny serwis zamówień

public class ZamowieniaSerwis
{
    private readonly SklepDbContext _ctx;

    public ZamowieniaSerwis(SklepDbContext ctx) => _ctx = ctx;

    public async Task<ZamowienieDto> ZlozZamowienieAsync(
        int klientId,
        IEnumerable<(int ProduktId, int Ilosc)> pozycje,
        CancellationToken ct = default)
    {
        // Sprawdź czy klient istnieje
        var klient = await _ctx.Klienci
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == klientId && k.Aktywny, ct)
            ?? throw new InvalidOperationException($"Klient #{klientId} nieaktywny");

        var prodIds = pozycje.Select(p => p.ProduktId).ToArray();

        // Pobierz produkty z blokadą
        var produkty = await _ctx.Produkty
            .Where(p => prodIds.Contains(p.Id) && p.Aktywny)
            .ToListAsync(ct);

        // Walidacja
        foreach (var (prodId, ilosc) in pozycje)
        {
            var prod = produkty.FirstOrDefault(p => p.Id == prodId)
                ?? throw new InvalidOperationException($"Produkt #{prodId} niedostępny");

            if (prod.StanMagazynu < ilosc)
                throw new InvalidOperationException(
                    $"'{prod.Nazwa}': stan {prod.StanMagazynu}, potrzeba {ilosc}");
        }

        // Utwórz zamówienie w transakcji
        await using var trx = await _ctx.Database.BeginTransactionAsync(ct);
        try
        {
            var zamowienie = new Zamowienie
            {
                KlientId = klientId,
                Status   = "Potwierdzone"
            };
            _ctx.Zamowienia.Add(zamowienie);
            await _ctx.SaveChangesAsync(ct);

            decimal suma = 0;
            foreach (var (prodId, ilosc) in pozycje)
            {
                var prod = produkty.First(p => p.Id == prodId);
                decimal cena = prod.Cena;

                _ctx.PozycjeZamowien.Add(new PozycjaZamowienia
                {
                    ZamowienieId  = zamowienie.Id,
                    ProduktId     = prodId,
                    Ilosc         = ilosc,
                    CenaW_Chwili  = cena
                });

                prod.StanMagazynu -= ilosc;   // zmiana śledzona
                suma += cena * ilosc;
            }

            zamowienie.SumaCalkowita = suma;
            await _ctx.SaveChangesAsync(ct);
            await trx.CommitAsync(ct);

            return new ZamowienieDto(
                zamowienie.Id,
                $"{klient.Imie} {klient.Nazwisko}",
                zamowienie.DataZlozenia,
                suma,
                zamowienie.Status,
                pozycje.Count());
        }
        catch
        {
            await trx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<RaportSprzedazy> PobierzRaportAsync(
        DateTime od, DateTime do_, CancellationToken ct = default)
    {
        var statystyki = await _ctx.Zamowienia
            .AsNoTracking()
            .Where(z => z.DataZlozenia >= od && z.DataZlozenia <= do_)
            .GroupBy(z => z.Status)
            .Select(g => new { Status = g.Key, Liczba = g.Count(), Suma = g.Sum(z => z.SumaCalkowita) })
            .ToListAsync(ct);

        var topProd = await _ctx.PozycjeZamowien
            .AsNoTracking()
            .Include(p => p.Produkt)
            .Include(p => p.Zamowienie)
            .Where(p => p.Zamowienie.DataZlozenia >= od && p.Zamowienie.DataZlozenia <= do_)
            .GroupBy(p => new { p.ProduktId, p.Produkt.Nazwa })
            .Select(g => new
            {
                g.Key.Nazwa,
                Sprzedano = g.Sum(p => p.Ilosc),
                Przychod  = g.Sum(p => p.Ilosc * p.CenaW_Chwili)
            })
            .OrderByDescending(p => p.Przychod)
            .Take(5)
            .ToListAsync(ct);

        return new RaportSprzedazy(
            Okres:          $"{od:dd.MM} — {do_:dd.MM.yyyy}",
            LacznaSprzedaz: statystyki.Sum(s => s.Suma),
            LiczbaZamowien: statystyki.Sum(s => s.Liczba),
            WgStatusu:      statystyki.ToDictionary(s => s.Status, s => s.Suma),
            TopProdukty:    topProd.Select(p =>
                $"{p.Nazwa}: {p.Sprzedano} szt., {p.Przychod:C}").ToList());
    }
}

public record ZamowienieDto(int Id, string Klient, DateTime Data,
    decimal Suma, string Status, int LiczbaPozycji);

public record RaportSprzedazy(string Okres, decimal LacznaSprzedaz,
    int LiczbaZamowien, Dictionary<string, decimal> WgStatusu,
    List<string> TopProdukty);

// Demonstracja
var serwis = new ZamowieniaSerwis(ctx);

var zam = await serwis.ZlozZamowienieAsync(
    klientId: 1,
    new[] { (ProduktId: 1, Ilosc: 2), (ProduktId: 2, Ilosc: 1) });

Console.WriteLine($"Złożono: #{zam.Id} dla {zam.Klient} — {zam.Suma:C}");

var raport = await serwis.PobierzRaportAsync(
    DateTime.Now.AddMonths(-1), DateTime.Now);

Console.WriteLine($"\nRaport: {raport.Okres}");
Console.WriteLine($"Sprzedaż: {raport.LacznaSprzedaz:C} ({raport.LiczbaZamowien} zam.)");
raport.TopProdukty.ForEach(p => Console.WriteLine($"  • {p}"));
```

---

### Typowe pytania rekrutacyjne

**"Co to DbContext i jak działa Unit of Work?"** `DbContext` to implementacja wzorca Unit of Work i Repository w jednym. Śledzi wszystkie zmiany w obiektach (Change Tracker). Przy `SaveChanges()` otwiera transakcję, generuje minimalne SQL dla wszystkich zmian i zatwierdza. Jedna operacja bazodanowa zamiast wielu. Powinien być krótkotrwały — jeden request w ASP.NET Core = jeden DbContext.

**"Jaka różnica między `Find` a `FirstOrDefault`?"** `Find(id)` — najpierw szuka w cache Change Trackera, potem idzie do bazy. Zawsze pobiera wszystkie kolumny. Nie działa z `AsNoTracking()`. `FirstOrDefault(x => x.Id == id)` — zawsze idzie do bazy, możesz użyć z `AsNoTracking()`, możesz dodać `Include()` i projekcje. W serwisach do odczytu — `FirstOrDefault` z `AsNoTracking()`. Do edycji — `Find` (lub `FirstOrDefault` z trackingiem).

**"Kiedy `AsNoTracking()`?"** Dla zapytań tylko do odczytu — gdy nie zamierzasz modyfikować i zapisywać pobranych obiektów. EF Core nie musi tworzyć snapshot oryginalnych wartości ani śledzić zmian. Szybsze o 20-40% i zużywa mniej pamięci. Używaj dla GET endpointów API, raportów, list do wyświetlenia. NIE używaj gdy planujesz SaveChanges na tych obiektach.

**"Co to `ExecuteUpdateAsync` i `ExecuteDeleteAsync` i kiedy używać?"** EF Core 7+ — operacje masowe bez pobierania danych do C#. `ExecuteUpdateAsync` generuje `UPDATE ... WHERE ...` bezpośrednio w SQL. `ExecuteDeleteAsync` generuje `DELETE ... WHERE ...`. Nie używają Change Tracker — brak śledzenia. Idealne dla operacji na wielu rekordach: podwyżka cen kategorii, usunięcie starych rekordów. NIE używaj gdy potrzebujesz logiki C# przy każdym rekordzie lub gdy encje mają cascade logic.