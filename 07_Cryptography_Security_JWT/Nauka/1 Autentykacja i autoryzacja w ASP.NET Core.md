### Autentykacja i autoryzacja w ASP.NET Core

**Autentykacja** = _Kim jesteś?_ (weryfikacja tożsamości) **Autoryzacja** = _Co możesz zrobić?_ (weryfikacja uprawnień)

---

### 1. Fundamenty — Claims, Identity, Principal

csharp

```csharp
// CLAIM — pojedynczy fakt o użytkowniku
// Typ (co to jest) + Wartość (jaka jest wartość)
var roszczenia = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, "42"),          // ID użytkownika
    new Claim(ClaimTypes.Name,           "jan.kowalski"),// login
    new Claim(ClaimTypes.Email,          "jan@test.pl"), // email
    new Claim(ClaimTypes.Role,           "Admin"),       // rola
    new Claim(ClaimTypes.Role,           "Manager"),     // wiele ról OK
    new Claim("department",              "IT"),          // własny claim
    new Claim("subscription",            "Premium"),     // własny claim
    new Claim("tenant_id",               "sklep-abc"),   // multi-tenant
};

// CLAIMSIDENTITY — zbiór claims + informacja o metodzie autentykacji
var identity = new ClaimsIdentity(
    roszczenia,
    authenticationType: "Bearer");  // jak użytkownik się zalogował

Console.WriteLine($"Zautentykowany: {identity.IsAuthenticated}");  // true
Console.WriteLine($"Typ auth: {identity.AuthenticationType}");      // Bearer
Console.WriteLine($"Nazwa: {identity.Name}");           // ClaimTypes.Name

// CLAIMSPRINCIPAL — kontener na wiele tożsamości
// (użytkownik może mieć np. Windows Identity + External Identity)
var principal = new ClaimsPrincipal(identity);

// Dostęp do claims
string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
string? email  = principal.FindFirstValue(ClaimTypes.Email);
bool isAdmin   = principal.IsInRole("Admin");
bool hasClaim  = principal.HasClaim("subscription", "Premium");

// Wszystkie role
var role = principal.FindAll(ClaimTypes.Role)
    .Select(c => c.Value)
    .ToList();

// W kontrolerze — User to ClaimsPrincipal z bieżącego requestu
[ApiController]
public class BazaController : ControllerBase
{
    // Pomocniki do pobierania danych z tokenu
    protected int    UserId   => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    protected string UserName => User.FindFirstValue(ClaimTypes.Name)!;
    protected string Email    => User.FindFirstValue(ClaimTypes.Email)!;
    protected bool   IsAdmin  => User.IsInRole("Admin");

    protected IEnumerable<string> Roles =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value);

    protected string? GetClaim(string typ) =>
        User.FindFirstValue(typ);
}
```

---

### 2. JWT — JSON Web Token

csharp

```csharp
// JWT = Header.Payload.Signature
// Zakodowany Base64 — JAWNY (nie szyfrowany!)
// Podpisany — nie można zmienić bez unieważnienia

// Header:  { "alg": "HS256", "typ": "JWT" }
// Payload: { "sub": "42", "email": "jan@test.pl", "role": "Admin", "exp": 1710000000 }
// Signature: HMACSHA256(base64(header) + "." + base64(payload), secret)

// Konfiguracja JWT — appsettings.json
// {
//   "Jwt": {
//     "SecretKey":  "super-tajny-klucz-min-32-znaki-abcdef",
//     "Issuer":     "https://api.sklep.pl",
//     "Audience":   "https://sklep.pl",
//     "ExpiryMin":  60
//   }
// }

// Opcje JWT
public class JwtOpcje
{
    public const string Sekcja = "Jwt";
    public string SecretKey { get; set; } = "";
    public string Issuer    { get; set; } = "";
    public string Audience  { get; set; } = "";
    public int    ExpiryMin { get; set; } = 60;
}

// Serwis generowania i walidacji tokenów
public interface ITokenSerwis
{
    string GenerujToken(UzytkownikDto uzytkownik);
    string GenerujRefreshToken();
    ClaimsPrincipal? WalidujToken(string token);
}

public class JwtTokenSerwis : ITokenSerwis
{
    private readonly JwtOpcje _opcje;

    public JwtTokenSerwis(IOptions<JwtOpcje> opcje)
        => _opcje = opcje.Value;

    public string GenerujToken(UzytkownikDto uzytkownik)
    {
        var klucz      = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(_opcje.SecretKey));
        var poswiadczenia = new SigningCredentials(
            klucz, SecurityAlgorithms.HmacSha256);

        // Claims w tokenie
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   uzytkownik.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, uzytkownik.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(ClaimTypes.Name,               uzytkownik.Login),
        };

        // Dodaj wszystkie role
        claims.AddRange(uzytkownik.Role.Select(r =>
            new Claim(ClaimTypes.Role, r)));

        // Dodaj własne claims
        if (uzytkownik.TenantId != null)
            claims.Add(new Claim("tenant_id", uzytkownik.TenantId));

        if (uzytkownik.Subskrypcja != null)
            claims.Add(new Claim("subscription", uzytkownik.Subskrypcja));

        var token = new JwtSecurityToken(
            issuer:             _opcje.Issuer,
            audience:           _opcje.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_opcje.ExpiryMin),
            signingCredentials: poswiadczenia);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerujRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? WalidujToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parametry = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = false,  // NIE sprawdzaj wygaśnięcia (dla refresh)
            ValidateIssuerSigningKey  = true,
            ValidIssuer              = _opcje.Issuer,
            ValidAudience            = _opcje.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_opcje.SecretKey))
        };

        try
        {
            return handler.ValidateToken(token, parametry, out _);
        }
        catch
        {
            return null;
        }
    }
}

public record UzytkownikDto(
    int    Id,
    string Login,
    string Email,
    List<string> Role,
    string? TenantId    = null,
    string? Subskrypcja = null);
```

---

### 3. Konfiguracja autentykacji JWT w ASP.NET Core

csharp

```csharp
// Program.cs — rejestracja JWT Authentication

// dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Rejestracja opcji JWT
builder.Services.Configure<JwtOpcje>(
    builder.Configuration.GetSection(JwtOpcje.Sekcja));
builder.Services.AddScoped<ITokenSerwis, JwtTokenSerwis>();

var jwtOpcje = builder.Configuration
    .GetSection(JwtOpcje.Sekcja)
    .Get<JwtOpcje>()!;

var kluczBytes = System.Text.Encoding.UTF8.GetBytes(jwtOpcje.SecretKey);

// Konfiguracja autentykacji
builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey  = true,
            ValidIssuer              = jwtOpcje.Issuer,
            ValidAudience            = jwtOpcje.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(kluczBytes),
            ClockSkew                = TimeSpan.FromSeconds(30) // tolerancja zegara
        };

        // Zdarzenia JWT — hooks do customizacji
        opt.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers["Token-Expired"] = "true";

                return Task.CompletedTask;
            },

            OnTokenValidated = ctx =>
            {
                // Możesz dodać dodatkową walidację
                var userId = ctx.Principal!
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                Console.WriteLine($"Token zwalidowany dla użytkownika: {userId}");
                return Task.CompletedTask;
            },

            // Dla SignalR / WebSocket — token z query string
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path  = ctx.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(token)
                    && path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// Konfiguracja autoryzacji
builder.Services.AddAuthorization(opt =>
{
    // Polityki — kompozycja wymagań
    opt.AddPolicy("TylkoAdmin", policy =>
        policy.RequireRole("Admin"));

    opt.AddPolicy("AdminLubManager", policy =>
        policy.RequireRole("Admin", "Manager"));

    opt.AddPolicy("Premium", policy =>
        policy.RequireClaim("subscription", "Premium"));

    opt.AddPolicy("TenantAccess", policy =>
        policy.Requirements.Add(new TenantRequirement()));

    opt.AddPolicy("MinWiek18", policy =>
        policy.RequireAssertion(ctx =>
        {
            var dataUrClaim = ctx.User.FindFirstValue("birth_date");
            if (dataUrClaim == null) return false;
            var dataUr = DateTime.Parse(dataUrClaim);
            return (DateTime.Today - dataUr).TotalDays >= 18 * 365;
        }));

    // Domyślna polityka — wymagaj autentykacji
    opt.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    // Każdy endpoint wymaga logowania CHYBA ŻE [AllowAnonymous]!
});

var app = builder.Build();

app.UseAuthentication();  // MUSI być przed UseAuthorization!
app.UseAuthorization();

app.MapControllers();
app.Run();
```

---

### 4. Kontroler autentykacji — login i refresh

csharp

```csharp
[ApiController]
[Route("api/auth")]
[AllowAnonymous]          // cały kontroler dostępny bez tokenu
public class AuthController : ControllerBase
{
    private readonly IAuthSerwis    _authSerwis;
    private readonly ITokenSerwis   _tokenSerwis;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthSerwis authSerwis,
        ITokenSerwis tokenSerwis,
        ILogger<AuthController> logger)
    {
        _authSerwis  = authSerwis;
        _tokenSerwis = tokenSerwis;
        _logger      = logger;
    }

    /// <summary>Zaloguj się i uzyskaj tokeny JWT</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenyDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto,
        CancellationToken ct)
    {
        var uzytkownik = await _authSerwis.WalidujAsync(dto.Email, dto.Haslo, ct);

        if (uzytkownik is null)
        {
            _logger.LogWarning(
                "Nieudana próba logowania: {Email}", dto.Email);
            return Unauthorized(new { blad = "Nieprawidłowy email lub hasło" });
        }

        string accessToken  = _tokenSerwis.GenerujToken(uzytkownik);
        string refreshToken = _tokenSerwis.GenerujRefreshToken();

        // Zapisz refresh token w bazie
        await _authSerwis.ZapiszRefreshTokenAsync(
            uzytkownik.Id, refreshToken, ct);

        _logger.LogInformation("Zalogowano: {Email}", dto.Email);

        return Ok(new TokenyDto(
            AccessToken:  accessToken,
            RefreshToken: refreshToken,
            ExpiresIn:    3600,
            TokenType:    "Bearer"));
    }

    /// <summary>Odśwież token dostępu</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshDto dto,
        CancellationToken ct)
    {
        // Waliduj stary access token (bez sprawdzania wygaśnięcia!)
        var principal = _tokenSerwis.WalidujToken(dto.AccessToken);
        if (principal is null)
            return Unauthorized(new { blad = "Nieprawidłowy token" });

        string userId = principal.FindFirstValue(
            ClaimTypes.NameIdentifier)!;

        // Waliduj refresh token w bazie
        bool refreshPoprawny = await _authSerwis
            .WalidujRefreshTokenAsync(int.Parse(userId), dto.RefreshToken, ct);

        if (!refreshPoprawny)
            return Unauthorized(new { blad = "Refresh token nieważny lub wygasł" });

        // Pobierz aktualne dane użytkownika
        var uzytkownik = await _authSerwis
            .PobierzUzytkownikaAsync(int.Parse(userId), ct);

        if (uzytkownik is null)
            return Unauthorized();

        // Wygeneruj nowe tokeny
        string nowyAccess  = _tokenSerwis.GenerujToken(uzytkownik);
        string nowyRefresh = _tokenSerwis.GenerujRefreshToken();

        await _authSerwis.OdswiezRefreshTokenAsync(
            int.Parse(userId), dto.RefreshToken, nowyRefresh, ct);

        return Ok(new TokenyDto(nowyAccess, nowyRefresh, 3600, "Bearer"));
    }

    /// <summary>Wyloguj — unieważnij refresh token</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        int userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await _authSerwis.UsunRefreshTokenyAsync(userId, ct);
        return NoContent();
    }

    /// <summary>Zmień hasło</summary>
    [HttpPost("zmien-haslo")]
    [Authorize]
    public async Task<IActionResult> ZmienHaslo(
        [FromBody] ZmienHasloDto dto,
        CancellationToken ct)
    {
        int userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        bool ok = await _authSerwis
            .ZmienHasloAsync(userId, dto.Obecne, dto.Nowe, ct);

        return ok
            ? NoContent()
            : BadRequest(new { blad = "Obecne hasło nieprawidłowe" });
    }
}

// DTOs
public record LoginDto(string Email, string Haslo);
public record RefreshDto(string AccessToken, string RefreshToken);
public record ZmienHasloDto(string Obecne, string Nowe);
public record TokenyDto(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    string TokenType);
```

---

### 5. Autoryzacja — Role i Polityki

csharp

```csharp
// Role-based authorization
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]     // cały kontroler tylko dla Admin
public class AdminController : ControllerBase
{
    [HttpGet("uzytkownicy")]
    public IActionResult PobierzUzytkownikow() => Ok("lista");

    [HttpDelete("uzytkownik/{id}")]
    [Authorize(Roles = "Admin")]      // nadmiarowe ale czytelne
    public IActionResult UsunUzytkownika(int id) => NoContent();

    [HttpGet("logi")]
    [Authorize(Roles = "Admin,SuperAdmin")]  // Admin LUB SuperAdmin
    public IActionResult PobierzLogi() => Ok("logi");
}

// Policy-based authorization
[ApiController]
[Route("api/premium")]
public class PremiumController : ControllerBase
{
    [HttpGet("raporty")]
    [Authorize(Policy = "Premium")]        // wymaga claim subscription=Premium
    public IActionResult Raporty() => Ok("raporty premium");

    [HttpGet("eksport")]
    [Authorize(Policy = "AdminLubManager")]
    public IActionResult Eksport() => Ok("eksport danych");
}

// Mieszanie autoryzacji
[ApiController]
[Route("api/produkty")]
[Authorize]                               // zalogowany użytkownik
public class ProduktyController : ControllerBase
{
    [HttpGet]
    // Bez dodatkowego [Authorize] — wystarczy zalogowanie
    public IActionResult Lista() => Ok("lista dla zalogowanych");

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]  // tylko Admin lub Manager
    public IActionResult Dodaj() => Ok("dodano");

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]          // tylko Admin
    public IActionResult Usun(int id) => Ok("usunięto");

    [HttpGet("publiczne")]
    [AllowAnonymous]                      // nadpisuje [Authorize] z kontrolera!
    public IActionResult Publiczne() => Ok("dostępne dla wszystkich");
}
```

---

### 6. Custom Authorization Handler

csharp

```csharp
// Własne wymaganie autoryzacji

// Wymaganie — dane konfiguracyjne polityki
public class MinimalnyWiekRequirement
    : IAuthorizationRequirement
{
    public int MinWiek { get; }
    public MinimalnyWiekRequirement(int minWiek) => MinWiek = minWiek;
}

// Handler — sprawdza czy wymaganie jest spełnione
public class MinimalnyWiekHandler
    : AuthorizationHandler<MinimalnyWiekRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        MinimalnyWiekRequirement requirement)
    {
        var dataUrClaim = ctx.User.FindFirstValue("birth_date");

        if (dataUrClaim is null)
        {
            ctx.Fail();  // brak claimu — odmów
            return Task.CompletedTask;
        }

        if (!DateTime.TryParse(dataUrClaim, out var dataUr))
        {
            ctx.Fail();
            return Task.CompletedTask;
        }

        int wiek = (int)((DateTime.Today - dataUr).TotalDays / 365.25);

        if (wiek >= requirement.MinWiek)
            ctx.Succeed(requirement);  // warunek spełniony!
        else
            ctx.Fail(new AuthorizationFailureReason(
                this, $"Wymagany wiek: {requirement.MinWiek}+, masz: {wiek}"));

        return Task.CompletedTask;
    }
}

// Wymaganie multi-tenant
public class TenantRequirement : IAuthorizationRequirement { }

public class TenantHandler
    : AuthorizationHandler<TenantRequirement>
{
    private readonly IHttpContextAccessor _httpCtx;
    private readonly ITenantSerwis _tenantSerwis;

    public TenantHandler(
        IHttpContextAccessor httpCtx,
        ITenantSerwis tenantSerwis)
    {
        _httpCtx       = httpCtx;
        _tenantSerwis  = tenantSerwis;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        TenantRequirement requirement)
    {
        var tenantId = ctx.User.FindFirstValue("tenant_id");

        if (tenantId is null)
        {
            ctx.Fail();
            return;
        }

        // Sprawdź czy tenant istnieje i jest aktywny
        bool aktywny = await _tenantSerwis.CzyAktywnyAsync(tenantId);

        if (aktywny)
            ctx.Succeed(requirement);
        else
            ctx.Fail(new AuthorizationFailureReason(
                this, $"Tenant '{tenantId}' jest nieaktywny"));
    }
}

// Rejestracja wymagań i handlerów
builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("Pelnoletni", policy =>
        policy.Requirements.Add(new MinimalnyWiekRequirement(18)));

    opt.AddPolicy("Dorosly21", policy =>
        policy.Requirements.Add(new MinimalnyWiekRequirement(21)));

    opt.AddPolicy("TenantAccess", policy =>
        policy.Requirements.Add(new TenantRequirement()));
});

builder.Services.AddScoped
    IAuthorizationHandler, MinimalnyWiekHandler>();
builder.Services.AddScoped
    IAuthorizationHandler, TenantHandler>();
builder.Services.AddHttpContextAccessor();

// Użycie w kontrolerze z manualną autoryzacją
[ApiController]
[Route("api/zamowienia")]
[Authorize]
public class ZamowieniaController : ControllerBase
{
    private readonly IAuthorizationService _authService;

    public ZamowieniaController(IAuthorizationService authService)
        => _authService = authService;

    [HttpGet("{id}")]
    public async Task<IActionResult> Pobierz(int id)
    {
        var zamowienie = await PobierzZamowienieAsync(id);

        // Sprawdź ręcznie czy użytkownik może widzieć to zamówienie
        var wynik = await _authService.AuthorizeAsync(
            User, zamowienie, "WlascicielLubAdmin");

        if (!wynik.Succeeded)
        {
            return Forbid();  // 403
        }

        return Ok(zamowienie);
    }

    // Resource-based authorization
    [HttpDelete("{id}")]
    public async Task<IActionResult> Usun(int id)
    {
        var zasob = await PobierzZamowienieAsync(id);

        // Sprawdź czy użytkownik może usunąć ten konkretny zasób
        var wynik = await _authService.AuthorizeAsync(
            User, zasob, "MozeUsunac");

        return wynik.Succeeded ? NoContent() : Forbid();
    }

    private Task<object> PobierzZamowienieAsync(int id) =>
        Task.FromResult<object>(new { Id = id });
}
```

---

### 7. Pobieranie danych użytkownika w serwisach

csharp

```csharp
// ICurrentUserService — abstrakcja nad HttpContext.User

public interface ICurrentUserService
{
    int     UserId     { get; }
    string  Email      { get; }
    string  Login      { get; }
    bool    IsAdmin    { get; }
    string? TenantId   { get; }
    IEnumerable<string> Roles { get; }
    bool HasClaim(string type, string value);
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpCtx;

    public CurrentUserService(IHttpContextAccessor httpCtx)
        => _httpCtx = httpCtx;

    private ClaimsPrincipal User =>
        _httpCtx.HttpContext?.User
        ?? throw new InvalidOperationException("Brak kontekstu HTTP");

    public int UserId => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Brak claim UserId"));

    public string Email =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Brak claim Email");

    public string Login =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? throw new UnauthorizedAccessException("Brak claim Name");

    public bool IsAdmin => User.IsInRole("Admin");

    public string? TenantId =>
        User.FindFirstValue("tenant_id");

    public IEnumerable<string> Roles =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value);

    public bool HasClaim(string type, string value) =>
        User.HasClaim(type, value);
}

// Rejestracja
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Użycie w serwisie biznesowym
public class ZamowieniaSerwis
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _user;

    public ZamowieniaSerwis(IUnitOfWork uow, ICurrentUserService user)
    {
        _uow  = uow;
        _user = user;
    }

    public async Task<List<Zamowienie>> PobierzMojeAsync(
        CancellationToken ct = default)
    {
        // Automatycznie filtruj po zalogowanym użytkowniku
        var zamowienia = await _uow.Zamowienia
            .PobierzPoKliencieAsync(_user.UserId, ct);

        return zamowienia.ToList();
    }

    public async Task<int> ZlozAsync(
        NoweZamowienieDto dto,
        CancellationToken ct = default)
    {
        // Wymusz ID zalogowanego użytkownika (ignoruj to co przysłał klient)
        var zamowienie = new Zamowienie
        {
            KlientId = _user.UserId,   // zawsze z tokenu!
            // ...
        };

        await _uow.Zamowienia.DodajAsync(zamowienie, ct);
        await _uow.ZapiszAsync(ct);
        return zamowienie.Id;
    }
}
```

---

### 8. Praktyczny przykład — kompletny system auth

csharp

```csharp
// Kompletna implementacja z loginiem, refresh tokenem i autoryzacją

// Model użytkownika w bazie
public class Uzytkownik
{
    public int      Id           { get; set; }
    public string   Login        { get; set; } = "";
    public string   Email        { get; set; } = "";
    public string   HashHasla    { get; set; } = "";
    public bool     Aktywny      { get; set; } = true;
    public DateTime DataRejestracji { get; set; } = DateTime.UtcNow;

    public List<Rola>          Role          { get; set; } = new();
    public List<RefreshToken>  RefreshTokeny { get; set; } = new();
}

public class Rola
{
    public int    Id    { get; set; }
    public string Nazwa { get; set; } = "";
}

public class RefreshToken
{
    public int      Id          { get; set; }
    public int      UzytkownikId{ get; set; }
    public string   Token       { get; set; } = "";
    public DateTime Wygasa      { get; set; }
    public bool     Uzyty       { get; set; } = false;
    public string?  ZastapionoPrzez { get; set; }
}

// Serwis autoryzacji
public class AuthSerwis : IAuthSerwis
{
    private readonly AppDbContext _ctx;
    private readonly IPasswordHasher<Uzytkownik> _hasher;

    public AuthSerwis(AppDbContext ctx, IPasswordHasher<Uzytkownik> hasher)
    {
        _ctx    = ctx;
        _hasher = hasher;
    }

    public async Task<UzytkownikDto?> WalidujAsync(
        string email, string haslo, CancellationToken ct)
    {
        var user = await _ctx.Uzytkownicy
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u =>
                u.Email == email && u.Aktywny, ct);

        if (user is null) return null;

        var wynikWeryfikacji = _hasher.VerifyHashedPassword(
            user, user.HashHasla, haslo);

        if (wynikWeryfikacji == PasswordVerificationResult.Failed)
            return null;

        return new UzytkownikDto(
            user.Id,
            user.Login,
            user.Email,
            user.Role.Select(r => r.Nazwa).ToList());
    }

    public async Task ZapiszRefreshTokenAsync(
        int userId, string token, CancellationToken ct)
    {
        // Unieważnij stare tokeny
        var stare = await _ctx.RefreshTokeny
            .Where(t => t.UzytkownikId == userId && !t.Uzyty)
            .ToListAsync(ct);

        stare.ForEach(t => t.Uzyty = true);

        // Dodaj nowy
        _ctx.RefreshTokeny.Add(new RefreshToken
        {
            UzytkownikId = userId,
            Token        = token,
            Wygasa       = DateTime.UtcNow.AddDays(7)
        });

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> WalidujRefreshTokenAsync(
        int userId, string token, CancellationToken ct)
    {
        var rt = await _ctx.RefreshTokeny
            .FirstOrDefaultAsync(t =>
                t.UzytkownikId == userId
                && t.Token == token
                && !t.Uzyty
                && t.Wygasa > DateTime.UtcNow, ct);

        return rt is not null;
    }

    public async Task OdswiezRefreshTokenAsync(
        int userId, string staryToken, string nowyToken, CancellationToken ct)
    {
        var rt = await _ctx.RefreshTokeny
            .FirstOrDefaultAsync(t =>
                t.UzytkownikId == userId && t.Token == staryToken, ct);

        if (rt is not null)
        {
            rt.Uzyty            = true;
            rt.ZastapionoPrzez  = nowyToken;
        }

        _ctx.RefreshTokeny.Add(new RefreshToken
        {
            UzytkownikId = userId,
            Token        = nowyToken,
            Wygasa       = DateTime.UtcNow.AddDays(7)
        });

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task UsunRefreshTokenyAsync(int userId, CancellationToken ct)
    {
        var tokeny = await _ctx.RefreshTokeny
            .Where(t => t.UzytkownikId == userId && !t.Uzyty)
            .ToListAsync(ct);

        tokeny.ForEach(t => t.Uzyty = true);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<UzytkownikDto?> PobierzUzytkownikaAsync(
        int id, CancellationToken ct)
    {
        var user = await _ctx.Uzytkownicy
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.Aktywny, ct);

        return user is null ? null : new UzytkownikDto(
            user.Id, user.Login, user.Email,
            user.Role.Select(r => r.Nazwa).ToList());
    }

    public async Task<bool> ZmienHasloAsync(
        int userId, string obecne, string nowe, CancellationToken ct)
    {
        var user = await _ctx.Uzytkownicy.FindAsync(
            new object[] { userId }, ct);

        if (user is null) return false;

        var wynik = _hasher.VerifyHashedPassword(user, user.HashHasla, obecne);
        if (wynik == PasswordVerificationResult.Failed) return false;

        user.HashHasla = _hasher.HashPassword(user, nowe);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

// Stub interfaces i klasy
public interface IAuthSerwis
{
    Task<UzytkownikDto?> WalidujAsync(string email, string haslo, CancellationToken ct);
    Task ZapiszRefreshTokenAsync(int userId, string token, CancellationToken ct);
    Task<bool> WalidujRefreshTokenAsync(int userId, string token, CancellationToken ct);
    Task OdswiezRefreshTokenAsync(int userId, string stary, string nowy, CancellationToken ct);
    Task UsunRefreshTokenyAsync(int userId, CancellationToken ct);
    Task<UzytkownikDto?> PobierzUzytkownikaAsync(int id, CancellationToken ct);
    Task<bool> ZmienHasloAsync(int userId, string obecne, string nowe, CancellationToken ct);
}
public interface ITenantSerwis { Task<bool> CzyAktywnyAsync(string id); }
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }
    public DbSet<Uzytkownik>  Uzytkownicy  => Set<Uzytkownik>();
    public DbSet<RefreshToken> RefreshTokeny => Set<RefreshToken>();
}
public record Zamowienie(int Id, int KlientId);
public record NoweZamowienieDto(int KlientId);
public interface IUnitOfWork
{
    IZamowienieRepo Zamowienia { get; }
    Task<int> ZapiszAsync(CancellationToken ct = default);
}
public interface IZamowienieRepo
{
    Task<IEnumerable<Zamowienie>> PobierzPoKliencieAsync(int id, CancellationToken ct);
    Task DodajAsync(Zamowienie z, CancellationToken ct);
}
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między autentykacją a autoryzacją?"** Autentykacja weryfikuje tożsamość — odpowiada na pytanie "Kim jesteś?" (weryfikacja tokenu JWT, hasła). Autoryzacja weryfikuje uprawnienia — odpowiada na pytanie "Co możesz zrobić?" (czy rola Admin, czy masz claim premium). W ASP.NET Core kolejność jest kluczowa: `UseAuthentication()` MUSI być przed `UseAuthorization()` — najpierw ustal kto to jest, potem co może.

**"Co to JWT i dlaczego nie trzymamy sesji na serwerze?"** JWT (JSON Web Token) to podpisany token zawierający claims użytkownika. Serwer nie trzyma stanu — token jest samodzielny i weryfikowany przez podpis kryptograficzny. Zalety: skalowalność (każdy serwer może weryfikować token bez wspólnej sesji), stateless, nadaje się do mikroserwisów. Wady: nie można unieważnić przed wygaśnięciem (chyba że prowadzisz blacklistę), większy rozmiar niż session ID.

**"Co to Claim i po co używać własnych claims?"** Claim to para typ-wartość opisująca użytkownika (`ClaimTypes.Email = "jan@test.pl"`). Standardowe claims (NameIdentifier, Email, Role) obsługiwane automatycznie. Własne claims gdy potrzebujesz przekazać w tokenie dane biznesowe dostępne w każdym serwisie bez dodatkowych zapytań do bazy: `tenant_id` (multi-tenancy), `subscription` (plan użytkownika), `department` (dział firmy). Pamiętaj: JWT jest jawny — nie umieszczaj sekretów w claims!

**"Jak działa Refresh Token i po co?"** Access token ma krótki czas życia (15-60 min) dla bezpieczeństwa. Gdy wygaśnie użytkownik potrzebuje nowego — bez ponownego logowania. Refresh token to długożyjący (7-30 dni), losowy token przechowywany w bazie. Klient wysyła stary access token + refresh token → serwer waliduje refresh token w bazie → wystawia nowe oba tokeny (rotation) → oznacza stary refresh jako użyty. Jeśli skradziony refresh token zostanie użyty po rotacji — wykryjesz podwójne użycie i możesz unieważnić wszystkie tokeny użytkownika.