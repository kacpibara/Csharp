### Strategie Nazewnictwa Testów w C#

Dobra nazwa testu to **dokumentacja zachowania** — czytając nazwę wiesz co robi system bez czytania kodu.

---

### 1. Dlaczego nazewnictwo ma znaczenie

csharp

```csharp
// ŹLE — nazwy które nic nie mówią
[Fact] public void Test1() { }
[Fact] public void TestDodaj() { }
[Fact] public void WalidacjaTest() { }
[Fact] public void Zamowienie_Test_1() { }

// ŹLE — nazwy opisują implementację, nie zachowanie
[Fact] public void WywolajDodajZKlientemIdRownym5() { }
[Fact] public void SprawdzCzyNazwaJestEmpty() { }

// DOBRZE — czytam nazwę i wiem CO system robi
[Fact] public void DodajDoKoszyka_ProduktDostepny_ZwiekszyLiczbeElementow() { }
[Fact] public void ZlozZamowienie_PustyKoszyk_RzucaArgumentException() { }
[Fact] public void Rejestracja_EmailJuzUzywany_ZwracaConflict() { }

// Wartość dobrego nazewnictwa:
// 1. Test jako dokumentacja — czytasz nazwę = rozumiesz wymaganie
// 2. Raport z testów jest czytelny bez zaglądania do kodu
// 3. Szybka diagnostyka gdy test nie przechodzi
// 4. Łatwiej napisać test gdy najpierw piszesz jego nazwę (TDD)
```

---

### 2. Konwencja: MethodName_Scenario_ExpectedResult

csharp

```csharp
// Format: NazwaMetody_Warunek_OczekiwanyWynik
// Najpopularniejsza konwencja w C# — prosta, czytelna

public class KalkulatorRabatuTests
{
    // Schemat: Co_testujemy_Warunek_Wynik
    [Fact]
    public void ObliczRabat_SumaPonad1000_Zwraca10Procent()
    {
        var kalk = new KalkulatorRabatu();
        kalk.ObliczRabat(1200m).Should().Be(120m);
    }

    [Fact]
    public void ObliczRabat_SumaPonizej1000_Zwraca0()
    {
        var kalk = new KalkulatorRabatu();
        kalk.ObliczRabat(999m).Should().Be(0m);
    }

    [Fact]
    public void ObliczRabat_SumaRowna1000_Zwraca10Procent()
    {
        var kalk = new KalkulatorRabatu();
        kalk.ObliczRabat(1000m).Should().Be(100m);  // granica włącznie
    }

    [Fact]
    public void ObliczRabat_SumaZero_Zwraca0()
    {
        var kalk = new KalkulatorRabatu();
        kalk.ObliczRabat(0m).Should().Be(0m);
    }

    [Fact]
    public void ObliczRabat_SumaUjemna_RzucaArgumentException()
    {
        var kalk = new KalkulatorRabatu();
        Action akcja = () => kalk.ObliczRabat(-100m);
        akcja.Should().Throw<ArgumentException>()
            .WithMessage("*ujemna*");
    }
}

// Format dla async: DodajAsync_Warunek_Wynik
public class ZamowieniaServisTests
{
    [Fact]
    public async Task ZlozZamowienieAsync_PoprawnyKlient_ZwracaNoweId()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienieAsync_NieistniejacyKlient_RzucaKeyNotFoundException()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienieAsync_ProduktyNiedostepne_RzucaInvalidOperationException()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienieAsync_PomyslaWeZlozeniu_WysylaEmailPotwierdzenia()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienieAsync_PomyslaWeZlozeniu_ZapisywajeDoRepozytorium()
    {
        // ...
    }
}

public class KalkulatorRabatu
{
    public decimal ObliczRabat(decimal suma)
    {
        if (suma < 0)
            throw new ArgumentException("Suma nie może być ujemna");
        return suma >= 1000m ? suma * 0.1m : 0m;
    }
}
```

---

### 3. Konwencja: Should_ExpectedBehavior_When_Condition

csharp

```csharp
// Format: Should_OczekiwaneZachowanie_When_Warunek
// BDD-inspired — bardziej narracyjne, skupia się na zachowaniu

public class KoszykZakupowTests
{
    // Should + zachowanie + When + kontekst
    [Fact]
    public void Should_ZwrocićSumę_When_KoszykMaElementy()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka("Laptop", 3500m));
        koszyk.Dodaj(new ElementKoszyka("Mysz", 150m));

        koszyk.ObliczSume().Should().Be(3650m);
    }

    [Fact]
    public void Should_ZwrócićZero_When_KoszykJestPusty()
    {
        var koszyk = new KoszykZakupow();

        koszyk.ObliczSume().Should().Be(0m);
    }

    [Fact]
    public void Should_ZastosowacRabat10Procent_When_SumaPrzekracza1000()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka("Laptop", 1200m));

        decimal sumaPoRabacie = koszyk.ObliczSumeZRabatem();

        sumaPoRabacie.Should().Be(1080m);
    }

    [Fact]
    public void Should_RzucicArgumentException_When_DodajesNullElement()
    {
        var koszyk = new KoszykZakupow();

        Action akcja = () => koszyk.Dodaj(null!);

        akcja.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_ZwrocicPoprawnaliczbe_When_DodajesKilkaElementow()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka("A", 100m));
        koszyk.Dodaj(new ElementKoszyka("B", 200m));
        koszyk.Dodaj(new ElementKoszyka("C", 300m));

        koszyk.LiczbaElementow.Should().Be(3);
    }

    [Fact]
    public void Should_ByćPusty_When_WyczyscisKoszyk()
    {
        var koszyk = new KoszykZakupow();
        koszyk.Dodaj(new ElementKoszyka("A", 100m));

        koszyk.Wyczysc();

        koszyk.LiczbaElementow.Should().Be(0);
        koszyk.ObliczSume().Should().Be(0m);
    }
}

// Klasy pomocnicze
public class KoszykZakupow
{
    private readonly List<ElementKoszyka> _elementy = new();

    public int LiczbaElementow => _elementy.Count;

    public void Dodaj(ElementKoszyka element)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        _elementy.Add(element);
    }

    public decimal ObliczSume() => _elementy.Sum(e => e.Cena);

    public decimal ObliczSumeZRabatem()
    {
        decimal suma = ObliczSume();
        return suma >= 1000m ? suma * 0.9m : suma;
    }

    public void Wyczysc() => _elementy.Clear();
}

public record ElementKoszyka(string Nazwa, decimal Cena);
```

---

### 4. Konwencja: Given_When_Then — pełne BDD

csharp

```csharp
// Given_When_Then — Behavior Driven Development
// Given = stan początkowy (Arrange)
// When  = akcja (Act)
// Then  = oczekiwany wynik (Assert)

public class UzytkownikRejestracja_GivenWhenThenTests
{
    [Fact]
    public async Task
        Given_NowyUzytkownik_When_Rejestruje_Then_KontoZostajUtwrzone()
    {
        // Given
        var serwis = new SerwisRejestracji(new InMemoryUzytkownikRepo());
        var request = new RejestracjaRequest("jan@test.pl", "Tajne123!");

        // When
        var wynik = await serwis.ZarejestujAsync(request);

        // Then
        wynik.Sukces.Should().BeTrue();
        wynik.UzytkownikId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task
        Given_IstniejacyEmail_When_Rejestruje_Then_BladEmailZajety()
    {
        // Given
        var repo = new InMemoryUzytkownikRepo();
        await repo.DodajAsync(new Uzytkownik("jan@test.pl"));
        var serwis = new SerwisRejestracji(repo);

        // When
        var wynik = await serwis.ZarejestujAsync(
            new RejestracjaRequest("jan@test.pl", "Inne123!"));

        // Then
        wynik.Sukces.Should().BeFalse();
        wynik.Blad.Should().Contain("Email");
        wynik.Blad.Should().Contain("zajęty");
    }

    [Fact]
    public async Task
        Given_SlabieHaslo_When_Rejestruje_Then_BladWalidacjiHasla()
    {
        // Given
        var serwis = new SerwisRejestracji(new InMemoryUzytkownikRepo());

        // When
        var wynik = await serwis.ZarejestujAsync(
            new RejestracjaRequest("jan@test.pl", "123"));

        // Then
        wynik.Sukces.Should().BeFalse();
        wynik.Blad.Should().Contain("hasło");
    }

    [Fact]
    public async Task
        Given_PoprawneHaslo_When_Rejestruje_Then_HasloZostaleZahashowane()
    {
        // Given
        var repo = new InMemoryUzytkownikRepo();
        var serwis = new SerwisRejestracji(repo);

        // When
        await serwis.ZarejestujAsync(
            new RejestracjaRequest("jan@test.pl", "Tajne123!"));

        // Then
        var user = await repo.FindByEmailAsync("jan@test.pl");
        user.Should().NotBeNull();
        user!.HashHasla.Should().NotBe("Tajne123!");  // zahashowane!
        user.HashHasla.Should().StartWith("$2");       // BCrypt prefix
    }
}

// Klasy pomocnicze
public record RejestracjaRequest(string Email, string Haslo);
public record WynikRejestracji(bool Sukces, int UzytkownikId = 0, string? Blad = null);

public class Uzytkownik
{
    public int    Id       { get; set; }
    public string Email    { get; set; }
    public string HashHasla{ get; set; } = "";
    public Uzytkownik(string email) => Email = email;
}

public interface IUzytkownikRepo
{
    Task<Uzytkownik?> FindByEmailAsync(string email);
    Task<int> DodajAsync(Uzytkownik u);
}

public class InMemoryUzytkownikRepo : IUzytkownikRepo
{
    private readonly List<Uzytkownik> _dane = new();
    private int _nextId = 1;

    public Task<Uzytkownik?> FindByEmailAsync(string email)
        => Task.FromResult(_dane.FirstOrDefault(u => u.Email == email));

    public Task<int> DodajAsync(Uzytkownik u)
    {
        u.Id = _nextId++;
        _dane.Add(u);
        return Task.FromResult(u.Id);
    }
}

public class SerwisRejestracji
{
    private readonly IUzytkownikRepo _repo;

    public SerwisRejestracji(IUzytkownikRepo repo) => _repo = repo;

    public async Task<WynikRejestracji> ZarejestujAsync(RejestracjaRequest req)
    {
        if (req.Haslo.Length < 8)
            return new WynikRejestracji(false, Blad: "Hasło za krótkie");

        var istniejacy = await _repo.FindByEmailAsync(req.Email);
        if (istniejacy != null)
            return new WynikRejestracji(false, Blad: "Email zajęty");

        var user = new Uzytkownik(req.Email)
        {
            HashHasla = BCrypt.Net.BCrypt.HashPassword(req.Haslo)
        };

        int id = await _repo.DodajAsync(user);
        return new WynikRejestracji(true, id);
    }
}
```

---

### 5. Nested Classes — najlepsza organizacja

csharp

```csharp
// Nested classes grupują testy per metoda/scenariusz
// Raport testów jest hierarchiczny i czytelny

public class ProduktSerwisTests
{
    // Fixture współdzielona — inicjalizacja w konstruktorze
    private readonly Mock<IProduktRepo>  _repoMock;
    private readonly Mock<IEmailSerwis>  _emailMock;
    private readonly ProduktSerwis       _serwis;

    public ProduktSerwisTests()
    {
        _repoMock  = new Mock<IProduktRepo>();
        _emailMock = new Mock<IEmailSerwis>();
        _emailMock.Setup(e => e.WyslijAsync(
            It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _serwis = new ProduktSerwis(
            _repoMock.Object, _emailMock.Object);
    }

    // === NESTED CLASS dla metody PobierzAsync ===
    public class PobierzAsync : ProduktSerwisTests
    {
        [Fact]
        public async Task ZwracaProdukt_GdyIdIstnieje()
        {
            _repoMock.Setup(r => r.FindAsync(1, default))
                .ReturnsAsync(new Produkt { Id = 1, Nazwa = "Laptop" });

            var wynik = await _serwis.PobierzAsync(1);

            wynik.Should().NotBeNull();
            wynik!.Nazwa.Should().Be("Laptop");
        }

        [Fact]
        public async Task ZwracaNull_GdyIdNieIstnieje()
        {
            _repoMock.Setup(r => r.FindAsync(99, default))
                .ReturnsAsync((Produkt?)null);

            var wynik = await _serwis.PobierzAsync(99);

            wynik.Should().BeNull();
        }

        [Fact]
        public async Task ZwracaZCache_GdyCacheHit()
        {
            // Arrange — cache zwraca wynik
            var cached = new ProduktDto(1, "Cached", 100m);
            // (uproszczone)

            // Test cache hit
        }
    }

    // === NESTED CLASS dla metody DodajAsync ===
    public class DodajAsync : ProduktSerwisTests
    {
        [Fact]
        public async Task ZwracaNoweId_GdyDanePoprawne()
        {
            _repoMock.Setup(r => r.NazwaIstniejeAsync("Laptop", default))
                .ReturnsAsync(false);
            _repoMock.Setup(r => r.DodajAsync(It.IsAny<Produkt>(), default))
                .ReturnsAsync(42);

            int id = await _serwis.DodajAsync(
                new NowyProduktDto("Laptop", 3500m));

            id.Should().Be(42);
        }

        [Fact]
        public async Task RzucaArgumentException_GdyNazwaPusta()
        {
            Func<Task> akcja = () =>
                _serwis.DodajAsync(new NowyProduktDto("", 100m));

            await akcja.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Nazwa*");
        }

        [Fact]
        public async Task RzucaInvalidOperationException_GdyNazwaZajeta()
        {
            _repoMock.Setup(r => r.NazwaIstniejeAsync("Laptop", default))
                .ReturnsAsync(true);  // nazwa zajęta!

            Func<Task> akcja = () =>
                _serwis.DodajAsync(new NowyProduktDto("Laptop", 100m));

            await akcja.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*już istnieje*");
        }

        [Fact]
        public async Task WysylaEmailAdminowi_GdyDodaniePomyslne()
        {
            _repoMock.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>(), default))
                .ReturnsAsync(false);
            _repoMock.Setup(r => r.DodajAsync(It.IsAny<Produkt>(), default))
                .ReturnsAsync(1);

            await _serwis.DodajAsync(new NowyProduktDto("Tablet", 2000m));

            _emailMock.Verify(
                e => e.WyslijAsync(
                    "admin@sklep.pl",
                    It.Is<string>(s => s.Contains("Tablet"))),
                Times.Once);
        }

        [Fact]
        public async Task NieWysylaEmail_GdyDodanieNieudane()
        {
            _repoMock.Setup(r => r.NazwaIstniejeAsync(It.IsAny<string>(), default))
                .ReturnsAsync(true);  // zajęta — nie doda

            try
            {
                await _serwis.DodajAsync(new NowyProduktDto("X", 100m));
            }
            catch { }

            _emailMock.Verify(
                e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
    }

    // Raport xUnit będzie wyglądał:
    // ProduktSerwisTests
    //   PobierzAsync
    //     ZwracaProdukt_GdyIdIstnieje         ✅
    //     ZwracaNull_GdyIdNieIstnieje          ✅
    //     ZwracaZCache_GdyCacheHit             ✅
    //   DodajAsync
    //     ZwracaNoweId_GdyDanePoprawne         ✅
    //     RzucaArgumentException_GdyNazwaPusta ✅
    //     RzucaInvalidException_GdyNazwaZajeta ✅
    //     WysylaEmailAdminowi_GdyDodaniePomysle✅
    //     NieWysylaEmail_GdyDodanieNieudane    ✅
}

// Stub serwis dla kompilacji
public class ProduktSerwis
{
    private readonly IProduktRepo _repo;
    private readonly IEmailSerwis _email;

    public ProduktSerwis(IProduktRepo repo, IEmailSerwis email)
    { _repo = repo; _email = email; }

    public async Task<ProduktDto?> PobierzAsync(int id, CancellationToken ct = default)
    {
        var p = await _repo.FindAsync(id, ct);
        return p == null ? null : new ProduktDto(p.Id, p.Nazwa, p.Cena);
    }

    public async Task<int> DodajAsync(NowyProduktDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(dto.Nazwa))
            throw new ArgumentException("Nazwa jest wymagana");

        if (await _repo.NazwaIstniejeAsync(dto.Nazwa, ct))
            throw new InvalidOperationException($"Produkt '{dto.Nazwa}' już istnieje");

        int id = await _repo.DodajAsync(new Produkt { Nazwa = dto.Nazwa, Cena = dto.Cena }, ct);
        await _email.WyslijAsync("admin@sklep.pl", $"Dodano: {dto.Nazwa}");
        return id;
    }
}
```

---

### 6. Porównanie konwencji i kiedy co używać

csharp

```csharp
// Ten sam test — cztery konwencje

// 1. MethodName_Scenario_ExpectedResult (najpopularniejsza w .NET)
[Fact]
public void ZlozZamowienie_PustyKoszyk_RzucaArgumentException() { }

// 2. Should_ExpectedBehavior_When_Condition (BDD-lite)
[Fact]
public void Should_RzucicArgumentException_When_KoszykJestPusty() { }

// 3. Given_When_Then (pełne BDD)
[Fact]
public void Given_PustyKoszyk_When_ZlozZamowienie_Then_RzucaArgumentException() { }

// 4. DisplayName — własna czytelna nazwa przez atrybut
[Fact(DisplayName = "Nie można złożyć zamówienia gdy koszyk jest pusty")]
public void ZlozenieNiemozliwe_GdyKoszykPusty() { }

// KIEDY CO:
// MethodName_Scenario_Result — domyślna, prosta, szeroko używana w .NET
// Should_When               — gdy klasa testowa skupia się na jednym obiekcie
//                            (Fluent API, CQRS command/query)
// Given_When_Then           — gdy pracujesz z BDD, Cucumber, Gherkin
// DisplayName               — gdy nazwa jest zbyt długa lub zawiera spacje/polskie znaki

// ZASADY OGÓLNE:
// 1. Czytasz nazwę → wiesz co testuje BEZ czytania kodu
// 2. Gdy test nie przejdzie → nazwa mówi co się złamało
// 3. Unikaj technicznego żargonu → "ZwracaNull" zamiast "ReturnsNullReference"
// 4. Opisuj ZACHOWANIE nie implementację → "RzucaWyjatek" nie "WywolujeThrow"
// 5. Jeden test = jeden warunek = jedna asercja konceptualnie

// === ZŁE nazwy → DOBRE nazwy ===
// Test1                     → PobierzProdukt_IstniejaceId_ZwracaProdukt
// WeryfikujEmail             → WalidujEmail_EmailBezMalpy_ZwracaFalse
// KlientTest                 → DodajKlienta_DuplicatEmail_RzucaConflictException
// HandleException            → PrzetworzPlatnosc_BrakFunduszow_ZwracaOdmowe
// NullPointerScenario        → PobierzZamowienie_IdRowneNull_RzucaArgumentNullException
```

---

### 7. Praktyczny przykład — kompletna klasa testowa

csharp

```csharp
// Kompletna klasa testowa z mieszanymi konwencjami i pełną organizacją

public class ZamowienieAggregatTests
{
    // Fabryka danych testowych
    private static Zamowienie2 TworzZamowienieZPozycjami(int liczbaPozycji = 1)
    {
        var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");
        for (int i = 1; i <= liczbaPozycji; i++)
            z.DodajPozycje(Guid.NewGuid(), $"Produkt {i}", 100m * i, 1);
        return z;
    }

    // === TWORZENIE ===
    public class Utworz : ZamowienieAggregatTests
    {
        [Fact]
        public void Given_PoprawnyKlient_When_Tworze_Then_StatusJestSzkic()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            z.Status.Should().Be(StatusZam.Szkic);
        }

        [Fact]
        public void Given_PoprawnyKlient_When_Tworze_Then_PozycjeSaPuste()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            z.Pozycje.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("nie-email")]
        [InlineData("@brakuzytkownika.com")]
        public void Given_NieprawidlowyEmail_When_Tworze_Then_RzucaArgumentException(
            string nieprawidlowyEmail)
        {
            Action akcja = () =>
                Zamowienie2.Utworz(Guid.NewGuid(), nieprawidlowyEmail);

            akcja.Should().Throw<ArgumentException>()
                .WithMessage("*email*");
        }
    }

    // === DODAWANIE POZYCJI ===
    public class DodajPozycje : ZamowienieAggregatTests
    {
        [Fact]
        public void
            Given_ZamowienieSzkic_When_DodajePozycje_Then_LiczbaPozycjiRosnie()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            z.DodajPozycje(Guid.NewGuid(), "Laptop", 3500m, 1);

            z.Pozycje.Should().HaveCount(1);
        }

        [Fact]
        public void
            Given_ZamowienieSzkic_When_DodajePozycje_Then_SumaZostajePrzeliczona()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            z.DodajPozycje(Guid.NewGuid(), "Laptop", 3500m, 2);
            z.DodajPozycje(Guid.NewGuid(), "Mysz",    150m, 3);

            z.SumaCalkowita.Should().Be(7000m + 450m);  // 3500*2 + 150*3
        }

        [Fact]
        public void
            Given_ZamowienieOplacone_When_DodajePozycje_Then_RzucaInvalidOperationException()
        {
            var z = TworzZamowienieZPozycjami();
            z.Zloz();
            z.Oplac("TXN-001");

            Action akcja = () =>
                z.DodajPozycje(Guid.NewGuid(), "Nowy", 100m, 1);

            akcja.Should().Throw<InvalidOperationException>()
                .WithMessage("*Oplacone*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void
            Given_NieprawidlowaIlosc_When_DodajePozycje_Then_RzucaArgumentException(
            int nieprawidlowaIlosc)
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            Action akcja = () =>
                z.DodajPozycje(Guid.NewGuid(), "Produkt", 100m,
                    nieprawidlowaIlosc);

            akcja.Should().Throw<ArgumentException>()
                .WithMessage("*ilość*");
        }
    }

    // === SKŁADANIE ===
    public class Zloz : ZamowienieAggregatTests
    {
        [Fact]
        public void
            Given_ZamowienieSzkicZPozycjami_When_Sklada_Then_StatusJestZlozone()
        {
            var z = TworzZamowienieZPozycjami();

            z.Zloz();

            z.Status.Should().Be(StatusZam.Zlozone);
        }

        [Fact]
        public void
            Given_PusteZamowienie_When_Sklada_Then_RzucaInvalidOperationException()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            Action akcja = () => z.Zloz();

            akcja.Should().Throw<InvalidOperationException>()
                .WithMessage("*puste*");
        }

        [Fact]
        public void
            Given_ZamowienieJuzZlozone_When_SkładaPonownie_Then_RzucaInvalidOperationException()
        {
            var z = TworzZamowienieZPozycjami();
            z.Zloz();

            Action akcja = () => z.Zloz();

            akcja.Should().Throw<InvalidOperationException>();
        }
    }

    // === ZDARZENIA DOMENOWE ===
    public class ZdarzeniaDomenowe : ZamowienieAggregatTests
    {
        [Fact]
        public void
            Given_NoweZamowienie_When_Tworze_Then_EmitujeZamowienieUtworzono()
        {
            var z = Zamowienie2.Utworz(Guid.NewGuid(), "jan@test.pl");

            z.ZdarzeniaDom.Should().ContainSingle()
                .Which.Should().BeOfType<ZamowienieUtworzono2>();
        }

        [Fact]
        public void
            Given_ZamowienieOplacone_When_Oplaca_Then_EmitujeZamowienieOplacono()
        {
            var z = TworzZamowienieZPozycjami();
            z.Zloz();
            z.CzyscZdarzenia();  // wyczyść wcześniejsze

            z.Oplac("TXN-001");

            z.ZdarzeniaDom.Should().ContainSingle()
                .Which.Should().BeOfType<ZamowienieOplacono2>();
        }

        [Fact]
        public void
            Given_ZamowienieAnulowane_When_Anuluje_Then_EmitujeZamowienieAnulowano()
        {
            var z = TworzZamowienieZPozycjami();
            z.Zloz();
            z.CzyscZdarzenia();

            z.Anuluj("Zmiana decyzji");

            z.ZdarzeniaDom.Should().ContainSingle()
                .Which.Should().BeOfType<ZamowienieAnulowano2>();
        }
    }
}

// === KLASY DOMENOWE do testów ===
public enum StatusZam { Szkic, Zlozone, Oplacone, Anulowane }

public abstract record ZdarzenieDom;
public record ZamowienieUtworzono2(Guid Id) : ZdarzenieDom;
public record ZamowienieOplacono2(Guid Id, string TxnId) : ZdarzenieDom;
public record ZamowienieAnulowano2(Guid Id, string Powod) : ZdarzenieDom;

public class Zamowienie2
{
    private readonly List<(Guid ProdId, string Nazwa, decimal Cena, int Ilosc)> _poz = new();
    private readonly List<ZdarzenieDom> _zdarzenia = new();

    public Guid       Id            { get; private set; }
    public StatusZam  Status        { get; private set; } = StatusZam.Szkic;
    public IReadOnlyList<(Guid ProdId, string Nazwa, decimal Cena, int Ilosc)>
                      Pozycje       => _poz.AsReadOnly();
    public decimal    SumaCalkowita => _poz.Sum(p => p.Cena * p.Ilosc);
    public IReadOnlyList<ZdarzenieDom> ZdarzeniaDom => _zdarzenia.AsReadOnly();

    private Zamowienie2() { }

    public static Zamowienie2 Utworz(Guid klientId, string email)
    {
        if (!email.Contains('@') || email.StartsWith('@'))
            throw new ArgumentException("Nieprawidłowy email");

        var z = new Zamowienie2 { Id = Guid.NewGuid() };
        z._zdarzenia.Add(new ZamowienieUtworzono2(z.Id));
        return z;
    }

    public void DodajPozycje(Guid prodId, string nazwa, decimal cena, int ilosc)
    {
        if (Status != StatusZam.Szkic)
            throw new InvalidOperationException($"Niedozwolone w statusie {Status}");
        if (ilosc <= 0)
            throw new ArgumentException("ilość musi być > 0");
        _poz.Add((prodId, nazwa, cena, ilosc));
    }

    public void Zloz()
    {
        if (Status != StatusZam.Szkic)
            throw new InvalidOperationException("Już złożone");
        if (!_poz.Any())
            throw new InvalidOperationException("Zamówienie jest puste");
        Status = StatusZam.Zlozone;
    }

    public void Oplac(string txnId)
    {
        if (Status != StatusZam.Zlozone)
            throw new InvalidOperationException($"Niedozwolone w statusie {Status}");
        Status = StatusZam.Oplacone;
        _zdarzenia.Add(new ZamowienieOplacono2(Id, txnId));
    }

    public void Anuluj(string powod)
    {
        if (Status == StatusZam.Oplacone)
            throw new InvalidOperationException("Nie można anulować opłaconego");
        Status = StatusZam.Anulowane;
        _zdarzenia.Add(new ZamowienieAnulowano2(Id, powod));
    }

    public void CzyscZdarzenia() => _zdarzenia.Clear();
}
```

---

### Typowe pytania rekrutacyjne

**"Dlaczego dobra nazwa testu jest ważniejsza niż dobra nazwa metody produkcyjnej?"** Testy to dokumentacja wykonywalną — opisują wymagania systemu. Kod produkcyjny może mieć krótką nazwę bo jest uzupełniany komentarzami, docstringami, kontekstem klasy. Test musi być samowystarczalny — czytasz tylko nazwę w raporcie bez kodu. Gdy test nie przechodzi o 3 w nocy, nazwa `DodajProdukt_NazwaZajeta_RzucaConflict` mówi dokładnie co się złamało. `TestDodajanie3` — nic nie mówi.

**"Jaka różnica między MethodName_Scenario_Result a Given_When_Then?"** Obie opisują to samo, różni je perspektywa. `MethodName_Scenario_Result` skupia się na interfejsie publicznym — "co wywołujesz, w jakim kontekście, co dostajesz". Dobra gdy testujesz konkretne metody klas. `Given_When_Then` skupia się na zachowaniu biznesowym — "jaki jest stan systemu, co się dzieje, jaki efekt". Dobra dla testów akceptacyjnych i Domain Tests (agregaty, use cases). W praktyce w C# dominuje pierwsza konwencja, `Given_When_Then` w BDD frameworkach (SpecFlow, Reqnroll).

**"Kiedy używać nested classes w testach?"** Nested classes gdy jedna klasa ma wiele metod publicznych i każda wymaga wielu testów. Bez nested: `ProduktTests` z 30 testami wymieszanych dla wszystkich metod. Z nested: `ProduktTests.Pobierz` (5 testów), `ProduktTests.Dodaj` (8 testów), `ProduktTests.Usun` (4 testy) — hierarchia w raportcie. Wadą jest verbosita (dziedziczenie konstruktora). Alternatywa: osobne pliki per metoda: `ProduktPobierzTests.cs`, `ProduktDodajTests.cs`.

**"Ile asercji powinien mieć jeden test?"** Zasada: jeden test = jedna konceptualna asercja. Ale to nie znaczy jeden `Assert.Equal`. Dla jednego obiektu możesz sprawdzić kilka jego właściwości — to wciąż jedna koncepcja ("produkt ma prawidłowe dane"). Zły przykład: test sprawdza i dodanie produktu i wysłanie emaila i walidację cache — to trzy koncepcje = trzy testy. Reguła pomocnicza: jeśli test nie przechodzi, czy wiesz od razu CO się złamało? Jeśli nie — podziel na mniejsze testy.