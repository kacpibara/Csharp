namespace _05_Databases;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// ─── Encje (suffix ER) ───────────────────────────────────────────────────────

// 1:N — KlientER → ZamowienieER
public class KlientER
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Email    { get; set; } = "";
    public bool   Aktywny  { get; set; } = true;

    public ICollection<ZamowienieER> Zamowienia { get; set; } = new List<ZamowienieER>();
}

public class ZamowienieER
{
    public int      Id            { get; set; }
    public string   Status        { get; set; } = "Nowe";
    public decimal  SumaCalkowita { get; set; }
    public DateTime DataZlozenia  { get; set; } = DateTime.UtcNow;

    public int      KlientId { get; set; }
    public KlientER Klient   { get; set; } = null!;

    public ICollection<PozycjaERel> Pozycje { get; set; } = new List<PozycjaERel>();
}

public class PozycjaERel
{
    public int      Id           { get; set; }
    public int      Ilosc        { get; set; }
    public decimal  CenaWChwili  { get; set; }

    public int          ZamowienieId { get; set; }
    public ZamowienieER Zamowienie   { get; set; } = null!;

    public int        ProduktId { get; set; }
    public ProduktER  Produkt   { get; set; } = null!;
}

// N:N prosty — ProduktER ↔ TagER (automatyczna tabela łącząca)
public class ProduktER
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public int     StanMagazynu { get; set; }
    public bool    Aktywny      { get; set; } = true;

    public int        KategoriaId { get; set; }
    public KategoriaER Kategoria  { get; set; } = null!;

    public ICollection<TagER>      Tagi   { get; set; } = new List<TagER>();
    public ICollection<PozycjaERel> Pozycje { get; set; } = new List<PozycjaERel>();
}

public class KategoriaER
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<ProduktER> Produkty { get; set; } = new List<ProduktER>();
}

public class TagER
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<ProduktER> Produkty { get; set; } = new List<ProduktER>();
}

// N:N z jawną klasą łączącą — KursER ↔ StudentER via KursStudentER
public class KursER
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<KursStudentER> KursStudenci { get; set; } = new List<KursStudentER>();
}

public class StudentER
{
    public int    Id   { get; set; }
    public string Imie { get; set; } = "";

    public ICollection<KursStudentER> KursStudenci { get; set; } = new List<KursStudentER>();
}

// Tabela łącząca z dodatkowymi danymi (klucz złożony!)
public class KursStudentER
{
    public int      KursId    { get; set; }
    public int      StudentId { get; set; }
    public DateTime DataZapis { get; set; } = DateTime.UtcNow;
    public decimal? Ocena     { get; set; }
    public bool     Ukonczony { get; set; } = false;

    public KursER    Kurs    { get; set; } = null!;
    public StudentER Student { get; set; } = null!;
}

// 1:1 — UzytkownikER → ProfilUzytkownikaER
public class UzytkownikER
{
    public int    Id    { get; set; }
    public string Login { get; set; } = "";
    public string Email { get; set; } = "";

    public ProfilUzytkownikaER? Profil { get; set; }
}

public class ProfilUzytkownikaER
{
    public int      Id            { get; set; }
    public string?  Bio           { get; set; }
    public string?  AvatarUrl     { get; set; }
    public DateTime DataUrodzenia { get; set; }

    public int          UzytkownikId { get; set; }
    public UzytkownikER Uzytkownik   { get; set; } = null!;
}

// Owned Entity — AdresER przechowywany w tabeli FirmaER
public class AdresER
{
    public string Ulica  { get; set; } = "";
    public string Miasto { get; set; } = "";
    public string Kod    { get; set; } = "";
    public string Kraj   { get; set; } = "PL";
}

public class FirmaER
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public AdresER Adres { get; set; } = new();
}

// ─── DbContext ────────────────────────────────────────────────────────────────

public class RelacjeContext : DbContext
{
    public DbSet<KlientER>           Klienci      { get; set; }
    public DbSet<ZamowienieER>       Zamowienia   { get; set; }
    public DbSet<PozycjaERel>        Pozycje      { get; set; }
    public DbSet<ProduktER>          Produkty     { get; set; }
    public DbSet<KategoriaER>        Kategorie    { get; set; }
    public DbSet<TagER>              Tagi         { get; set; }
    public DbSet<KursER>             Kursy        { get; set; }
    public DbSet<StudentER>          Studenci     { get; set; }
    public DbSet<KursStudentER>      KursStudenci { get; set; }
    public DbSet<UzytkownikER>       Uzytkownicy  { get; set; }
    public DbSet<ProfilUzytkownikaER> Profile     { get; set; }
    public DbSet<FirmaER>            Firmy        { get; set; }

    public RelacjeContext(DbContextOptions<RelacjeContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // 1:N — Klient → Zamówienia (Cascade delete)
        mb.Entity<ZamowienieER>()
          .HasOne(z => z.Klient)
          .WithMany(k => k.Zamowienia)
          .HasForeignKey(z => z.KlientId)
          .OnDelete(DeleteBehavior.Cascade);

        // 1:N — Zamówienie → Pozycje
        mb.Entity<PozycjaERel>()
          .HasOne(p => p.Zamowienie)
          .WithMany(z => z.Pozycje)
          .HasForeignKey(p => p.ZamowienieId);

        mb.Entity<PozycjaERel>()
          .HasOne(p => p.Produkt)
          .WithMany(pr => pr.Pozycje)
          .HasForeignKey(p => p.ProduktId);

        // 1:N — Kategoria → Produkty (Restrict — nie usuwaj kategorii z produktami)
        mb.Entity<ProduktER>()
          .HasOne(p => p.Kategoria)
          .WithMany(k => k.Produkty)
          .HasForeignKey(p => p.KategoriaId)
          .OnDelete(DeleteBehavior.Restrict);

        // N:N prosty — EF Core 5+ automatyczna tabela łącząca ProduktTag
        mb.Entity<ProduktER>()
          .HasMany(p => p.Tagi)
          .WithMany(t => t.Produkty)
          .UsingEntity(j => j.ToTable("ProduktTag"));

        // N:N z jawną klasą łączącą — klucz złożony
        mb.Entity<KursStudentER>(e =>
        {
            e.HasKey(ks => new { ks.KursId, ks.StudentId }); // klucz złożony!
            e.HasOne(ks => ks.Kurs)
             .WithMany(k => k.KursStudenci)
             .HasForeignKey(ks => ks.KursId);
            e.HasOne(ks => ks.Student)
             .WithMany(s => s.KursStudenci)
             .HasForeignKey(ks => ks.StudentId);
        });

        // 1:1 — Uzytkownik → ProfilUzytkownika
        mb.Entity<UzytkownikER>()
          .HasOne(u => u.Profil)
          .WithOne(p => p.Uzytkownik)
          .HasForeignKey<ProfilUzytkownikaER>(p => p.UzytkownikId)
          .OnDelete(DeleteBehavior.Cascade);

        // Owned Entity — AdresER przechowywany w tabeli Firmy
        // Tabela Firmy: Id | Nazwa | Adres_Ulica | Adres_Miasto | Adres_Kod | Adres_Kraj
        mb.Entity<FirmaER>()
          .OwnsOne(f => f.Adres, a =>
          {
              a.Property(x => x.Ulica).HasColumnName("Adres_Ulica").HasMaxLength(200);
              a.Property(x => x.Miasto).HasColumnName("Adres_Miasto").HasMaxLength(100);
              a.Property(x => x.Kod).HasColumnName("Adres_Kod").HasMaxLength(10);
              a.Property(x => x.Kraj).HasColumnName("Adres_Kraj").HasMaxLength(5);
          });
    }
}

// ─── Klasa Demo ───────────────────────────────────────────────────────────────

public static class EFRelacje
{
    private static readonly SqliteConnection _conn =
        new SqliteConnection("Data Source=:memory:");
    private static bool _init;

    private static RelacjeContext NowContext()
    {
        if (!_init)
        {
            _conn.Open();
            using var seed = new RelacjeContext(
                new DbContextOptionsBuilder<RelacjeContext>()
                    .UseSqlite(_conn).Options);
            seed.Database.EnsureCreated();
            // Seed data
            seed.Kategorie.AddRange(
                new KategoriaER { Id = 1, Nazwa = "Elektronika" },
                new KategoriaER { Id = 2, Nazwa = "Odzież" });
            seed.Produkty.AddRange(
                new ProduktER { Id = 1, Nazwa = "Laptop",    Cena = 3499m, StanMagazynu = 10, KategoriaId = 1 },
                new ProduktER { Id = 2, Nazwa = "Słuchawki", Cena =  299m, StanMagazynu = 25, KategoriaId = 1 },
                new ProduktER { Id = 3, Nazwa = "Kurtka",    Cena =  199m, StanMagazynu = 15, KategoriaId = 2 });
            seed.Tagi.AddRange(
                new TagER { Id = 1, Nazwa = "Premium" },
                new TagER { Id = 2, Nazwa = "Nowy" },
                new TagER { Id = 3, Nazwa = "Bestseller" });
            seed.Klienci.AddRange(
                new KlientER { Id = 1, Imie = "Anna", Nazwisko = "Kowalska", Email = "anna@er.pl" },
                new KlientER { Id = 2, Imie = "Jan",  Nazwisko = "Nowak",    Email = "jan@er.pl" });
            seed.Kursy.AddRange(
                new KursER { Id = 1, Nazwa = "C# Advanced" },
                new KursER { Id = 2, Nazwa = "EF Core Mastery" });
            seed.Studenci.AddRange(
                new StudentER { Id = 1, Imie = "Piotr" },
                new StudentER { Id = 2, Imie = "Maria" },
                new StudentER { Id = 3, Imie = "Karol" });
            seed.SaveChanges();
            _init = true;
        }
        return new RelacjeContext(
            new DbContextOptionsBuilder<RelacjeContext>().UseSqlite(_conn).Options);
    }

    // 1. Relacja 1:N — Klient → Zamówienia (Cascade)
    public static async Task Demo1doNAsync()
    {
        Console.WriteLine("\n--- EF Relacje: 1:N (Klient → Zamówienia) ---");
        await using var ctx = NowContext();

        // Tworzenie zamówień przez navigation property
        var klient = await ctx.Klienci.FindAsync(1);
        klient!.Zamowienia.Add(new ZamowienieER
        {
            Status = "Nowe", SumaCalkowita = 3499m, DataZlozenia = DateTime.UtcNow,
            Pozycje = { new PozycjaERel { ProduktId = 1, Ilosc = 1, CenaWChwili = 3499m } }
        });
        klient.Zamowienia.Add(new ZamowienieER
        {
            Status = "Dostarczone", SumaCalkowita = 299m, DataZlozenia = DateTime.UtcNow,
            Pozycje = { new PozycjaERel { ProduktId = 2, Ilosc = 1, CenaWChwili = 299m } }
        });
        await ctx.SaveChangesAsync();

        // Zapytanie 1:N — Include + ThenInclude
        var klientZZam = await ctx.Klienci
            .Include(k => k.Zamowienia)
                .ThenInclude(z => z.Pozycje)
            .AsNoTracking()
            .FirstAsync(k => k.Id == 1);

        Console.WriteLine($"Klient: {klientZZam.Imie} {klientZZam.Nazwisko}");
        foreach (var z in klientZZam.Zamowienia)
        {
            Console.WriteLine($"  Zam#{z.Id} [{z.Status}] {z.SumaCalkowita:C} | {z.Pozycje.Count} poz.");
        }

        // Konfiguracja Fluent API:
        // HasOne(z => z.Klient).WithMany(k => k.Zamowienia).HasForeignKey(z => z.KlientId)
        // .OnDelete(DeleteBehavior.Cascade) — usunięcie Klienta usuwa jego Zamówienia
        // Inne opcje: Restrict (błąd), SetNull (FK = null), NoAction

        // Filtrowanie Include — EF Core 5+
        var klienciFiltred = await ctx.Klienci
            .Include(k => k.Zamowienia
                .Where(z => z.Status == "Nowe")
                .OrderByDescending(z => z.DataZlozenia))
            .AsNoTracking()
            .ToListAsync();
        Console.WriteLine($"Include z filtrem (tylko Nowe): {klienciFiltred.Sum(k => k.Zamowienia.Count)} zamówień");
    }

    // 2. Relacja N:N — ProduktER ↔ TagER (automatyczna tabela łącząca)
    public static async Task DemoNdoNProstyAsync()
    {
        Console.WriteLine("\n--- EF Relacje: N:N prosty (Produkt ↔ Tag) ---");
        await using var ctx = NowContext();

        // Dodaj tagi do produktu przez navigation property
        var laptop = await ctx.Produkty
            .Include(p => p.Tagi)
            .FirstAsync(p => p.Id == 1);

        var tagPremium    = await ctx.Tagi.FindAsync(1);
        var tagBestseller = await ctx.Tagi.FindAsync(3);

        if (!laptop.Tagi.Any(t => t.Id == 1)) laptop.Tagi.Add(tagPremium!);
        if (!laptop.Tagi.Any(t => t.Id == 3)) laptop.Tagi.Add(tagBestseller!);
        await ctx.SaveChangesAsync();
        // EF Core automatycznie zarządza tabelą ProduktTag!

        // Odczyt N:N
        var produktyZTagami = await ctx.Produkty
            .Include(p => p.Tagi)
            .Where(p => p.Aktywny)
            .AsNoTracking()
            .ToListAsync();

        Console.WriteLine("Produkty z tagami (N:N auto join table):");
        foreach (var p in produktyZTagami)
            Console.WriteLine($"  {p.Nazwa,-12}: [{string.Join(", ", p.Tagi.Select(t => t.Nazwa))}]");

        // Usunięcie tagu z produktu
        var laptopZTag = await ctx.Produkty.Include(p => p.Tagi).FirstAsync(p => p.Id == 1);
        var tagDo = laptopZTag.Tagi.FirstOrDefault(t => t.Id == 3);
        if (tagDo != null)
        {
            laptopZTag.Tagi.Remove(tagDo); // EF usuwa wiersz z ProduktTag
            await ctx.SaveChangesAsync();
        }
        Console.WriteLine($"Po usunięciu Bestseller: {(await ctx.Produkty.Include(p=>p.Tagi).AsNoTracking().FirstAsync(p=>p.Id==1)).Tagi.Count} tagów");
    }

    // 3. Relacja N:N z jawną klasą łączącą (KursER ↔ StudentER via KursStudentER)
    public static async Task DemoNdoNJawnaKlasaAsync()
    {
        Console.WriteLine("\n--- EF Relacje: N:N jawna klasa łącząca (Kurs ↔ Student) ---");
        await using var ctx = NowContext();

        // Zapisz studentów na kursy — przez encję łączącą z dodatkowymi danymi
        ctx.KursStudenci.AddRange(
            new KursStudentER { KursId = 1, StudentId = 1, DataZapis = DateTime.UtcNow },
            new KursStudentER { KursId = 1, StudentId = 2, DataZapis = DateTime.UtcNow },
            new KursStudentER { KursId = 2, StudentId = 1, DataZapis = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        Console.WriteLine("Zapisano studentów na kursy");

        // Oceń studenta — update encji łączącej z kluczem złożonym
        var wpis = await ctx.KursStudenci
            .FirstOrDefaultAsync(ks => ks.KursId == 1 && ks.StudentId == 1);
        if (wpis != null)
        {
            wpis.Ocena     = 5.0m;
            wpis.Ukonczony = true;
            await ctx.SaveChangesAsync();
        }

        // Odczyt z nawigacją przez klasę łączącą
        var kursy = await ctx.Kursy
            .Include(k => k.KursStudenci)
                .ThenInclude(ks => ks.Student)
            .AsNoTracking()
            .ToListAsync();

        Console.WriteLine("Kursy ze studentami (N:N jawna klasa + klucz złożony):");
        foreach (var k in kursy)
        {
            Console.WriteLine($"  Kurs: {k.Nazwa}");
            foreach (var ks in k.KursStudenci)
                Console.WriteLine($"    {ks.Student.Imie} | zapis={ks.DataZapis:d} " +
                                 $"| ocena={ks.Ocena?.ToString("F1") ?? "brak"} | ukończony={ks.Ukonczony}");
        }
    }

    // 4. Relacja 1:1 + Owned Entity
    public static async Task Demo1do1Async()
    {
        Console.WriteLine("\n--- EF Relacje: 1:1 + Owned Entity ---");
        await using var ctx = NowContext();

        // 1:1 — Uzytkownik → Profil
        var uzytkownik = new UzytkownikER
        {
            Login = "admin",
            Email = "admin@app.pl",
            Profil = new ProfilUzytkownikaER
            {
                Bio          = "Administrator systemu",
                AvatarUrl    = "https://avatar.url/admin.png",
                DataUrodzenia = new DateTime(1990, 5, 15)
            }
        };
        ctx.Uzytkownicy.Add(uzytkownik);
        await ctx.SaveChangesAsync();

        var pobrany = await ctx.Uzytkownicy
            .Include(u => u.Profil)
            .AsNoTracking()
            .FirstAsync(u => u.Login == "admin");
        Console.WriteLine($"1:1 Uzytkownik: {pobrany.Login} | Bio: {pobrany.Profil?.Bio}");

        // HasOne.WithOne: klucz obcy po stronie "zależnej" (ProfilUzytkownikaER)
        // Profil bez Uzytkownika nie istnieje (cascade delete)

        // Owned Entity — Firma + Adres w jednej tabeli
        var firma = new FirmaER
        {
            Nazwa = "Tech S.A.",
            Adres = new AdresER { Ulica = "Al. Jerozolimskie 99", Miasto = "Warszawa", Kod = "00-001" }
        };
        ctx.Firmy.Add(firma);
        await ctx.SaveChangesAsync();

        // Adres przechowywany w kolumnach Adres_Ulica, Adres_Miasto, Adres_Kod, Adres_Kraj w tabeli Firmy
        var pobrana = await ctx.Firmy.AsNoTracking().FirstAsync(f => f.Nazwa == "Tech S.A.");
        Console.WriteLine($"Owned Entity Firma: {pobrana.Nazwa} | " +
                         $"{pobrana.Adres.Ulica}, {pobrana.Adres.Miasto} ({pobrana.Adres.Kraj})");
        Console.WriteLine("Owned: brak osobnej tabeli — Adres_Ulica, Adres_Miasto... w tabeli Firmy");
    }

    // 5. Eager Loading — Include, ThenInclude, AsSplitQuery, karteziański produkt
    public static async Task DemoEagerLoadingAsync()
    {
        Console.WriteLine("\n--- EF Relacje: Eager Loading ---");
        await using var ctx = NowContext();

        // Upewnij się że są dane z 1:N demo
        if (!await ctx.Zamowienia.AnyAsync())
        {
            ctx.Zamowienia.Add(new ZamowienieER
            {
                KlientId = 1, Status = "Nowe", SumaCalkowita = 3499m, DataZlozenia = DateTime.UtcNow,
                Pozycje = { new PozycjaERel { ProduktId = 1, Ilosc = 1, CenaWChwili = 3499m } }
            });
            await ctx.SaveChangesAsync();
        }

        // Include — jeden poziom (generuje JOIN w SQL)
        var zamZKlient = await ctx.Zamowienia
            .Include(z => z.Klient)
            .AsNoTracking()
            .ToListAsync();
        Console.WriteLine($"Include(Klient): {zamZKlient.Count} zamówień z danymi klienta");

        // ThenInclude — głębsze poziomy
        var zamKomplet = await ctx.Zamowienia
            .Include(z => z.Klient)
            .Include(z => z.Pozycje)
                .ThenInclude(p => p.Produkt)
                    .ThenInclude(pr => pr.Kategoria)
            .AsNoTracking()
            .ToListAsync();

        foreach (var z in zamKomplet)
        {
            Console.WriteLine($"  Zam#{z.Id} — {z.Klient.Imie} {z.Klient.Nazwisko}:");
            foreach (var p in z.Pozycje)
                Console.WriteLine($"    {p.Produkt.Nazwa} ({p.Produkt.Kategoria.Nazwa}) " +
                                 $"| {p.Ilosc}× {p.CenaWChwili:C}");
        }

        // AsSplitQuery — zamiast jednego JOINa z karteziańskim produktem
        // Bez AsSplitQuery: 10 zamówień × 50 pozycji = 500 wierszy SQL (duplikaty!)
        // Z AsSplitQuery: osobne SELECT dla każdego Include — mniej danych, więcej round-tripów
        var zamSplit = await ctx.Zamowienia
            .Include(z => z.Klient)
            .Include(z => z.Pozycje)
                .ThenInclude(p => p.Produkt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
        Console.WriteLine($"AsSplitQuery: {zamSplit.Count} zamówień (osobne SELECT dla każdego Include)");

        // Projekcja (Select) — NAJWYDAJNIEJSZE podejście
        // Pobiera tylko potrzebne kolumny, EF tłumaczy SELECT + JOIN
        var dtos = await ctx.Zamowienia
            .AsNoTracking()
            .Select(z => new
            {
                z.Id, z.Status, z.SumaCalkowita,
                KlientNazwa  = z.Klient.Imie + " " + z.Klient.Nazwisko,
                LiczbaPozycji = z.Pozycje.Count
            })
            .ToListAsync();
        Console.WriteLine($"Select projekcja: {dtos.Count} dto-zamówień bez nadmiarowych danych");
    }

    // 6. Explicit Loading — Collection().LoadAsync(), Reference().LoadAsync()
    public static async Task DemoExplicitLoadingAsync()
    {
        Console.WriteLine("\n--- EF Relacje: Explicit Loading ---");
        await using var ctx = NowContext();

        // FindAsync — pobiera BEZ navigation properties (State = Unchanged)
        var klient = await ctx.Klienci.FindAsync(1);
        Console.WriteLine($"Po FindAsync — Zamowienia załadowane: {ctx.Entry(klient!).Collection(k => k.Zamowienia).IsLoaded}");

        // Collection().LoadAsync() — załaduj kolekcję na żądanie
        await ctx.Entry(klient!)
            .Collection(k => k.Zamowienia)
            .LoadAsync();
        Console.WriteLine($"Po LoadAsync — Zamowienia: {klient!.Zamowienia.Count}");

        // Explicit Loading z filtrowaniem — QueryAsync
        var nowePyt = ctx.Entry(klient)
            .Collection(k => k.Zamowienia)
            .Query()
            .Where(z => z.Status == "Nowe");
        var noweZam = await nowePyt.ToListAsync();
        Console.WriteLine($"Explicit + filtr (Status=Nowe): {noweZam.Count} zamówień");

        // Reference().LoadAsync() — załaduj referencję (strona "wiele")
        if (await ctx.Zamowienia.AnyAsync())
        {
            var zamowienie = await ctx.Zamowienia.FirstAsync();
            await ctx.Entry(zamowienie)
                .Reference(z => z.Klient)
                .LoadAsync();
            Console.WriteLine($"Reference LoadAsync: Zam#{zamowienie.Id} → klient={zamowienie.Klient.Imie}");
        }

        // Lazy Loading (bez implementacji — wymaga Microsoft.EntityFrameworkCore.Proxies)
        // Konfiguracja: opt.UseLazyLoadingProxies()
        // Encje: navigation properties MUSZĄ być virtual!
        // Zagrożenie: N+1 Queries — dla N klientów N dodatkowych zapytań o zamówienia
        Console.WriteLine("Lazy Loading: wymaga virtual nav props + UseLazyLoadingProxies() + osobny pakiet");
        Console.WriteLine("Ryzyko N+1: 100 klientów = 1 SELECT klientów + 100 SELECT zamówień = 101 zapytań!");
    }

    // 7. Porównanie strategii ładowania + N+1 detekcja
    public static async Task DemoStrategieAsync()
    {
        Console.WriteLine("\n--- EF Relacje: Strategie ładowania ---");
        await using var ctx = NowContext();

        // Symulacja N+1 — ANTYWZORZEC
        int selectCount = 0;
        // Każde wywołanie LazyLoad (gdyby było włączone) dodałoby SELECT:
        // var klienci = await ctx.Klienci.ToListAsync();   // 1 SELECT
        // foreach (var k in klienci)
        //     _ = k.Zamowienia.Count;                      // N SELECTs!
        // = N+1 problem

        // DOBRE PRAKTYKI:
        // 1. Eager Loading (Include) — dla API endpoints, jeden round-trip
        var eagarKlienci = await ctx.Klienci
            .Include(k => k.Zamowienia)
            .AsNoTracking()
            .ToListAsync();
        selectCount = 1; // jeden SQL z JOIN
        Console.WriteLine($"Eager (Include): {selectCount} SQL, {eagarKlienci.Count} klientów załadowanych");

        // 2. Projekcja (Select) — NAJWYDAJNIEJSZE, tylko potrzebne kolumny
        var projekcja = await ctx.Klienci
            .AsNoTracking()
            .Select(k => new
            {
                k.Id,
                PelneNazwisko  = k.Imie + " " + k.Nazwisko,
                LiczbaZamowien = k.Zamowienia.Count,
                SumaZakupow    = k.Zamowienia.Sum(z => (double)z.SumaCalkowita)
            })
            .ToListAsync();
        Console.WriteLine($"Projekcja (Select): COUNT/SUM bez ładowania kolekcji do pamięci");
        foreach (var k in projekcja)
            Console.WriteLine($"  {k.PelneNazwisko}: {k.LiczbaZamowien} zam., łącznie {k.SumaZakupow:C}");

        // 3. AsSplitQuery — gdy Include kilku kolekcji na tym samym poziomie
        // Karteziański produkt: 10 zamówień × 20 pozycji × 5 tagów = 1000 wierszy!
        // AsSplitQuery(): 3 oddzielne SELECTy zamiast jednego ogromnego JOINa
        Console.WriteLine("\nKiedy AsSplitQuery():");
        Console.WriteLine("  Include kilku kolekcji → kartez. produkt → AsSplitQuery()");
        Console.WriteLine("  WYKRYWAJ: SQL z dużą liczbą wierszy z powielonymi danymi");

        // Porównanie:
        // ┌──────────────┬────────────────────────┬──────────────────────────┐
        // │ Strategia    │ Zalety                 │ Wady                     │
        // ├──────────────┼────────────────────────┼──────────────────────────┤
        // │ Eager Include│ 1 round-trip, prosty   │ ładuje za dużo danych    │
        // │ Select       │ minimalne dane, szybki │ więcej kodu              │
        // │ Lazy Loading │ prosty kod             │ N+1, wymaga virtual+pkg  │
        // │ Explicit     │ ładuj kiedy potrzeba   │ ręczna kontrola          │
        // └──────────────┴────────────────────────┴──────────────────────────┘
        Console.WriteLine("ZALECENIE: Select dla GET-endpoint | Include gdy znasz potrzebne dane");
    }
}
