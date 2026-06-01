### Mockowanie z Moq i InMemory Database

---

### 1. Czym jest mock i kiedy używać

csharp

```csharp
// MOCK — podmiana zależności na kontrolowaną "atrapę"
// Cel: testowanie klasy W IZOLACJI od zewnętrznych systemów

// Typy testowych dublurów (Test Doubles):
// Dummy    — obiekt przekazywany ale nieużywany
// Stub     — zwraca predefiniowane odpowiedzi
// Mock     — stub + weryfikacja wywołań
// Spy      — wrapper na prawdziwy obiekt, loguje wywołania
// Fake     — uproszczona implementacja (np. InMemory DB)

// dotnet add package Moq
// dotnet add package Moq.AutoMock  (opcjonalne)

using Moq;
using Xunit;
using FluentAssertions;

// Klasa którą będziemy testować
public class ProduktSerwis
{
    private readonly IProduktRepo        _repo;
    private readonly IEmailSerwis        _email;
    private readonly ICacheService       _cache;
    private readonly ILogger<ProduktSerwis> _logger;

    public ProduktSerwis(
        IProduktRepo repo,
        IEmailSerwis email,
        ICacheService cache,
        ILogger<ProduktSerwis> logger)
    {
        _repo   = repo;
        _email  = email;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<ProduktDto?> PobierzAsync(int id,
        CancellationToken ct = default)
    {
        // Sprawdź cache
        var cached = await _cache.GetAsync<ProduktDto>($"produkt:{id}");
        if (cached != null) return cached;

        var produkt = await _repo.FindAsync(id, ct);
        if (produkt == null) return null;

        var dto = new ProduktDto(produkt.Id, produkt.Nazwa, produkt.Cena);
        await _cache.SetAsync($"produkt:{id}", dto);
        return dto;
    }

    public async Task<int> DodajAsync(NowyProduktDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Nazwa))
            throw new ArgumentException("Nazwa jest wymagana");

        bool nazwaZajeta = await _repo.NazwaIstniejeAsync(dto.Nazwa, ct);
        if (nazwaZajeta)
            throw new InvalidOperationException($"Produkt '{dto.Nazwa}' już istnieje");

        var produkt = new Produkt { Nazwa = dto.Nazwa, Cena = dto.Cena };
        int id = await _repo.DodajAsync(produkt, ct);

        await _email.WyslijAsync("admin@sklep.pl",
            $"Dodano produkt: {dto.Nazwa}");

        await _cache.InvalidateAsync("produkty:wszystkie");
        return id;
    }
}

// Interfejsy
public interface IProduktRepo
{
    Task<Produkt?> FindAsync(int id, CancellationToken ct = default);
    Task<bool> NazwaIstniejeAsync(string nazwa, CancellationToken ct = default);
    Task<int> DodajAsync(Produkt p, CancellationToken ct = default);
    Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
}
public interface IEmailSerwis
{
    Task WyslijAsync(string to, string body);
}
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);
    Task InvalidateAsync(string key);
}

// Modele
public class Produkt { public int Id { get; set; } public string Nazwa { get; set; } = ""; public decimal Cena { get; set; } }
public record ProduktDto(int Id, string Nazwa, decimal Cena);
public record NowyProduktDto(string Nazwa, decimal Cena);
```

---

### 2. Mock — podstawy Setup i Returns

csharp

```csharp
public class MockPodstawyTests
{
    // === TWORZENIE MOCKA ===
    [Fact]
    public void TworzenieMocka()
    {
        // Mock<T> — tworzy mock interfejsu lub klasy wirtualnej
        var mock = new Mock<IProduktRepo>();

        // .Object — pobierz instancję która implementuje interfejs
        IProduktRepo repo = mock.Object;
    }

    // === SETUP — definicja zachowania ===
    [Fact]
    public async Task Setup_Returns_ZwracaOczekiwanaWartosc()
    {
        var repoMock = new Mock<IProduktRepo>();

        // Zwróć konkretną wartość
        repoMock
            .Setup(r => r.FindAsync(1, default))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m });

        // Wywołaj mockowaną metodę
        var wynik = await repoMock.Object.FindAsync(1);

        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
    }

    // === It.IsAny<T>() — dowolna wartość ===
    [Fact]
    public async Task Setup_IsAny_DopasujDowolaJWartosc()
    {
        var repoMock = new Mock<IProduktRepo>();

        // Reaguj na DOWOLNE id
        repoMock
            .Setup(r => r.FindAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Produkt { Id = 99, Nazwa = "Dowolny" });

        var wynik1 = await repoMock.Object.FindAsync(1);
        var wynik2 = await repoMock.Object.FindAsync(42);
        var wynik3 = await repoMock.Object.FindAsync(999);

        wynik1!.Nazwa.Should().Be("Dowolny");
        wynik2!.Nazwa.Should().Be("Dowolny");
        wynik3!.Nazwa.Should().Be("Dowolny");
    }

    // === It.Is<T>() — custom predykat ===
    [Fact]
    public async Task Setup_IsCustom_DopasujWarunkowo()
    {
        var repoMock = new Mock<IProduktRepo>();

        // Zwróć produkt tylko gdy id > 0
        repoMock
            .Setup(r => r.FindAsync(
                It.Is<int>(id => id > 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Znaleziony" });

        // id = 1 → pasuje
        var wynik = await repoMock.Object.FindAsync(1);
        wynik.Should().NotBeNull();

        // id = -1 → nie pasuje → zwraca domyślne (null dla ref type)
        var brak = await repoMock.Object.FindAsync(-1);
        brak.Should().BeNull();
    }

    // === Różne wartości dla różnych wywołań ===
    [Fact]
    public async Task Setup_RozneWartosci_DlaDozwolonychId()
    {
        var repoMock = new Mock<IProduktRepo>();

        // Różne setup dla różnych parametrów
        repoMock
            .Setup(r => r.FindAsync(1, default))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop" });

        repoMock
            .Setup(r => r.FindAsync(2, default))
            .ReturnsAsync(new Produkt { Id = 2, Nazwa = "Mysz" });

        repoMock
            .Setup(r => r.FindAsync(It.Is<int>(x => x > 100), default))
            .ReturnsAsync((Produkt?)null);

        (await repoMock.Object.FindAsync(1))!.Nazwa.Should().Be("Laptop");
        (await repoMock.Object.FindAsync(2))!.Nazwa.Should().Be("Mysz");
        (await repoMock.Object.FindAsync(200)).Should().BeNull();
    }

    // === Sekwencyjne wyniki ===
    [Fact]
    public async Task Setup_SetupSequence_InneWartosciKolejno()
    {
        var repoMock = new Mock<IProduktRepo>();

        // Pierwsze wywołanie → null, drugie → produkt, trzecie → null
        repoMock
            .SetupSequence(r => r.FindAsync(1, default))
            .ReturnsAsync((Produkt?)null)
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop" })
            .ReturnsAsync((Produkt?)null);

        (await repoMock.Object.FindAsync(1)).Should().BeNull();
        (await repoMock.Object.FindAsync(1))!.Nazwa.Should().Be("Laptop");
        (await repoMock.Object.FindAsync(1)).Should().BeNull();
    }
}
```

---

### 3. Setup zaawansowany — Callback, Throws, ReturnsAsync

csharp

```csharp
public class SetupZaawansowanyTests
{
    // === Callback — wykonaj kod przy wywołaniu ===
    [Fact]
    public async Task Callback_PrzechwytujParametry()
    {
        var repoMock = new Mock<IProduktRepo>();
        Produkt? zapisanyProdukt = null;

        repoMock
            .Setup(r => r.DodajAsync(It.IsAny<Produkt>(), default))
            .Callback<Produkt, CancellationToken>((p, _) =>
            {
                // Przechwytuje parametry wywołania!
                zapisanyProdukt = p;
                p.Id = 42;  // symuluj nadanie ID przez bazę
            })
            .ReturnsAsync(42);

        var serwis = new ProduktSerwis(
            repoMock.Object,
            new Mock<IEmailSerwis>().Object,
            new Mock<ICacheService>().Object,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<ProduktSerwis>.Instance);

        int id = await serwis.DodajAsync(new NowyProduktDto("Laptop", 3500m));

        id.Should().Be(42);
        zapisanyProdukt.Should().NotBeNull();
        zapisanyProdukt!.Nazwa.Should().Be("Laptop");
    }

    // === Throws — rzuć wyjątek ===
    [Fact]
    public async Task Throws_SymulujBladBazy()
    {
        var repoMock = new Mock<IProduktRepo>();

        repoMock
            .Setup(r => r.DodajAsync(It.IsAny<Produkt>(), default))
            .ThrowsAsync(new Exception("Connection timeout"));

        // Teraz testuj czy serwis poprawnie obsługuje błąd
        var serwis = new ProduktSerwis(
            repoMock.Object,
            new Mock<IEmailSerwis>().Object,
            new Mock<ICacheService>().Object,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<ProduktSerwis>.Instance);

        Func<Task> akcja = () =>
            serwis.DodajAsync(new NowyProduktDto("Laptop", 3500m));

        await akcja.Should().ThrowAsync<Exception>()
            .WithMessage("Connection timeout");
    }

    // === Returns z logiką — lambda ===
    [Fact]
    public async Task Returns_ZLambda_DynamicznaWartosc()
    {
        var repoMock  = new Mock<IProduktRepo>();
        var produkty  = new Dictionary<int, Produkt>
        {
            [1] = new() { Id = 1, Nazwa = "Laptop" },
            [2] = new() { Id = 2, Nazwa = "Mysz"   }
        };

        // Lambda dynamicznie zwraca na podstawie parametru
        repoMock
            .Setup(r => r.FindAsync(It.IsAny<int>(), default))
            .ReturnsAsync((int id, CancellationToken _) =>
                produkty.TryGetValue(id, out var p) ? p : null);

        (await repoMock.Object.FindAsync(1))!.Nazwa.Should().Be("Laptop");
        (await repoMock.Object.FindAsync(2))!.Nazwa.Should().Be("Mysz");
        (await repoMock.Object.FindAsync(99)).Should().BeNull();
    }

    // === SetupProperty — właściwości z getterem/setterem ===
    [Fact]
    public void SetupProperty_WlasciwoscZGetterSet()
    {
        var mock = new Mock<IProduktRepoZWlasciwoscia>();

        // Śledź odczyty i zapisy właściwości
        mock.SetupProperty(r => r.TimeoutMs, 30);

        mock.Object.TimeoutMs = 60;
        mock.Object.TimeoutMs.Should().Be(60);
    }
}

public interface IProduktRepoZWlasciwoscia : IProduktRepo
{
    int TimeoutMs { get; set; }
}
```

---

### 4. Verify — weryfikacja wywołań

csharp

```csharp
public class VerifyTests
{
    private static ProduktSerwis TworzSerwis(
        Mock<IProduktRepo> repo,
        Mock<IEmailSerwis> email,
        Mock<ICacheService> cache)
    {
        repo.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        repo.Setup(r => r.DodajAsync(It.IsAny<Produkt>(), default))
            .ReturnsAsync(1);
        cache.Setup(c => c.GetAsync<ProduktDto>(It.IsAny<string>()))
            .ReturnsAsync((ProduktDto?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(),
            It.IsAny<ProduktDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.InvalidateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        email.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new ProduktSerwis(repo.Object, email.Object, cache.Object,
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<ProduktSerwis>.Instance);
    }

    [Fact]
    public async Task Verify_Times_WywolanoDokladnieLiczbeRazy()
    {
        var repoMock  = new Mock<IProduktRepo>();
        var emailMock = new Mock<IEmailSerwis>();
        var cacheMock = new Mock<ICacheService>();
        var serwis    = TworzSerwis(repoMock, emailMock, cacheMock);

        await serwis.DodajAsync(new NowyProduktDto("Laptop", 3500m));

        // Times.Once — dokładnie raz
        repoMock.Verify(
            r => r.DodajAsync(It.IsAny<Produkt>(), default),
            Times.Once);

        // Times.Exactly(n) — dokładnie n razy
        emailMock.Verify(
            e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(1));

        // Nigdy nie wywołano
        repoMock.Verify(
            r => r.PobierzWszystkieAsync(default),
            Times.Never);
    }

    [Fact]
    public async Task Verify_ZKonkretnymParametrem()
    {
        var repoMock  = new Mock<IProduktRepo>();
        var emailMock = new Mock<IEmailSerwis>();
        var cacheMock = new Mock<ICacheService>();
        var serwis    = TworzSerwis(repoMock, emailMock, cacheMock);

        await serwis.DodajAsync(new NowyProduktDto("Monitor", 1200m));

        // Sprawdź dokładne parametry
        emailMock.Verify(
            e => e.WyslijAsync(
                "admin@sklep.pl",              // konkretny adres
                It.Is<string>(s =>
                    s.Contains("Monitor"))),   // body zawiera nazwę
            Times.Once);

        // Cache invalidation z kluczem
        cacheMock.Verify(
            c => c.InvalidateAsync("produkty:wszystkie"),
            Times.Once);
    }

    [Fact]
    public async Task VerifyAll_WszystkieSetupWywolane()
    {
        var repoMock  = new Mock<IProduktRepo>(MockBehavior.Strict);

        // MockBehavior.Strict — wszystkie wywołania muszą mieć Setup
        // lub test rzuci wyjątek!
        repoMock
            .Setup(r => r.NazwaIstniejeAsync("Laptop", default))
            .ReturnsAsync(false);
        repoMock
            .Setup(r => r.DodajAsync(
                It.Is<Produkt>(p => p.Nazwa == "Laptop"), default))
            .ReturnsAsync(1);

        var emailMock = new Mock<IEmailSerwis>();
        emailMock.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.InvalidateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var serwis = new ProduktSerwis(repoMock.Object, emailMock.Object,
            cacheMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProduktSerwis>.Instance);

        await serwis.DodajAsync(new NowyProduktDto("Laptop", 3500m));

        // VerifyAll sprawdza że WSZYSTKIE Setupy zostały wywołane
        repoMock.VerifyAll();
    }

    [Fact]
    public async Task Verify_Times_AtLeastAtMost()
    {
        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<ProduktDto>(It.IsAny<string>()))
            .ReturnsAsync((ProduktDto?)null);
        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(),
            It.IsAny<ProduktDto>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        cacheMock.Setup(c => c.InvalidateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var repoMock = new Mock<IProduktRepo>();
        repoMock.Setup(r => r.FindAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Test", Cena = 100m });

        var emailMock = new Mock<IEmailSerwis>();
        var serwis = new ProduktSerwis(repoMock.Object, emailMock.Object,
            cacheMock.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProduktSerwis>.Instance);

        // Wywołaj kilka razy
        await serwis.PobierzAsync(1);
        await serwis.PobierzAsync(2);
        await serwis.PobierzAsync(3);

        // AtLeast — co najmniej n razy
        cacheMock.Verify(
            c => c.GetAsync<ProduktDto>(It.IsAny<string>()),
            Times.AtLeast(2));

        // AtMost — co najwyżej n razy
        cacheMock.Verify(
            c => c.GetAsync<ProduktDto>(It.IsAny<string>()),
            Times.AtMost(5));

        // Between — między min a max
        cacheMock.Verify(
            c => c.GetAsync<ProduktDto>(It.IsAny<string>()),
            Times.Between(2, 5, Moq.Range.Inclusive));
    }
}
```

---

### 5. InMemory Database — EF Core

csharp

```csharp
// dotnet add package Microsoft.EntityFrameworkCore.InMemory

using Microsoft.EntityFrameworkCore;

// DbContext do testów
public class SklepContext : DbContext
{
    public SklepContext(DbContextOptions<SklepContext> options)
        : base(options) { }

    public DbSet<Produkt>    Produkty   => Set<Produkt>();
    public DbSet<Zamowienie> Zamowienia => Set<Zamowienie>();
    public DbSet<Klient>     Klienci    => Set<Klient>();
}

public class Zamowienie
{
    public int     Id       { get; set; }
    public int     KlientId { get; set; }
    public decimal Suma     { get; set; }
    public string  Status   { get; set; } = "Nowe";
    public Klient  Klient   { get; set; } = null!;
    public List<Produkt> Produkty { get; set; } = new();
}

public class Klient
{
    public int    Id    { get; set; }
    public string Imie  { get; set; } = "";
    public string Email { get; set; } = "";
    public List<Zamowienie> Zamowienia { get; set; } = new();
}

// === HELPER — fabryka kontekstu testowego ===
public static class TestDbContextFactory
{
    // WAŻNE: każdy test dostaje INNĄ bazę przez unikalną nazwę!
    public static SklepContext Utworz(string? nazwaDb = null)
    {
        var options = new DbContextOptionsBuilder<SklepContext>()
            .UseInMemoryDatabase(nazwaDb ?? Guid.NewGuid().ToString())
            .Options;

        return new SklepContext(options);
    }

    public static async Task<SklepContext> UtworzZDanymi(
        Action<SklepContext>? seed = null)
    {
        var ctx = Utworz();

        if (seed != null)
        {
            seed(ctx);
            await ctx.SaveChangesAsync();
        }
        else
        {
            await DomyslnySeedAsync(ctx);
        }

        return ctx;
    }

    private static async Task DomyslnySeedAsync(SklepContext ctx)
    {
        ctx.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop",    Cena = 3500m },
            new Produkt { Id = 2, Nazwa = "Mysz",       Cena =  150m },
            new Produkt { Id = 3, Nazwa = "Klawiatura", Cena =  250m }
        );

        ctx.Klienci.AddRange(
            new Klient { Id = 1, Imie = "Anna",   Email = "anna@test.pl" },
            new Klient { Id = 2, Imie = "Bartek",  Email = "bartek@test.pl" }
        );

        await ctx.SaveChangesAsync();
    }
}
```

---

### 6. Testy z InMemory Database

csharp

```csharp
public class ProduktRepoInMemoryTests : IAsyncLifetime
{
    private SklepContext _ctx = null!;

    public async Task InitializeAsync()
    {
        // Świeży kontekst dla każdego testu (unikalna nazwa = izolacja!)
        _ctx = await TestDbContextFactory.UtworzZDanymi();
    }

    public async Task DisposeAsync()
    {
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task FindAsync_IstniejaceId_ZwracaProdukt()
    {
        var repo = new ProduktEFRepo(_ctx);

        var wynik = await repo.FindAsync(1);

        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
        wynik.Cena.Should().Be(3500m);
    }

    [Fact]
    public async Task FindAsync_NieistniejaceId_ZwracaNull()
    {
        var repo = new ProduktEFRepo(_ctx);

        var wynik = await repo.FindAsync(999);

        wynik.Should().BeNull();
    }

    [Fact]
    public async Task DodajAsync_NowyProdukt_ZwiekszyLiczbe()
    {
        var repo   = new ProduktEFRepo(_ctx);
        var nowy   = new Produkt { Nazwa = "Monitor", Cena = 1200m };
        int przed  = await _ctx.Produkty.CountAsync();

        int id = await repo.DodajAsync(nowy);

        int po = await _ctx.Produkty.CountAsync();
        po.Should().Be(przed + 1);
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DodajAsync_NowyProdukt_ZapisujePrawidloweWartosci()
    {
        var repo = new ProduktEFRepo(_ctx);
        var nowy = new Produkt { Nazwa = "Tablet", Cena = 2000m };

        int id = await repo.DodajAsync(nowy);

        // Pobierz z bazy nowym kontekstem (bez cache!)
        await using var nCtx = TestDbContextFactory.Utworz(
            "sprawdzenie_zapisu");  // BŁĄD — to inna baza!

        // Poprawnie — sprawdź przez ten sam kontekst
        var zapisany = await _ctx.Produkty
            .AsNoTracking()  // bez cache EF!
            .FirstOrDefaultAsync(p => p.Id == id);

        zapisany.Should().NotBeNull();
        zapisany!.Nazwa.Should().Be("Tablet");
        zapisany.Cena.Should().Be(2000m);
    }

    [Fact]
    public async Task PobierzWszystkie_ZwracaWszystkieProdukty()
    {
        var repo = new ProduktEFRepo(_ctx);

        var wynik = await repo.PobierzWszystkieAsync();

        wynik.Should().HaveCount(3);
        wynik.Should().Contain(p => p.Nazwa == "Laptop");
        wynik.Should().Contain(p => p.Nazwa == "Mysz");
    }

    [Fact]
    public async Task Usun_IstniejacyProdukt_ZmniejszaLiczbe()
    {
        var repo = new ProduktEFRepo(_ctx);

        bool wynik = await repo.UsunAsync(1);

        wynik.Should().BeTrue();
        _ctx.Produkty.Should().HaveCount(2);
        (await repo.FindAsync(1)).Should().BeNull();
    }

    [Fact]
    public async Task Usun_NieistniejacyProdukt_ZwracaFalse()
    {
        var repo = new ProduktEFRepo(_ctx);

        bool wynik = await repo.UsunAsync(999);

        wynik.Should().BeFalse();
    }
}

// Implementacja repozytorium z EF Core
public class ProduktEFRepo : IProduktRepo
{
    private readonly SklepContext _ctx;

    public ProduktEFRepo(SklepContext ctx) => _ctx = ctx;

    public async Task<Produkt?> FindAsync(int id,
        CancellationToken ct = default)
        => await _ctx.Produkty.FindAsync(new object[] { id }, ct);

    public async Task<bool> NazwaIstniejeAsync(string nazwa,
        CancellationToken ct = default)
        => await _ctx.Produkty.AnyAsync(p => p.Nazwa == nazwa, ct);

    public async Task<int> DodajAsync(Produkt p,
        CancellationToken ct = default)
    {
        _ctx.Produkty.Add(p);
        await _ctx.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync(
        CancellationToken ct = default)
        => await _ctx.Produkty.AsNoTracking().ToListAsync(ct);

    public async Task<bool> UsunAsync(int id,
        CancellationToken ct = default)
    {
        var p = await _ctx.Produkty.FindAsync(new object[] { id }, ct);
        if (p == null) return false;
        _ctx.Produkty.Remove(p);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
```

---

### 7. Łączenie Moq i InMemory — kompletny test

csharp

```csharp
// Kompletny test serwisu: InMemory DB dla repozytorium,
// Moq dla emaila i cache

public class ZamowieniaSerwisTests : IAsyncLifetime
{
    private SklepContext         _ctx     = null!;
    private Mock<IEmailSerwis>   _email   = null!;
    private Mock<ICacheService>  _cache   = null!;
    private ZamowienieSerwisImpl _serwis  = null!;

    public async Task InitializeAsync()
    {
        _ctx = TestDbContextFactory.Utworz();

        // Dane startowe
        _ctx.Klienci.Add(new Klient
            { Id = 1, Imie = "Anna", Email = "anna@test.pl" });
        _ctx.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m },
            new Produkt { Id = 2, Nazwa = "Mysz",   Cena = 150m });
        await _ctx.SaveChangesAsync();

        // Moq dla zewnętrznych serwisów
        _email = new Mock<IEmailSerwis>();
        _cache = new Mock<ICacheService>();

        _email.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(c => c.SetAsync(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(c => c.InvalidateAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Serwis z prawdziwym repo (InMemory) i mockowanymi serwisami
        var repo = new ProduktEFRepo(_ctx);
        _serwis  = new ZamowienieSerwisImpl(_ctx, _email.Object);
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task ZlozZamowienie_Poprawne_TworzysWBazie()
    {
        var request = new ZlozZamowienieRequest(
            KlientId: 1,
            PozycjeIds: new[] { 1, 2 });

        int id = await _serwis.ZlozZamowienieAsync(request);

        id.Should().BeGreaterThan(0);
        var zamowienie = await _ctx.Zamowienia
            .Include(z => z.Produkty)
            .FirstOrDefaultAsync(z => z.Id == id);

        zamowienie.Should().NotBeNull();
        zamowienie!.KlientId.Should().Be(1);
        zamowienie.Produkty.Should().HaveCount(2);
    }

    [Fact]
    public async Task ZlozZamowienie_Poprawne_WysylaEmailPotwierdzenia()
    {
        await _serwis.ZlozZamowienieAsync(
            new ZlozZamowienieRequest(1, new[] { 1 }));

        _email.Verify(
            e => e.WyslijAsync(
                "anna@test.pl",
                It.Is<string>(s => s.Contains("zamówienie"))),
            Times.Once);
    }

    [Fact]
    public async Task ZlozZamowienie_NieistniejacyKlient_RzucaWyjatek()
    {
        Func<Task> akcja = () =>
            _serwis.ZlozZamowienieAsync(
                new ZlozZamowienieRequest(999, new[] { 1 }));

        await akcja.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("*klient*");
    }
}

// Implementacja serwisu
public class ZamowienieSerwisImpl
{
    private readonly SklepContext _ctx;
    private readonly IEmailSerwis _email;

    public ZamowienieSerwisImpl(SklepContext ctx, IEmailSerwis email)
    { _ctx = ctx; _email = email; }

    public async Task<int> ZlozZamowienieAsync(
        ZlozZamowienieRequest req,
        CancellationToken ct = default)
    {
        var klient = await _ctx.Klienci.FindAsync(new object[] { req.KlientId }, ct)
            ?? throw new KeyNotFoundException($"Nie znaleziono klienta");

        var produkty = await _ctx.Produkty
            .Where(p => req.PozycjeIds.Contains(p.Id))
            .ToListAsync(ct);

        var zamowienie = new Zamowienie
        {
            KlientId = klient.Id,
            Suma     = produkty.Sum(p => p.Cena),
            Status   = "Nowe",
            Produkty = produkty
        };

        _ctx.Zamowienia.Add(zamowienie);
        await _ctx.SaveChangesAsync(ct);

        await _email.WyslijAsync(
            klient.Email,
            $"Twoje zamówienie #{zamowienie.Id} zostało złożone");

        return zamowienie.Id;
    }
}

public record ZlozZamowienieRequest(int KlientId, int[] PozycjeIds);
```

---

### Typowe pytania rekrutacyjne

**"Czym różni się Mock od Stub?"** Stub zwraca predefiniowane wartości — służy do dostarczenia danych testowych, nie weryfikuje wywołań. Mock to stub + możliwość weryfikacji — sprawdzasz CZY i JAK metoda została wywołana (`Verify`). W Moq domyślnie tworzy Mock (możesz też nie weryfikować). Kiedy używać Stub: chcesz tylko dostarczyć dane. Mock: chcesz też upewnić się że metoda została wywołana (np. email faktycznie wysłany, cache zainwalidowany).

**"Dlaczego każdy test powinien mieć własną instancję InMemory DB?"** InMemory Database z tą samą nazwą współdzieli dane między testami — test A może zostawić dane które popsują test B. Unikalny `Guid.NewGuid().ToString()` jako nazwa zapewnia izolację — każdy test startuje z czystą bazą. Alternatywa: SQLite in-memory (`UseInMemoryDatabase` → `UseSqlite("DataSource=:memory:")`), który lepiej imituje prawdziwy SQL (np. obsługuje transakcje i relacje z kaskadami).

**"Kiedy `MockBehavior.Strict`?"** `MockBehavior.Strict` rzuca wyjątek gdy wywołasz metodę bez wcześniejszego `Setup`. Przydatne gdy chcesz upewnić się że testowany kod nie wywołuje niczego niespodziewanego — test jawnie dokumentuje każdą zależność. Domyślne `MockBehavior.Loose` zwraca domyślne wartości (`null`, `0`, `false`) dla metod bez Setupu. Używaj Strict dla krytycznych komponentów gdzie każde wywołanie zewnętrzne powinno być świadome. Loose dla szybkich testów gdzie nie wszystkie wywołania są istotne.

**"Jak testować prywatne metody?"** Nie testuj prywatnych metod bezpośrednio — to szczegół implementacji. Testuj publiczne metody które je wywołują — jeśli publiczna metoda działa poprawnie, prywatna też. Jeśli prywatna metoda jest na tyle złożona że chcesz ją testować osobno — to sygnał że powinna być w osobnej klasie. Wyjątek: `InternalsVisibleTo` — zamiast prywatnej, użyj `internal` i udostępnij assembly testom: `[assembly: InternalsVisibleTo("Sklep.Tests")]`.