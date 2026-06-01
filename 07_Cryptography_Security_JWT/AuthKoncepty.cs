using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace _07_Cryptography_Security_JWT;

// ============================================================
// 1. CLAIMS, CLAIMSIDENTITY, CLAIMSPRINCIPAL
// ============================================================
// Autentykacja = "Kim jesteś?" (weryfikacja tożsamości)
// Autoryzacja  = "Co możesz zrobić?" (weryfikacja uprawnień)

public static class ClaimsDemoHelper
{
    public static ClaimsPrincipal UtworzPrincipal()
    {
        // CLAIM — pojedynczy fakt o użytkowniku (typ + wartość)
        var roszczenia = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "42"),           // ID użytkownika
            new(ClaimTypes.Name,           "jan.kowalski"), // login
            new(ClaimTypes.Email,          "jan@test.pl"),  // email
            new(ClaimTypes.Role,           "Admin"),        // rola — można wiele!
            new(ClaimTypes.Role,           "Manager"),      // druga rola
            new("department",              "IT"),           // własny claim
            new("subscription",            "Premium"),      // własny claim
            new("tenant_id",               "sklep-abc"),    // multi-tenant
            new("birth_date",              "1990-06-15"),   // dla walidacji wieku
        };

        // CLAIMSIDENTITY — zbiór claims + informacja o schemacie autentykacji
        var identity = new ClaimsIdentity(roszczenia, authenticationType: "Bearer");

        // CLAIMSPRINCIPAL — kontener na wiele tożsamości (np. Windows + External)
        return new ClaimsPrincipal(identity);
    }

    public static void PokazDostepDoClaims(ClaimsPrincipal principal)
    {
        // Dostęp do claims
        string? userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email  = principal.FindFirstValue(ClaimTypes.Email);
        bool isAdmin   = principal.IsInRole("Admin");
        bool hasClaim  = principal.HasClaim("subscription", "Premium");

        // Wszystkie role
        var role = principal.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        Console.WriteLine($"  UserId: {userId}, Email: {email}, IsAdmin: {isAdmin}");
        Console.WriteLine($"  HasClaim(subscription=Premium): {hasClaim}");
        Console.WriteLine($"  Role: [{string.Join(", ", role)}]");
        Console.WriteLine($"  IsAuthenticated: {principal.Identity?.IsAuthenticated}");
        Console.WriteLine($"  AuthType: {principal.Identity?.AuthenticationType}");
    }
}

// ============================================================
// 2. CUSTOM AUTHORIZATION HANDLER — MINIMALNY WIEK
// ============================================================

// Wymaganie — dane konfiguracyjne polityki
public class MinimalnyWiekRequirement : IAuthorizationRequirement
{
    public int MinWiek { get; }
    public MinimalnyWiekRequirement(int minWiek) => MinWiek = minWiek;
}

// Handler — sprawdza czy wymaganie jest spełnione
public class MinimalnyWiekHandler : AuthorizationHandler<MinimalnyWiekRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        MinimalnyWiekRequirement requirement)
    {
        var dataUrClaim = ctx.User.FindFirstValue("birth_date");

        if (dataUrClaim is null)
        {
            ctx.Fail(); // brak claimu — odmów
            return Task.CompletedTask;
        }

        if (!DateTime.TryParse(dataUrClaim, out var dataUr))
        {
            ctx.Fail();
            return Task.CompletedTask;
        }

        int wiek = (int)((DateTime.Today - dataUr).TotalDays / 365.25);

        if (wiek >= requirement.MinWiek)
            ctx.Succeed(requirement); // warunek spełniony
        else
            ctx.Fail(new AuthorizationFailureReason(
                this, $"Wymagany wiek: {requirement.MinWiek}+, masz: {wiek}"));

        return Task.CompletedTask;
    }
}

// ============================================================
// 3. TENANT REQUIREMENT — MULTI-TENANT AUTHORIZATION
// ============================================================

public class TenantRequirement : IAuthorizationRequirement { }

public interface ITenantSerwis
{
    Task<bool> CzyAktywnyAsync(string tenantId);
}

public class StubTenantSerwis : ITenantSerwis
{
    private readonly HashSet<string> _aktywne = ["sklep-abc", "firma-xyz"];
    public Task<bool> CzyAktywnyAsync(string tenantId)
        => Task.FromResult(_aktywne.Contains(tenantId));
}

public class TenantHandler : AuthorizationHandler<TenantRequirement>
{
    private readonly IHttpContextAccessor _httpCtx;
    private readonly ITenantSerwis        _tenantSerwis;

    public TenantHandler(IHttpContextAccessor httpCtx, ITenantSerwis tenantSerwis)
    {
        _httpCtx      = httpCtx;
        _tenantSerwis = tenantSerwis;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        TenantRequirement requirement)
    {
        var tenantId = ctx.User.FindFirstValue("tenant_id");

        if (tenantId is null) { ctx.Fail(); return; }

        bool aktywny = await _tenantSerwis.CzyAktywnyAsync(tenantId);

        if (aktywny)
            ctx.Succeed(requirement);
        else
            ctx.Fail(new AuthorizationFailureReason(
                this, $"Tenant '{tenantId}' jest nieaktywny"));
    }
}

// ============================================================
// 4. ICURRENTUSERSERVICE — DOSTĘP DO DANYCH Z TOKENU
// ============================================================

public interface ICurrentUserService
{
    int     UserId   { get; }
    string  Email    { get; }
    string  Login    { get; }
    bool    IsAdmin  { get; }
    string? TenantId { get; }
    IEnumerable<string> Roles { get; }
    bool HasClaim(string type, string value);
}

// Serwis pobierający dane zalogowanego użytkownika z HttpContext.User
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal User =>
        _http.HttpContext?.User
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

    public string? TenantId => User.FindFirstValue("tenant_id");

    public IEnumerable<string> Roles =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value);

    public bool HasClaim(string type, string value) =>
        User.HasClaim(type, value);
}

// ============================================================
// 5. BASIC AUTH HELPER — BASE64 ENCODE/DECODE
// ============================================================
// Basic Auth = Base64(login:haslo) w nagłówku Authorization
// Authorization: Basic amFuQHRlc3QucGw6VGFqbmUxMjM=
// ZAWSZE przez HTTPS — Base64 to NIE jest szyfrowanie!

public static class BasicAuthHelper
{
    // Enkoduj credentials → nagłówek Authorization
    public static string EnkodujCredentials(string login, string haslo)
    {
        string combo  = $"{login}:{haslo}";
        byte[] bajty  = Encoding.UTF8.GetBytes(combo);
        return $"Basic {Convert.ToBase64String(bajty)}";
    }

    // Dekoduj nagłówek → (login, hasło)
    public static (string Login, string Haslo)? DekodujCredentials(string naglowek)
    {
        if (!naglowek.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            string base64  = naglowek["Basic ".Length..].Trim();
            byte[] bajty   = Convert.FromBase64String(base64);
            string decoded = Encoding.UTF8.GetString(bajty);

            // Podziel na pierwsze ':' — hasło może zawierać ':'
            int idx = decoded.IndexOf(':');
            if (idx < 0) return null;

            return (decoded[..idx], decoded[(idx + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

// ============================================================
// 6. BASIC AUTH SERWIS (STUB DO DEMO)
// ============================================================

public interface IBasicAuthSerwis
{
    Task<UzytkownikInfo?> WalidujAsync(string email, string haslo, CancellationToken ct = default);
}

public class InMemoryBasicAuthSerwis : IBasicAuthSerwis
{
    private readonly Dictionary<string, (string HasloHash, UzytkownikInfo Info)> _users = new()
    {
        ["jan@test.pl"]  = (BCrypt.Net.BCrypt.HashPassword("Tajne123!"),
            new UzytkownikInfo(1, "jan.kowalski", "jan@test.pl", ["Admin", "User"])),
        ["anna@test.pl"] = (BCrypt.Net.BCrypt.HashPassword("Haslo456@"),
            new UzytkownikInfo(2, "anna.nowak", "anna@test.pl", ["User"])),
    };

    public Task<UzytkownikInfo?> WalidujAsync(string email, string haslo, CancellationToken ct = default)
    {
        if (_users.TryGetValue(email.ToLower(), out var dane)
            && BCrypt.Net.BCrypt.Verify(haslo, dane.HasloHash))
            return Task.FromResult<UzytkownikInfo?>(dane.Info);

        return Task.FromResult<UzytkownikInfo?>(null);
    }
}

// ============================================================
// 7. BASIC AUTH MIDDLEWARE
// ============================================================

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BasicAuthMiddleware> _logger;

    public BasicAuthMiddleware(RequestDelegate next, ILogger<BasicAuthMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    // Scoped serwisy wstrzykiwane przez parametry InvokeAsync (nie konstruktor!)
    public async Task InvokeAsync(HttpContext ctx, IBasicAuthSerwis authSerwis)
    {
        string? naglowek = ctx.Request.Headers.Authorization;

        if (string.IsNullOrEmpty(naglowek) ||
            !naglowek.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        try
        {
            var credentials = BasicAuthHelper.DekodujCredentials(naglowek);
            if (credentials is null) { ZwrocUnauthorized(ctx); return; }

            var (email, haslo) = credentials.Value;
            var user = await authSerwis.WalidujAsync(email, haslo, ctx.RequestAborted);

            if (user is null)
            {
                _logger.LogWarning("Nieudane Basic Auth: {Email}", email);
                ZwrocUnauthorized(ctx);
                return;
            }

            // Ustaw ClaimsPrincipal — użytkownik zalogowany
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.Login),
                new(ClaimTypes.Email,          user.Email),
            };
            claims.AddRange(user.Role.Select(r => new Claim(ClaimTypes.Role, r)));

            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Basic"));
            await _next(ctx);
        }
        catch (FormatException)
        {
            ZwrocUnauthorized(ctx);
        }
    }

    private static void ZwrocUnauthorized(HttpContext ctx)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"API\"";
    }
}

// ============================================================
// 8. BASIC AUTH HANDLER — ASP.NET CORE AUTHENTICATION
// ============================================================

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IBasicAuthSerwis _authSerwis;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opt,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IBasicAuthSerwis authSerwis)
        : base(opt, logger, encoder)
    {
        _authSerwis = authSerwis;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        string? naglowek = Request.Headers.Authorization;
        if (!naglowek!.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        try
        {
            var credentials = BasicAuthHelper.DekodujCredentials(naglowek);
            if (credentials is null)
                return AuthenticateResult.Fail("Nieprawidłowy format nagłówka Basic Auth");

            var (email, haslo) = credentials.Value;
            var user = await _authSerwis.WalidujAsync(email, haslo, Context.RequestAborted);

            if (user is null)
                return AuthenticateResult.Fail("Nieprawidłowe dane logowania");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.Login),
                new(ClaimTypes.Email,          user.Email),
            };
            claims.AddRange(user.Role.Select(r => new Claim(ClaimTypes.Role, r)));

            var identity  = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket    = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex);
        }
    }

    // Wywoływany gdy potrzebna autoryzacja — zwróć 401 z WWW-Authenticate
    protected override Task HandleChallengeAsync(AuthenticationProperties props)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Basic realm=\"API\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class AuthKonceptyDemo
{
    public static void Uruchom(IServiceProvider sp)
    {
        // --- Claims, Identity, Principal ---
        Console.WriteLine("  [Claims] Autentykacja='Kim jesteś?', Autoryzacja='Co możesz zrobić?'");
        var principal = ClaimsDemoHelper.UtworzPrincipal();
        ClaimsDemoHelper.PokazDostepDoClaims(principal);

        // UseAuthentication() MUSI być przed UseAuthorization()!
        Console.WriteLine("  [Middleware] UseAuthentication() → UseAuthorization() (kolejność krytyczna!)");

        // --- Custom Authorization Handler ---
        var handlerCtx = new AuthorizationHandlerContext(
            [new MinimalnyWiekRequirement(18)],
            principal,
            null);

        var handler = new MinimalnyWiekHandler();
        handler.HandleAsync(handlerCtx).Wait();
        Console.WriteLine($"  [MinimalnyWiekHandler] Wiek z claimu '1990-06-15': Succeeded={handlerCtx.HasSucceeded}");

        // Za młody
        var youngClaims = new[] { new Claim("birth_date", "2015-01-01") };
        var youngPrincipal = new ClaimsPrincipal(new ClaimsIdentity(youngClaims, "Bearer"));
        var youngCtx = new AuthorizationHandlerContext(
            [new MinimalnyWiekRequirement(18)],
            youngPrincipal,
            null);
        handler.HandleAsync(youngCtx).Wait();
        Console.WriteLine($"  [MinimalnyWiekHandler] Za młody (2015): Succeeded={youngCtx.HasSucceeded}, Failed={youngCtx.HasFailed}");

        // --- Tenant Handler ---
        var tenantSerwis = new StubTenantSerwis();
        var httpCtxAcc = sp.GetRequiredService<IHttpContextAccessor>();
        var tenantHandler = new TenantHandler(httpCtxAcc, tenantSerwis);
        var tenantCtx = new AuthorizationHandlerContext(
            [new TenantRequirement()],
            principal,
            null);
        tenantHandler.HandleAsync(tenantCtx).Wait();
        Console.WriteLine($"  [TenantHandler] tenant_id='sklep-abc': Succeeded={tenantCtx.HasSucceeded}");

        // --- Polityki autoryzacji ---
        Console.WriteLine("  [Polityki] AddAuthorization:");
        Console.WriteLine("    'TylkoAdmin'     → RequireRole(\"Admin\")");
        Console.WriteLine("    'AdminManager'   → RequireRole(\"Admin\", \"Manager\")");
        Console.WriteLine("    'Premium'        → RequireClaim(\"sub_tier\", \"premium\")");
        Console.WriteLine("    'TenantAccess'   → Requirements.Add(new TenantRequirement())");
        Console.WriteLine("    'MinWiek18'      → RequireAssertion(ctx => wiek >= 18)");
        Console.WriteLine("    FallbackPolicy   → RequireAuthenticatedUser() (wymaga [AllowAnonymous]!)");

        // --- Role-based na kontrolerach ---
        Console.WriteLine("  [Role] [Authorize(Roles=\"Admin\")] — całym kontroler");
        Console.WriteLine("  [Role] [Authorize(Roles=\"Admin,Manager\")] — Admin LUB Manager");
        Console.WriteLine("  [Role] [AllowAnonymous] — nadpisuje [Authorize] na kontrolerze");
        Console.WriteLine("  [Role] [Authorize(Policy=\"Premium\")] — policy-based");

        // --- ICurrentUserService ---
        Console.WriteLine("  [CurrentUser] ICurrentUserService → CurrentUserService(IHttpContextAccessor)");
        Console.WriteLine("    UserId, Email, Login, IsAdmin, TenantId, Roles, HasClaim(type, value)");
        Console.WriteLine("  Używaj w serwisach biznesowych zamiast przekazywać UserId jako parametr!");

        // --- Resource-based authorization ---
        Console.WriteLine("  [Resource-based] IAuthorizationService.AuthorizeAsync(User, resource, \"PolicyName\")");
        Console.WriteLine("    → sprawdza czy użytkownik może wykonać akcję na konkretnym zasobie");

        // --- Basic Auth ---
        string naglowek = BasicAuthHelper.EnkodujCredentials("jan@test.pl", "Tajne123!");
        Console.WriteLine($"  [BasicAuth] Nagłówek: {naglowek}");
        var decoded = BasicAuthHelper.DekodujCredentials(naglowek);
        Console.WriteLine($"  [BasicAuth] Dekodowanie: login={decoded?.Login}, haslo={decoded?.Haslo}");
        Console.WriteLine("  [BasicAuth] ZAWSZE przez HTTPS! Base64 to NIE szyfrowanie!");

        // --- Basic Auth Handler rejestracja ---
        Console.WriteLine("  [BasicAuthHandler] : AuthenticationHandler<AuthenticationSchemeOptions>");
        Console.WriteLine("    HandleAuthenticateAsync() → AuthenticateResult.Success(ticket)");
        Console.WriteLine("    HandleChallengeAsync() → 401 + WWW-Authenticate: Basic realm");
        Console.WriteLine("  Rejestracja: services.AddAuthentication(\"Basic\").AddScheme<..., BasicAuthHandler>(\"Basic\", null)");
    }
}
