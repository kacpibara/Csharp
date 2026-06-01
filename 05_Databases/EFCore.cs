namespace _05_Databases;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

// ─── Encje (suffix EF) ───────────────────────────────────────────────────────

public class KategoriaEF
{
    public int    Id      { get; set; }
    public string Nazwa   { get; set; } = "";
    public string Opis    { get; set; } = "";
    public bool   Aktywna { get; set; } = true;

    public ICollection<ProduktEF> Produkty { get; set; } = new List<ProduktEF>();
}

public class ProduktEF
{
    public int      Id           { get; set; }
    public string   Nazwa        { get; set; } = "";
    public decimal  Cena         { get; set; }
    public int      StanMagazynu { get; set; }
    public bool     Aktywny      { get; set; } = true;
    public DateTime DataDodania  { get; set; }

    public int         KategoriaId { get; set; }
    public KategoriaEF Kategoria   { get; set; } = null!;

    public ICollection<PozycjaZamowieniaEF> Pozycje { get; set; } = new List<PozycjaZamowieniaEF>();
}

public class KlientEF
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Email    { get; set; } = "";
    public bool   Aktywny  { get; set; } = true;

    public ICollection<ZamowienieEF> Zamowienia { get; set; } = new List<ZamowienieEF>();
}

public class ZamowienieEF
{
    public int      Id            { get; set; }
    public string   Status        { get; set; } = "Nowe";
    public decimal  SumaCalkowita { get; set; }
    public DateTime DataZlozenia  { get; set; }

    public int      KlientId { get; set; }
    public KlientEF Klient   { get; set; } = null!;

    public ICollection<PozycjaZamowieniaEF> Pozycje { get; set; } = new List<PozycjaZamowieniaEF>();
}

public class PozycjaZamowieniaEF
{
    public int     Id          { get; set; }
    public int     Ilosc       { get; set; }
    public decimal CenaWChwili { get; set; }

    public int          ZamowienieId { get; set; }
    public ZamowienieEF Zamowienie   { get; set; } = null!;

    public int       ProduktId { get; set; }
    public ProduktEF Produkt   { get; set; } = null!;
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record ProduktListDtoEF(int Id, string Nazwa, decimal Cena, string KategoriaNazwa);

// ─── DbContext ────────────────────────────────────────────────────────────────

public class SklepEFContext : DbContext
{
    public DbSet<KategoriaEF>        Kategorie      { get; set; }
    public DbSet<ProduktEF>          Produkty       { get; set; }
    public DbSet<KlientEF>           Klienci        { get; set; }
    public DbSet<ZamowienieEF>       Zamowienia     { get; set; }
    public DbSet<PozycjaZamowieniaEF> PozycjeZam    { get; set; }

    public SklepEFContext(DbContextOptions<SklepEFContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── KategoriaEF ──
        mb.Entity<KategoriaEF>(e =>
        {
            e.ToTable("Kategorie");
            e.HasKey(k => k.Id);
            e.Property(k => k.Nazwa).HasMaxLength(100).IsRequired();
            e.Property(k => k.Opis).HasMaxLength(500).HasDefaultValue("");
            e.HasData(
                new KategoriaEF { Id = 1, Nazwa = "Elektronika", Opis = "Sprzęt elektroniczny" },
                new KategoriaEF { Id = 2, Nazwa = "Odzież",      Opis = "Ubrania i akcesoria" },
                new KategoriaEF { Id = 3, Nazwa = "Narzędzia",   Opis = "Narzędzia i majsterkowanie" });
        });

        // ── ProduktEF ──
        mb.Entity<ProduktEF>(e =>
        {
            e.ToTable("Produkty");
            e.HasKey(p => p.Id);
            e.Property(p => p.Nazwa).HasMaxLength(200).IsRequired();
            // SQLite: decimal przechowywany jako TEXT (dokładność) lub REAL (szybkość)
            e.Property(p => p.Cena).IsRequired();
            e.Property(p => p.StanMagazynu).HasDefaultValue(0);
            e.Property(p => p.Aktywny).HasDefaultValue(true);
            // SQLite datetime: datetime('now') zamiast SQL Server getutcdate()
            e.Property(p => p.DataDodania).HasDefaultValueSql("datetime('now')");
            e.HasIndex(p => p.Nazwa);
            e.HasOne(p => p.Kategoria)
             .WithMany(k => k.Produkty)
             .HasForeignKey(p => p.KategoriaId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasData(
                new ProduktEF { Id = 1, Nazwa = "Laptop",    Cena = 3499.99m, StanMagazynu = 10, KategoriaId = 1, DataDodania = DateTime.UtcNow },
                new ProduktEF { Id = 2, Nazwa = "Słuchawki", Cena =  299.99m, StanMagazynu = 25, KategoriaId = 1, DataDodania = DateTime.UtcNow },
                new ProduktEF { Id = 3, Nazwa = "Kurtka",    Cena =  199.99m, StanMagazynu = 15, KategoriaId = 2, DataDodania = DateTime.UtcNow },
                new ProduktEF { Id = 4, Nazwa = "Wiertarka", Cena =  599.99m, StanMagazynu =  8, KategoriaId = 3, DataDodania = DateTime.UtcNow });
        });

        // ── KlientEF ──
        mb.Entity<KlientEF>(e =>
        {
            e.ToTable("Klienci");
            e.HasKey(k => k.Id);
            e.Property(k => k.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(k => k.Email).IsUnique();
            e.HasData(
                new KlientEF { Id = 1, Imie = "Anna", Nazwisko = "Kowalska", Email = "anna@email.pl" },
                new KlientEF { Id = 2, Imie = "Jan",  Nazwisko = "Nowak",    Email = "jan@email.pl"  });
        });

        // ── ZamowienieEF ──
        mb.Entity<ZamowienieEF>(e =>
        {
            e.ToTable("Zamowienia");
            e.HasKey(z => z.Id);
            e.Property(z => z.Status).HasMaxLength(50).HasDefaultValue("Nowe");
            e.Property(z => z.DataZlozenia).HasDefaultValueSql("datetime('now')");
            e.HasOne(z => z.Klient)
             .WithMany(k => k.Zamowienia)
             .HasForeignKey(z => z.KlientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PozycjaZamowieniaEF ──
        mb.Entity<PozycjaZamowieniaEF>(e =>
        {
            e.ToTable("PozycjeZamowien");
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Zamowienie)
             .WithMany(z => z.Pozycje)
             .HasForeignKey(p => p.ZamowienieId);
            e.HasOne(p => p.Produkt)
             .WithMany(pr => pr.Pozycje)
             .HasForeignKey(p => p.ProduktId);
        });
    }
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class EFCore
{
    // Statyczne połączenie — in-memory baza żyje razem z nim
    private static readonly SqliteConnection _conn =
        new SqliteConnection("Data Source=:memory:");

    private static bool _init;

    internal static DbContextOptions<SklepEFContext> GetOptions()
    {
        if (!_init)
        {
            _conn.Open();
            // EnsureCreated — tworzy schemat bazy z modelu; NIE tworzy tabeli migracji
            using var ctx = new SklepEFContext(
                new DbContextOptionsBuilder<SklepEFContext>()
                    .UseSqlite(_conn)
                    .Options);
            ctx.Database.EnsureCreated();
            _init = true;
        }
        return new DbContextOptionsBuilder<SklepEFContext>()
            .UseSqlite(_conn)
            .Options;
    }

    // 1. DbContext — tworzenie, EnsureCreated vs Migrate, DI
    public static async Task DemoDbContextAsync()
    {
        Console.WriteLine("\n--- EF Core: DbContext ---");
        var options = GetOptions();

        await using var ctx = new SklepEFContext(options);

        // Informacje o bazie
        Console.WriteLine($"Provider: {ctx.Database.ProviderName}");
        Console.WriteLine($"CanConnect: {await ctx.Database.CanConnectAsync()}");

        // Liczba encji z każdego DbSet
        long katCount  = await ctx.Kategorie.CountAsync();
        long prodCount = await ctx.Produkty.CountAsync();
        long klCount   = await ctx.Klienci.CountAsync();
        Console.WriteLine($"Seed data: {katCount} kategorii, {prodCount} produktów, {klCount} klientów");

        // DI rejestracja (dla Web API):
        // builder.Services.AddDbContext<SklepEFContext>(opt =>
        //     opt.UseSqlServer(connStr,
        //         b => b.CommandTimeout(30).EnableRetryOnFailure(3)));
        // Zakres: Scoped (jeden kontekst na request HTTP)
        Console.WriteLine("DI: AddDbContext<T> → Scoped (jeden kontekst na request)");

        // EnsureCreated vs Migrate:
        // EnsureCreated() — szybkie tworzenie schematu, brak tabeli __EFMigrationsHistory
        //                   Idealne dla testów i in-memory. Nie aktualizuje istniejących baz.
        // Migrate()       — stosuje oczekujące migracje z tabeli __EFMigrationsHistory
        //                   Używaj w produkcji. Wspiera inkrementalne zmiany schematu.
        Console.WriteLine("EnsureCreated: dla testów/in-memory | Migrate: dla produkcji");
    }

    // 2. CRUD — Add, FindAsync, FirstOrDefaultAsync, AsNoTracking, Select (projekcja)
    public static async Task DemoCRUDAsync()
    {
        Console.WriteLine("\n--- EF Core: CRUD ---");
        await using var ctx = new SklepEFContext(GetOptions());

        // CREATE — Add + SaveChangesAsync
        var nowyProdukt = new ProduktEF
        {
            Nazwa = "Monitor 4K", Cena = 1499.99m, StanMagazynu = 7, KategoriaId = 1,
            DataDodania = DateTime.UtcNow
        };
        ctx.Produkty.Add(nowyProdukt);
        await ctx.SaveChangesAsync();
        Console.WriteLine($"Add: Monitor 4K ID={nowyProdukt.Id}");

        // AddRangeAsync — wiele encji naraz
        await ctx.Klienci.AddRangeAsync(new[]
        {
            new KlientEF { Imie = "Piotr", Nazwisko = "Wiśniewski", Email = "pw@email.pl" }
        });
        await ctx.SaveChangesAsync();

        // READ — FindAsync (szuka w Change Trackerze, potem w DB; O(1) po PK)
        var znaleziony = await ctx.Produkty.FindAsync(1);
        Console.WriteLine($"FindAsync(1): {znaleziony?.Nazwa}");

        // FirstOrDefaultAsync — LINQ Where (tłumaczone na SQL WHERE)
        var laptopLub = await ctx.Produkty
            .FirstOrDefaultAsync(p => p.Nazwa.StartsWith("Laptop"));
        Console.WriteLine($"FirstOrDefault Laptop: {laptopLub?.Cena:C}");

        // AsNoTracking — bez śledzenia zmian (szybsze dla read-only)
        // SQLite nie obsługuje ORDER BY decimal → rzutujemy na double po stronie klienta
        var readOnly = await ctx.Produkty
            .AsNoTracking()
            .Where(p => p.Aktywny && p.Cena < 500m)
            .ToListAsync();
        readOnly = readOnly.OrderBy(p => p.Cena).ToList();
        Console.WriteLine($"AsNoTracking (<500 PLN): {readOnly.Count} produktów");

        // Select projekcja — EF tłumaczy na SQL SELECT tylko potrzebnych kolumn
        var dtos = await ctx.Produkty
            .AsNoTracking()
            .Where(p => p.Aktywny)
            .OrderBy(p => p.Nazwa)
            .Select(p => new ProduktListDtoEF(p.Id, p.Nazwa, p.Cena, p.Kategoria.Nazwa))
            .ToListAsync();
        Console.WriteLine($"Projekcja do DTO: {dtos.Count} rekordów");
        foreach (var d in dtos.Take(3))
            Console.WriteLine($"  {d.Nazwa,-14} {d.Cena,8:C} | {d.KategoriaNazwa}");

        // Include — eager loading (JOIN w SQL)
        var zKategoria = await ctx.Produkty
            .Include(p => p.Kategoria)
            .AsNoTracking()
            .FirstAsync(p => p.Id == 1);
        Console.WriteLine($"Include: {zKategoria.Nazwa} → {zKategoria.Kategoria.Nazwa}");

        // Sprzątanie
        ctx.Produkty.Remove(nowyProdukt);
        await ctx.SaveChangesAsync();
    }

    // 3. Aktualizacja — 3 podejścia + ExecuteUpdateAsync (EF7+)
    public static async Task DemoAktualizacjaAsync()
    {
        Console.WriteLine("\n--- EF Core: Aktualizacja ---");

        // Podejście 1: śledzona encja — Tracked Entity Modification
        await using (var ctx = new SklepEFContext(GetOptions()))
        {
            var produkt = await ctx.Produkty.FindAsync(1);
            produkt!.Cena         = 3299.99m; // zmiana właściwości
            produkt.StanMagazynu += 5;
            // EF Core wykrywa zmiany przez Change Tracker → generuje UPDATE tylko zmienionych kolumn
            int rows = await ctx.SaveChangesAsync();
            Console.WriteLine($"Podejście 1 (tracked): {rows} wierszy, Cena={produkt.Cena:C}");
        }

        // Podejście 2: Attach — dla odłączonego obiektu (detached)
        await using (var ctx = new SklepEFContext(GetOptions()))
        {
            var odlaczony = new ProduktEF { Id = 2, Nazwa = "Słuchawki Pro",
                Cena = 399.99m, StanMagazynu = 20, KategoriaId = 1, DataDodania = DateTime.UtcNow };
            // Attach + EntityState.Modified → UPDATE wszystkich kolumn
            ctx.Produkty.Attach(odlaczony);
            ctx.Entry(odlaczony).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
            Console.WriteLine($"Podejście 2 (Attach+Modified): {odlaczony.Nazwa}");
        }

        // Podejście 3 (EF7+): ExecuteUpdateAsync — bulk, BEZ ładowania encji do pamięci
        await using (var ctx = new SklepEFContext(GetOptions()))
        {
            int zaktualizowanych = await ctx.Produkty
                .Where(p => p.KategoriaId == 1 && p.Aktywny)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StanMagazynu, p => p.StanMagazynu + 2));
            Console.WriteLine($"Podejście 3 (ExecuteUpdateAsync bulk): {zaktualizowanych} produktów +2 na stanie");
        }
    }

    // 4. Usuwanie — Remove + ExecuteDeleteAsync (EF7+)
    public static async Task DemoUsuwanieAsync()
    {
        Console.WriteLine("\n--- EF Core: Usuwanie ---");

        // Podejście 1: Remove — śledzona encja
        await using (var ctx = new SklepEFContext(GetOptions()))
        {
            var tmp = new ProduktEF
            {
                Nazwa = "TmpDoUsuniecia", Cena = 1m, KategoriaId = 1, DataDodania = DateTime.UtcNow
            };
            ctx.Produkty.Add(tmp);
            await ctx.SaveChangesAsync();
            int tmpId = tmp.Id;

            ctx.Produkty.Remove(tmp);   // lub ctx.Remove(tmp)
            int deleted = await ctx.SaveChangesAsync();
            Console.WriteLine($"Remove(tmp): {deleted} usuniętych, ID={tmpId}");
        }

        // Podejście 2 (EF7+): ExecuteDeleteAsync — bulk, BEZ ładowania encji
        await using (var ctx = new SklepEFContext(GetOptions()))
        {
            // Dodaj kilka nieaktywnych produktów do usunięcia
            ctx.Produkty.AddRange(
                new ProduktEF { Nazwa = "Old1", Cena = 1m, Aktywny = false, KategoriaId = 1, DataDodania = DateTime.UtcNow },
                new ProduktEF { Nazwa = "Old2", Cena = 1m, Aktywny = false, KategoriaId = 1, DataDodania = DateTime.UtcNow });
            await ctx.SaveChangesAsync();

            int usunietych = await ctx.Produkty
                .Where(p => !p.Aktywny)
                .ExecuteDeleteAsync();
            Console.WriteLine($"ExecuteDeleteAsync (nieaktywne): {usunietych} usuniętych");
        }
    }

    // 5. Change Tracker — Entry().State, OriginalValues, CurrentValues, Clear, Reload
    public static async Task DemoChangeTrackerAsync()
    {
        Console.WriteLine("\n--- EF Core: Change Tracker ---");
        await using var ctx = new SklepEFContext(GetOptions());

        // Pobierz encję — State = Unchanged
        var produkt = await ctx.Produkty.FindAsync(1);
        PropertyEntry<ProduktEF, decimal> cenaProp = ctx.Entry(produkt!).Property(p => p.Cena);

        Console.WriteLine($"Po FindAsync — State: {ctx.Entry(produkt!).State}"); // Unchanged

        decimal originalCena = cenaProp.OriginalValue;
        produkt!.Cena = 9999m;

        // Po zmianie — State automatycznie zmienia się na Modified
        Console.WriteLine($"Po zmianie Ceny — State: {ctx.Entry(produkt).State}"); // Modified
        Console.WriteLine($"OriginalValue.Cena: {originalCena:C}");
        Console.WriteLine($"CurrentValue.Cena:  {cenaProp.CurrentValue:C}");

        // ChangeTracker.Entries() — wszystkie śledzone encje
        int modifiedCount = ctx.ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Modified);
        Console.WriteLine($"Modified entries: {modifiedCount}");

        // ChangeTracker.Clear() — usuń wszystkie śledzone encje (bez SaveChanges)
        ctx.ChangeTracker.Clear();
        Console.WriteLine($"Po Clear — Entries: {ctx.ChangeTracker.Entries().Count()}");

        // Reload — odśwież z bazy (resetuje zmiany)
        var produkt2 = await ctx.Produkty.FindAsync(1);
        produkt2!.Cena = 12345m;
        await ctx.Entry(produkt2).ReloadAsync();   // pobiera aktualną wartość z DB
        Console.WriteLine($"Po ReloadAsync — Cena: {produkt2.Cena:C} (State: {ctx.Entry(produkt2).State})");

        // Jawna zmiana State (Deleted bez wywołania Remove)
        ctx.Entry(produkt2).State = EntityState.Unchanged;
        Console.WriteLine($"Po Unchanged ─ State: {ctx.Entry(produkt2).State}");
    }

    // 6. Transakcja EF Core — Database.BeginTransactionAsync, ExecuteSqlRaw
    public static async Task DemoTransakcjaEFAsync()
    {
        Console.WriteLine("\n--- EF Core: Transakcja ---");
        await using var ctx = new SklepEFContext(GetOptions());

        // BeginTransactionAsync — jawna transakcja dla wielu SaveChanges
        await using var trx = await ctx.Database.BeginTransactionAsync();
        try
        {
            // Operacja 1 — EF Core CRUD
            var klient = new KlientEF
                { Imie = "Transakcja", Nazwisko = "Test", Email = "trx@test.pl" };
            ctx.Klienci.Add(klient);
            await ctx.SaveChangesAsync(); // SQL INSERT do transakcji

            // Operacja 2 — ExecuteSqlRawAsync — surowy SQL (NIE parametryzowany — ostrożnie!)
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE Klienci SET Nazwisko = 'TestOK' WHERE Id = {0}", klient.Id);

            // Operacja 3 — ExecuteSqlInterpolatedAsync — bezpieczna interpolacja (parametry)
            string nowyEmail = "trxok@test.pl";
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Klienci SET Email = {nowyEmail} WHERE Id = {klient.Id}");

            await trx.CommitAsync(); // zatwierdź wszystkie 3 operacje naraz

            await ctx.Entry(klient).ReloadAsync();
            Console.WriteLine($"Transakcja OK: {klient.Imie} {klient.Nazwisko}, email={klient.Email}");

            // Sprzątanie
            ctx.Klienci.Remove(klient);
            await ctx.SaveChangesAsync();
        }
        catch
        {
            await trx.RollbackAsync();
            Console.WriteLine("Transakcja ROLLED BACK");
            throw;
        }
    }

    // 7. Migracje — koncepcja i CLI commands
    public static void DemoMigracje()
    {
        Console.WriteLine("\n--- EF Core: Migracje ---");

        // Migracje = wersjonowany opis zmian schematu bazy
        // Każda migracja to klasa C# z metodami Up() i Down()

        // CLI commands (wymagają pakietu Microsoft.EntityFrameworkCore.Tools):
        Console.WriteLine("CLI commands:");
        Console.WriteLine("  dotnet ef migrations add NazwaMigracji");
        Console.WriteLine("  dotnet ef database update            -- zastosuj wszystkie");
        Console.WriteLine("  dotnet ef database update NazwaMig   -- do konkretnej");
        Console.WriteLine("  dotnet ef database update 0          -- cofnij wszystkie (Down)");
        Console.WriteLine("  dotnet ef migrations remove          -- usuń ostatnią (cofnij)");
        Console.WriteLine("  dotnet ef migrations script          -- wygeneruj SQL script");

        // EnsureCreated vs Migrate:
        // EnsureCreated():
        //   - Tworzy schemat jeśli baza nie istnieje
        //   - Nie tworzy tabeli __EFMigrationsHistory
        //   - Nie może aktualizować istniejącej bazy (zmiany schematu)
        //   - Idealne: testy, in-memory, prototypy
        // ctx.Database.EnsureCreated();

        // Migrate():
        //   - Stosuje migracje z tabeli __EFMigrationsHistory
        //   - Może być wywołane z kodu (startup)
        //   - Obsługuje inkrementalne zmiany schematu
        //   - Produkcja: zazwyczaj przez pipeline CI/CD lub IDbMigrationService
        // await ctx.Database.MigrateAsync();

        // Struktura pliku migracji:
        // public class PoczatkowaStruktura : Migration
        // {
        //     protected override void Up(MigrationBuilder mb)
        //     {
        //         mb.CreateTable("Produkty", t => new { Id = t.Column<int>(nullable: false)... });
        //         mb.CreateIndex("IX_Produkty_Nazwa", "Produkty", "Nazwa");
        //     }
        //     protected override void Down(MigrationBuilder mb)
        //     {
        //         mb.DropTable("Produkty");
        //     }
        // }
        Console.WriteLine("EnsureCreated: dev/test | Migrate: produkcja z historią zmian");
    }
}
