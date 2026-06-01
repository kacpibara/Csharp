using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace _09_CleanCode_Testing;

// ============================================================
// SHARED INTERFACES & DOMAIN TYPES
// (IEmailSerwis used in UnitTestowanie.cs — defined here once)
// ============================================================

public interface IEmailSerwis
{
    Task WyslijAsync(string to, string body);
}

public class Produkt
{
    public int     Id          { get; set; }
    public string  Nazwa       { get; set; } = "";
    public decimal Cena        { get; set; }
    public int     KategoriaId { get; set; }
    public int     StanMagazyn { get; set; }
    public bool    Aktywny     { get; set; } = true;
}

public class Klient
{
    public int              Id        { get; set; }
    public string           Imie      { get; set; } = "";
    public string           Nazwisko  { get; set; } = "";
    public string           Email     { get; set; } = "";
    public bool             Aktywny   { get; set; } = true;
    public List<ZamowienieM> Zamowienia { get; set; } = new();
}

public class ZamowienieM
{
    public int     Id       { get; set; }
    public int     KlientId { get; set; }
    public decimal Suma     { get; set; }
    public Klient? Klient   { get; set; }
}

public class Kategoria
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";
}

public interface IProduktRepo
{
    Task<Produkt?>      PobierzAsync(int id);
    Task<bool>          NazwaIstniejeAsync(string nazwa);
    Task                DodajAsync(Produkt p);
    Task<List<Produkt>> PobierzWszystkieAsync();
    Task                UsunAsync(int id);
}

public interface ICacheService
{
    T?   Get<T>(string klucz);
    void Set<T>(string klucz, T wartosc, TimeSpan? ttl = null);
    void Remove(string klucz);
}

public interface IProduktRepoZWlasciwoscia : IProduktRepo
{
    bool CzyPolaczone { get; }
}

public record ProduktDto(int Id, string Nazwa, decimal Cena, int StanMagazyn);
public record NowyProduktDto(string Nazwa, decimal Cena, int KategoriaId, int StanMagazyn);

// ============================================================
// SKLEP KONTEKST — DbContext z InMemory
// ============================================================

public class SklepKontekst : DbContext
{
    public SklepKontekst(DbContextOptions<SklepKontekst> opts) : base(opts) { }

    public DbSet<Produkt>    Produkty   => Set<Produkt>();
    public DbSet<ZamowienieM> Zamowienia => Set<ZamowienieM>();
    public DbSet<Klient>     Klienci    => Set<Klient>();
    public DbSet<Kategoria>  Kategorie  => Set<Kategoria>();
}

// ============================================================
// PRODUKT SERWIS — 4 zależności: repo, email, cache, logger
// ============================================================

public class ProduktSerwis
{
    private readonly IProduktRepo           _repo;
    private readonly IEmailSerwis           _email;
    private readonly ICacheService          _cache;
    private readonly ILogger<ProduktSerwis> _logger;

    public ProduktSerwis(
        IProduktRepo           repo,
        IEmailSerwis           email,
        ICacheService          cache,
        ILogger<ProduktSerwis> logger)
    {
        _repo   = repo;
        _email  = email;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<ProduktDto?> PobierzAsync(int id)
    {
        var cached = _cache.Get<ProduktDto>($"produkt:{id}");
        if (cached != null) return cached;

        var p = await _repo.PobierzAsync(id);
        if (p == null) return null;

        var dto = new ProduktDto(p.Id, p.Nazwa, p.Cena, p.StanMagazyn);
        _cache.Set($"produkt:{id}", dto, TimeSpan.FromMinutes(5));
        return dto;
    }

    public async Task<ProduktDto> DodajAsync(NowyProduktDto dto)
    {
        if (await _repo.NazwaIstniejeAsync(dto.Nazwa))
            throw new InvalidOperationException($"Produkt '{dto.Nazwa}' już istnieje");

        var produkt = new Produkt
        {
            Nazwa       = dto.Nazwa,
            Cena        = dto.Cena,
            KategoriaId = dto.KategoriaId,
            StanMagazyn = dto.StanMagazyn,
            Aktywny     = true
        };

        await _repo.DodajAsync(produkt);
        _logger.LogInformation("Dodano produkt: {Nazwa}", dto.Nazwa);
        await _email.WyslijAsync("admin@sklep.pl", $"Nowy produkt: {dto.Nazwa}");

        return new ProduktDto(produkt.Id, produkt.Nazwa, produkt.Cena, produkt.StanMagazyn);
    }

    public async Task<List<ProduktDto>> PobierzWszystkieAsync()
    {
        var lista = await _repo.PobierzWszystkieAsync();
        return lista.Select(p => new ProduktDto(p.Id, p.Nazwa, p.Cena, p.StanMagazyn)).ToList();
    }

    public async Task UsunAsync(int id)
    {
        _cache.Remove($"produkt:{id}");
        await _repo.UsunAsync(id);
        _logger.LogInformation("Usunięto produkt ID={Id}", id);
    }
}

// ============================================================
// PRODUKT EF REPO — konkretna implementacja IProduktRepo
// ============================================================

public class ProduktEFRepo : IProduktRepo
{
    private readonly SklepKontekst _db;
    public ProduktEFRepo(SklepKontekst db) => _db = db;

    public async Task<Produkt?> PobierzAsync(int id)
        => await _db.Produkty.FindAsync(id);

    public async Task<bool> NazwaIstniejeAsync(string nazwa)
        => await _db.Produkty.AnyAsync(p => p.Nazwa == nazwa);

    public async Task DodajAsync(Produkt p)
    {
        _db.Produkty.Add(p);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync()
        => await _db.Produkty.ToListAsync();

    public async Task UsunAsync(int id)
    {
        var p = await _db.Produkty.FindAsync(id);
        if (p != null) { _db.Produkty.Remove(p); await _db.SaveChangesAsync(); }
    }
}

// ============================================================
// TEST DB CONTEXT FACTORY — fabryka kontekstu dla testów
// ============================================================

public static class TestDbContextFactory
{
    public static SklepKontekst Utworz()
    {
        var opts = new DbContextOptionsBuilder<SklepKontekst>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())   // nowa baza dla każdego testu
            .Options;
        return new SklepKontekst(opts);
    }

    public static async Task<SklepKontekst> UtworzZDanymiAsync()
    {
        var db = Utworz();
        db.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop",    Cena = 3500m, StanMagazyn = 10, Aktywny = true  },
            new Produkt { Id = 2, Nazwa = "Mysz",       Cena = 150m,  StanMagazyn = 50, Aktywny = true  },
            new Produkt { Id = 3, Nazwa = "Klawiatura", Cena = 250m,  StanMagazyn = 30, Aktywny = false }
        );
        await db.SaveChangesAsync();
        return db;
    }
}

// ============================================================
// 1. MOQ — PODSTAWY: Setup/Returns/ReturnsAsync/ThrowsAsync
// ============================================================

public class MockPodstawyTests
{
    private static (ProduktSerwis serwis,
                    Mock<IProduktRepo>           repoMock,
                    Mock<IEmailSerwis>           emailMock,
                    Mock<ICacheService>          cacheMock,
                    Mock<ILogger<ProduktSerwis>> loggerMock)
    TworzSerwis()
    {
        var repo   = new Mock<IProduktRepo>();
        var email  = new Mock<IEmailSerwis>();
        var cache  = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ProduktSerwis>>();
        var serwis = new ProduktSerwis(repo.Object, email.Object, cache.Object, logger.Object);
        return (serwis, repo, email, cache, logger);
    }

    [Fact]
    public async Task PobierzAsync_ProduktIstnieje_ZwracaDto()
    {
        var (serwis, repo, _, cache, _) = TworzSerwis();
        cache.Setup(c => c.Get<ProduktDto>("produkt:1")).Returns((ProduktDto?)null);
        repo.Setup(r => r.PobierzAsync(1))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m, StanMagazyn = 10 });

        var wynik = await serwis.PobierzAsync(1);

        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
        wynik.Cena.Should().Be(3500m);
    }

    [Fact]
    public async Task PobierzAsync_ProduktNieIstnieje_ZwracaNull()
    {
        var (serwis, repo, _, cache, _) = TworzSerwis();
        cache.Setup(c => c.Get<ProduktDto>(It.IsAny<string>())).Returns((ProduktDto?)null);
        repo.Setup(r => r.PobierzAsync(It.IsAny<int>())).ReturnsAsync((Produkt?)null);

        var wynik = await serwis.PobierzAsync(999);

        wynik.Should().BeNull();
    }

    [Fact]
    public async Task PobierzAsync_WCachu_NieWolujRepo()
    {
        var (serwis, repo, _, cache, _) = TworzSerwis();
        var dto = new ProduktDto(1, "Laptop z cache", 3500m, 10);
        cache.Setup(c => c.Get<ProduktDto>("produkt:1")).Returns(dto);

        var wynik = await serwis.PobierzAsync(1);

        wynik!.Nazwa.Should().Be("Laptop z cache");
        repo.Verify(r => r.PobierzAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DodajAsync_NazwaJuzIstnieje_RzucaInvalidOperation()
    {
        var (serwis, repo, _, _, _) = TworzSerwis();
        repo.Setup(r => r.NazwaIstniejeAsync("Laptop")).ReturnsAsync(true);

        Func<Task> akcja = () => serwis.DodajAsync(new NowyProduktDto("Laptop", 3500m, 1, 10));

        await akcja.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*już istnieje*");
    }

    [Fact]
    public async Task DodajAsync_NowaNazwa_DodajeDoBazy()
    {
        var (serwis, repo, email, _, _) = TworzSerwis();
        repo.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r => r.DodajAsync(It.IsAny<Produkt>())).Returns(Task.CompletedTask);
        email.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        var wynik = await serwis.DodajAsync(new NowyProduktDto("Monitor", 1500m, 1, 5));

        wynik.Nazwa.Should().Be("Monitor");
        repo.Verify(r => r.DodajAsync(It.IsAny<Produkt>()), Times.Once);
    }
}

// ============================================================
// 2. MOQ — SETUP ZAAWANSOWANY
// ============================================================

public class SetupZaawansowanyTests
{
    [Fact]
    public async Task ItIs_FiltrujePodstawyWarunku()
    {
        var mockRepo = new Mock<IProduktRepo>();
        mockRepo.Setup(r => r.PobierzAsync(It.Is<int>(id => id > 0)))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m });
        mockRepo.Setup(r => r.PobierzAsync(It.Is<int>(id => id <= 0)))
            .ReturnsAsync((Produkt?)null);

        var wynikDodatni = await mockRepo.Object.PobierzAsync(5);
        var wynikZero    = await mockRepo.Object.PobierzAsync(0);

        wynikDodatni.Should().NotBeNull();
        wynikZero.Should().BeNull();
    }

    [Fact]
    public async Task SetupSequence_RozneWartosciKolejnosc()
    {
        var mockRepo = new Mock<IProduktRepo>();
        mockRepo.SetupSequence(r => r.PobierzAsync(1))
            .ReturnsAsync((Produkt?)null)
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m })
            .ThrowsAsync(new Exception("Błąd połączenia"));

        var pierwsze = await mockRepo.Object.PobierzAsync(1);
        var drugie   = await mockRepo.Object.PobierzAsync(1);
        Func<Task> trzecie = () => mockRepo.Object.PobierzAsync(1);

        pierwsze.Should().BeNull();
        drugie.Should().NotBeNull();
        await trzecie.Should().ThrowAsync<Exception>().WithMessage("Błąd połączenia");
    }

    [Fact]
    public async Task Callback_PrzechwytaArgumenty()
    {
        var mockRepo = new Mock<IProduktRepo>();
        Produkt? zapisany = null;

        mockRepo.Setup(r => r.DodajAsync(It.IsAny<Produkt>()))
            .Callback<Produkt>(p => zapisany = p)
            .Returns(Task.CompletedTask);

        await mockRepo.Object.DodajAsync(new Produkt { Nazwa = "Monitor", Cena = 1500m });

        zapisany.Should().NotBeNull();
        zapisany!.Nazwa.Should().Be("Monitor");
    }

    [Fact]
    public void MockujeProperty()
    {
        var mockRepo = new Mock<IProduktRepoZWlasciwoscia>();
        mockRepo.Setup(r => r.CzyPolaczone).Returns(true);

        mockRepo.Object.CzyPolaczone.Should().BeTrue();
    }

    [Fact]
    public async Task ReturnsLambda_DynamicznyWynikPoId()
    {
        var mockRepo = new Mock<IProduktRepo>();
        mockRepo.Setup(r => r.PobierzAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => id > 0
                ? new Produkt { Id = id, Nazwa = $"Produkt {id}", Cena = id * 100m }
                : null);

        var p5 = await mockRepo.Object.PobierzAsync(5);
        p5.Should().NotBeNull();
        p5!.Nazwa.Should().Be("Produkt 5");
        p5.Cena.Should().Be(500m);
    }
}

// ============================================================
// 3. MOQ — VERIFY: Times, VerifyAll, MockBehavior.Strict
// ============================================================

public class VerifyTests
{
    [Fact]
    public async Task DodajAsync_WeryfikujeWywolania()
    {
        var repo   = new Mock<IProduktRepo>();
        var email  = new Mock<IEmailSerwis>();
        var cache  = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ProduktSerwis>>();

        repo.Setup(r  => r.NazwaIstniejeAsync(It.IsAny<string>())).ReturnsAsync(false);
        repo.Setup(r  => r.DodajAsync(It.IsAny<Produkt>())).Returns(Task.CompletedTask);
        email.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);

        var serwis = new ProduktSerwis(repo.Object, email.Object, cache.Object, logger.Object);
        await serwis.DodajAsync(new NowyProduktDto("Kamera", 800m, 1, 20));

        repo.Verify(r => r.DodajAsync(It.IsAny<Produkt>()), Times.Once);
        repo.Verify(r => r.NazwaIstniejeAsync("Kamera"), Times.Once);
        email.Verify(e => e.WyslijAsync("admin@sklep.pl",
            It.Is<string>(s => s.Contains("Kamera"))), Times.Once);
    }

    [Fact]
    public async Task PobierzAsync_NieWolajEmaila()
    {
        var repo   = new Mock<IProduktRepo>();
        var email  = new Mock<IEmailSerwis>();
        var cache  = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ProduktSerwis>>();

        cache.Setup(c => c.Get<ProduktDto>("produkt:1")).Returns((ProduktDto?)null);
        repo.Setup(r  => r.PobierzAsync(1))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m });

        var serwis = new ProduktSerwis(repo.Object, email.Object, cache.Object, logger.Object);
        await serwis.PobierzAsync(1);

        email.Verify(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAll_WszystkieSetupyWywolane()
    {
        var repo  = new Mock<IProduktRepo>();
        var email = new Mock<IEmailSerwis>();
        var cache = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ProduktSerwis>>();

        repo.Setup(r  => r.NazwaIstniejeAsync("X")).ReturnsAsync(false).Verifiable();
        repo.Setup(r  => r.DodajAsync(It.IsAny<Produkt>())).Returns(Task.CompletedTask).Verifiable();
        email.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask).Verifiable();

        var serwis = new ProduktSerwis(repo.Object, email.Object, cache.Object, logger.Object);
        await serwis.DodajAsync(new NowyProduktDto("X", 50m, 1, 1));

        repo.VerifyAll();
        email.VerifyAll();
    }

    [Fact]
    public async Task MockBehaviorStrict_NieskonfigurowaneWywolanieDajeWyjatek()
    {
        var mockRepo = new Mock<IProduktRepo>(MockBehavior.Strict);
        mockRepo.Setup(r => r.PobierzAsync(1))
            .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m });

        var skonfigurowany = await mockRepo.Object.PobierzAsync(1);
        skonfigurowany.Should().NotBeNull();

        Func<Task> nieskonfigurowany = () => mockRepo.Object.PobierzAsync(999);
        await nieskonfigurowany.Should().ThrowAsync<MockException>();
    }
}

// ============================================================
// 4. EF CORE IN-MEMORY — ProduktEFRepo pełne testy
// ============================================================

public class ProduktRepoInMemoryTests : IAsyncLifetime
{
    private SklepKontekst _db   = null!;
    private ProduktEFRepo _repo = null!;

    public async Task InitializeAsync()
    {
        _db   = TestDbContextFactory.Utworz();
        _repo = new ProduktEFRepo(_db);
        _db.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop",    Cena = 3500m, StanMagazyn = 10, Aktywny = true  },
            new Produkt { Id = 2, Nazwa = "Mysz",       Cena = 150m,  StanMagazyn = 50, Aktywny = true  },
            new Produkt { Id = 3, Nazwa = "Klawiatura", Cena = 250m,  StanMagazyn = 30, Aktywny = false }
        );
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task PobierzAsync_IstniejacyId_ZwracaProdukt()
    {
        var wynik = await _repo.PobierzAsync(1);
        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
    }

    [Fact]
    public async Task PobierzAsync_NieistniejacyId_ZwracaNull()
    {
        (await _repo.PobierzAsync(999)).Should().BeNull();
    }

    [Fact]
    public async Task DodajAsync_NowyProdukt_PersystujeWBazie()
    {
        var nowy = new Produkt { Nazwa = "Monitor", Cena = 1500m, StanMagazyn = 5, Aktywny = true };
        await _repo.DodajAsync(nowy);

        var pobrany = await _repo.PobierzAsync(nowy.Id);
        pobrany.Should().NotBeNull();
        pobrany!.Nazwa.Should().Be("Monitor");
    }

    [Fact]
    public async Task NazwaIstniejeAsync_IstniejacaNazwa_ZwracaTrue()
        => (await _repo.NazwaIstniejeAsync("Laptop")).Should().BeTrue();

    [Fact]
    public async Task NazwaIstniejeAsync_NieistniejacaNazwa_ZwracaFalse()
        => (await _repo.NazwaIstniejeAsync("Telewizor")).Should().BeFalse();

    [Fact]
    public async Task PobierzWszystkieAsync_ZwracaWszystkie()
        => (await _repo.PobierzWszystkieAsync()).Should().HaveCount(3);

    [Fact]
    public async Task UsunAsync_IstniejacyId_UsuwaZBazy()
    {
        await _repo.UsunAsync(1);
        (await _repo.PobierzAsync(1)).Should().BeNull();
    }
}

// ============================================================
// 5. COMBINED — InMemory repo + Moq dla zewnętrznych serwisów
// ============================================================

public interface ISmsSerwisM
{
    Task WyslijSmsAsync(string telefon, string wiadomosc);
}

public class ZamowienieSerwisImplM
{
    private readonly IProduktRepo _repo;
    private readonly IEmailSerwis _email;
    private readonly ISmsSerwisM  _sms;

    public ZamowienieSerwisImplM(IProduktRepo repo, IEmailSerwis email, ISmsSerwisM sms)
    { _repo = repo; _email = email; _sms = sms; }

    public async Task<decimal> ZlozAsync(
        int produktId, int ilosc, string email, string? telefon = null)
    {
        var produkt = await _repo.PobierzAsync(produktId)
            ?? throw new InvalidOperationException("Produkt nie istnieje");

        if (produkt.StanMagazyn < ilosc)
            throw new InvalidOperationException("Brak wystarczającego stanu magazynowego");

        decimal suma = produkt.Cena * ilosc;
        await _email.WyslijAsync(email, $"Zamówienie: {produkt.Nazwa} x{ilosc} = {suma:C}");

        if (telefon != null)
            await _sms.WyslijSmsAsync(telefon, $"Zamówienie złożone: {suma:C}");

        return suma;
    }
}

public class ZamowieniaSerwisTestsM : IAsyncLifetime
{
    private SklepKontekst         _db        = null!;
    private ProduktEFRepo         _repo      = null!;
    private Mock<IEmailSerwis>    _mockEmail = new();
    private Mock<ISmsSerwisM>     _mockSms   = new();
    private ZamowienieSerwisImplM _serwis    = null!;

    public async Task InitializeAsync()
    {
        _db   = TestDbContextFactory.Utworz();
        _repo = new ProduktEFRepo(_db);
        _db.Produkty.Add(
            new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m, StanMagazyn = 5, Aktywny = true });
        await _db.SaveChangesAsync();

        _mockEmail.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockSms.Setup(s => s.WyslijSmsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _serwis = new ZamowienieSerwisImplM(_repo, _mockEmail.Object, _mockSms.Object);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task ZlozAsync_DostepcyProdukt_ZwracaSume()
    {
        var suma = await _serwis.ZlozAsync(1, 2, "jan@test.pl");
        suma.Should().Be(7000m);
    }

    [Fact]
    public async Task ZlozAsync_ZTelefonem_WysylaSms()
    {
        await _serwis.ZlozAsync(1, 1, "jan@test.pl", "+48123456789");
        _mockSms.Verify(s => s.WyslijSmsAsync(
            "+48123456789", It.Is<string>(m => m.Contains("złożone"))), Times.Once);
    }

    [Fact]
    public async Task ZlozAsync_BezTelefonu_NieWysylaSms()
    {
        await _serwis.ZlozAsync(1, 1, "jan@test.pl");
        _mockSms.Verify(s => s.WyslijSmsAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ZlozAsync_NieprawidlowyProdukt_RzucaWyjatek()
    {
        Func<Task> akcja = () => _serwis.ZlozAsync(999, 1, "jan@test.pl");
        await akcja.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nie istnieje*");
    }

    [Fact]
    public async Task ZlozAsync_NiedostepnyStanMagazynu_RzucaWyjatek()
    {
        Func<Task> akcja = () => _serwis.ZlozAsync(1, 100, "jan@test.pl");
        await akcja.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*stan*");
    }
}

// ============================================================
// DEMO
// ============================================================

public static class MockowanieDemo
{
    public static void Uruchom()
    {
        Console.WriteLine("\n=== Mockowanie (Moq + EF InMemory) ===");

        Console.WriteLine("\n  Moq — pojęcia:");
        Console.WriteLine("    Setup()        — konfiguruje zwracaną wartość lub wyjątek");
        Console.WriteLine("    It.IsAny<T>()  — dowolna wartość danego typu");
        Console.WriteLine("    It.Is<T>(pred) — wartość spełniająca predykat");
        Console.WriteLine("    Returns/ReturnsAsync/ThrowsAsync — wynik zwracany przez mock");
        Console.WriteLine("    Callback()     — kod boczny przy wywołaniu (przechwyt argumentu)");
        Console.WriteLine("    SetupSequence  — różne wartości przy kolejnych wywołaniach");
        Console.WriteLine("    Verify/Times   — weryfikacja ile razy metoda została wywołana");
        Console.WriteLine("    VerifyAll()    — wszystkie .Verifiable() setupy wywołane");
        Console.WriteLine("    MockBehavior.Strict — nieznany setup = wyjątek");

        Console.WriteLine("\n-- Mock podstawy --");
        MiniTestRunner.Run<MockPodstawyTests>();

        Console.WriteLine("\n-- Setup zaawansowany (It.Is, Sequence, Callback, lambda) --");
        MiniTestRunner.Run<SetupZaawansowanyTests>();

        Console.WriteLine("\n-- Verify (Times, VerifyAll, Strict) --");
        MiniTestRunner.Run<VerifyTests>();

        Console.WriteLine("\n-- EF Core InMemory: ProduktEFRepo --");
        MiniTestRunner.Run<ProduktRepoInMemoryTests>();

        Console.WriteLine("\n-- Combined: InMemory + Moq --");
        MiniTestRunner.Run<ZamowieniaSerwisTestsM>();
    }
}
