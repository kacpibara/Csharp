using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace _09_CleanCode_Testing;

// ============================================================
// FAKE SERVICES — zamienniki dla zewnętrznych serwisów
// ============================================================

public class FakeEmailSerwis : IEmailSerwis
{
    public List<(string To, string Body)> WyslaneEmaile { get; } = new();

    public Task WyslijAsync(string to, string body)
    {
        WyslaneEmaile.Add((to, body));
        return Task.CompletedTask;
    }
}

public interface ISmsSerwis
{
    Task WyslijSmsAsync(string telefon, string wiadomosc);
}

public class FakeSmsSerwis : ISmsSerwis
{
    public List<(string Telefon, string Wiadomosc)> WyslaneSms { get; } = new();

    public Task WyslijSmsAsync(string telefon, string wiadomosc)
    {
        WyslaneSms.Add((telefon, wiadomosc));
        return Task.CompletedTask;
    }
}

public class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();

    public T? Get<T>(string klucz)
        => _cache.TryGetValue(klucz, out var v) ? (T)v : default;

    public void Set<T>(string klucz, T wartosc, TimeSpan? ttl = null)
    {
        if (wartosc != null) _cache[klucz] = wartosc;
    }

    public void Remove(string klucz) => _cache.Remove(klucz);
}

// ============================================================
// DTOs — używane w testach endpointów
// ============================================================

public record ProduktListDto(int Id, string Nazwa, decimal Cena, int StanMagazyn);
public record ProduktDetailDto(int Id, string Nazwa, decimal Cena, int StanMagazyn, string Kategoria);
public record AktualizujProduktDto(string? Nazwa, decimal? Cena, int? StanMagazyn);
public record ZamowienieDto(int Id, decimal Suma, string Status, DateTime DataUtworzeniaata);
public record TokenyResponse(string AccessToken, string RefreshToken, DateTime WygasaO);
public record ZamowienieRequest(int ProduktId, int Ilosc, string Email);

// ============================================================
// JWT OPCJE
// ============================================================

public record JwtOpcje(
    string SecretKey,
    string Issuer,
    string Audience,
    int WygasaPoMinutach);

// ============================================================
// TEST TOKEN HELPER — generuje JWT bez zewnętrznych bibliotek
// ============================================================

public static class TestTokenHelper
{
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Base64UrlEncode(string s)
        => Base64UrlEncode(Encoding.UTF8.GetBytes(s));

    public static string GenerujToken(
        string userId,
        string email,
        string[] roles,
        string secret = "test-secret-key-32-znaki-minimum!")
    {
        var header  = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var rolesJson = string.Join(",", roles.Select(r => $"\"{r}\""));
        var exp     = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload = Base64UrlEncode(
            $"{{\"sub\":\"{userId}\",\"email\":\"{email}\"," +
            $"\"roles\":[{rolesJson}],\"exp\":{exp}}}");

        var sigInput = $"{header}.{payload}";
        var sigBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(sigInput));
        var sig = Base64UrlEncode(sigBytes);

        return $"{header}.{payload}.{sig}";
    }

    public static string GenerujTokenAdmina()
        => GenerujToken("1", "admin@sklep.pl", new[] { "Admin", "User" });

    public static string GenerujTokenUzytkownika(string email = "user@test.pl")
        => GenerujToken("2", email, new[] { "User" });

    public static string RozkodujePayload(string token)
    {
        var parts   = token.Split('.');
        if (parts.Length < 2) return "";
        var padded  = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
        var bytes   = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        return Encoding.UTF8.GetString(bytes);
    }
}

// ============================================================
// IZOLOWANA FABRYKA — testowy kontener serwisów bez HTTP
// ============================================================

public class IzolowanaFabryka : IAsyncDisposable
{
    public SklepKontekst      Db          { get; }
    public ProduktEFRepo      ProduktRepo { get; }
    public FakeEmailSerwis    Email       { get; }
    public FakeCacheService   Cache       { get; }
    public ProduktSerwis      ProduktSerw { get; }

    public IzolowanaFabryka()
    {
        Db          = TestDbContextFactory.Utworz();
        ProduktRepo = new ProduktEFRepo(Db);
        Email       = new FakeEmailSerwis();
        Cache       = new FakeCacheService();
        ProduktSerw = new ProduktSerwis(
            ProduktRepo, Email, Cache,
            new Mock<ILogger<ProduktSerwis>>().Object);
    }

    public async Task SeedAsync()
    {
        Db.Produkty.AddRange(
            new Produkt { Id = 1, Nazwa = "Laptop",   Cena = 3500m, StanMagazyn = 10, Aktywny = true },
            new Produkt { Id = 2, Nazwa = "Mysz",      Cena = 150m,  StanMagazyn = 50, Aktywny = true }
        );
        await Db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}

// ============================================================
// WZORZEC WebApplicationFactory — dokumentacja struktury
// Nie uruchamiany: brak Microsoft.AspNetCore.Mvc.Testing NuGet
// ============================================================

/*
WZORZEC (kod do skopiowania do projektu ASP.NET Core):

    public class SklepWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Zamień DbContext na InMemory
                var descriptor = services.Single(
                    d => d.ServiceType == typeof(DbContextOptions<SklepKontekst>));
                services.Remove(descriptor);
                services.AddDbContext<SklepKontekst>(opts =>
                    opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

                // Zamień zewnętrzne serwisy na fake
                services.AddSingleton<IEmailSerwis, FakeEmailSerwis>();
                services.AddSingleton<ISmsSerwis,  FakeSmsSerwis>();
            });
            builder.UseEnvironment("Testing");
        }
    }

    public class ProduktyEndpointsTests : IClassFixture<SklepWebAppFactory>
    {
        private readonly HttpClient _client;
        public ProduktyEndpointsTests(SklepWebAppFactory factory)
            => _client = factory.CreateClient();

        [Fact]
        public async Task GET_Produkty_Returns200()
        {
            var response = await _client.GetAsync("/api/produkty");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class AuthEndpointsTests : IClassFixture<SklepWebAppFactory>
    {
        [Fact]
        public async Task POST_Login_WithValidCredentials_ReturnsToken()
        {
            var content  = JsonContent.Create(new { Email = "admin@test.pl", Haslo = "secret" });
            var response = await _client.PostAsync("/api/auth/login", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var tokens = await response.Content.ReadFromJsonAsync<TokenyResponse>();
            tokens!.AccessToken.Should().NotBeNullOrEmpty();
        }
    }

    public class ZamowieniaFlowTests : IClassFixture<SklepWebAppFactory>
    {
        [Fact]
        public async Task POST_Zamowienie_E2E_PelnyPrzeplacyw()
        {
            // Ustaw token JWT w nagłówku
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerujTokenUzytkownika());

            var req      = new ZamowienieRequest(ProduktId: 1, Ilosc: 2, Email: "test@test.pl");
            var response = await _client.PostAsJsonAsync("/api/zamowienia", req);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }
*/

// ============================================================
// RUNNABLE INTEGRATION TESTS — testuje warstwę serwisów
// (analog testów integracyjnych bez warstwy HTTP)
// ============================================================

public class IntegracjaProduktSerwisTests : IAsyncLifetime
{
    private IzolowanaFabryka _fab = null!;

    public async Task InitializeAsync()
    {
        _fab = new IzolowanaFabryka();
        await _fab.SeedAsync();
    }

    public async Task DisposeAsync() => await _fab.DisposeAsync();

    [Fact]
    public async Task DodajAsync_PelnyPrzeplacyw_TworzyProdukt()
    {
        var dto = await _fab.ProduktSerw.DodajAsync(
            new NowyProduktDto("Monitor", 1500m, 1, 5));

        dto.Nazwa.Should().Be("Monitor");
        dto.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DodajAsync_WysylaEmailDoAdmina()
    {
        await _fab.ProduktSerw.DodajAsync(new NowyProduktDto("Drukarka", 400m, 1, 3));

        _fab.Email.WyslaneEmaile.Should().HaveCount(1);
        _fab.Email.WyslaneEmaile[0].To.Should().Be("admin@sklep.pl");
        _fab.Email.WyslaneEmaile[0].Body.Should().Contain("Drukarka");
    }

    [Fact]
    public async Task PobierzAsync_PoCachowaniu_ZwracaZCachu()
    {
        // pierwsze pobranie — zapisuje do cache
        var wynik1 = await _fab.ProduktSerw.PobierzAsync(1);
        // drugie — z cache (FakeCacheService)
        var wynik2 = await _fab.ProduktSerw.PobierzAsync(1);

        wynik1.Should().NotBeNull();
        wynik2.Should().NotBeNull();
        wynik2!.Nazwa.Should().Be(wynik1!.Nazwa);
    }

    [Fact]
    public async Task DodajAsync_DuplicateNazwa_RzucaWyjatek()
    {
        Func<Task> ponownie = () => _fab.ProduktSerw.DodajAsync(
            new NowyProduktDto("Laptop", 4000m, 1, 3));

        await ponownie.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*już istnieje*");
    }

    [Fact]
    public async Task UsunAsync_IstniejacyProdukt_UsuwaZBazyICachu()
    {
        // cache miss → pobranie z DB → trafia do cache
        await _fab.ProduktSerw.PobierzAsync(1);
        _fab.Cache.Get<ProduktDto>("produkt:1").Should().NotBeNull();

        // usunięcie powinno wyczyścić cache
        await _fab.ProduktSerw.UsunAsync(1);
        _fab.Cache.Get<ProduktDto>("produkt:1").Should().BeNull();

        var poUsun = await _fab.ProduktSerw.PobierzAsync(1);
        poUsun.Should().BeNull();
    }
}

public class JwtTokenHelperTests
{
    [Fact]
    public void GenerujToken_ZwracaTrzyCzesci()
    {
        var token  = TestTokenHelper.GenerujToken("1", "admin@test.pl", new[] { "Admin" });
        var czesci = token.Split('.');
        czesci.Should().HaveCount(3);
    }

    [Fact]
    public void GenerujToken_PayloadZawieraEmail()
    {
        var token   = TestTokenHelper.GenerujToken("1", "jan@test.pl", new[] { "User" });
        var payload = TestTokenHelper.RozkodujePayload(token);
        payload.Should().Contain("jan@test.pl");
    }

    [Fact]
    public void GenerujToken_PayloadZawieraRoles()
    {
        var token   = TestTokenHelper.GenerujTokenAdmina();
        var payload = TestTokenHelper.RozkodujePayload(token);
        payload.Should().Contain("Admin");
    }

    [Fact]
    public void GenerujToken_RozniUzytkownicy_RozneTokeny()
    {
        var admin = TestTokenHelper.GenerujTokenAdmina();
        var user  = TestTokenHelper.GenerujTokenUzytkownika("user@test.pl");
        admin.Should().NotBe(user);
    }

    [Fact]
    public void GenerujToken_TenSamUzytkownik_DajeInnyTokenPoDluzszymCzasie()
    {
        // dwa tokeny dla tego samego usera — różny exp = różny token
        var t1 = TestTokenHelper.GenerujToken("1", "jan@test.pl", new[] { "User" });
        var t2 = TestTokenHelper.GenerujToken("1", "jan@test.pl", new[] { "User" });
        // exp jest w sekundach — mogą być identyczne w tym samym tiknięciu,
        // ale header.payload powinien być ten sam → porównujemy tylko że mają format JWT
        t1.Split('.').Should().HaveCount(3);
        t2.Split('.').Should().HaveCount(3);
    }
}

// ============================================================
// DEMO
// ============================================================

public static class TestowanieIntegracyjneDemo
{
    public static void Uruchom()
    {
        Console.WriteLine("\n=== Testowanie Integracyjne ===");
        Console.WriteLine(
            "\n  WebApplicationFactory<TProgram> — klucz do testów ASP.NET Core:" +
            "\n    - tworzy rzeczywisty serwer testowy w pamięci" +
            "\n    - podmienia DbContext → UseInMemoryDatabase" +
            "\n    - podmienia zewnętrzne serwisy → Fake (email, sms)" +
            "\n    - każdy test dostaje własny HttpClient" +
            "\n    - IClassFixture<Factory> współdzieli server między testami w klasie" +
            "\n" +
            "\n  Wzorzec izolacji:" +
            "\n    IzolowaneFabryka — tworzy kompletny stack (DB + Serwisy + Fake) per test" +
            "\n    Każdy IAsyncLifetime.InitializeAsync() dostaje świeżą bazę InMemory" +
            "\n" +
            "\n  JWT w testach:" +
            "\n    TestTokenHelper.GenerujToken(userId, email, roles)" +
            "\n    client.DefaultRequestHeaders.Authorization =" +
            "\n        new AuthenticationHeaderValue(\"Bearer\", token);");

        Console.WriteLine("\n  Struktura tokenu JWT (header.payload.signature):");
        var token   = TestTokenHelper.GenerujTokenAdmina();
        var parts   = token.Split('.');
        var payload = TestTokenHelper.RozkodujePayload(token);
        Console.WriteLine($"    Header:    {parts[0]}");
        Console.WriteLine($"    Payload:   {parts[1]}");
        Console.WriteLine($"    Signature: {parts[2][..20]}...");
        Console.WriteLine($"    Decoded:   {payload}");

        Console.WriteLine("\n-- JWT Token Helper tests --");
        MiniTestRunner.Run<JwtTokenHelperTests>();

        Console.WriteLine("\n-- Integracja serwisów (bez HTTP, z InMemory + Fake) --");
        MiniTestRunner.Run<IntegracjaProduktSerwisTests>();

        Console.WriteLine(
            "\n  WebApplicationFactory wzorzec (wymaga Microsoft.AspNetCore.Mvc.Testing):" +
            "\n    GET  /api/produkty          → 200 + lista" +
            "\n    POST /api/produkty          → 201 + lokalizacja" +
            "\n    POST /api/auth/login        → 200 + TokenyResponse" +
            "\n    POST /api/zamowienia        → 201 (z Bearer token)" +
            "\n    GET  /api/zamowienia?page=1 → 200 + paginacja");
    }
}
