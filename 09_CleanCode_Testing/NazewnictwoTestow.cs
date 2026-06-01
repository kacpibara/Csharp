using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace _09_CleanCode_Testing;

// ============================================================
// DOMENY DO TESTOWANIA
// ============================================================

public class ElementKoszyka
{
    public string  Nazwa { get; init; } = "";
    public decimal Cena  { get; init; }
    public int     Ilosc { get; init; }
}

public class KoszykZakupow
{
    private readonly List<ElementKoszyka> _elementy = new();

    public IReadOnlyList<ElementKoszyka> Elementy => _elementy.AsReadOnly();
    public decimal Suma    => _elementy.Sum(e => e.Cena * e.Ilosc);
    public bool    CzyPusty => !_elementy.Any();

    public void Dodaj(ElementKoszyka e)
    {
        if (e.Ilosc <= 0) throw new ArgumentException("Ilość musi być > 0");
        _elementy.Add(e);
    }

    public void Usun(string nazwa) => _elementy.RemoveAll(e => e.Nazwa == nazwa);
}

public record RejestracjaRequest(string Email, string Haslo, string PowtorzHaslo);

public class WynikRejestracji
{
    public bool   Sukces { get; init; }
    public string Blad   { get; init; } = "";
    public static WynikRejestracji Ok()              => new() { Sukces = true };
    public static WynikRejestracji NieUdana(string b) => new() { Sukces = false, Blad = b };
}

public class Uzytkownik
{
    public int    Id    { get; set; }
    public string Email { get; set; } = "";
    public string Haslo { get; set; } = "";
}

public interface IUzytkownikRepo
{
    Task<Uzytkownik?> PobierzPoEmailAsync(string email);
    Task              DodajAsync(Uzytkownik u);
}

public class InMemoryUzytkownikRepo : IUzytkownikRepo
{
    private readonly List<Uzytkownik> _users = new();
    public Task<Uzytkownik?> PobierzPoEmailAsync(string email)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email));
    public Task DodajAsync(Uzytkownik u) { _users.Add(u); return Task.CompletedTask; }
}

public class SerwisRejestracji
{
    private readonly IUzytkownikRepo _repo;
    public SerwisRejestracji(IUzytkownikRepo repo) => _repo = repo;

    public async Task<WynikRejestracji> ZarejestrujAsync(RejestracjaRequest req)
    {
        if (req.Haslo != req.PowtorzHaslo)
            return WynikRejestracji.NieUdana("Hasła nie są identyczne");
        if (req.Haslo.Length < 8)
            return WynikRejestracji.NieUdana("Hasło musi mieć min. 8 znaków");
        if (await _repo.PobierzPoEmailAsync(req.Email) != null)
            return WynikRejestracji.NieUdana("Email już zarejestrowany");

        // Symulacja BCrypt: "$2b$" + pierwsze 20 znaków hex SHA-256
        var hash = "$2b$" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(req.Haslo)))[..20];

        await _repo.DodajAsync(new Uzytkownik { Email = req.Email, Haslo = hash });
        return WynikRejestracji.Ok();
    }
}

// Agregat Zamowienie do demonstracji nested class tests + zdarzeń domenowych
public enum StatusZam { Szkic, Zlozone, Oplacone, Anulowane }

public abstract record ZdarzenieDom(DateTime Kiedy);
public record ZamowienieUtworzono2(Guid Id, Guid KlientId, DateTime Kiedy) : ZdarzenieDom(Kiedy);
public record ZamowienieOplacono2(Guid Id, decimal Kwota, DateTime Kiedy)  : ZdarzenieDom(Kiedy);
public record ZamowienieAnulowano2(Guid Id, string Powod, DateTime Kiedy)  : ZdarzenieDom(Kiedy);

public class PozycjaZam
{
    public Guid    ProduktId { get; init; }
    public string  Nazwa     { get; init; } = "";
    public decimal Cena      { get; init; }
    public int     Ilosc     { get; init; }
    public decimal Suma      => Cena * Ilosc;
}

public class Zamowienie2
{
    private readonly List<PozycjaZam>    _pozycje   = new();
    private readonly List<ZdarzenieDom> _zdarzenia  = new();

    public Guid                        Id        { get; private set; }
    public Guid                        KlientId  { get; private set; }
    public StatusZam                   Status    { get; private set; } = StatusZam.Szkic;
    public decimal                     Suma      => _pozycje.Sum(p => p.Suma);
    public IReadOnlyList<PozycjaZam>   Pozycje   => _pozycje.AsReadOnly();
    public IReadOnlyList<ZdarzenieDom> Zdarzenia => _zdarzenia.AsReadOnly();

    public static Zamowienie2 Utworz(Guid klientId)
    {
        var z = new Zamowienie2 { Id = Guid.NewGuid(), KlientId = klientId };
        z._zdarzenia.Add(new ZamowienieUtworzono2(z.Id, klientId, DateTime.UtcNow));
        return z;
    }

    public void DodajPozycje(PozycjaZam p)
    {
        if (Status != StatusZam.Szkic)
            throw new InvalidOperationException("Pozycje można dodawać tylko do szkicu");
        if (p.Ilosc <= 0)
            throw new ArgumentException("Ilość musi być > 0");
        _pozycje.Add(p);
    }

    public void Zloz()
    {
        if (Status != StatusZam.Szkic)
            throw new InvalidOperationException("Zamówienie nie jest w statusie Szkic");
        if (!_pozycje.Any())
            throw new InvalidOperationException("Zamówienie nie ma pozycji");
        Status = StatusZam.Zlozone;
    }

    public void Oplac(decimal kwota)
    {
        if (Status != StatusZam.Zlozone)
            throw new InvalidOperationException("Zamówienie nie zostało złożone");
        if (kwota < Suma)
            throw new ArgumentException("Kwota jest niewystarczająca");
        Status = StatusZam.Oplacone;
        _zdarzenia.Add(new ZamowienieOplacono2(Id, kwota, DateTime.UtcNow));
    }

    public void Anuluj(string powod)
    {
        if (Status == StatusZam.Oplacone)
            throw new InvalidOperationException("Nie można anulować opłaconego zamówienia");
        Status = StatusZam.Anulowane;
        _zdarzenia.Add(new ZamowienieAnulowano2(Id, powod, DateTime.UtcNow));
    }
}

// ============================================================
// KONWENCJA 1 — MethodName_Scenario_ExpectedResult
// Czytelna nazwa: CO testujemy _ kiedy (kontekst) _ czego oczekujemy
// ============================================================

public class NazewnictwoStyl1Tests
{
    [Fact]
    public void ObliczRabat_GdySumaPonizej1000_ZwracaZero()
        => new KalkulatorRabatu().ObliczRabat(500m).Should().Be(0m);

    [Fact]
    public void ObliczRabat_GdySumaRowna1000_Zwraca10Procent()
        => new KalkulatorRabatu().ObliczRabat(1000m).Should().Be(100m);

    [Fact]
    public void ObliczRabat_GdySumaPonad1000_Zwraca10Procent()
        => new KalkulatorRabatu().ObliczRabat(2000m).Should().Be(200m);

    [Fact]
    public void ObliczRabat_GdySumaUjemna_RzucaArgumentException()
    {
        Action akcja = () => new KalkulatorRabatu().ObliczRabat(-1m);
        akcja.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(999.99, false)]
    [InlineData(1000.00, true)]
    [InlineData(5000.00, true)]
    public void CzyPrzysługuje_RozneSumy_ZwracaOczekiwany(decimal suma, bool oczekiwany)
        => new KalkulatorRabatu().CzyPrzysługuje(suma).Should().Be(oczekiwany);
}

// ============================================================
// KONWENCJA 2 — Should_[Behavior]_When_[Condition]
// Czyta się jak zdanie: "serwis powinien X gdy Y"
// ============================================================

public class NazewnictwoStyl2Tests
{
    [Fact]
    public void Should_BeEmpty_When_NoElementsAdded()
        => new KoszykZakupow().CzyPusty.Should().BeTrue();

    [Fact]
    public void Should_ContainElement_When_ElementAdded()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka { Nazwa = "Laptop", Cena = 3500m, Ilosc = 1 });
        koszyk.Elementy.Should().HaveCount(1);
        koszyk.CzyPusty.Should().BeFalse();
    }

    [Fact]
    public void Should_ThrowArgumentException_When_IloscIsZeroOrNegative()
    {
        var koszyk = new KoszykZakupow();
        Action zero     = () => koszyk.Dodaj(new ElementKoszyka { Ilosc = 0  });
        Action ujemna   = () => koszyk.Dodaj(new ElementKoszyka { Ilosc = -1 });
        zero.Should().Throw<ArgumentException>();
        ujemna.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_RemoveElement_When_UsunCalledWithMatchingName()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka { Nazwa = "Laptop", Cena = 3500m, Ilosc = 1 });
        koszyk.Usun("Laptop");
        koszyk.CzyPusty.Should().BeTrue();
    }

    [Fact]
    public void Should_CalculateSumCorrectly_When_MultipleElementsPresent()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka { Nazwa = "Laptop",  Cena = 3500m, Ilosc = 1 });
        koszyk.Dodaj(new ElementKoszyka { Nazwa = "Mysz",    Cena = 150m,  Ilosc = 2 });
        koszyk.Dodaj(new ElementKoszyka { Nazwa = "Torba",   Cena = 200m,  Ilosc = 1 });
        koszyk.Suma.Should().Be(3500m + 300m + 200m); // 4000
    }
}

// ============================================================
// KONWENCJA 3 — Given_When_Then (BDD / Gherkin)
// Trójstopniowa struktura — idealna dla złożonej logiki
// ============================================================

public class NazewnictwoStyl3Tests
{
    private SerwisRejestracji TworzSerwis(IUzytkownikRepo? repo = null)
        => new SerwisRejestracji(repo ?? new InMemoryUzytkownikRepo());

    [Fact]
    public async Task Given_ValidRequest_When_Registering_Then_ReturnsSuccess()
    {
        var serwis = TworzSerwis();
        var req    = new RejestracjaRequest("jan@test.pl", "TajneHaslo1!", "TajneHaslo1!");

        var wynik = await serwis.ZarejestrujAsync(req);

        wynik.Sukces.Should().BeTrue();
        wynik.Blad.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_DifferentPasswords_When_Registering_Then_ReturnsFailed()
    {
        var serwis = TworzSerwis();
        var req    = new RejestracjaRequest("jan@test.pl", "Haslo1!", "Haslo2!");

        var wynik = await serwis.ZarejestrujAsync(req);

        wynik.Sukces.Should().BeFalse();
        wynik.Blad.Should().Contain("identyczne");
    }

    [Fact]
    public async Task Given_TooShortPassword_When_Registering_Then_ReturnsFailed()
    {
        var serwis = TworzSerwis();
        var req    = new RejestracjaRequest("jan@test.pl", "Abc1!", "Abc1!");

        var wynik = await serwis.ZarejestrujAsync(req);

        wynik.Sukces.Should().BeFalse();
        wynik.Blad.Should().Contain("8 znaków");
    }

    [Fact]
    public async Task Given_EmailAlreadyRegistered_When_Registering_Then_ReturnsFailed()
    {
        var repo = new InMemoryUzytkownikRepo();
        await repo.DodajAsync(new Uzytkownik { Email = "jan@test.pl", Haslo = "hash" });
        var serwis = TworzSerwis(repo);
        var req    = new RejestracjaRequest("jan@test.pl", "TajneHaslo1!", "TajneHaslo1!");

        var wynik = await serwis.ZarejestrujAsync(req);

        wynik.Sukces.Should().BeFalse();
        wynik.Blad.Should().Contain("zarejestrowany");
    }

    [Fact]
    public async Task Given_ValidRequest_When_Registering_Then_PasswordIsHashedWithBcryptPrefix()
    {
        var repo   = new InMemoryUzytkownikRepo();
        var serwis = TworzSerwis(repo);
        var req    = new RejestracjaRequest("jan@test.pl", "TajneHaslo1!", "TajneHaslo1!");

        await serwis.ZarejestrujAsync(req);

        var user = await repo.PobierzPoEmailAsync("jan@test.pl");
        user!.Haslo.Should().NotBe("TajneHaslo1!");
        user.Haslo.Should().StartWith("$2");
    }
}

// ============================================================
// KONWENCJA 4 — Zagnieżdżone klasy (nested class pattern)
// Grupują testy wg metody/scenariusza — najczytelniejsza struktura
// ============================================================

public class ProduktSerwisTests
{
    protected readonly Mock<IProduktRepo>           RepoMock   = new();
    protected readonly Mock<IEmailSerwis>           EmailMock  = new();
    protected readonly Mock<ICacheService>          CacheMock  = new();
    protected readonly Mock<ILogger<ProduktSerwis>> LoggerMock = new();
    protected readonly ProduktSerwis                Serwis;

    public ProduktSerwisTests()
    {
        Serwis = new ProduktSerwis(
            RepoMock.Object, EmailMock.Object, CacheMock.Object, LoggerMock.Object);
    }

    public class PobierzAsync : ProduktSerwisTests
    {
        [Fact]
        public async Task GdyProduktIstnieje_ZwracaDto()
        {
            CacheMock.Setup(c => c.Get<ProduktDto>("produkt:1")).Returns((ProduktDto?)null);
            RepoMock.Setup(r => r.PobierzAsync(1))
                .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m, StanMagazyn = 10 });

            var wynik = await Serwis.PobierzAsync(1);

            wynik.Should().NotBeNull();
            wynik!.Nazwa.Should().Be("Laptop");
        }

        [Fact]
        public async Task GdyProduktNieIstnieje_ZwracaNull()
        {
            CacheMock.Setup(c => c.Get<ProduktDto>(It.IsAny<string>())).Returns((ProduktDto?)null);
            RepoMock.Setup(r => r.PobierzAsync(It.IsAny<int>())).ReturnsAsync((Produkt?)null);

            var wynik = await Serwis.PobierzAsync(999);
            wynik.Should().BeNull();
        }

        [Fact]
        public async Task GdyWCachu_NieWolaRepo()
        {
            var cachedDto = new ProduktDto(1, "Laptop (cache)", 3500m, 10);
            CacheMock.Setup(c => c.Get<ProduktDto>("produkt:1")).Returns(cachedDto);

            var wynik = await Serwis.PobierzAsync(1);

            wynik!.Nazwa.Should().Contain("cache");
            RepoMock.Verify(r => r.PobierzAsync(It.IsAny<int>()), Times.Never);
        }
    }

    public class DodajAsync : ProduktSerwisTests
    {
        public DodajAsync()
        {
            EmailMock.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            RepoMock.Setup(r => r.DodajAsync(It.IsAny<Produkt>())).Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task GdyNazwaJuzIstnieje_RzucaInvalidOperation()
        {
            RepoMock.Setup(r => r.NazwaIstniejeAsync("Istniejacy")).ReturnsAsync(true);

            Func<Task> akcja = () => Serwis.DodajAsync(new NowyProduktDto("Istniejacy", 100m, 1, 1));
            await akcja.Should().ThrowAsync<InvalidOperationException>().WithMessage("*już istnieje*");
        }

        [Fact]
        public async Task GdyNowaNazwa_ZapisujeProdukt()
        {
            RepoMock.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>())).ReturnsAsync(false);

            await Serwis.DodajAsync(new NowyProduktDto("Nowy", 200m, 1, 5));

            RepoMock.Verify(r => r.DodajAsync(It.IsAny<Produkt>()), Times.Once);
        }

        [Fact]
        public async Task GdyNowaNazwa_WysylaEmailDoAdmina()
        {
            RepoMock.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>())).ReturnsAsync(false);

            await Serwis.DodajAsync(new NowyProduktDto("NowyP", 300m, 1, 10));

            EmailMock.Verify(e => e.WyslijAsync(
                "admin@sklep.pl", It.Is<string>(s => s.Contains("NowyP"))), Times.Once);
        }
    }
}

// ============================================================
// NESTED CLASS TESTS — Agregat Zamowienie2
// ============================================================

public class ZamowienieAggregatTests
{
    protected static Guid KlientId = Guid.NewGuid();
    protected static Guid ProdId   = Guid.NewGuid();

    protected static PozycjaZam NowaPozycja(int ilosc = 2)
        => new() { ProduktId = ProdId, Nazwa = "Laptop", Cena = 1000m, Ilosc = ilosc };

    public class Utworz : ZamowienieAggregatTests
    {
        [Fact]
        public void ZwracaZamowienieZStatusemSzkic()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.Status.Should().Be(StatusZam.Szkic);
        }

        [Fact]
        public void ZamowieniePosiada_KlientId()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.KlientId.Should().Be(KlientId);
        }

        [Fact]
        public void PustePozycje_PoCzasieUtworzenia()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.Pozycje.Should().BeEmpty();
        }
    }

    public class DodajPozycje : ZamowienieAggregatTests
    {
        [Fact]
        public void GdySzkic_DodajePozycje()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja());
            zam.Pozycje.Should().HaveCount(1);
        }

        [Fact]
        public void GdyZlozone_RzucaWyjatek()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja());
            zam.Zloz();

            Action akcja = () => zam.DodajPozycje(NowaPozycja());
            akcja.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void GdyIloscZero_RzucaArgumentException()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            Action akcja = () => zam.DodajPozycje(NowaPozycja(0));
            akcja.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ObliczaSume_Poprawnie()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja(2)); // 1000 * 2 = 2000
            zam.DodajPozycje(NowaPozycja(3)); // 1000 * 3 = 3000
            zam.Suma.Should().Be(5000m);
        }
    }

    public class Zloz : ZamowienieAggregatTests
    {
        [Fact]
        public void GdySzkicZPozycjami_ZmieniStatusNaZlozone()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja());
            zam.Zloz();
            zam.Status.Should().Be(StatusZam.Zlozone);
        }

        [Fact]
        public void GdySzkicBezPozycji_RzucaWyjatek()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            Action akcja = () => zam.Zloz();
            akcja.Should().Throw<InvalidOperationException>().WithMessage("*pozycji*");
        }

        [Fact]
        public void GdyJuzZlozone_RzucaWyjatek()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja());
            zam.Zloz();
            Action akcja = () => zam.Zloz();
            akcja.Should().Throw<InvalidOperationException>();
        }
    }

    public class ZdarzeniaDomenowe : ZamowienieAggregatTests
    {
        [Fact]
        public void Utworz_GenerujeZdarzenieDomenowe_Utworzono()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.Zdarzenia.Should().ContainSingle();
            zam.Zdarzenia[0].Should().BeOfType<ZamowienieUtworzono2>();
        }

        [Fact]
        public void Oplac_GenerujeZdarzenieDomenowe_Oplacono()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja(1)); // suma = 1000
            zam.Zloz();
            zam.Oplac(1000m);

            var oplacono = zam.Zdarzenia.OfType<ZamowienieOplacono2>().SingleOrDefault();
            oplacono.Should().NotBeNull();
            oplacono!.Kwota.Should().Be(1000m);
        }

        [Fact]
        public void Anuluj_GenerujeZdarzenieDomenowe_Anulowano()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.Anuluj("Test anulowania");

            var anulowane = zam.Zdarzenia.OfType<ZamowienieAnulowano2>().SingleOrDefault();
            anulowane.Should().NotBeNull();
            anulowane!.Powod.Should().Be("Test anulowania");
        }

        [Fact]
        public void AnulowanieOplaconegoZamowienia_RzucaWyjatek()
        {
            var zam = Zamowienie2.Utworz(KlientId);
            zam.DodajPozycje(NowaPozycja(1));
            zam.Zloz();
            zam.Oplac(1000m);

            Action akcja = () => zam.Anuluj("powód");
            akcja.Should().Throw<InvalidOperationException>().WithMessage("*anulować*");
        }
    }
}

// ============================================================
// DEMO
// ============================================================

public static class NazewnictwoTestowDemo
{
    public static void Uruchom()
    {
        Console.WriteLine("\n=== Nazewnictwo Testów ===");
        Console.WriteLine("  Styl 1: MethodName_Scenario_ExpectedResult  (klasyczny MSTest/NUnit)");
        Console.WriteLine("  Styl 2: Should_[Behavior]_When_[Condition]  (xUnit fluent)");
        Console.WriteLine("  Styl 3: Given_When_Then                     (BDD/Gherkin)");
        Console.WriteLine("  Styl 4: Zagnieżdżone klasy                  (grupowanie po metodzie)");

        Console.WriteLine("\n-- Styl 1: MethodName_Scenario_Result --");
        MiniTestRunner.Run<NazewnictwoStyl1Tests>();

        Console.WriteLine("\n-- Styl 2: Should_When --");
        MiniTestRunner.Run<NazewnictwoStyl2Tests>();

        Console.WriteLine("\n-- Styl 3: Given_When_Then (BCrypt sim) --");
        MiniTestRunner.Run<NazewnictwoStyl3Tests>();

        Console.WriteLine("\n-- Styl 4a: ProduktSerwisTests.PobierzAsync (nested) --");
        MiniTestRunner.Run<ProduktSerwisTests.PobierzAsync>();

        Console.WriteLine("\n-- Styl 4b: ProduktSerwisTests.DodajAsync (nested) --");
        MiniTestRunner.Run<ProduktSerwisTests.DodajAsync>();

        Console.WriteLine("\n-- Styl 4c: ZamowienieAggregatTests (nested x4) --");
        MiniTestRunner.Run<ZamowienieAggregatTests.Utworz>();
        MiniTestRunner.Run<ZamowienieAggregatTests.DodajPozycje>();
        MiniTestRunner.Run<ZamowienieAggregatTests.Zloz>();
        MiniTestRunner.Run<ZamowienieAggregatTests.ZdarzeniaDomenowe>();
    }
}
