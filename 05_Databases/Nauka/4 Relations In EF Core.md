### Relacje w EF Core

---

### 1. One-to-Many — jeden do wielu

csharp

```csharp
// Najczęstsza relacja — np. jeden Klient ma wiele Zamówień

public class Klient
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Email    { get; set; } = "";

    // Navigation property — kolekcja po stronie "jeden"
    public ICollection<Zamowienie> Zamowienia { get; set; } = new List<Zamowienie>();
}

public class Zamowienie
{
    public int      Id     { get; set; }
    public decimal  Wartosc{ get; set; }
    public string   Status { get; set; } = "Nowe";
    public DateTime Data   { get; set; } = DateTime.UtcNow;

    // FK — klucz obcy po stronie "wiele"
    public int    KlientId { get; set; }
    // Navigation property — referencja po stronie "wiele"
    public Klient Klient   { get; set; } = null!;
}

// Konfiguracja Fluent API
modelBuilder.Entity<Zamowienie>(e =>
{
    e.HasOne(z => z.Klient)           // Zamowienie MA JEDNEGO Klienta
     .WithMany(k => k.Zamowienia)     // Klient MA WIELE Zamowień
     .HasForeignKey(z => z.KlientId)  // FK
     .OnDelete(DeleteBehavior.Cascade); // usunięcie Klienta = usunięcie Zamówień
});

// Alternatywna konfiguracja od strony kolekcji
modelBuilder.Entity<Klient>(e =>
{
    e.HasMany(k => k.Zamowienia)
     .WithOne(z => z.Klient)
     .HasForeignKey(z => z.KlientId)
     .IsRequired();                    // FK nie może być null
});

// DeleteBehavior opcje:
// Cascade      — usuń powiązane rekordy (default dla required FK)
// Restrict     — błąd gdy próba usunięcia z powiązanymi
// SetNull      — ustaw FK na null (FK musi być nullable)
// ClientSetNull— SetNull tylko po stronie klienta (EF tracked objects)
// NoAction     — brak akcji w bazie (musisz obsłużyć ręcznie)
```

---

### 2. Many-to-Many — wiele do wielu

csharp

```csharp
// EF Core 5+ — automatyczna tabela łącząca (bez jawnej klasy)
public class Produkt
{
    public int    Id      { get; set; }
    public string Nazwa   { get; set; } = "";
    public decimal Cena   { get; set; }

    // Navigation property N:N
    public ICollection<Tag> Tagi { get; set; } = new List<Tag>();
}

public class Tag
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<Produkt> Produkty { get; set; } = new List<Produkt>();
}

// Konfiguracja — EF Core sam tworzy tabelę ProduktTag!
modelBuilder.Entity<Produkt>()
    .HasMany(p => p.Tagi)
    .WithMany(t => t.Produkty)
    .UsingEntity(j => j.ToTable("ProduktTag")); // własna nazwa tabeli łączącej

// Wygeneruje: CREATE TABLE ProduktTag (ProduktyId INT, TagiId INT, PRIMARY KEY ...)

// N:N Z DANYMI na tabeli łączącej — jawna klasa łącząca
public class Kurs
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    public ICollection<KursStudent> KursStudenci { get; set; } = new();
}

public class Student
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";

    public ICollection<KursStudent> KursStudenci { get; set; } = new();
}

// Tabela łącząca z dodatkowymi danymi
public class KursStudent
{
    public int      KursId    { get; set; }
    public int      StudentId { get; set; }
    public DateTime DataZapis { get; set; } = DateTime.UtcNow;
    public decimal? Ocena     { get; set; }
    public bool     Ukonczony { get; set; } = false;

    public Kurs    Kurs    { get; set; } = null!;
    public Student Student { get; set; } = null!;
}

// Konfiguracja
modelBuilder.Entity<KursStudent>(e =>
{
    e.HasKey(ks => new { ks.KursId, ks.StudentId }); // klucz złożony!

    e.HasOne(ks => ks.Kurs)
     .WithMany(k => k.KursStudenci)
     .HasForeignKey(ks => ks.KursId);

    e.HasOne(ks => ks.Student)
     .WithMany(s => s.KursStudenci)
     .HasForeignKey(ks => ks.StudentId);
});

// Użycie N:N z jawną klasą
await using var ctx = new AppDbContext(options);

// Zapisz studenta na kurs
ctx.KursStudenci.Add(new KursStudent
{
    KursId    = 1,
    StudentId = 42,
    DataZapis = DateTime.UtcNow
});
await ctx.SaveChangesAsync();

// Oceń studenta
var wpis = await ctx.KursStudenci
    .FirstOrDefaultAsync(ks => ks.KursId == 1 && ks.StudentId == 42);
if (wpis != null)
{
    wpis.Ocena     = 5.0m;
    wpis.Ukonczony = true;
    await ctx.SaveChangesAsync();
}
```

---

### 3. One-to-One — jeden do jednego

csharp

```csharp
// Rzadsza relacja — np. Uzytkownik i Profil

public class Uzytkownik
{
    public int    Id    { get; set; }
    public string Login { get; set; } = "";
    public string Email { get; set; } = "";

    // Navigation property 1:1
    public ProfilUzytkownika? Profil { get; set; }
}

public class ProfilUzytkownika
{
    public int    Id          { get; set; }
    public string? Bio        { get; set; }
    public string? AvatarUrl  { get; set; }
    public DateTime DataUrodzenia { get; set; }
    public string? Telefon    { get; set; }

    // FK po stronie "zależnej"
    public int        UzytkownikId { get; set; }
    public Uzytkownik Uzytkownik   { get; set; } = null!;
}

// Konfiguracja
modelBuilder.Entity<Uzytkownik>(e =>
{
    e.HasOne(u => u.Profil)           // Uzytkownik MA JEDEN Profil
     .WithOne(p => p.Uzytkownik)      // Profil NALEŻY DO jednego Uzytkownika
     .HasForeignKey<ProfilUzytkownika>(p => p.UzytkownikId)
     .OnDelete(DeleteBehavior.Cascade);
});

// Owned Entity — alternatywa dla 1:1 gdy profil jest "częścią" encji głównej
// Dane przechowywane w tej samej tabeli!
public class Adres
{
    public string Ulica  { get; set; } = "";
    public string Miasto { get; set; } = "";
    public string Kod    { get; set; } = "";
    public string Kraj   { get; set; } = "PL";
}

public class Firma
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";

    // Owned type — przechowywany w tabeli Firma
    public Adres Adres { get; set; } = new();
}

// Konfiguracja Owned
modelBuilder.Entity<Firma>()
    .OwnsOne(f => f.Adres, a =>
    {
        a.Property(x => x.Ulica).HasColumnName("Adres_Ulica").HasMaxLength(200);
        a.Property(x => x.Miasto).HasColumnName("Adres_Miasto").HasMaxLength(100);
        a.Property(x => x.Kod).HasColumnName("Adres_Kod").HasMaxLength(10);
    });
// Tabela Firmy: Id | Nazwa | Adres_Ulica | Adres_Miasto | Adres_Kod | Adres_Kraj
```

---

### 4. Include i ThenInclude — Eager Loading

csharp

```csharp
// Eager Loading — ładuj powiązane dane razem z głównym zapytaniem
// Generuje JOIN w SQL

await using var ctx = new AppDbContext(options);

// Include — jeden poziom głębokości
var zamowieniaZKlientami = await ctx.Zamowienia
    .Include(z => z.Klient)           // JOIN z Klientami
    .ToListAsync();

// Dostęp do danych klienta — brak dodatkowych zapytań do bazy
foreach (var z in zamowieniaZKlientami)
    Console.WriteLine($"#{z.Id} | {z.Klient.Imie} | {z.Wartosc:C}");

// ThenInclude — głębsze poziomy
var zamowieniaZPelnaHierarchia = await ctx.Zamowienia
    .Include(z => z.Klient)               // JOIN Klient
    .Include(z => z.Pozycje)              // JOIN PozycjeZamowien
        .ThenInclude(p => p.Produkt)      // JOIN Produkty
        .ThenInclude(p => p.Kategoria)   // JOIN Kategorie (głębiej!)
    .Where(z => z.Status == "Nowe")
    .AsNoTracking()
    .ToListAsync();

foreach (var z in zamowieniaZPelnaHierarchia)
{
    Console.WriteLine($"\nZamówienie #{z.Id} — {z.Klient.Imie}:");
    foreach (var p in z.Pozycje)
        Console.WriteLine($"  {p.Produkt.Nazwa} ({p.Produkt.Kategoria.Nazwa}): {p.Ilosc} × {p.CenaW_Chwili:C}");
}

// Include z filtrowaniem — EF Core 5+
var klienciZAktywnymi = await ctx.Klienci
    .Include(k => k.Zamowienia
        .Where(z => z.Status != "Anulowane")    // filtruj powiązane!
        .OrderByDescending(z => z.Data))        // sortuj powiązane!
    .AsNoTracking()
    .ToListAsync();

// Include na kolekcji N:N
var produktyZTagami = await ctx.Produkty
    .Include(p => p.Tagi)
    .Where(p => p.Aktywny)
    .AsNoTracking()
    .ToListAsync();

// Wiele Include na tym samym poziomie
var zamowieniaKompletne = await ctx.Zamowienia
    .Include(z => z.Klient)           // Include 1
    .Include(z => z.Pozycje)          // Include 2 (osobny JOIN)
        .ThenInclude(p => p.Produkt)  // ThenInclude dla Include 2
    .AsNoTracking()
    .ToListAsync();

// UWAGA na kartezjański explozję!
// Include kilku kolekcji na tym samym poziomie = ogromny wynik
// Np. Zamówienie z 10 pozycjami i 5 tagami = 10 × 5 = 50 wierszy SQL!
// Rozwiązanie: AsSplitQuery()

var bezKartezjanskiego = await ctx.Zamowienia
    .Include(z => z.Pozycje)
    .Include(z => z.Tagi)             // gdyby Zamowienie miało Tagi
    .AsSplitQuery()                   // EF Core wysyła osobne zapytania dla każdego Include!
    .ToListAsync();
// Zamiast jednego JOIN który mnoży wiersze — kilka prostych SELECT
```

---

### 5. Lazy Loading — leniwe ładowanie

csharp

```csharp
// Lazy Loading — powiązane dane ładowane automatycznie przy pierwszym dostępie
// WYMAGA: dotnet add package Microsoft.EntityFrameworkCore.Proxies

// Konfiguracja
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connStr)
           .UseLazyLoadingProxies());  // włącz lazy loading!

// MODELE: navigation properties MUSZĄ być virtual!
public class KlientLazy
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";

    public virtual ICollection<ZamowienieLazy> Zamowienia { get; set; } = new List<ZamowienieLazy>();
    //     ↑ VIRTUAL — proxy może to przechwycić!
}

public class ZamowienieLazy
{
    public int     Id     { get; set; }
    public decimal Wartosc{ get; set; }

    public int          KlientId { get; set; }
    public virtual KlientLazy Klient { get; set; } = null!;
    //             ↑ VIRTUAL
}

// Użycie — brak Include!
var klient = await ctx.Klienci.FindAsync(1);

// Przy pierwszym dostępie do Zamowienia — automatyczne zapytanie do bazy
Console.WriteLine($"Klient: {klient!.Imie}");
Console.WriteLine($"Zamówień: {klient.Zamowienia.Count}");
// ↑ Tu EF Core wysyła: SELECT * FROM Zamowienia WHERE KlientId = 1

// PROBLEM: N+1 Queries!
var wszyscyKlienci = await ctx.Klienci.ToListAsync();
foreach (var k in wszyscyKlienci)
{
    // Każdy klient = osobne zapytanie! 100 klientów = 101 zapytań!
    Console.WriteLine($"{k.Imie}: {k.Zamowienia.Count} zamówień");
}
// N+1: 1 query dla klientów + N queries dla zamówień każdego klienta
// ROZWIĄZANIE: Include() dla listy, lazy loading tylko dla pojedynczego obiektu

// Explicit Loading — ładuj na żądanie bez proxy
var klientExplicit = await ctx.Klienci.FindAsync(1);

// Załaduj kolekcję manualnie gdy potrzebna
await ctx.Entry(klientExplicit!)
    .Collection(k => k.Zamowienia)
    .LoadAsync();

// Załaduj referencję manualnie
var zamowienie = await ctx.Zamowienia.FindAsync(1);
await ctx.Entry(zamowienie!)
    .Reference(z => z.Klient)
    .LoadAsync();

// Explicit Loading z filtrowaniem
await ctx.Entry(klientExplicit)
    .Collection(k => k.Zamowienia)
    .Query()
    .Where(z => z.Status == "Nowe")
    .LoadAsync();
```

---

### 6. Porównanie strategii ładowania

csharp

```csharp
// Kiedy co używać — praktyczny przewodnik

// 1. EAGER LOADING (Include) — ZALECANE domyślnie
// ✅ Znasz z góry jakie dane będą potrzebne
// ✅ API endpoints, serwisy
// ✅ Jeden round-trip do bazy
// ❌ Może ładować za dużo gdy nie potrzebujesz powiązanych

await ctx.Zamowienia
    .Include(z => z.Klient)
    .Include(z => z.Pozycje).ThenInclude(p => p.Produkt)
    .AsNoTracking()
    .ToListAsync();

// 2. PROJEKCJA (Select) — NAJWYDAJNIEJSZE
// ✅ Ładujesz TYLKO to co potrzebujesz
// ✅ Zero tracking overhead
// ✅ SQL pobiera tylko potrzebne kolumny
// ✅ Najlepsze dla GET endpoints w API

var dto = await ctx.Zamowienia
    .Where(z => z.Status == "Nowe")
    .Select(z => new ZamowienieListDto(      // EF tłumaczy cały Select na SQL!
        z.Id,
        z.Wartosc,
        z.Klient.Imie + " " + z.Klient.Nazwisko,  // JOIN automatyczny
        z.Pozycje.Count,                           // COUNT bez ładowania pozycji
        z.Data))
    .AsNoTracking()
    .ToListAsync();

// 3. LAZY LOADING — ostrożnie
// ✅ Prostota kodu (brak Include)
// ✅ Dla pojedynczych obiektów w UI
// ❌ Ryzyko N+1 queries w pętlach
// ❌ Wymaga otwartego kontekstu
// ❌ Proxy generuje narzut

// 4. EXPLICIT LOADING — dla rzadkich przypadków
// ✅ Gdy decydujesz w runtime czy ładować
// ✅ Warunkowe ładowanie powiązanych

// DETEKCJA N+1 — wykryj problem z logowaniem
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connStr)
           .LogTo(sql =>
           {
               if (sql.Contains("SELECT"))
               {
                   var stack = Environment.StackTrace;
                   Console.WriteLine($"SQL: {sql.Substring(0, Math.Min(100, sql.Length))}");
               }
           }));
// Lub użyj MiniProfiler / EF Core Query Tags
```

---

### 7. Praktyczny przykład — hierarchia z relacjami

csharp

```csharp
// Kompletna konfiguracja relacji dla sklepu

public class SklepContext : DbContext
{
    public DbSet<Klient>        Klienci    { get; set; }
    public DbSet<Zamowienie>    Zamowienia { get; set; }
    public DbSet<Produkt>       Produkty   { get; set; }
    public DbSet<Kategoria>     Kategorie  { get; set; }
    public DbSet<Tag>           Tagi       { get; set; }
    public DbSet<KursStudent>   KursStudenci { get; set; }

    public SklepContext(DbContextOptions<SklepContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // 1:N — Kategoria → Produkty
        mb.Entity<Produkt>()
          .HasOne(p => p.Kategoria)
          .WithMany(k => k.Produkty)
          .HasForeignKey(p => p.KategoriaId)
          .OnDelete(DeleteBehavior.Restrict);

        // 1:N — Klient → Zamowienia
        mb.Entity<Zamowienie>()
          .HasOne(z => z.Klient)
          .WithMany(k => k.Zamowienia)
          .HasForeignKey(z => z.KlientId)
          .OnDelete(DeleteBehavior.Cascade);

        // 1:N — Zamowienie → PozycjeZamowien
        mb.Entity<PozycjaZamowienia>()
          .HasOne(p => p.Zamowienie)
          .WithMany(z => z.Pozycje)
          .HasForeignKey(p => p.ZamowienieId);

        // N:N — Produkt ↔ Tag (automatyczna tabela)
        mb.Entity<Produkt>()
          .HasMany(p => p.Tagi)
          .WithMany(t => t.Produkty)
          .UsingEntity<Dictionary<string, object>>(
              "ProduktTag",
              j => j.HasOne<Tag>().WithMany()
                    .HasForeignKey("TagId"),
              j => j.HasOne<Produkt>().WithMany()
                    .HasForeignKey("ProduktId"));

        // 1:1 — Uzytkownik → Profil
        mb.Entity<Uzytkownik>()
          .HasOne(u => u.Profil)
          .WithOne(p => p.Uzytkownik)
          .HasForeignKey<ProfilUzytkownika>(p => p.UzytkownikId)
          .OnDelete(DeleteBehavior.Cascade);
    }
}

// Serwis z kompletnym użyciem relacji
public class RelacjeSerwis
{
    private readonly SklepContext _ctx;
    public RelacjeSerwis(SklepContext ctx) => _ctx = ctx;

    // --- TWORZENIE Z RELACJAMI ---
    public async Task<int> UtworzZamowienieAsync(
        int klientId, IEnumerable<(int ProdId, int Ilosc)> pozycje)
    {
        var produkty = await _ctx.Produkty
            .Where(p => pozycje.Select(x => x.ProdId).Contains(p.Id))
            .ToListAsync();

        var zamowienie = new Zamowienie
        {
            KlientId = klientId,
            Status   = "Nowe",
            Pozycje  = pozycje.Select(p =>
            {
                var prod = produkty.First(x => x.Id == p.ProdId);
                return new PozycjaZamowienia
                {
                    ProduktId    = p.ProdId,
                    Ilosc        = p.Ilosc,
                    CenaW_Chwili = prod.Cena
                };
            }).ToList(),
            Wartosc  = pozycje.Sum(p =>
                produkty.First(x => x.Id == p.ProdId).Cena * p.Ilosc)
        };

        _ctx.Zamowienia.Add(zamowienie);
        await _ctx.SaveChangesAsync();
        return zamowienie.Id;
    }

    // --- ODCZYT Z RELACJAMI ---
    public async Task<ZamowienieDetailDto?> PobierzZamowienieAsync(int id)
    {
        return await _ctx.Zamowienia
            .AsNoTracking()
            .Where(z => z.Id == id)
            .Select(z => new ZamowienieDetailDto(
                z.Id,
                z.Data,
                z.Status,
                z.Wartosc,
                new KlientSkroconaDto(
                    z.Klient.Id,
                    $"{z.Klient.Imie} {z.Klient.Nazwisko}",
                    z.Klient.Email),
                z.Pozycje.Select(p => new PozycjaDto(
                    p.Produkt.Nazwa,
                    p.Produkt.Kategoria.Nazwa,
                    p.Ilosc,
                    p.CenaW_Chwili,
                    p.Ilosc * p.CenaW_Chwili)).ToList()))
            .FirstOrDefaultAsync();
    }

    // --- N:N OPERACJE ---
    public async Task DodajTagiDoProduktuAsync(int produktId, int[] tagIds)
    {
        var produkt = await _ctx.Produkty
            .Include(p => p.Tagi)           // załaduj istniejące tagi
            .FirstOrDefaultAsync(p => p.Id == produktId)
            ?? throw new KeyNotFoundException($"Produkt #{produktId}");

        var noweTagi = await _ctx.Tagi
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync();

        foreach (var tag in noweTagi)
            if (!produkt.Tagi.Any(t => t.Id == tag.Id))
                produkt.Tagi.Add(tag);  // EF Core automatycznie zarządza tabelą łączącą!

        await _ctx.SaveChangesAsync();
    }

    public async Task UsunTagZProduktuAsync(int produktId, int tagId)
    {
        var produkt = await _ctx.Produkty
            .Include(p => p.Tagi.Where(t => t.Id == tagId))
            .FirstOrDefaultAsync(p => p.Id == produktId)
            ?? throw new KeyNotFoundException();

        var tag = produkt.Tagi.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
            produkt.Tagi.Remove(tag);  // EF usuwa wiersz z tabeli łączącej

        await _ctx.SaveChangesAsync();
    }

    // --- STATYSTYKI Z RELACJAMI ---
    public async Task<List<KlientStatDto>> PobierzStatystykiKlientowAsync()
    {
        return await _ctx.Klienci
            .AsNoTracking()
            .Where(k => k.Aktywny)
            .Select(k => new KlientStatDto(
                k.Id,
                $"{k.Imie} {k.Nazwisko}",
                k.Zamowienia.Count,                                    // COUNT bez ładowania
                k.Zamowienia.Sum(z => z.Wartosc),                      // SUM bez ładowania
                k.Zamowienia.Max(z => (DateTime?)z.Data),              // MAX nullable
                k.Zamowienia
                    .SelectMany(z => z.Pozycje)
                    .Select(p => p.Produkt.Kategoria.Nazwa)
                    .Distinct()
                    .Count()))                                           // ile kategorii kupił
            .OrderByDescending(k => k.SumaZamowien)
            .ToListAsync();
    }
}

// DTOs
public record ZamowienieDetailDto(int Id, DateTime Data, string Status, decimal Wartosc,
    KlientSkroconaDto Klient, List<PozycjaDto> Pozycje);
public record KlientSkroconaDto(int Id, string PelneNazwisko, string Email);
public record PozycjaDto(string Produkt, string Kategoria, int Ilosc,
    decimal CenaJedn, decimal Wartosc);
public record KlientStatDto(int Id, string Nazwa, int LiczbaZamowien,
    decimal SumaZamowien, DateTime? OstatniaData, int LiczbaKategorii);
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między eager, lazy i explicit loading?"** Eager (Include) — powiązane dane ładowane razem z głównym zapytaniem przez JOIN. Jeden round-trip, wiesz z góry co ładujesz. Lazy — powiązane dane ładowane automatycznie przy pierwszym dostępie do navigation property. Wymaga `virtual` i proxy. Ryzyko N+1. Explicit — ładujesz manualnie przez `ctx.Entry(obj).Collection(...).LoadAsync()`. Dajesz Ci kontrolę kiedy i czy ładować.

**"Co to N+1 problem?"** Pobierasz N obiektów (1 zapytanie), potem dla każdego lazily ładujesz powiązane (N zapytań) = N+1 total. 100 klientów z ich zamówieniami = 101 zapytań. Rozwiązanie: `Include()` dla kolekcji — jeden JOIN zamiast N zapytań. Lub `AsSplitQuery()` dla wielu Include na tym samym poziomie.

**"Kiedy `AsSplitQuery()`?"** Gdy masz Include kilku kolekcji na tym samym poziomie — EF Core generuje jeden SQL z wieloma JOINami co daje karteziański produkt (100 zamówień × 50 pozycji × 10 tagów = 50000 wierszy). `AsSplitQuery()` wysyła osobne SELECT dla każdej kolekcji. Mniej danych przez sieć, ale więcej round-tripów. Użyj gdy widzisz duplikaty w wynikach lub powolne zapytania z Include.

**"Jak działa N:N bez jawnej klasy łączącej?"** EF Core 5+ automatycznie tworzy tabelę łączącą z dwoma FK. Możesz dodawać/usuwać przez navigation property: `produkt.Tagi.Add(tag)` — EF wstawi wiersz do tabeli łączącej. `produkt.Tagi.Remove(tag)` — EF usunie wiersz. Jeśli potrzebujesz dodatkowych danych (data zapisu, ocena) — użyj jawnej klasy łączącej z właściwym kluczem złożonym.