using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace _09_CleanCode_Testing;

// ============================================================
// MINI TEST RUNNER — wywołuje [Fact]/[Theory] bezpośrednio
// ============================================================

public static class MiniTestRunner
{
    public static (int passed, int failed) Run<T>() where T : class
    {
        int passed = 0, failed = 0;
        var typ = typeof(T);
        Console.WriteLine($"\n  [{typ.Name}]");

        // [Fact] methods — exclude [Theory] (TheoryAttribute inherits FactAttribute in xUnit)
        foreach (var m in typ.GetMethods().Where(m =>
            m.GetCustomAttribute<FactAttribute>() != null &&
            m.GetCustomAttribute<TheoryAttribute>() == null))
        {
            var skip = m.GetCustomAttribute<FactAttribute>()!.Skip;
            if (skip != null) { Console.WriteLine($"    SKIP  {m.Name}"); continue; }

            try
            {
                var inst = (T)Activator.CreateInstance(typ)!;
                if (inst is IAsyncLifetime al) al.InitializeAsync().GetAwaiter().GetResult();
                var r = m.Invoke(inst, null);
                if (r is Task t) t.GetAwaiter().GetResult();
                if (inst is IAsyncLifetime al2) al2.DisposeAsync().GetAwaiter().GetResult();
                else if (inst is IDisposable d) d.Dispose();
                Console.WriteLine($"    ✓ {m.Name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ {m.Name}: {(ex.InnerException ?? ex).Message}");
                failed++;
            }
        }

        // [Theory] + [InlineData]
        foreach (var m in typ.GetMethods().Where(m => m.GetCustomAttribute<TheoryAttribute>() != null))
        {
            foreach (var attr in m.GetCustomAttributes<InlineDataAttribute>())
            {
                var rawData = attr.GetData(m).First();
                var parms   = m.GetParameters();
                var data    = rawData.Select((val, i) => CoerceArg(val, parms[i].ParameterType)).ToArray();
                var label   = string.Join(", ", data.Select(x => x?.ToString() ?? "null"));
                try
                {
                    var inst = (T)Activator.CreateInstance(typ)!;
                    var r = m.Invoke(inst, data);
                    if (r is Task t) t.GetAwaiter().GetResult();
                    Console.WriteLine($"    ✓ {m.Name}({label})");
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ {m.Name}({label}): {(ex.InnerException ?? ex).Message}");
                    failed++;
                }
            }

            // [MemberData]
            foreach (var attr in m.GetCustomAttributes<MemberDataAttribute>())
            {
                var memberName    = attr.MemberName;
                var declaringType = attr.MemberType ?? typ;
                IEnumerable<object[]>? rows = null;
                var prop = declaringType.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public);
                if (prop != null)
                    rows = (IEnumerable<object[]>)prop.GetValue(null)!;
                else
                {
                    var method2 = declaringType.GetMethod(memberName, BindingFlags.Static | BindingFlags.Public);
                    if (method2 != null)
                        rows = (IEnumerable<object[]>)method2.Invoke(null, null)!;
                }
                if (rows == null) continue;
                foreach (var row in rows)
                {
                    var label = string.Join(", ", row.Select(x => x?.ToString() ?? "null"));
                    try
                    {
                        var inst = (T)Activator.CreateInstance(typ)!;
                        var r = m.Invoke(inst, row);
                        if (r is Task t) t.GetAwaiter().GetResult();
                        Console.WriteLine($"    ✓ {m.Name}({label})");
                        passed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ✗ {m.Name}({label}): {(ex.InnerException ?? ex).Message}");
                        failed++;
                    }
                }
            }

            // [ClassData]
            foreach (var attr in m.GetCustomAttributes<ClassDataAttribute>())
            {
                var dataEnum = (IEnumerable<object[]>)Activator.CreateInstance(attr.Class)!;
                foreach (var row in dataEnum)
                {
                    var label = string.Join(", ", row.Select(x => x?.ToString() ?? "null"));
                    try
                    {
                        var inst = (T)Activator.CreateInstance(typ)!;
                        var r = m.Invoke(inst, row);
                        if (r is Task t) t.GetAwaiter().GetResult();
                        Console.WriteLine($"    ✓ {m.Name}({label})");
                        passed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ✗ {m.Name}({label}): {(ex.InnerException ?? ex).Message}");
                        failed++;
                    }
                }
            }
        }

        Console.WriteLine($"    --- Wynik: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    public static (int passed, int failed) RunWithFixture<T, TFixture>()
        where T : class
        where TFixture : class, IAsyncLifetime, new()
    {
        int passed = 0, failed = 0;
        var typ = typeof(T);
        Console.WriteLine($"\n  [{typ.Name}] (z fixture {typeof(TFixture).Name})");

        var fixture = new TFixture();
        fixture.InitializeAsync().GetAwaiter().GetResult();

        foreach (var m in typ.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() != null))
        {
            try
            {
                var inst = (T)Activator.CreateInstance(typ, fixture)!;
                var r = m.Invoke(inst, null);
                if (r is Task t) t.GetAwaiter().GetResult();
                Console.WriteLine($"    ✓ {m.Name}");
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ {m.Name}: {(ex.InnerException ?? ex).Message}");
                failed++;
            }
        }

        fixture.DisposeAsync().GetAwaiter().GetResult();
        Console.WriteLine($"    --- Wynik: {passed} passed, {failed} failed");
        return (passed, failed);
    }

    // Konwertuje typy prymitywne z InlineData (np. double → decimal)
    private static object? CoerceArg(object? val, Type targetType)
    {
        if (val == null) return null;
        if (val.GetType() == targetType) return val;
        try { return Convert.ChangeType(val, targetType); }
        catch { return val; }
    }
}

// ============================================================
// 1. KALKULATOR — podstawowa klasa testowana
// ============================================================

public class Kalkulator
{
    public int    Dodaj(int a, int b)   => a + b;
    public int    Odejmij(int a, int b) => a - b;
    public int    Mnoz(int a, int b)    => a * b;

    public int Podziel(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException("Dzielenie przez zero jest niedozwolone");
        return a / b;
    }

    public Task<int> PobierzWynikAsync(int id) => Task.FromResult(42);
}

// Prosty model do FluentAssertions
public class ProduktUT
{
    public int     Id    { get; set; }
    public string  Nazwa { get; set; } = "";
    public decimal Cena  { get; set; }
}

// ============================================================
// 2. KALKULATORTESTY — [Fact], Arrange-Act-Assert
// ============================================================

public class KalkulatorTests
{
    [Fact]
    public void Dodaj_DwaDodatneLiczby_ZwracaSume()
    {
        var kalkulator = new Kalkulator();
        int a = 5, b = 3;

        int wynik = kalkulator.Dodaj(a, b);

        Assert.Equal(8, wynik);
    }

    [Fact]
    public void Podziel_PrzezZero_RzucaDivideByZeroException()
    {
        var kalkulator = new Kalkulator();

        var wyjatek = Assert.Throws<DivideByZeroException>(
            () => kalkulator.Podziel(10, 0));

        Assert.Equal("Dzielenie przez zero jest niedozwolone", wyjatek.Message);
    }

    [Fact]
    public async Task PobierzWynikAsync_PoprawneId_ZwracaWynik()
    {
        var kalkulator = new Kalkulator();

        int wynik = await kalkulator.PobierzWynikAsync(1);

        Assert.Equal(42, wynik);
    }
}

// ============================================================
// 3. FLUENTASSERTIONS — czytelne asercje
// ============================================================

public class FluentPrzykladyTests
{
    [Fact]
    public void FluentAssertions_Liczby_Stringi_Kolekcje_Obiekty()
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
        tekst.Should().NotBeNullOrEmpty();

        // Kolekcje
        var lista = new List<int> { 1, 2, 3, 4, 5 };
        lista.Should().HaveCount(5);
        lista.Should().Contain(3);
        lista.Should().BeInAscendingOrder();
        lista.Should().NotContain(6);
        lista.Should().ContainInOrder(1, 2, 3);

        // Obiekty
        var produkt = new ProduktUT { Id = 1, Nazwa = "Laptop", Cena = 3500m };
        produkt.Should().NotBeNull();
        produkt.Nazwa.Should().Be("Laptop");
        produkt.Cena.Should().BePositive();

        // Wyjątki
        Action akcja = () => throw new ArgumentNullException("param");
        akcja.Should().Throw<ArgumentNullException>().WithMessage("*param*");
    }

    [Fact]
    public async Task FluentAssertions_AsyncWyjatek()
    {
        Func<Task> asyncAkcja = async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("test");
        };
        await asyncAkcja.Should().ThrowAsync<InvalidOperationException>();
    }
}

// ============================================================
// 4. WALIDATOREMAIL — [Theory] + [InlineData] + [MemberData] + [ClassData]
// ============================================================

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

public class WalidatorEmailTests
{
    private readonly WalidatorEmail _walidator = new();

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

    [Theory]
    [InlineData("")]
    [InlineData("brakmalpy.pl")]
    [InlineData("@brakuzytkownika.pl")]
    [InlineData("brakdomeny@")]
    public void CzyPoprawny_NieprawidlowyEmail_ZwracaFalse(string email)
    {
        bool wynik = _walidator.CzyPoprawny(email);
        Assert.False(wynik, $"Email '{email}' powinien być nieprawidłowy");
    }

    public static IEnumerable<object[]> DanePrawidlowychEmaili =>
        new List<object[]>
        {
            new object[] { "jan@test.pl",    true },
            new object[] { "ANNA@TEST.COM",  true },
            new object[] { "invalid",         false },
        };

    [Theory]
    [MemberData(nameof(DanePrawidlowychEmaili))]
    public void CzyPoprawny_ZMemberData_ZwracaOczekiwany(string? email, bool oczekiwany)
    {
        bool wynik = _walidator.CzyPoprawny(email!);
        Assert.Equal(oczekiwany, wynik);
    }

    [Theory]
    [ClassData(typeof(PrzykladyEmaili))]
    public void CzyPoprawny_ZClassData_ZwracaOczekiwany(string email, bool oczekiwany)
    {
        bool wynik = _walidator.CzyPoprawny(email);
        wynik.Should().Be(oczekiwany, because: $"email='{email}'");
    }
}

public class PrzykladyEmaili : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return new object[] { "a@b.c",        true };
        yield return new object[] { "test@test.pl",  true };
        yield return new object[] { "zle@",          false };
        yield return new object[] { "@zle.pl",       false };
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();
}

// ============================================================
// 5. SETUP / TEARDOWN — konstruktor, IDisposable, IAsyncLifetime
// ============================================================

public class TestDbContextUT : DbContext
{
    public DbSet<ProduktUT2> Produkty => Set<ProduktUT2>();

    protected override void OnConfiguring(DbContextOptionsBuilder opts)
        => opts.UseInMemoryDatabase(Guid.NewGuid().ToString());  // izolacja per instancja
}

public class ProduktUT2
{
    public int     Id    { get; set; }
    public string  Nazwa { get; set; } = "";
    public decimal Cena  { get; set; }
}

public class ProduktSerwisDirect
{
    private readonly TestDbContextUT _db;
    public ProduktSerwisDirect(TestDbContextUT db) => _db = db;
    public Task<List<ProduktUT2>> PobierzWszystkieAsync() => Task.FromResult(_db.Produkty.ToList());
    public async Task DodajAsync(ProduktUT2 p) { _db.Produkty.Add(p); await _db.SaveChangesAsync(); }
}

public class ProduktSerwisTestsUT : IDisposable, IAsyncLifetime
{
    private readonly ProduktSerwisDirect _serwis;
    private readonly TestDbContextUT     _db;

    public ProduktSerwisTestsUT()
    {
        _db     = new TestDbContextUT();
        _serwis = new ProduktSerwisDirect(_db);
        Console.WriteLine("      [Setup] Konstruktor — fresh context");
    }

    public async Task InitializeAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        _db.Produkty.AddRange(
            new ProduktUT2 { Id = 1, Nazwa = "Laptop",    Cena = 3500m },
            new ProduktUT2 { Id = 2, Nazwa = "Mysz",       Cena = 150m  },
            new ProduktUT2 { Id = 3, Nazwa = "Klawiatura", Cena = 250m  });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    public void Dispose()
    {
        Console.WriteLine("      [Teardown] Dispose");
        _db.Dispose();
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
        await _serwis.DodajAsync(new ProduktUT2 { Nazwa = "Monitor", Cena = 1500m });
        var wszystkie = await _serwis.PobierzWszystkieAsync();
        wszystkie.Should().HaveCount(4);
    }
}

// === IClassFixture — współdzielony stan ===
public class DatabaseFixtureUT : IAsyncLifetime
{
    public TestDbContextUT Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Db = new TestDbContextUT();
        await Db.Database.EnsureCreatedAsync();
        Db.Produkty.AddRange(
            new ProduktUT2 { Id = 1, Nazwa = "Laptop", Cena = 3500m },
            new ProduktUT2 { Id = 2, Nazwa = "Mysz",   Cena = 150m  });
        await Db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.Database.EnsureDeletedAsync();
        await Db.DisposeAsync();
    }
}

public class ProduktRepoTestsUT : IClassFixture<DatabaseFixtureUT>
{
    private readonly DatabaseFixtureUT _fixture;
    public ProduktRepoTestsUT(DatabaseFixtureUT fixture) => _fixture = fixture;

    [Fact]
    public void PobierzPoId_IstniejaceId_ZwracaProdukt()
    {
        var wynik = _fixture.Db.Produkty.FirstOrDefault(p => p.Id == 1);
        wynik.Should().NotBeNull();
        wynik!.Nazwa.Should().Be("Laptop");
    }

    [Fact]
    public void PobierzPoId_NieistniejaceId_ZwracaNull()
    {
        var wynik = _fixture.Db.Produkty.FirstOrDefault(p => p.Id == 999);
        wynik.Should().BeNull();
    }
}

// ============================================================
// 6. ATRYBUTY TESTOWE — pełna lista xUnit/NUnit
// ============================================================

public class AtrybutyXUnitDemo
{
    [Fact]
    public void ProsteTest() => Assert.True(true);

    [Fact(Skip = "Oczekuję na implementację API")]
    public void PominiętyTest() { }

    [Fact(DisplayName = "Dodawanie dwóch liczb dodatnich")]
    public void DodawanieTest() => Assert.Equal(5, 2 + 3);

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(0, 5, 5)]
    [InlineData(-1, 1, 0)]
    public void Dodaj_RozneDane(int a, int b, int oczekiwany)
        => Assert.Equal(oczekiwany, a + b);

    [Fact]
    [Trait("Kategoria", "Integracyjny")]
    [Trait("Priorytet", "Wysoki")]
    public void TestZTrait() => Assert.True(true);

    // NUnit odpowiedniki (w komentarzu — dla porównania):
    // [Test]                 = [Fact]
    // [TestCase(1, 2, 3)]    = [InlineData(1, 2, 3)]
    // [SetUp]                = konstruktor xUnit
    // [TearDown]             = Dispose xUnit
    // [OneTimeSetUp]         = IClassFixture.InitializeAsync
    // [Ignore("powód")]      = [Fact(Skip="powód")]
    // [Category("nazwa")]    = [Trait("Category","nazwa")]
}

public class SzybkieTests
{
    [Fact][Trait("Kategoria", "Szybkie")]
    public void SzybkiTest1() => Assert.True(true);

    [Fact][Trait("Kategoria", "Szybkie")]
    public void SzybkiTest2() => Assert.True(1 + 1 == 2);
}

public class WolneTests
{
    [Fact][Trait("Kategoria", "Wolne")][Trait("Typ", "Integracyjny")]
    public async Task WolnyTest()
    {
        await Task.Delay(10);
        Assert.True(true);
    }
}

// ============================================================
// 7. KALKULATOR RABATU — nested class tests (pełny przykład)
// ============================================================

public class KalkulatorRabatu
{
    public decimal ObliczRabat(decimal suma)
    {
        if (suma < 0) throw new ArgumentException("Suma nie może być ujemna");
        return suma >= 1000m ? suma * 0.1m : 0m;
    }

    public bool CzyPrzysługuje(decimal suma) => suma >= 1000m;
}

public class KalkulatorRabatuTests
{
    public class ObliczRabat
    {
        [Fact]
        public void GdySumaPowycej1000_Zwraca10Procent()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(1200m).Should().Be(120m);
        }

        [Fact]
        public void GdySumaPonizej1000_Zwraca0()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(999m).Should().Be(0m);
        }

        [Fact]
        public void GdySumaRowna1000_Zwraca10Procent()
        {
            var kalk = new KalkulatorRabatu();
            kalk.ObliczRabat(1000m).Should().Be(100m);
        }

        [Fact]
        public void GdySumaUjemna_RzucaArgumentException()
        {
            var kalk = new KalkulatorRabatu();
            Action akcja = () => kalk.ObliczRabat(-100m);
            akcja.Should().Throw<ArgumentException>().WithMessage("*ujemna*");
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

// ============================================================
// 8. KOMPLETNY PRZYKŁAD — ZamowieniaSerwis z Moq
// ============================================================

public interface IZamowienieRepoUT
{
    Task<int> DodajAsync(ZamowienieEntityUT z, CancellationToken ct);
}

public class ZamowienieEntityUT
{
    public int     Id            { get; set; }
    public Guid    KlientId      { get; set; }
    public decimal SumaCalkowita { get; set; }
}

public record ZlozZamowienieRequestUT(
    Guid KlientId, string Email, List<PozycjaRequestUT> Pozycje);

public record PozycjaRequestUT(Guid ProduktId, string Nazwa, decimal Cena, int Ilosc);

public class ZamowieniaSerwisUT
{
    private readonly IZamowienieRepoUT _repo;
    private readonly IEmailSerwis      _email;

    public ZamowieniaSerwisUT(IZamowienieRepoUT repo, IEmailSerwis email)
    { _repo = repo; _email = email; }

    public async Task<int> ZlozZamowienieAsync(
        ZlozZamowienieRequestUT req, CancellationToken ct = default)
    {
        if (!req.Pozycje.Any())
            throw new ArgumentException("Zamówienie musi mieć pozycje");
        if (!req.Email.Contains('@'))
            throw new ArgumentException("Nieprawidłowy email");
        foreach (var p in req.Pozycje)
            if (p.Ilosc <= 0)
                throw new ArgumentException("ilość musi być > 0");

        decimal suma = req.Pozycje.Sum(p => p.Cena * p.Ilosc);
        var zam = new ZamowienieEntityUT { KlientId = req.KlientId, SumaCalkowita = suma };
        int id = await _repo.DodajAsync(zam, ct);
        await _email.WyslijAsync(req.Email,
            $"Potwierdzenie: zamówienie #{id}, suma: {suma:C}");
        return id;
    }
}

public class ZamowieniaSerwisTestsUT
{
    private static readonly Guid KlientIdUT  = Guid.NewGuid();
    private static readonly Guid ProduktIdUT = Guid.NewGuid();
    private const string EmailUT = "jan@test.pl";

    private static ZlozZamowienieRequestUT DomyslnyRequest() => new(
        KlientId: KlientIdUT, Email: EmailUT,
        Pozycje: new List<PozycjaRequestUT>
        {
            new(ProduktIdUT, "Produkt testowy", 100m, 2)
        });

    private static (ZamowieniaSerwisUT Serwis,
                    Mock<IZamowienieRepoUT> RepoMock,
                    Mock<IEmailSerwis> EmailMock)
    TworzSerwis(bool emailBledny = false)
    {
        var repoMock  = new Mock<IZamowienieRepoUT>();
        var emailMock = new Mock<IEmailSerwis>();

        repoMock.Setup(r => r.DodajAsync(
            It.IsAny<ZamowienieEntityUT>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        if (emailBledny)
            emailMock.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP error"));
        else
            emailMock.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

        return (new ZamowieniaSerwisUT(repoMock.Object, emailMock.Object), repoMock, emailMock);
    }

    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_ZwracaId()
    {
        var (serwis, _, _) = TworzSerwis();
        var wynik = await serwis.ZlozZamowienieAsync(DomyslnyRequest());
        wynik.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_ZapisywajeDoRepo()
    {
        var (serwis, repoMock, _) = TworzSerwis();
        await serwis.ZlozZamowienieAsync(DomyslnyRequest());
        repoMock.Verify(r => r.DodajAsync(
            It.IsAny<ZamowienieEntityUT>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ZlozZamowienie_Prawidlowy_WysylaEmailPotwierdzenia()
    {
        var (serwis, _, emailMock) = TworzSerwis();
        await serwis.ZlozZamowienieAsync(DomyslnyRequest());
        emailMock.Verify(
            e => e.WyslijAsync(EmailUT, It.Is<string>(s => s.Contains("zamówienie"))),
            Times.Once);
    }

    [Fact]
    public async Task ZlozZamowienie_PustePozycje_RzucaArgumentException()
    {
        var (serwis, _, _) = TworzSerwis();
        var req = DomyslnyRequest() with { Pozycje = new List<PozycjaRequestUT>() };
        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(req);
        await akcja.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ZlozZamowienie_NieprawidlowyEmail_RzucaArgumentException()
    {
        var (serwis, _, _) = TworzSerwis();
        var req = DomyslnyRequest() with { Email = "nieprawidlowy-email" };
        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(req);
        await akcja.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task ZlozZamowienie_NieprawidlowaIlosc_RzucaArgumentException(int ilosc)
    {
        var (serwis, _, _) = TworzSerwis();
        var req = DomyslnyRequest() with
        {
            Pozycje = new List<PozycjaRequestUT> { new(ProduktIdUT, "Test", 100m, ilosc) }
        };
        Func<Task> akcja = () => serwis.ZlozZamowienieAsync(req);
        await akcja.Should().ThrowAsync<ArgumentException>().WithMessage("*ilość*");
    }

    [Fact]
    public async Task ZlozZamowienie_WielePozycjiTegoSamegoProduktu_LiczySumy()
    {
        var (serwis, repoMock, _) = TworzSerwis();
        var req = new ZlozZamowienieRequestUT(
            KlientIdUT, EmailUT,
            new List<PozycjaRequestUT>
            {
                new(ProduktIdUT, "Produkt", 100m, 2),
                new(ProduktIdUT, "Produkt", 100m, 3)
            });

        ZamowienieEntityUT? zapisane = null;
        repoMock.Setup(r => r.DodajAsync(
            It.IsAny<ZamowienieEntityUT>(), It.IsAny<CancellationToken>()))
            .Callback<ZamowienieEntityUT, CancellationToken>((z, _) => zapisane = z)
            .ReturnsAsync(1);

        await serwis.ZlozZamowienieAsync(req);

        zapisane.Should().NotBeNull();
        zapisane!.SumaCalkowita.Should().Be(500m);
    }
}

// ============================================================
// DEMO
// ============================================================

public static class UnitTestowanieDemo
{
    public static void Uruchom()
    {
        Console.WriteLine("\n=== Unit Testing ===");
        Console.WriteLine("\n-- xUnit vs NUnit --");
        Console.WriteLine("  [Fact]        = [Test]      | [Theory]+[InlineData] = [TestCase]");
        Console.WriteLine("  Constructor   = [SetUp]     | IDisposable.Dispose   = [TearDown]");
        Console.WriteLine("  IClassFixture = [OneTimeSetUp]");
        Console.WriteLine("  [Fact(Skip)]  = [Ignore]    | [Trait] = [Category]");

        Console.WriteLine("\n-- CLI: dotnet test --filter \"ZlozZamowienie\" --");

        int totalPassed = 0, totalFailed = 0;

        void Run((int p, int f) r) { totalPassed += r.p; totalFailed += r.f; }

        Run(MiniTestRunner.Run<KalkulatorTests>());
        Run(MiniTestRunner.Run<FluentPrzykladyTests>());
        Run(MiniTestRunner.Run<WalidatorEmailTests>());
        Run(MiniTestRunner.Run<AtrybutyXUnitDemo>());
        Run(MiniTestRunner.Run<SzybkieTests>());
        Run(MiniTestRunner.Run<WolneTests>());
        Run(MiniTestRunner.Run<ProduktSerwisTestsUT>());
        Run(MiniTestRunner.RunWithFixture<ProduktRepoTestsUT, DatabaseFixtureUT>());
        Run(MiniTestRunner.Run<KalkulatorRabatuTests.ObliczRabat>());
        Run(MiniTestRunner.Run<KalkulatorRabatuTests.CzyPrzysługujeRabat>());
        Run(MiniTestRunner.Run<ZamowieniaSerwisTestsUT>());

        Console.WriteLine($"\n  === RAZEM: {totalPassed} passed, {totalFailed} failed ===");
    }
}
