using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace _06_ASPNetCore_RestAPI;

// ============================================================
// INTERFEJSY I MODELE
// ============================================================

public interface ILicznikSerwis
{
    Guid InstanceId { get; }
    int Wywolania { get; }
    void Inkrementuj();
}

public interface INotifikacja
{
    string Nazwa { get; }
    Task WyslijAsync(string wiadomosc);
}

public interface IEmailSerwis
{
    Task WyslijEmailAsync(string do_, string temat, string tresc);
}

public interface IKeszSerwis
{
    void Ustaw(string klucz, object wartosc);
    object? Pobierz(string klucz);
}

// ============================================================
// IMPLEMENTACJE LIFETIME DEMO
// ============================================================

public class TransientLicznik : ILicznikSerwis
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int Wywolania { get; private set; }
    public void Inkrementuj() => Wywolania++;
}

public class ScopedLicznik : ILicznikSerwis
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int Wywolania { get; private set; }
    public void Inkrementuj() => Wywolania++;
}

public class SingletonLicznik : ILicznikSerwis
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int Wywolania { get; private set; }
    public void Inkrementuj() => Wywolania++;
}

// ============================================================
// WIELE IMPLEMENTACJI TEGO SAMEGO INTERFEJSU
// ============================================================

public class EmailNotifikacja : INotifikacja
{
    public string Nazwa => "Email";
    public Task WyslijAsync(string wiadomosc)
    {
        Console.WriteLine($"[Email] Wysyłam: {wiadomosc}");
        return Task.CompletedTask;
    }
}

public class SmsNotifikacja : INotifikacja
{
    public string Nazwa => "SMS";
    public Task WyslijAsync(string wiadomosc)
    {
        Console.WriteLine($"[SMS] Wysyłam: {wiadomosc}");
        return Task.CompletedTask;
    }
}

public class PushNotifikacja : INotifikacja
{
    public string Nazwa => "Push";
    public Task WyslijAsync(string wiadomosc)
    {
        Console.WriteLine($"[Push] Wysyłam: {wiadomosc}");
        return Task.CompletedTask;
    }
}

// ============================================================
// KEYED SERVICES (.NET 8)
// ============================================================

public class SqlKesz : IKeszSerwis
{
    private readonly Dictionary<string, object> _store = new();
    public void Ustaw(string klucz, object wartosc) => _store[klucz] = wartosc;
    public object? Pobierz(string klucz) => _store.TryGetValue(klucz, out var v) ? v : null;
}

public class RedisKesz : IKeszSerwis
{
    private readonly Dictionary<string, object> _store = new();
    public void Ustaw(string klucz, object wartosc) => _store[klucz] = wartosc;
    public object? Pobierz(string klucz) => _store.TryGetValue(klucz, out var v) ? v : null;
}

// ============================================================
// OPTIONS PATTERN
// ============================================================

public class SmtpOpcje
{
    public const string Sekcja = "Smtp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Uzytkownik { get; set; } = "";
    public bool UzyjSsl { get; set; } = true;
}

public class JwtOpcje
{
    public const string Sekcja = "Jwt";
    public string Klucz { get; set; } = "";
    public string Wydawca { get; set; } = "";
    public string Odbiorca { get; set; } = "";
    public int WygasaPoMinutach { get; set; } = 60;
}

public class LimityOpcje
{
    public const string Sekcja = "Limity";

    [System.ComponentModel.DataAnnotations.Required]
    public int MaksymalnaLiczbaPrzed { get; set; } = 100;

    [System.ComponentModel.DataAnnotations.Range(1, 10000)]
    public int MaksymalnyRozmiarStrony { get; set; } = 50;
}

// ============================================================
// SERWISY UŻYWAJĄCE OPTIONS
// ============================================================

public class SmtpEmailSerwis : IEmailSerwis
{
    private readonly SmtpOpcje _opcje;

    // IOptions<T> — wartość nie zmienia się po starcie
    public SmtpEmailSerwis(IOptions<SmtpOpcje> opcje)
    {
        _opcje = opcje.Value;
    }

    public Task WyslijEmailAsync(string do_, string temat, string tresc)
    {
        Console.WriteLine($"[SMTP:{_opcje.Host}:{_opcje.Port}] -> {do_}: {temat}");
        return Task.CompletedTask;
    }
}

public class MonitorSerwis
{
    private readonly IOptionsMonitor<SmtpOpcje> _monitor;

    // IOptionsMonitor<T> — powiadamia o zmianach w runtime
    public MonitorSerwis(IOptionsMonitor<SmtpOpcje> monitor)
    {
        _monitor = monitor;
        _monitor.OnChange(opcje =>
            Console.WriteLine($"[Monitor] Smtp Host zmieniony na: {opcje.Host}"));
    }

    public string PobierzHost() => _monitor.CurrentValue.Host;
}

public class SnapshotSerwis
{
    private readonly IOptionsSnapshot<SmtpOpcje> _snapshot;

    // IOptionsSnapshot<T> — Scoped, odświeżane per-request
    public SnapshotSerwis(IOptionsSnapshot<SmtpOpcje> snapshot)
    {
        _snapshot = snapshot;
    }

    public string PobierzHost() => _snapshot.Value.Host;
}

// ============================================================
// CAPTIVE DEPENDENCY — PROBLEM I ROZWIĄZANIE
// ============================================================

// Problem: Singleton nie powinien bezpośrednio zależeć od Scoped
// Rozwiązanie: IServiceScopeFactory tworzy scope per-operację

public class SingletonZScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SingletonZScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task WykonajOperacjeAsync()
    {
        // Tworzymy scope ręcznie — Scoped serwis żyje tylko przez czas operacji
        await using var scope = _scopeFactory.CreateAsyncScope();
        var scoped = scope.ServiceProvider.GetRequiredService<ScopedLicznik>();
        scoped.Inkrementuj();
        Console.WriteLine($"[CaptiveFix] Scoped instance: {scoped.InstanceId}, wywołania: {scoped.Wywolania}");
    }
}

// ============================================================
// BACKGROUND SERVICE
// ============================================================

public class SelfTestService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private IHttpClientFactory? _httpClientFactory;

    public SelfTestService(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Czekamy aż aplikacja w pełni wystartuje
        await Task.Delay(1500, stoppingToken);

        Console.WriteLine("\n=== 06_ASPNetCore_RestAPI — SELF-TEST START ===\n");

        await using var scope = _scopeFactory.CreateAsyncScope();
        _httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        await TestujArchitekture(scope.ServiceProvider);
        await TestujDI(scope.ServiceProvider);
        await TestujMiddleware(scope.ServiceProvider);
        await TestujEndpointy(stoppingToken);
        await TestujWalidacje(stoppingToken);
        await TestujSwagger(stoppingToken);
        await TestujHealthChecks(stoppingToken);

        Console.WriteLine("\n=== 06_ASPNetCore_RestAPI KOMPLETNY ===");
        _lifetime.StopApplication();
    }

    private async Task TestujArchitekture(IServiceProvider sp)
    {
        Console.WriteLine("--- [1] Architektura i fundamenty ---");

        // 3-fazowy wzorzec: Services → Build → Pipeline (pokazany w Program.cs)
        Console.WriteLine("WebApplication.CreateBuilder: 3-fazowy wzorzec (Services -> Build -> Pipeline)");
        Console.WriteLine("Middleware pipeline: Use/Run/Map/MapWhen/UseWhen");
        Console.WriteLine("DI lifetimes: Transient (nowa inst.), Scoped (per-request), Singleton (jedna inst.)");

        // Environments
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        Console.WriteLine($"Środowisko: {env.EnvironmentName}, IsDevelopment: {env.IsDevelopment()}");

        // Configuration hierarchy
        var config = sp.GetRequiredService<IConfiguration>();
        Console.WriteLine($"Konfiguracja — hierarchia: appsettings.json < env-specific < env vars < CLI args");
        Console.WriteLine($"  Smtp:Host = '{config["Smtp:Host"]}'");

        // Options pattern
        var smtpOpcje = sp.GetRequiredService<IOptions<SmtpOpcje>>().Value;
        Console.WriteLine($"Options<SmtpOpcje>: Host={smtpOpcje.Host}, Port={smtpOpcje.Port}, SSL={smtpOpcje.UzyjSsl}");

        var jwtOpcje = sp.GetRequiredService<IOptions<JwtOpcje>>().Value;
        Console.WriteLine($"Options<JwtOpcje>: Wydawca={jwtOpcje.Wydawca}, WygasaPo={jwtOpcje.WygasaPoMinutach}min");

        await Task.CompletedTask;
    }

    private async Task TestujDI(IServiceProvider sp)
    {
        Console.WriteLine("\n--- [2] Dependency Injection ---");

        // Lifetime demo
        var t1 = sp.GetRequiredService<TransientLicznik>();
        var t2 = sp.GetRequiredService<TransientLicznik>();
        Console.WriteLine($"Transient: inst1={t1.InstanceId.ToString()[..8]}, inst2={t2.InstanceId.ToString()[..8]}, różne={t1.InstanceId != t2.InstanceId}");

        var s1 = sp.GetRequiredService<ScopedLicznik>();
        var s2 = sp.GetRequiredService<ScopedLicznik>();
        Console.WriteLine($"Scoped: inst1=inst2 (w tym samym scope) = {s1.InstanceId == s2.InstanceId}");

        var sing1 = sp.GetRequiredService<SingletonLicznik>();
        var sing2 = sp.GetRequiredService<SingletonLicznik>();
        Console.WriteLine($"Singleton: zawsze ta sama instancja = {sing1.InstanceId == sing2.InstanceId}");

        // Wiele implementacji
        var notifikacje = sp.GetRequiredService<IEnumerable<INotifikacja>>();
        Console.WriteLine("Wiele impl. INotifikacja: " + string.Join(", ", notifikacje.Select(n => n.Nazwa)));
        foreach (var n in notifikacje)
            await n.WyslijAsync("Testowa wiadomość");

        // Keyed services
        var sqlKesz = sp.GetRequiredKeyedService<IKeszSerwis>("sql");
        var redisKesz = sp.GetRequiredKeyedService<IKeszSerwis>("redis");
        sqlKesz.Ustaw("key1", "value1");
        redisKesz.Ustaw("key1", "value_redis");
        Console.WriteLine($"Keyed[sql]: {sqlKesz.Pobierz("key1")}, Keyed[redis]: {redisKesz.Pobierz("key1")}");

        // Captive dependency fix
        var singleton = sp.GetRequiredService<SingletonZScopeFactory>();
        await singleton.WykonajOperacjeAsync();

        // IServiceCollection metody: TryAdd, TryAddEnumerable, Replace, ServiceDescriptor
        Console.WriteLine("IServiceCollection: TryAdd (nie nadpisuje), TryAddEnumerable, Replace, ServiceDescriptor.Describe(...)");
    }

    private async Task TestujMiddleware(IServiceProvider sp)
    {
        Console.WriteLine("\n--- [3] Middleware ---");
        Console.WriteLine("Kolejność middleware (Program.cs):");
        Console.WriteLine("  GlobalException -> HSTS -> HTTPS -> Static -> Cookie -> Routing -> CORS -> RateLimit -> Auth -> Authorization -> Custom -> Compress -> Endpoints");
        Console.WriteLine("GlobalExceptionMiddleware: przechwytuje wyjątki, mapuje na ProblemDetails, sprawdza Response.HasStarted");
        Console.WriteLine("RequestLoggerMiddleware: loguje czas, metodę, ścieżkę, status odpowiedzi");
        Console.WriteLine("SecurityHeadersMiddleware: X-Frame-Options, X-Content-Type-Options, CSP, Referrer-Policy");
        Console.WriteLine("CorrelationIdMiddleware: X-Correlation-Id header (pobiera lub generuje GUID)");
        Console.WriteLine("RateLimitingMiddleware: SlidingWindow, FixedWindow, TokenBucket przez opcje");
        Console.WriteLine("RequestBodyBufferingMiddleware: EnableBuffering(), Position=0 — wielokrotny odczyt body");
        Console.WriteLine("RequestTimeoutMiddleware: anuluje request po przekroczeniu limitu czasu");
        Console.WriteLine("Use/Run/Map/MapWhen/UseWhen: Use=chain, Run=terminal, Map=branch, MapWhen=warunkowe");
        await Task.CompletedTask;
    }

    private async Task TestujEndpointy(CancellationToken ct)
    {
        Console.WriteLine("\n--- [4] Controllers, Minimal API, REST ---");

        var client = _httpClientFactory!.CreateClient("self");

        // Health
        try
        {
            var health = await client.GetStringAsync("/health", ct);
            Console.WriteLine($"GET /health -> {health}");
        }
        catch (Exception ex) { Console.WriteLine($"GET /health -> {ex.Message}"); }

        // Minimal API
        try
        {
            var pong = await client.GetStringAsync("/ping", ct);
            Console.WriteLine($"GET /ping -> {pong}");
        }
        catch (Exception ex) { Console.WriteLine($"GET /ping -> {ex.Message}"); }

        // Controller endpoint
        try
        {
            var res = await client.GetAsync("/api/produkty", ct);
            Console.WriteLine($"GET /api/produkty -> {(int)res.StatusCode} {res.StatusCode}");
        }
        catch (Exception ex) { Console.WriteLine($"GET /api/produkty -> {ex.Message}"); }

        // POST — walidacja
        try
        {
            var zly = new StringContent("{\"nazwa\":\"\",\"cena\":-1}", System.Text.Encoding.UTF8, "application/json");
            var res = await client.PostAsync("/api/produkty", zly, ct);
            Console.WriteLine($"POST /api/produkty (zły) -> {(int)res.StatusCode} {res.StatusCode}");
        }
        catch (Exception ex) { Console.WriteLine($"POST /api/produkty (zły) -> {ex.Message}"); }

        // REST koncepty
        Console.WriteLine("REST constraints: Client-Server, Stateless, Cacheable, Layered, Code-on-demand, Uniform Interface");
        Console.WriteLine("Richardson Maturity Model: L0=1endpt, L1=resources, L2=HTTP methods, L3=HATEOAS");
        Console.WriteLine("HTTP methods: GET(safe+idempotent), POST, PUT(idempotent), PATCH, DELETE(idempotent), HEAD, OPTIONS");
        Console.WriteLine("Status codes: 200 OK, 201 Created, 204 NoContent, 301/302 Redirect, 400 Bad, 401 Unauth, 403 Forbid, 404 NotFound, 409 Conflict, 422 Unprocessable, 429 TooMany, 500 Internal");
        Console.WriteLine("HATEOAS: Link(href,rel,method) -> HateoasOdpowiedz<T> -> StronicowanaHateoas<T>");
        Console.WriteLine("Content Negotiation: Accept header (application/json, application/xml)");
        Console.WriteLine("ETag: W/\"hash\" -> 304 Not Modified / 412 Precondition Failed");
        Console.WriteLine("API Versioning: URI (/v1/), Header (X-Api-Version), Query (?api-version=1.0), Accept (application/vnd.api+json;v=1)");

        // Model binding
        Console.WriteLine("[FromRoute], [FromQuery], [FromBody], [FromHeader], [FromForm], [FromServices]");
        Console.WriteLine("[ApiController]: auto-validation, binding inference, Problem Details RFC 7807");

        // Action results
        Console.WriteLine("IActionResult: Ok, Created, CreatedAtRoute, NoContent, Accepted, BadRequest, NotFound, Conflict, StatusCode, ValidationProblem, Problem, Content, File");
        Console.WriteLine("TypedResults (Minimal API): Results.Ok<T>, Results.Created, Results.NotFound, Results.Problem");

        // Route constraints
        Console.WriteLine("Route constraints: {id:int}, {name:alpha}, {id:guid}, {date:datetime}, {code:regex(...)}, {age:min(18):max(99)}");

        // Action filters
        Console.WriteLine("ActionFilterAttribute: OnActionExecuting/OnActionExecuted, IActionFilter, IAsyncActionFilter");
        Console.WriteLine("EndpointFilter (Minimal API): IEndpointFilter.InvokeAsync, .AddEndpointFilter<T>()");
    }

    private async Task TestujWalidacje(CancellationToken ct)
    {
        Console.WriteLine("\n--- [5] Walidacja ---");

        var client = _httpClientFactory!.CreateClient("self");

        // DataAnnotations
        try
        {
            var zly = new StringContent("{\"email\":\"nie-email\",\"nip\":\"12345\"}", System.Text.Encoding.UTF8, "application/json");
            var res = await client.PostAsync("/api/walidacja/rejestruj", zly, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"POST /api/walidacja/rejestruj (zły) -> {(int)res.StatusCode}, body snippet: {body[..Math.Min(120, body.Length)]}");
        }
        catch (Exception ex) { Console.WriteLine($"Walidacja test -> {ex.Message}"); }

        Console.WriteLine("DataAnnotations: [Required], [StringLength], [Range], [EmailAddress], [Phone], [RegularExpression], [Compare], [Url], [CreditCard], [MinLength], [MaxLength]");
        Console.WriteLine("IValidatableObject: Validate() — walidacja przekrojowa (np. DataDo > DataOd)");
        Console.WriteLine("NipAttribute: checksum mod 11, DozwoloneRozszerzeniaAttribute, MaxRozmiarPlikuAttribute");
        Console.WriteLine("ModelState: IsValid, AddModelError, ValidationProblem, InvalidModelStateResponseFactory");
        Console.WriteLine("FluentValidation: AbstractValidator<T>, RuleFor().NotEmpty().MaximumLength(), MustAsync, When, RuleForEach, CascadeMode.Stop");
        Console.WriteLine("FV vs DataAnnotations: FV=kod/test/async/złożone, DA=proste/atrybuty/EF");
        await Task.CompletedTask;
    }

    private async Task TestujSwagger(CancellationToken ct)
    {
        Console.WriteLine("\n--- [6] Swagger / OpenAPI ---");

        var client = _httpClientFactory!.CreateClient("self");
        try
        {
            var res = await client.GetAsync("/swagger/v1/swagger.json", ct);
            Console.WriteLine($"GET /swagger/v1/swagger.json -> {(int)res.StatusCode} {res.StatusCode}");
        }
        catch (Exception ex) { Console.WriteLine($"Swagger JSON -> {ex.Message}"); }

        Console.WriteLine("AddSwaggerGen: OpenApiInfo (Title, Version, Description, Contact, License)");
        Console.WriteLine("IncludeXmlComments: GenerateDocumentationFile=true, XML comments (/// summary/param/returns)");
        Console.WriteLine("[ProducesResponseType]: dokumentuje kody odpowiedzi dla kontrolerów");
        Console.WriteLine("JWT Bearer: AddSecurityDefinition('Bearer') + AddSecurityRequirement");
        Console.WriteLine("IOperationFilter (GlobalNaglowkiFilter), IDocumentFilter, ISchemaFilter (EnumSchemaFilter)");
        Console.WriteLine("SwaggerUI: DisplayRequestDuration=true, EnableDeepLinking=true, RoutePrefix");
        Console.WriteLine("Minimal API + Swagger: .WithName(), .WithSummary(), .WithTags(), .Produces<T>()");
    }

    private async Task TestujHealthChecks(CancellationToken ct)
    {
        Console.WriteLine("\n--- [7] Health Checks ---");

        var client = _httpClientFactory!.CreateClient("self");
        try
        {
            var res = await client.GetAsync("/health/detail", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"GET /health/detail -> {(int)res.StatusCode}, body: {body[..Math.Min(150, body.Length)]}");
        }
        catch (Exception ex) { Console.WriteLine($"Health detail -> {ex.Message}"); }

        Console.WriteLine("IHealthCheck.CheckHealthAsync -> HealthCheckResult.Healthy/Degraded/Unhealthy");
        Console.WriteLine("MapHealthChecks: /health, /health/detail z HealthCheckOptions (ResponseWriter)");
    }
}

// ============================================================
// HEALTH CHECKS
// ============================================================

public class BazaDanychHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        // Symulacja sprawdzenia połączenia z bazą
        var danych = new Dictionary<string, object>
        {
            ["ping_ms"] = 5,
            ["polaczenia_aktywne"] = 3,
            ["wersja"] = "SQLite 3.x"
        };
        return Task.FromResult(HealthCheckResult.Healthy("Baza działa poprawnie", danych));
    }
}

public class ExternalApiHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        // Symulacja: zewnętrzne API jest zdegradowane
        return Task.FromResult(HealthCheckResult.Degraded("Zewnętrzne API odpowiada wolno (>2s)"));
    }
}

// ============================================================
// IServiceCollection EXTENSION METHODS
// ============================================================

public static class ServiceCollectionExtensions
{
    public static IServiceCollection DodajSerwisyAplikacji(this IServiceCollection services, IConfiguration configuration)
    {
        // Opcje z walidacją
        services.AddOptions<SmtpOpcje>()
            .Bind(configuration.GetSection(SmtpOpcje.Sekcja))
            .ValidateDataAnnotations();

        services.AddOptions<JwtOpcje>()
            .Bind(configuration.GetSection(JwtOpcje.Sekcja));

        services.AddOptions<LimityOpcje>()
            .Bind(configuration.GetSection(LimityOpcje.Sekcja))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Lifetimes
        services.AddTransient<TransientLicznik>();
        services.AddScoped<ScopedLicznik>();
        services.AddSingleton<SingletonLicznik>();

        // Wiele implementacji
        services.AddTransient<INotifikacja, EmailNotifikacja>();
        services.AddTransient<INotifikacja, SmsNotifikacja>();
        services.AddTransient<INotifikacja, PushNotifikacja>();

        // Keyed services (.NET 8)
        services.AddKeyedSingleton<IKeszSerwis, SqlKesz>("sql");
        services.AddKeyedSingleton<IKeszSerwis, RedisKesz>("redis");

        // Email
        services.AddTransient<IEmailSerwis, SmtpEmailSerwis>();

        // Captive fix demo
        services.AddSingleton<SingletonZScopeFactory>();

        // Monitor/Snapshot demo
        services.AddScoped<MonitorSerwis>();
        services.AddScoped<SnapshotSerwis>();

        return services;
    }

    public static IServiceCollection DodajHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<BazaDanychHealthCheck>("baza_danych", tags: ["db", "critical"])
            .AddCheck<ExternalApiHealthCheck>("external_api", tags: ["external"])
            .AddCheck("self", () => HealthCheckResult.Healthy("Aplikacja działa"));

        return services;
    }
}
