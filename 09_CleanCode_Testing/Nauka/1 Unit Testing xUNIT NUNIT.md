### Unit Testing w C#

Unit testy weryfikują **małe, izolowane fragmenty kodu** — jedną metodę lub klasę, bez zewnętrznych zależności.

---

### 1. Setup — xUnit vs NUnit

csharp

```csharp
// dotnet add package xunit
// dotnet add package xunit.runner.visualstudio
// dotnet add package Microsoft.NET.Test.Sdk
// dotnet add package NUnit                    (alternatywa)
// dotnet add package NUnit3TestAdapter        (dla NUnit)

// Struktura projektu:
// Sklep.sln
// ├── Sklep.Domain/
// ├── Sklep.Application/
// ├── Sklep.Infrastructure/
// └── Sklep.Tests/              ← projekt testowy
//     ├── Domain/
//     │   └── ZamowienieTests.cs
//     ├── Application/
//     │   └── UtworzZamowienieHandlerTests.cs
//     └── Infrastructure/
//         └── ProduktRepoTests.cs

// Sklep.Tests.csproj
// <Project Sdk="Microsoft.NET.Sdk">
//   <PropertyGroup>
//     <TargetFramework>net8.0</TargetFramework>
//     <IsPackable>false</IsPackable>
//   </PropertyGroup>
//   <ItemGroup>
//     <PackageReference Include="xunit" Version="2.*" />
//     <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
//     <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
//     <PackageReference Include="Moq" Version="4.*" />
//     <PackageReference Include="FluentAssertions" Version="6.*" />
//   </ItemGroup>
//   <ItemGroup>
//     <ProjectReference Include="..\Sklep.Application\Sklep.Application.csproj" />
//   </ItemGroup>
// </Project>

// === PORÓWNANIE XUNIT vs NUNIT ===
// Cecha          xUnit          NUnit
// Test method    [Fact]         [Test]
// Parametryczny  [Theory]       [TestCase]
// Setup          Constructor    [SetUp]
// Teardown       IDisposable    [TearDown]
// Class setup    IClassFixture  [OneTimeSetUp]
// Ignore         [Fact(Skip="")]  [Ignore("")]
// Kategoria      [Trait]        [Category]
```

---

### 2. Podstawy — Fact, Arrange-Act-Assert

csharp

```csharp
using Xunit;
using FluentAssertions;   // opcjonalne ale bardzo czytelne asercje

// Klasa testowa — zwykła klasa, bez atrybutu
public class KalkulatorTests
{
    // [Fact] — jeden test bez parametrów
    [Fact]
    public void Dodaj_DwaDodatneLiczby_ZwracaSume()
    {
        // ARRANGE — przygotuj dane
        var kalkulator = new Kalkulator();
        int a = 5;
        int b = 3;

        // ACT — wykonaj akcję
        int wynik = kalkulator.Dodaj(a, b);

        // ASSERT — sprawdź wynik
        Assert.Equal(8, wynik);
    }

    [Fact]
    public void Podziel_PrzezZero_RzucaWyjatek()
    {
        var kalkulator = new Kalkulator();

        // Assert.Throws sprawdza typ wyjątku
        var wyjatek = Assert.Throws<DivideByZeroException>(
            () => kalkulator.Podziel(10, 0));

        Assert.Equal("Dzielenie przez zero jest niedozwolone",
            wyjatek.Message);
    }

    [Fact]
    public async Task PobierzWynikAsync_PoprawneId_ZwracaWynik()
    {
        var kalkulator = new Kalkulator();

        // Async testy — po prostu async Task
        int wynik = await kalkulator.PobierzWynikAsync(1);

        Assert.Equal(42, wynik);
    }
}

// Klasa testowana
public class Kalkulator
{
    public int Dodaj(int a, int b) => a + b;
    public int Odejmij(int a, int b) => a - b;
    public int Mnoz(int a, int b) => a * b;

    public int Podziel(int a, int b)
    {
        if (b == 0)
            throw new DivideByZeroException(
                "Dzielenie przez zero jest niedozwolone");
        return a / b;
    }

    public Task<int> PobierzWynikAsync(int id)
        => Task.FromResult(42);
}

// === FLUENT ASSERTIONS — czytelniejsze asercje ===
public class FluentPrzykladyTests
{
    [Fact]
    public void FluentAssertions_Podstawowe()
    {
        // Liczby
        int wynik = 42;
        wynik.Should().Be(42);
        wynik.Should().BeGreaterThan(10);
        wynik.Should().BeInRange(40, 50);

        // Stringi
        string tekst = "Witaj świecie";
        tekst.Should().StartWith("Witaj");
        tekst.Should().Contain("świecie");
        tekst.Should().HaveLength(13);
        tekst.Should().NotBeNullOrEmpty();

        // Kolekcje
        var lista = new List<int> { 1, 2, 3, 4, 5 };
        lista.Should().HaveCount(5);
        lista.Should().Contain(3);
        lista.Should().BeInAscendingOrder();
        lista.Should().NotContain(6);
        lista.Should().ContainInOrder(1, 2, 3);

        // Obiekty
        var produkt = new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m };
        produkt.Should().NotBeNull();
        produkt.Nazwa.Should().Be("Laptop");
        produkt.Cena.Should().BePositive();

        // Wyjątki
        Action akcja = () => throw new ArgumentNullException("param");
        akcja.Should().Throw<ArgumentNullException>()
            .WithMessage("*param*");

        // Async wyjątki
        Func<Task> asyncAkcja = async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("test");
        };
        asyncAkcja.Should().ThrowAsync<InvalidOperationException>();
    }
}

// Stubs
public class Produkt
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = "";
    public decimal Cena { get; set; }
}
```

---

### 3. Theory — testy parametryczne

csharp

```csharp
// [Theory] + [InlineData] — test z wieloma zestawami danych

public class WalidatorEmailTests
{
    private readonly WalidatorEmail _walidator = new();

    // Prawidłowe emaile
    [Theory]
    [InlineData("jan@test.pl")]
    [InlineData("anna.kowalska@firma.com")]
    [InlineData("user+tag@example.org")]
    [InlineData("x@y.io")]
    public void CzyPoprawny_PrawidlowyEmail_ZwracaTrue(string email)
    {
        bool wynik = _walidator.CzyPoprawny(email);

        Assert.True(wynik, $"Email '{email}' powinien być prawidłowy");
    }

    // Nieprawidłowe emaile
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("brakmalpy.pl")]
    [InlineData("@brakuzytkownika.pl")]
    [InlineData("brakdomeny@")]
    [InlineData(null)]
    public void CzyPoprawny_NieprawidlowyEmail_ZwracaFalse(string? email)
    {
        bool wynik = _walidator.CzyPoprawny(email!);

        Assert.False(wynik, $"Email '{email}' powinien być nieprawidłowy");
    }

    // [MemberData] — dane z metody lub właściwości
    public static IEnumerable<object[]> DanePrawidlowychEmaili =>
        new List<object[]>
        {
            new object[] { "jan@test.pl",    true },
            new object[] { "ANNA@TEST.COM",  true },  // case insensitive
            new object[] { "invalid",         false },
            new object[] { null!,             false }
        };

    [Theory]
    [MemberData(nameof(DanePrawidlowychEmaili))]
    public void CzyPoprawny_ZMemberData_ZwracaOczekiwany(
        string? email, bool oczekiwany)
    {
        bool wynik = _walidator.CzyPoprawny(email!);
        Assert.Equal(oczekiwany, wynik);
    }

    // [ClassData] — dane z osobnej klasy
    [Theory]
    [ClassData(typeof(PrzykladyEmaili))]
    public void CzyPoprawny_ZClassData_ZwracaOczekiwany(
        string email, bool oczekiwany)
    {
        bool wynik = _walidator.CzyPoprawny(email);
        wynik.Should().Be(oczekiwany, because: $"email='{email}'");
    }
}

// Klasa danych dla [ClassData]
public class PrzykladyEmaili
    : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { "a@b.c",       true };
        yield return new object[] { "test@test.pl", true };
        yield return new object[] { "złe@",         false };
        yield return new object[] { "@złe.pl",      false };
    }

    System.Collections.IEnumerator
        System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();
}

// Implementacja walidatora
public class WalidatorEmail
{
    public bool CzyPoprawny(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return email.Contains('@')
            && email.IndexOf('@') > 0
            && email.LastIndexOf('.') > email.IndexOf('@');
    }
}
```

---

### 4. Setup i Teardown — organizacja testów

csharp

```csharp
// xUnit — konstruktor jako Setup, IDisposable jako Teardown

public class ProduktSerwisTests : IDisposable, IAsyncLifetime
{
    private readonly ProduktSerwis _serwis;
    private readonly TestDbContext _db;

    // Konstruktor = [SetUp] — wywoływany przed KAŻDYM testem
    public ProduktSerwisTests()
    {
        // Twórz fresh instancje dla każdego testu — izolacja!
        _db     = new TestDbContext();
        _serwis = new ProduktSerwis(_db);

        Console.WriteLine("Setup testu");
    }

    // IAsyncLifetime.InitializeAsync — async setup
    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        await ZaladujDaneTestoweAsync();
    }

    // IAsyncLifetime.DisposeAsync — async teardown
    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    // IDisposable.Dispose — synchroniczny teardown
    public void Dispose()
    {
        Console.WriteLine("Teardown testu");
        _db.Dispose();
    }

    private async Task ZaladujDaneTestoweAsync()
    {
        _db.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop",    Cena = 3500m },
            new Produkt { Id = 2, Nazwa = "Mysz",       Cena =  150m },
            new Produkt { Id = 3, Nazwa = "Klawiatura", Cena =  250m }
        );
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task PobierzWszystkie_MaDane_ZwracaListeProd()
    {
        var wynik = await _serwis.PobierzWszystkieAsync();
        wynik.Should().HaveCount(3);
    }

    [Fact]
    public async Task Dodaj_NowyProdukt_ZwiekszyLiczbe()
    {
        var nowy = new Produkt { Nazwa = "Monitor", Cena = 1500m };

        await _serwis.DodajAsync(nowy);

        var wszystkie = await _serwis.PobierzWszystkieAsync();
        wszystkie.Should().HaveCount(4);
    }
}

// === IClassFixture — współdzielony stan między testami ===
// Jeden obiekt dla WSZYSTKICH testów w klasie

public class DatabaseFixture : IAsyncLifetime
{
    public TestDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Db = new TestDbContext();
        await Db.Database.EnsureCreatedAsync();
        // Droga inicjalizacja — tylko raz!
        await SeedDataAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.Database.EnsureDeletedAsync();
        await Db.DisposeAsync();
    }

    private async Task SeedDataAsync()
    {
        Db.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop", Cena = 3500m },
            new Produkt { Id = 2, Nazwa = "Mysz",   Cena =  150m }
        );
        await Db.SaveChangesAsync();
    }
}

public class ProduktRepoTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    // Fixture wstrzykiwany — jeden obiekt dla wszystkich testów!
    public ProduktRepoTests(DatabaseFixture fixture)
        => _fixture = fixture;

    [Fact]
    public async Task PobierzPoId_IstniejaceId_ZwracaProdukt()
    {
        var repo = new ProduktRepo(_fixture.Db);

        var wynik = await repo.FindAsync(1);

        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
    }

    [Fact]
    public async Task PobierzPoId_NieistniejaceId_ZwracaNull()
    {
        var repo = new ProduktRepo(_fixture.Db);

        var wynik = await repo.FindAsync(999);

        wynik.Should().BeNull();
    }
}

// Stubs dla kompilacji
public class TestDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public Microsoft.EntityFrameworkCore.DbSet<Produkt> Produkty => Set<Produkt>();

    protected override void OnConfiguring(
        Microsoft.EntityFrameworkCore.DbContextOptionsBuilder opts)
        => opts.UseInMemoryDatabase(Guid.NewGuid().ToString());
}
public class ProduktSerwis
{
    private readonly TestDbContext _db;
    public ProduktSerwis(TestDbContext db) => _db = db;
    public Task<List<Produkt>> PobierzWszystkieAsync()
        => Task.FromResult(_db.Produkty.ToList());
    public async Task DodajAsync(Produkt p)
        { _db.Produkty.Add(p); await _db.SaveChangesAsync(); }
}
public class ProduktRepo
{
    private readonly TestDbContext _db;
    public ProduktRepo(TestDbContext db) => _db = db;
    public Task<Produkt?> FindAsync(int id)
        => Task.FromResult(_db.Produkty.FirstOrDefault(p => p.Id == id));
}
```

---

### 5. Atrybuty testowe — pełna lista

csharp

```csharp
// === XUNIT ATRYBUTY ===

// [Fact] — prosty test
[Fact]
public void ProsteTest() => Assert.True(true);

// [Fact(Skip = "powód")] — pomiń test
[Fact(Skip = "Czekam na implementację API")]
public void PominieryTest() { }

// [Fact(DisplayName = "Czytelna nazwa")] — własna nazwa wyświetlana
[Fact(DisplayName = "Dodawanie dwóch liczb dodatnich")]
public void DodawanieTest()
{
    Assert.Equal(5, 2 + 3);
}

// [Theory] + [InlineData]
[Theory]
[InlineData(1, 1, 2)]
[InlineData(0, 5, 5)]
[InlineData(-1, 1, 0)]
public void Dodaj_RozneDane(int a, int b, int oczekiwany)
    => Assert.Equal(oczekiwany, a + b);

// [Trait] — kategorie i metadane
[Fact]
[Trait("Kategoria", "Integracyjny")]
[Trait("Priorytet", "Wysoki")]
[Trait("Obszar", "Platnosci")]
public void TestPlatnosci() => Assert.True(true);

// === NUNIT ATRYBUTY (dla porównania) ===
// [Test]                 = [Fact]
// [TestCase(1, 2, 3)]    = [InlineData(1, 2, 3)]
// [TestFixture]          = klasa testowa (opcjonalne w NUnit 3)
// [SetUp]                = konstruktor xUnit
// [TearDown]             = Dispose xUnit
// [OneTimeSetUp]         = IClassFixture xUnit
// [OneTimeTearDown]      = IClassFixture.Dispose
// [Ignore("powód")]      = [Fact(Skip="powód")]
// [Category("nazwa")]    = [Trait("Category","nazwa")]
// [Timeout(1000)]        = własny timeout w ms
// [Repeat(10)]           = powtórz test 10 razy
// [MaxTime(500)]         = maksymalny czas wykonania

// Przykład NUnit:
// [TestFixture]
// public class KalkulatorNUnitTests
// {
//     private Kalkulator _kalkulator;
//
//     [SetUp]
//     public void Setup() => _kalkulator = new Kalkulator();
//
//     [Test]
//     public void Dodaj_Prawidlowe_ZwracaSume()
//         => Assert.That(_kalkulator.Dodaj(2, 3), Is.EqualTo(5));
//
//     [TestCase(2, 3, 5)]
//     [TestCase(0, 0, 0)]
//     [TestCase(-1, 1, 0)]
//     public void Dodaj_Przypadki(int a, int b, int oczekiwany)
//         => Assert.That(_kalkulator.Dodaj(a, b), Is.EqualTo(oczekiwany));
// }
```

---

### 6. Nazewnictwo i organizacja testów

csharp

```csharp
// Konwencje nazewnictwa:
// Metoda_Warunek_OczekiwanyWynik  (najpopularniejsza)
// Should_OczekiwanyWynik_When_Warunek  (BDD style)
// Given_When_Then  (BDD style)

public class ZamowieniaServiceTests
{
    // Konwencja: NazwaMetody_Scenariusz_OczekiwanyWynik
    [Fact]
    public async Task ZlozZamowienie_PustyKoszyk_RzucaArgumentException()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienie_NieistniejacyKlient_RzucaKeyNotFoundException()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienie_PoprawneZamowienie_ZwracaIdZamowienia()
    {
        // ...
    }

    [Fact]
    public async Task ZlozZamowienie_PoprawneZamowienie_WysylaEmailPotwierdzenia()
    {
        // ...
    }
}

// === ORGANIZACJA PRZEZ NESTED CLASSES ===
// Grupuj testy per metoda dla lepszej czytelności

public class KalkulatorRabatTests
{
    // Klasy zagnieżdżone grupują testy dla jednej metody
    public class ObliczRabat
    {
        [Fact]
        public void GdySumaPowycej1000_ZwraczaRabat10Procent()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(1200m).Should().Be(120m);
        }

        [Fact]
        public void GdySumaPonizej1000_ZwracaBrakRabatu()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(500m).Should().Be(0m);
        }

        [Fact]
        public void GdySumaRowna1000_ZwracaRabat10Procent()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(1000m).Should().Be(100m);
        }
    }

    public class CzyPrzysługujeRabat
    {
        [Theory]
        [InlineData(999.99, false)]
        [InlineData(1000.00, true)]
        [InlineData(1500.00, true)]
        public void ZwracaPoprawnyWynik(decimal suma, bool oczekiwany)
        {
            var kalk = new KalkulatorRabatu();
            kalk.CzyPrzysługuje(suma).Should().Be(oczekiwany);
        }
    }
}

public class KalkulatorRabatu
{
    public decimal ObliczRabat(decimal suma)
        => suma >= 1000m ? suma * 0.1m : 0m;

    public bool CzyPrzysługuje(decimal suma) => suma >= 1000m;
}
```

---

### 7. Uruchamianie testów — CLI i Visual Studio

bash

```bash
# === DOTNET CLI ===

# Uruchom wszystkie testy
dotnet test

# Uruchom z outputem
dotnet test --verbosity normal

# Uruchom konkretny projekt
dotnet test Sklep.Tests/Sklep.Tests.csproj

# Filtruj po nazwie testu (wildcard)
dotnet test --filter "ZlozZamowienie"

# Filtruj po Trait
dotnet test --filter "Kategoria=Integracyjny"

# Filtruj po metodzie
dotnet test --filter "FullyQualifiedName~KalkulatorTests"

# Uruchom tylko nieudane z poprzedniej sesji
dotnet test --no-build --blame-hang-timeout 10s

# Z raportem pokrycia kodu
dotnet test --collect:"XPlat Code Coverage"

# Raport HTML z coverlet
# dotnet add package coverlet.collector
dotnet test --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Generuj raport HTML
# dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./CoverageReport" \
  -reporttypes:Html

# Wiele projektów równolegle
dotnet test --parallel

# === VISUAL STUDIO / RIDER ===
# Test Explorer: View → Test Explorer
# Ctrl+R, A    — uruchom wszystkie
# Ctrl+R, T    — uruchom zaznaczony test
# Ctrl+R, L    — uruchom ostatnio uruchomione
```

csharp

```csharp
// === FILTROWANIE PRZEZ TRAIT ===
// Uruchom: dotnet test --filter "Kategoria=Szybkie"

public class SzybkieTests
{
    [Fact]
    [Trait("Kategoria", "Szybkie")]
    public void SzybkiTest1() => Assert.True(true);

    [Fact]
    [Trait("Kategoria", "Szybkie")]
    public void SzybkiTest2() => Assert.True(1 + 1 == 2);
}

public class WolneTests
{
    [Fact]
    [Trait("Kategoria", "Wolne")]
    [Trait("Typ", "Integracyjny")]
    public async Task WolnyTest()
    {
        await Task.Delay(1000);
        Assert.True(true);
    }
}
```

---

### 8. Praktyczny przykład — kompletne testy serwisu

csharp

```csharp
// Kompletny zestaw testów dla ZamowieniaSerwis

public class ZamowieniaSerwisTests
{
    // Dane testowe jako stałe
    private static readonly Guid KlientId = Guid.NewGuid();
    private static readonly Guid ProduktId = Guid.NewGuid();
    private const string Email = "jan@test.pl";
    private const decimal CenaProduktu = 100m;

    // Helper — tworzy domyślny request
    private static ZlozZamowienieRequest DomyslnyRequest() => new(
        KlientId:  KlientId,
        Email:     Email,
        Pozycje:   new List<PozycjaRequest>
        {
            new(ProduktId, "Produkt testowy", CenaProduktu, 2)
        }
    );

    // === HAPPY PATH ===
    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_ZwracaId()
    {
        // Arrange
        var (serwis, _, _) = TworzSerwis();

        // Act
        var wynik = await serwis.ZlozZamowienieAsync(DomyslnyRequest());

        // Assert
        wynik.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_ZapisywajeDoRepo()
    {
        // Arrange
        var (serwis, repoMock, _) = TworzSerwis();

        // Act
        await serwis.ZlozZamowienieAsync(DomyslnyRequest());

        // Assert
        repoMock.Verify(
            r => r.DodajAsync(It.IsAny<ZamowienieEntity>(),
                              It.IsAny<CancellationToken>()),
            Moq.Times.Once,
            "Repo.DodajAsync powinno zostać wywołane dokładnie raz");
    }

    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_WysylaEmailPotwierdzenia()
    {
        // Arrange
        var (serwis, _, emailMock) = TworzSerwis();

        // Act
        await serwis.ZlozZamowienieAsync(DomyslnyRequest());

        // Assert
        emailMock.Verify(
            e => e.WyslijAsync(
                Email,
                It.Is<string>(s => s.Contains("zamówienie"))),
            Moq.Times.Once);
    }

    // === ERROR CASES ===
    [Fact]
    public async Task ZlozZamowienie_PustePozycje_RzucaArgumentException()
    {
        // Arrange
        var (serwis, _, _) = TworzSerwis();
        var request = DomyslnyRequest() with { Pozycje = new List<PozycjaRequest>() };

        // Act
        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(request);

        // Assert
        await akcja.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*pozycje*");
    }

    [Fact]
    public async Task ZlozZamowienie_NieprawidlowyEmail_RzucaArgumentException()
    {
        var (serwis, _, _) = TworzSerwis();
        var request = DomyslnyRequest() with { Email = "nieprawidlowy-email" };

        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(request);

        await akcja.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ZlozZamowienie_NieprawidlowaIlosc_RzucaArgumentException(
        int ilosc)
    {
        var (serwis, _, _) = TworzSerwis();
        var request = DomyslnyRequest() with
        {
            Pozycje = new List<PozycjaRequest>
            {
                new(ProduktId, "Test", CenaProduktu, ilosc)
            }
        };

        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(request);

        await akcja.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ilość*");
    }

    // === EDGE CASES ===
    [Fact]
    public async Task ZlozZamowienie_EmailBledRepoPomijaSave()
    {
        // Arrange — email serwis rzuca wyjątek
        var (serwis, repoMock, emailMock) = TworzSerwis(emailBledny: true);

        // Act
        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(DomyslnyRequest());

        // Assert — wyjątek jest propagowany
        await akcja.Should().ThrowAsync<Exception>();

        // Repo NIE powinno być wywołane gdy email nie działa
        // (albo odwrotnie — zależy od wymagań biznesowych)
    }

    [Fact]
    public async Task ZlozZamowienie_WielePozycjiTegoSamegoProduktu_LaczySumy()
    {
        // Arrange
        var (serwis, repoMock, _) = TworzSerwis();
        var request = new ZlozZamowienieRequest(
            KlientId: KlientId,
            Email:    Email,
            Pozycje:  new List<PozycjaRequest>
            {
                new(ProduktId, "Produkt", 100m, 2),
                new(ProduktId, "Produkt", 100m, 3)  // ten sam produkt x2
            });

        ZamowienieEntity? zapisane = null;
        repoMock.Setup(r =>
            r.DodajAsync(Moq.It.IsAny<ZamowienieEntity>(),
                         Moq.It.IsAny<CancellationToken>()))
            .Callback<ZamowienieEntity, CancellationToken>(
                (z, _) => zapisane = z)
            .ReturnsAsync(1);

        // Act
        await serwis.ZlozZamowienieAsync(request);

        // Assert — pozycje powinny być połączone lub suma prawidłowa
        zapisane.Should().NotBeNull();
        zapisane!.SumaCalkowita.Should().Be(500m);  // 5 × 100m
    }

    // === HELPER — fabryka serwisu z mockami ===
    private static (
        ZamowieniaSerwis Serwis,
        Moq.Mock<IZamowienieRepository> RepoMock,
        Moq.Mock<IEmailSerwis> EmailMock)
    TworzSerwis(bool emailBledny = false)
    {
        var repoMock  = new Moq.Mock<IZamowienieRepository>();
        var emailMock = new Moq.Mock<IEmailSerwis>();

        // Domyślne zachowania
        repoMock
            .Setup(r => r.DodajAsync(
                Moq.It.IsAny<ZamowienieEntity>(),
                Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        if (emailBledny)
            emailMock
                .Setup(e => e.WyslijAsync(
                    Moq.It.IsAny<string>(),
                    Moq.It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP error"));
        else
            emailMock
                .Setup(e => e.WyslijAsync(
                    Moq.It.IsAny<string>(),
                    Moq.It.IsAny<string>()))
                .Returns(Task.CompletedTask);

        var serwis = new ZamowieniaSerwis(
            repoMock.Object,
            emailMock.Object);

        return (serwis, repoMock, emailMock);
    }
}

// === KLASY TESTOWANE ===
public record ZlozZamowienieRequest(
    Guid KlientId, string Email,
    List<PozycjaRequest> Pozycje);

public record PozycjaRequest(
    Guid ProduktId, string Nazwa,
    decimal Cena, int Ilosc);

public class ZamowienieEntity
{
    public int    Id             { get; set; }
    public Guid   KlientId       { get; set; }
    public decimal SumaCalkowita { get; set; }
}

public interface IZamowienieRepository
{
    Task<int> DodajAsync(ZamowienieEntity z, CancellationToken ct);
}

public interface IEmailSerwis
{
    Task WyslijAsync(string to, string body);
}

public class ZamowieniaSerwis
{
    private readonly IZamowienieRepository _repo;
    private readonly IEmailSerwis          _email;

    public ZamowieniaSerwis(
        IZamowienieRepository repo,
        IEmailSerwis email)
    {
        _repo  = repo;
        _email = email;
    }

    public async Task<int> ZlozZamowienieAsync(
        ZlozZamowienieRequest req,
        CancellationToken ct = default)
    {
        if (!req.Pozycje.Any())
            throw new ArgumentException("Zamówienie musi mieć pozycje");

        if (!req.Email.Contains('@'))
            throw new ArgumentException("Nieprawidłowy email");

        foreach (var p in req.Pozycje)
            if (p.Ilosc <= 0)
                throw new ArgumentException("ilość musi być > 0");

        decimal suma = req.Pozycje.Sum(p => p.Cena * p.Ilosc);

        var zamowienie = new ZamowienieEntity
        {
            KlientId       = req.KlientId,
            SumaCalkowita  = suma
        };

        int id = await _repo.DodajAsync(zamowienie, ct);
        await _email.WyslijAsync(req.Email,
            $"Potwierdzenie: zamówienie #{id}, suma: {suma:C}");

        return id;
    }
}
```

---

### Typowe pytania rekrutacyjne

**"Co to unit test i czym różni się od integration test?"** Unit test weryfikuje jeden moduł (klasę/metodę) w izolacji — bez bazy danych, HTTP, pliku. Wszystkie zależności podmienione na mocki. Szybki (<1ms), deterministyczny, izolowany. Integration test weryfikuje współpracę wielu komponentów — np. serwis + EF Core + SQLite in-memory. Wolniejszy, ale sprawdza że komponenty poprawnie ze sobą działają. Unit test odpowiada: "czy logika w izolacji jest poprawna?". Integration test: "czy komponenty poprawnie ze sobą współpracują?"

**"Co to Arrange-Act-Assert?"** AAA to wzorzec struktury testu. Arrange — przygotuj dane wejściowe, utwórz testowany obiekt, skonfiguruj mocki. Act — wywołaj testowaną metodę (jeden call, jeden Act). Assert — sprawdź wynik (asercje na wartości zwracanej, stanie obiektu, wywołaniach mocków). Każda sekcja powinna być wyraźnie oddzielona (komentarze lub pusta linia). Zasada: jeden test = jeden Act = jedna konceptualna asercja (choć fizycznie może być kilka `Assert.`).

**"Jaka różnica między [Fact] a [Theory] w xUnit?"** `[Fact]` — jeden test, brak parametrów, jeden scenariusz. `[Theory]` — test parametryczny, wiele zestawów danych, jeden zestaw = jedna instancja testu. Z `[InlineData]` dla literałów, `[MemberData]` dla danych z metody/właściwości (obiekty, klasy), `[ClassData]` dla danych z zewnętrznej klasy. Używaj Theory gdy testujesz tę samą logikę z wieloma wejściami — unikasz duplikacji.

**"Jak organizować testy żeby były czytelne?"** Konwencja nazewnictwa: `MetodaTestowana_Warunek_OczekiwanyWynik`. Nested classes dla grupowania testów jednej metody. Helper factory method `TworzSerwis()` zamiast powtarzania setupu. Stałe dla danych testowych (`private const string Email = "..."` zamiast magic strings). Jeden test = jedna koncepcja — nie testuj dwóch rzeczy naraz. Jeśli test ma więcej niż 20 linii — to sygnał że testuje za wiele.