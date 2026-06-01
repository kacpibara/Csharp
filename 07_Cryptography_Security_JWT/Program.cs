using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using _07_Cryptography_Security_JWT;

// ============================================================
// PROGRAM.CS — 07_Cryptography_Security_JWT
// Pokazuje konfigurację JWT Bearer + Authorization + DI,
// ale NIE uruchamia serwera HTTP (brak app.Run())
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ---- OPCJE JWT ----
var jwtOpcje = new JwtOpcje
{
    SecretKey       = "super-tajny-klucz-produkcyjny-min32znaki!",
    Issuer          = "https://api.demo.pl",
    Audience        = "https://demo.pl",
    AccessTokenMin  = 15,
    RefreshTokenDni = 7
};
builder.Services.Configure<JwtOpcje>(opt =>
{
    opt.SecretKey       = jwtOpcje.SecretKey;
    opt.Issuer          = jwtOpcje.Issuer;
    opt.Audience        = jwtOpcje.Audience;
    opt.AccessTokenMin  = jwtOpcje.AccessTokenMin;
    opt.RefreshTokenDni = jwtOpcje.RefreshTokenDni;
});
// ValidateDataAnnotations + ValidateOnStart — walidacja opcji przy starcie
builder.Services.AddOptions<JwtOpcje>()
    .Configure(opt =>
    {
        opt.SecretKey       = jwtOpcje.SecretKey;
        opt.Issuer          = jwtOpcje.Issuer;
        opt.Audience        = jwtOpcje.Audience;
        opt.AccessTokenMin  = jwtOpcje.AccessTokenMin;
        opt.RefreshTokenDni = jwtOpcje.RefreshTokenDni;
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---- AUTENTYKACJA JWT BEARER ----
// UseAuthentication() MUSI być przed UseAuthorization() w pipeline!
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Walidacja wystawcy (Issuer)
            ValidateIssuer           = true,
            ValidIssuer              = jwtOpcje.Issuer,

            // Walidacja odbiorcy (Audience)
            ValidateAudience         = true,
            ValidAudience            = jwtOpcje.Audience,

            // Walidacja czasu życia (exp + nbf)
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero, // produkcja: 0s; domyślnie 5 min

            // Walidacja podpisu (SecretKey)
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = jwtOpcje.KluczSymetryczny,

            // Obrona przed "alg:none" — wymaga algorytmu z białej listy
            ValidAlgorithms          = [SecurityAlgorithms.HmacSha256],
        };

        // JwtBearerEvents — haki w cyklu życia autentykacji
        options.Events = new JwtBearerEvents
        {
            // Wyzwalany gdy brak/zły token → 401
            OnChallenge = ctx =>
            {
                Console.WriteLine($"  [JwtEvent] OnChallenge: {ctx.Error} — {ctx.ErrorDescription}");
                return Task.CompletedTask;
            },

            // Wyzwalany gdy token OK, ale brak uprawnień → 403
            OnForbidden = ctx =>
            {
                Console.WriteLine("  [JwtEvent] OnForbidden: użytkownik uwierzytelniony, ale brak uprawnień (403)");
                return Task.CompletedTask;
            },

            // Wyzwalany gdy token jest nieprawidłowy (zły podpis, wygasły, "alg:none")
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"  [JwtEvent] OnAuthenticationFailed: {ctx.Exception.GetType().Name}");
                return Task.CompletedTask;
            },

            // Wyzwalany PO pomyślnej walidacji — można dodać/zmodyfikować claims
            OnTokenValidated = ctx =>
            {
                var jti = ctx.Principal?.FindFirst("jti")?.Value ?? "brak";
                Console.WriteLine($"  [JwtEvent] OnTokenValidated: jti={jti}");
                // Tu można: sprawdzić blacklistę odwołanych tokenów, dodać tenant claims, itp.
                return Task.CompletedTask;
            },

            // Wyzwalany przed walidacją — można odczytać token z cookie/query
            OnMessageReceived = ctx =>
            {
                // Przykład: odczyt tokenu z query string zamiast nagłówka (np. dla WebSocket)
                var tokenZQuery = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(tokenZQuery))
                    ctx.Token = tokenZQuery;
                return Task.CompletedTask;
            }
        };
    });

// ---- AUTORYZACJA — POLITYKI ----
// AddAuthorization — definiowanie polityk dostępu
builder.Services.AddAuthorization(options =>
{
    // Prosta rola
    options.AddPolicy("TylkoAdmin", policy =>
        policy.RequireRole("Admin"));

    // Wiele ról (Admin LUB Manager)
    options.AddPolicy("AdminManager", policy =>
        policy.RequireRole("Admin", "Manager"));

    // Claim-based (RequireClaim)
    options.AddPolicy("Premium", policy =>
        policy.RequireClaim("sub_tier", "premium"));

    // Custom requirement — TenantHandler
    options.AddPolicy("TenantAccess", policy =>
        policy.Requirements.Add(new TenantRequirement()));

    // RequireAssertion — minimalny wiek
    options.AddPolicy("MinWiek18", policy =>
        policy.RequireAssertion(ctx =>
        {
            var birthClaim = ctx.User.FindFirst("birth_date")?.Value;
            if (!DateOnly.TryParse(birthClaim, out var dob)) return false;
            return DateOnly.FromDateTime(DateTime.Today).Year - dob.Year >= 18;
        }));

    // FallbackPolicy — każdy endpoint wymaga autentykacji (chyba że [AllowAnonymous])
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---- REJESTRACJA HANDLERÓW ----
// IAuthorizationHandler musi być w DI (może być Transient/Scoped/Singleton)
builder.Services.AddSingleton<IAuthorizationHandler, MinimalnyWiekHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, TenantHandler>();
builder.Services.AddSingleton<ITenantSerwis, StubTenantSerwis>();

// IHttpContextAccessor — potrzebny przez TenantHandler i CurrentUserService
builder.Services.AddHttpContextAccessor();

// ICurrentUserService — scoped (żyje przez czas żądania HTTP)
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- BUILD (bez app.Run — nie startujemy serwera HTTP) ----
var app = builder.Build();

// Middleware pipeline — kolejność krytyczna!
// UseAuthentication → UseAuthorization (odwrotna kolejność = brak autoryzacji!)
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// URUCHOMIENIE DEMONSTRACJI
// ============================================================

Console.WriteLine("=== 07_Cryptography_Security_JWT ===\n");

// --- 1. HASZOWANIE HASEŁ ---
Console.WriteLine("--- Haszowanie ---");
await HaszowanieDemo.Uruchom();

// --- 2. JWT ---
Console.WriteLine("\n--- JWT ---");
JwtDemoSerwis.Uruchom();

// --- 3. AUTH KONCEPTY (Claims, Polityki, BasicAuth) ---
Console.WriteLine("\n--- Auth Koncepty ---");
AuthKonceptyDemo.Uruchom(app.Services);

// --- 4. HTTPS I BEZPIECZEŃSTWO ---
Console.WriteLine("\n--- HTTPS i Bezpieczeństwo ---");
HttpsBezpieczenstwoDemo.Uruchom();

Console.WriteLine("\n=== 07_Cryptography_Security_JWT KOMPLETNY ===");
