### Integration Testing w ASP.NET Core

Testy integracyjne weryfikują **cały stack aplikacji** — routing, middleware, kontrolery, serwisy, bazę danych — w jednym teście.

---

### 1. Czym są testy integracyjne

csharp

```csharp
// Unit test    — jedna klasa, zależności podmienione na mocki
// Integration test — wiele warstw razem, prawdziwe lub in-memory zależności
// E2E test     — pełna aplikacja z prawdziwą bazą i przeglądarką

// WebApplicationFactory<TProgram>:
// - uruchamia CAŁĄ aplikację w pamięci
// - tworzy HttpClient który wysyła requesty bez sieci
// - możesz podmienić dowolny serwis (DI override)
// - domyślnie używa konfiguracji testowej

// Instalacja
// dotnet add package Microsoft.AspNetCore.Mvc.Testing
// dotnet add package Microsoft.EntityFrameworkCore.InMemory

// Program.cs — musi być dostępny dla TestServer
// Dodaj na końcu Program.cs:
// public partial class Program { }  // ← wymagane dla WebApplicationFactory
```

---

### 2. Podstawowa konfiguracja WebApplicationFactory

csharp

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

// === PROSTA FABRYKA — minimum konfiguracji ===
public class SklepWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Podmień DbContext na InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SklepContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<SklepContext>(options =>
                options.UseInMemoryDatabase(
                    Guid.NewGuid().ToString()));  // unikalna baza na test!

            // Opcjonalnie podmień inne serwisy
            services.AddScoped<IEmailSerwis, FakeEmailSerwis>();
        });

        // Ustaw środowisko na Test
        builder.UseEnvironment("Testing");
    }
}

// === ROZBUDOWANA FABRYKA — z seed data i pomocnikami ===
public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SklepContext? _dbContext;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Podmień DbContext
            UsunDbContextRejestracje(services);

            services.AddDbContext<SklepContext>(opt =>
                opt.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            // Podmień serwisy zewnętrzne
            services.AddScoped<IEmailSerwis, FakeEmailSerwis>();
            services.AddScoped<ISmsSerwis, FakeSmsSerwis>();

            // Podmień konfigurację JWT (krótszy czas dla testów)
            services.Configure<JwtOpcje>(opt =>
            {
                opt.SecretKey      = "test-secret-key-minimum-32-chars!";
                opt.AccessTokenMin = 60;
                opt.Issuer         = "test-issuer";
                opt.Audience       = "test-audience";
            });
        });

        builder.UseEnvironment("Testing");
    }

    // Inicjalizacja po zbudowaniu aplikacji
    public async Task InitializeAsync()
    {
        // Pobierz DbContext i załaduj dane testowe
        using var scope = Services.CreateScope();
        _dbContext = scope.ServiceProvider
            .GetRequiredService<SklepContext>();

        await _dbContext.Database.EnsureCreatedAsync();
        await ZaladujDaneTestoweAsync(_dbContext);
    }

    public new async Task DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.Database.EnsureDeletedAsync();

        await base.DisposeAsync();
    }

    private static async Task ZaladujDaneTestoweAsync(SklepContext ctx)
    {
        ctx.Kategorie.AddRange(
            new Kategoria { Id = 1, Nazwa = "IT" },
            new Kategoria { Id = 2, Nazwa = "Dom" });

        ctx.Produkty.AddRange(
            new Produkt
            {
                Id          = 1, Nazwa = "Laptop",
                Cena        = 3500m, KategoriaId = 1,
                StanMagaz   = 10,   Aktywny = true
            },
            new Produkt
            {
                Id          = 2, Nazwa = "Mysz",
                Cena        = 150m, KategoriaId = 1,
                StanMagaz   = 50,  Aktywny = true
            },
            new Produkt
            {
                Id          = 3, Nazwa = "Fotel",
                Cena        = 800m, KategoriaId = 2,
                StanMagaz   = 5,   Aktywny = false  // nieaktywny!
            });

        ctx.Klienci.Add(new Klient
        {
            Id    = 1, Imie = "Anna", Nazwisko = "Kowalska",
            Email = "anna@test.pl", Aktywny = true
        });

        await ctx.SaveChangesAsync();
    }

    private static void UsunDbContextRejestracje(IServiceCollection services)
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(DbContextOptions<SklepContext>)
                     || d.ServiceType == typeof(SklepContext))
            .ToList();

        foreach (var d in descriptors)
            services.Remove(d);
    }

    // Helper — pobierz DbContext dla weryfikacji
    public SklepContext PobierzDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SklepContext>();
    }
}

// Stubs
public class FakeEmailSerwis : IEmailSerwis
{
    public List<(string To, string Body)> WyslanePowiadomienia = new();
    public Task WyslijAsync(string to, string body)
    {
        WyslanePowiadomienia.Add((to, body));
        return Task.CompletedTask;
    }
}
public class FakeSmsSerwis : ISmsSerwis
{
    public Task WyslijAsync(string nr, string tresc) => Task.CompletedTask;
}
```

---

### 3. Testy endpointów — GET, POST, PUT, DELETE

csharp

```csharp
public class ProduktyEndpointsTests
    : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient        _client;

    public ProduktyEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,    // nie podążaj za redirectami automatycznie
            BaseAddress       = new Uri("http://localhost")
        });
    }

    // === GET /api/produkty ===
    [Fact]
    public async Task GetProdukty_ZwracaListeAktywnychProduktow()
    {
        var response = await _client.GetAsync("/api/v1/produkty");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var produkty = await response.Content
            .ReadFromJsonAsync<List<ProduktListDto>>();

        produkty.Should().NotBeNull();
        produkty!.Should().HaveCount(2);  // tylko aktywne (3 - 1 nieaktywny)
        produkty.Should().NotContain(p => p.Nazwa == "Fotel");  // nieaktywny
    }

    [Fact]
    public async Task GetProdukty_ZFiltrKategorii_ZwracaFiltrowana()
    {
        var response = await _client.GetAsync("/api/v1/produkty?kategoria=IT");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var produkty = await response.Content
            .ReadFromJsonAsync<List<ProduktListDto>>();

        produkty!.Should().HaveCount(2);
        produkty.Should().OnlyContain(p => p.Kategoria == "IT");
    }

    // === GET /api/produkty/{id} ===
    [Fact]
    public async Task GetProduktPoId_IstniejaceId_Zwraca200ZProduktem()
    {
        var response = await _client.GetAsync("/api/v1/produkty/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var produkt = await response.Content
            .ReadFromJsonAsync<ProduktDetailDto>();

        produkt.Should().NotBeNull();
        produkt!.Id.Should().Be(1);
        produkt.Nazwa.Should().Be("Laptop");
        produkt.Cena.Should().Be(3500m);
    }

    [Fact]
    public async Task GetProduktPoId_NieistniejaceId_Zwraca404()
    {
        var response = await _client.GetAsync("/api/v1/produkty/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // === POST /api/produkty ===
    [Fact]
    public async Task PostProdukt_PoprawneDto_Zwraca201ZNowymProduktem()
    {
        // Arrange
        var nowyProdukt = new NowyProduktDto(
            Nazwa:       "Tablet",
            Cena:        1200m,
            KategoriaId: 1,
            Stan:        20);

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/produkty", nowyProdukt);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Sprawdź Location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString()
            .Should().Contain("/api/v1/produkty/");

        // Sprawdź body
        var utworzony = await response.Content
            .ReadFromJsonAsync<ProduktDetailDto>();
        utworzony!.Nazwa.Should().Be("Tablet");
        utworzony.Id.Should().BeGreaterThan(0);

        // Sprawdź że faktycznie jest w bazie
        var db = _factory.PobierzDbContext();
        var wBazie = await db.Produkty
            .FirstOrDefaultAsync(p => p.Nazwa == "Tablet");
        wBazie.Should().NotBeNull();
    }

    [Fact]
    public async Task PostProdukt_PustaNazwa_Zwraca400()
    {
        var nieprawidlowy = new NowyProduktDto(
            Nazwa:       "",           // pusta!
            Cena:        100m,
            KategoriaId: 1,
            Stan:        5);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/produkty", nieprawidlowy);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Nazwa");
    }

    [Fact]
    public async Task PostProdukt_UjemnaCena_Zwraca400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/produkty",
            new NowyProduktDto("Produkt", -100m, 1, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // === PUT /api/produkty/{id} ===
    [Fact]
    public async Task PutProdukt_IstniejacyProdukt_Zwraca204()
    {
        var aktualizacja = new AktualizujProduktDto(
            Nazwa: "Laptop Pro", Cena: 4000m, Stan: 8);

        var response = await _client.PutAsJsonAsync(
            "/api/v1/produkty/1", aktualizacja);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Weryfikacja w bazie
        var db = _factory.PobierzDbContext();
        var zaktualizowany = await db.Produkty.FindAsync(1);
        zaktualizowany!.Nazwa.Should().Be("Laptop Pro");
        zaktualizowany.Cena.Should().Be(4000m);
    }

    [Fact]
    public async Task PutProdukt_NieistniejaceId_Zwraca404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/v1/produkty/9999",
            new AktualizujProduktDto("Test", 100m, 1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // === DELETE /api/produkty/{id} ===
    [Fact]
    public async Task DeleteProdukt_IstniejacyProdukt_Zwraca204()
    {
        var response = await _client.DeleteAsync("/api/v1/produkty/2");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Weryfikacja usunięcia
        var sprawdzenie = await _client.GetAsync("/api/v1/produkty/2");
        sprawdzenie.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// DTOs
public record ProduktListDto(int Id, string Nazwa, decimal Cena, string Kategoria);
public record ProduktDetailDto(int Id, string Nazwa, decimal Cena, string Kategoria,
    int Stan, bool Aktywny);
public record NowyProduktDto(string Nazwa, decimal Cena, int KategoriaId, int Stan);
public record AktualizujProduktDto(string Nazwa, decimal Cena, int Stan);
```

---

### 4. Testy z autentykacją JWT

csharp

```csharp
// Helper — generowanie tokenów testowych
public static class TestTokenHelper
{
    private const string SecretKey = "test-secret-key-minimum-32-chars!";
    private const string Issuer    = "test-issuer";
    private const string Audience  = "test-audience";

    public static string GenerujToken(
        int userId = 1,
        string email = "test@test.pl",
        string[] role = null!)
    {
        role ??= new[] { "User" };

        var klucz = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(SecretKey));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email,          email),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(role.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer:  Issuer,
            audience: Audience,
            claims:  claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                klucz, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerujTokenAdmin()
        => GenerujToken(99, "admin@test.pl", new[] { "Admin", "User" });

    // Extension method dla HttpClient
    public static HttpClient ZJWT(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);
        return client;
    }
}

// Testy z autentykacją
public class AuthEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebAppFactory _factory;

    public AuthEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    [Fact]
    public async Task GetProdukt_BezTokenu_Zwraca401()
    {
        // Brak nagłówka Authorization
        var response = await _client.GetAsync("/api/v1/admin/produkty");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProdukt_ZTokenemUser_Zwraca200()
    {
        string token = TestTokenHelper.GenerujToken(
            email: "jan@test.pl",
            role: new[] { "User" });

        var response = await _client
            .ZJWT(token)
            .GetAsync("/api/v1/produkty/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteProdukt_ZTokenemAdmin_Zwraca204()
    {
        string token = TestTokenHelper.GenerujTokenAdmin();

        var response = await _client
            .ZJWT(token)
            .DeleteAsync("/api/v1/admin/produkty/1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostProdukt_ZTokenemUserNieAdmin_Zwraca403()
    {
        string token = TestTokenHelper.GenerujToken(
            role: new[] { "User" });  // brak roli Admin

        var response = await _client
            .ZJWT(token)
            .PostAsJsonAsync("/api/v1/admin/produkty",
                new NowyProduktDto("Test", 100m, 1, 5));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Test pełnego flow logowania
    [Fact]
    public async Task Login_PoprawneCredentials_ZwracaTokeny()
    {
        var loginDto = new { Email = "anna@test.pl", Haslo = "Tajne123!" };

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", loginDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokeny = await response.Content
            .ReadFromJsonAsync<TokenyResponse>();

        tokeny.Should().NotBeNull();
        tokeny!.AccessToken.Should().NotBeNullOrEmpty();
        tokeny.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ZleHaslo_Zwraca401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = "anna@test.pl", Haslo = "ZleHaslo!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public record TokenyResponse(string AccessToken, string RefreshToken);

// Using statements dla JWT
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
```

---

### 5. Izolacja testów — CustomWebApplicationFactory per test

csharp

```csharp
// Gdy każdy test potrzebuje innego stanu bazy

public class IzolowaneTestyFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName;
    private readonly Action<IServiceCollection>? _dodatkowerSerwisy;

    public IzolowaneTestyFactory(
        Action<IServiceCollection>? serwisy = null)
    {
        _dbName            = Guid.NewGuid().ToString();
        _dodatkowerSerwisy = serwisy;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Zawsze podmień DbContext
            UsunDbContext(services);
            services.AddDbContext<SklepContext>(opt =>
                opt.UseInMemoryDatabase(_dbName));

            // Opcjonalne dodatkowe podmiany
            _dodatkowerSerwisy?.Invoke(services);
        });

        builder.UseEnvironment("Testing");
    }

    private static void UsunDbContext(IServiceCollection s)
    {
        var d = s.SingleOrDefault(
            x => x.ServiceType == typeof(DbContextOptions<SklepContext>));
        if (d != null) s.Remove(d);
    }

    // Helper — seed bazy i zwróć klienta
    public async Task<HttpClient> KlientZDanymi(
        Func<SklepContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SklepContext>();
        await seed(ctx);

        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }
}

// Użycie — każdy test ma własną fabrykę
public class IzolowaneTestyTests
{
    [Fact]
    public async Task GetProdukt_WlasnaWersja_DzialaIzolowanie()
    {
        // Własna fabryka dla tego testu
        await using var factory = new IzolowaneTestyFactory();
        var client = await factory.KlientZDanymi(async ctx =>
        {
            ctx.Produkty.Add(new Produkt
            {
                Id = 1, Nazwa = "Tylko dla tego testu",
                Cena = 100m, KategoriaId = 1
            });
            await ctx.SaveChangesAsync();
        });

        var response = await client.GetAsync("/api/v1/produkty/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var produkt = await response.Content
            .ReadFromJsonAsync<ProduktDetailDto>();
        produkt!.Nazwa.Should().Be("Tylko dla tego testu");
    }

    [Fact]
    public async Task PostProdukt_ZMockiemEmaila_WeryfikujeWyslanie()
    {
        var emailMock = new Mock<IEmailSerwis>();
        emailMock.Setup(e => e.WyslijAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await using var factory = new IzolowaneTestyFactory(
            services => services.AddScoped(_ => emailMock.Object));

        var client = factory.CreateClient();
        var token  = TestTokenHelper.GenerujTokenAdmin();

        await client.ZJWT(token).PostAsJsonAsync("/api/v1/produkty",
            new NowyProduktDto("Test", 100m, 1, 5));

        emailMock.Verify(
            e => e.WyslijAsync(
                "admin@sklep.pl",
                It.Is<string>(s => s.Contains("Test"))),
            Times.Once);
    }
}
```

---

### 6. Testy złożonych scenariuszy

csharp

```csharp
// Testy pełnego flow biznesowego
public class ZamowieniaFlowTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient        _client;
    private readonly string            _tokenKlienta;
    private readonly string            _tokenAdmin;

    public ZamowieniaFlowTests(TestWebAppFactory factory)
    {
        _factory     = factory;
        _client      = factory.CreateClient();
        _tokenKlienta = TestTokenHelper.GenerujToken(1, "anna@test.pl");
        _tokenAdmin   = TestTokenHelper.GenerujTokenAdmin();
    }

    // Test całego flow: złóż → opłać → wyślij → dostarczone
    [Fact]
    public async Task FlowZamowienia_OdZlozeniaDoDostarczenia()
    {
        // === KROK 1 — złóż zamówienie ===
        var zamRequest = new
        {
            KlientId = 1,
            Pozycje  = new[]
            {
                new { ProduktId = 1, Ilosc = 2, Cena = 3500m },
                new { ProduktId = 2, Ilosc = 1, Cena = 150m  }
            }
        };

        var zamResponse = await _client.ZJWT(_tokenKlienta)
            .PostAsJsonAsync("/api/v1/zamowienia", zamRequest);

        zamResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var zamowienie = await zamResponse.Content
            .ReadFromJsonAsync<ZamowienieDto>();

        int zamId = zamowienie!.Id;
        zamowienie.Status.Should().Be("Nowe");

        // === KROK 2 — sprawdź że stan magazynu zmniejszony ===
        var laptopResp = await _client.GetAsync("/api/v1/produkty/1");
        var laptop = await laptopResp.Content.ReadFromJsonAsync<ProduktDetailDto>();
        laptop!.Stan.Should().Be(8);  // było 10, zamówiono 2

        // === KROK 3 — opłać zamówienie ===
        var platResponse = await _client.ZJWT(_tokenKlienta)
            .PostAsJsonAsync($"/api/v1/zamowienia/{zamId}/oplac",
                new { TransakcjaId = "TXN-123", Kwota = 7150m });

        platResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var poOplacie = await platResponse.Content
            .ReadFromJsonAsync<ZamowienieDto>();
        poOplacie!.Status.Should().Be("Oplacone");

        // === KROK 4 — admin wysyła zamówienie ===
        var wysylkaResp = await _client.ZJWT(_tokenAdmin)
            .PostAsJsonAsync($"/api/v1/zamowienia/{zamId}/wyslij",
                new { NumerSledzenia = "DHL-ABC123" });

        wysylkaResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // === KROK 5 — sprawdź końcowy stan ===
        var finalResp = await _client.ZJWT(_tokenKlienta)
            .GetAsync($"/api/v1/zamowienia/{zamId}");

        var finalZam = await finalResp.Content
            .ReadFromJsonAsync<ZamowienieDto>();

        finalZam!.Status.Should().Be("Wyslane");
        finalZam.NumerSledzenia.Should().Be("DHL-ABC123");
        finalZam.SumaCalkowita.Should().Be(7150m);
    }

    [Fact]
    public async Task ZlozZamowienie_NiedostepnyProdukt_Zwraca409()
    {
        var zamRequest = new
        {
            KlientId = 1,
            Pozycje  = new[]
            {
                new { ProduktId = 3, Ilosc = 1, Cena = 800m }  // Fotel nieaktywny
            }
        };

        var response = await _client.ZJWT(_tokenKlienta)
            .PostAsJsonAsync("/api/v1/zamowienia", zamRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

public record ZamowienieDto(int Id, string Status, decimal SumaCalkowita,
    string? NumerSledzenia = null);
```

---

### 7. Paginacja i nagłówki HTTP

csharp

```csharp
public class PaginacjaIPagingTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public PaginacjaIPagingTests(TestWebAppFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetProdukty_Paginacja_ZwracaStrone()
    {
        var response = await _client.GetAsync(
            "/api/v1/produkty?strona=1&rozmiar=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Sprawdź nagłówki paginacji
        response.Headers.Should().ContainKey("X-Total-Count");
        response.Headers.Should().ContainKey("X-Page-Size");
        response.Headers.Should().ContainKey("X-Current-Page");

        int totalCount = int.Parse(
            response.Headers.GetValues("X-Total-Count").First());
        totalCount.Should().Be(2);  // 2 aktywne produkty

        var produkty = await response.Content
            .ReadFromJsonAsync<List<ProduktListDto>>();
        produkty!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProdukty_DrugazStrona_ZwracaResztę()
    {
        var response = await _client.GetAsync(
            "/api/v1/produkty?strona=2&rozmiar=1");

        var produkty = await response.Content
            .ReadFromJsonAsync<List<ProduktListDto>>();
        produkty!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProdukt_ContentType_ApplicationJson()
    {
        var response = await _client.GetAsync("/api/v1/produkty/1");

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json");
    }

    [Fact]
    public async Task PostProdukt_Location_WskazujeNaNowyZasob()
    {
        var token = TestTokenHelper.GenerujTokenAdmin();
        var response = await _client.ZJWT(token)
            .PostAsJsonAsync("/api/v1/produkty",
                new NowyProduktDto("Nowy", 100m, 1, 10));

        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!.ToString();
        location.Should().MatchRegex(@"/api/v1/produkty/\d+");
    }
}
```

---

### 8. Uruchamianie i konfiguracja

csharp

```csharp
// xunit.runner.json — konfiguracja równoległości
// {
//   "parallelizeAssembly": false,
//   "parallelizeTestCollections": true,
//   "maxParallelThreads": 4
// }

// Uruchamianie testów
// dotnet test                                    — wszystkie
// dotnet test --filter "Category=Integration"    — filtruj przez Trait
// dotnet test --filter "ZamowieniaFlowTests"     — konkretna klasa
// dotnet test --no-build --verbosity normal      — szczegółowe logi

// Trait dla kategorii testów
[Trait("Kategoria", "Integracyjny")]
[Trait("Obszar", "Zamowienia")]
public class ZamowieniaIntegrationTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public ZamowieniaIntegrationTests(TestWebAppFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    [Trait("Priorytet", "Krytyczny")]
    public async Task HealthCheck_Zwraca200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// appsettings.Testing.json — konfiguracja dla testów
// {
//   "Logging": { "LogLevel": { "Default": "Warning" } },
//   "ConnectionStrings": { "Default": "..." },
//   "Jwt": {
//     "SecretKey": "test-secret-key-minimum-32-chars!",
//     "Issuer":    "test-issuer",
//     "Audience":  "test-audience"
//   }
// }

// Modele domenowe dla kompilacji
public class SklepContext : DbContext
{
    public SklepContext(DbContextOptions<SklepContext> o) : base(o) { }
    public DbSet<Produkt>  Produkty  => Set<Produkt>();
    public DbSet<Klient>   Klienci   => Set<Klient>();
    public DbSet<Kategoria> Kategorie => Set<Kategoria>();
}
public class Produkt
{
    public int    Id         { get; set; }
    public string Nazwa      { get; set; } = "";
    public decimal Cena      { get; set; }
    public int    KategoriaId{ get; set; }
    public int    StanMagaz  { get; set; }
    public bool   Aktywny    { get; set; } = true;
}
public class Klient
{
    public int    Id       { get; set; }
    public string Imie     { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Email    { get; set; } = "";
    public bool   Aktywny  { get; set; } = true;
}
public class Kategoria { public int Id { get; set; } public string Nazwa { get; set; } = ""; }
public interface IEmailSerwis { Task WyslijAsync(string to, string body); }
public interface ISmsSerwis { Task WyslijAsync(string nr, string tresc); }
public class JwtOpcje
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessTokenMin { get; set; } = 60;
}
```

---

### Typowe pytania rekrutacyjne

**"Czym różni się test integracyjny od unit testu w kontekście ASP.NET Core?"** Unit test testuje jedną klasę w izolacji — mocki zamiast zależności, zero infrastruktury. Integration test z `WebApplicationFactory` uruchamia cały stack aplikacji w pamięci — routing, middleware, pipeline autoryzacji, kontrolery, serwisy, baza danych (InMemory). Sprawdza że komponenty poprawnie ze sobą współpracują. Unit: "czy serwis zamówień poprawnie oblicza sumę?" Integration: "czy POST /api/zamowienia z prawidłowym body zwraca 201 i tworzy rekord w bazie?"

**"Jak izolować testy integracyjne od siebie?"** Każdy test lub klasa testowa powinna używać unikalnej nazwy bazy InMemory (`Guid.NewGuid().ToString()`). `IClassFixture<T>` — jeden obiekt factory na klasę testową, jedna baza dla wszystkich testów klasy. `IAsyncLifetime` — seed danych przed testami klasy, cleanup po. Gdy testy muszą być całkowicie izolowane — utwórz nową `WebApplicationFactory` per test (wolniejsze ale pełna izolacja). Unikaj `Guid.NewGuid()` w `IClassFixture` gdy testy modyfikują bazę — kolejny test może znaleźć zmieniony stan.

**"Jak testować endpointy wymagające autoryzacji?"** Opcja 1: `TestTokenHelper` generuje prawdziwy JWT z testowymmkluczem skonfigurowanym w `ConfigureWebHost`. Opcja 2: `AddAuthentication("Test")` + własny `TestAuthHandler` który zawsze zwraca zalogowanego użytkownika. Opcja 3: podmień `IAuthorizationService` na zawsze zwracający sukces. Opcja 1 jest najlepsza — testuje też middleware autentykacji. Pamiętaj żeby konfiguracja JWT w testach (`appsettings.Testing.json`) używała tego samego klucza co `TestTokenHelper`.

**"Dlaczego `public partial class Program` jest potrzebny?"** `WebApplicationFactory<TProgram>` wymaga dostępu do klasy `Program` żeby móc skonfigurować TestServer. W .NET 6+ Program.cs używa top-level statements — kompilator generuje klasę `Program` ale jako `internal`. `public partial class Program { }` eksponuje ją dla projektu testowego. Alternatywa: ustaw `InternalsVisibleTo` lub zmień widoczność przez atrybut assembly w Program.cs.