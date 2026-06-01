### ASP.NET Core — architektura i fundamenty

---

### 1. Czym jest ASP.NET Core

csharp

```csharp
// ASP.NET Core = cross-platform, high-performance web framework
// Jeden framework dla: REST API, MVC, Razor Pages, gRPC, SignalR, Blazor

// Program.cs — punkt wejścia całej aplikacji (od .NET 6 — minimal hosting)
var builder = WebApplication.CreateBuilder(args);

// FAZA 1: Konfiguracja serwisów (DI Container)
// Rejestrujesz wszystko czego aplikacja będzie potrzebować
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IProduktSerwis, ProduktSerwis>();

// FAZA 2: Budowanie aplikacji
var app = builder.Build();

// FAZA 3: Konfiguracja pipeline'u HTTP (kolejność MA ZNACZENIE!)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();  // przekieruj HTTP → HTTPS
app.UseAuthentication();    // sprawdź KTO jesteś
app.UseAuthorization();     // sprawdź CZY MOŻESZ
app.MapControllers();       // mapuj endpointy z kontrolerów

app.Run();  // uruchom serwer HTTP

// CO SIĘ DZIEJE Z REQUESTEM:
// Klient → HTTPS Redirect → Auth → Authorization → Controller → Response
//          ↑ Każdy krok to middleware ↑
```

---

### 2. Middleware Pipeline — serce ASP.NET Core

csharp

```csharp
// Middleware = funkcja która przetwarza request i przekazuje dalej
// Pipeline = łańcuch middleware — każdy może:
// 1. Przetworzyć request (przed następnym)
// 2. Wywołać next() — przekaż dalej
// 3. Przetworzyć response (po następnym)

// Wizualizacja pipeline'u:
// Request →  [MW1] → [MW2] → [MW3] → Handler
//            ↓         ↓       ↓
// Response ← [MW1] ← [MW2] ← [MW3] ←

// Własny middleware — klasa
public class RequestLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggerMiddleware> _logger;

    public RequestLoggerMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggerMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // KOD PRZED — przetwarza request
        _logger.LogInformation(
            "→ {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        await _next(context);  // wywołaj następny middleware

        // KOD PO — przetwarza response
        sw.Stop();
        _logger.LogInformation(
            "← {Status} {Method} {Path} [{Ms}ms]",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path,
            sw.ElapsedMilliseconds);
    }
}

// Rejestracja własnego middleware
app.UseMiddleware<RequestLoggerMiddleware>();

// Middleware jako lambda — dla prostych przypadków
app.Use(async (context, next) =>
{
    Console.WriteLine($"Przed: {context.Request.Path}");
    await next();  // wywołaj kolejny
    Console.WriteLine($"Po: {context.Response.StatusCode}");
});

// app.Run — terminal middleware — kończy pipeline, nie wywołuje next()
app.Run(async context =>
{
    await context.Response.WriteAsync("Koniec pipeline'u!");
});

// app.Map — rozgałęzienie pipeline'u dla ścieżki
app.Map("/admin", adminApp =>
{
    adminApp.UseMiddleware<AdminAuthMiddleware>();
    adminApp.Run(async ctx =>
        await ctx.Response.WriteAsync("Panel admina"));
});

// app.MapWhen — warunkowe rozgałęzienie
app.MapWhen(
    ctx => ctx.Request.Headers.ContainsKey("X-Api-Key"),
    apiApp => apiApp.UseMiddleware<ApiKeyMiddleware>()
);

// Własny middleware — obsługa globalnych wyjątków
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nieobsłużony wyjątek: {Message}", ex.Message);
            await ObsluzBladAsync(ctx, ex);
        }
    }

    private static async Task ObsluzBladAsync(HttpContext ctx, Exception ex)
    {
        ctx.Response.ContentType = "application/json";

        (int kod, string komunikat) = ex switch
        {
            DomainException        => (400, ex.Message),
            NieznalezionyException => (404, ex.Message),
            UnauthorizedAccessException => (401, "Brak autoryzacji"),
            _ => (500, "Wewnętrzny błąd serwera")
        };

        ctx.Response.StatusCode = kod;

        await ctx.Response.WriteAsJsonAsync(new
        {
            blad    = komunikat,
            status  = kod,
            sciezka = ctx.Request.Path.ToString(),
            czas    = DateTime.UtcNow
        });
    }
}

// Rejestracja — ZAWSZE jako pierwszy!
app.UseMiddleware<GlobalExceptionMiddleware>();
// (musi być przed innymi żeby przechwytywać błędy z całego pipeline'u)
```

---

### 3. Dependency Injection — wbudowany kontener

csharp

```csharp
// ASP.NET Core ma wbudowany kontener DI — nie potrzebujesz Autofac/Ninject

// CZASY ŻYCIA:
// Transient  — nowa instancja przy każdym pobraniu z kontenera
// Scoped     — jedna instancja na request HTTP (lub scope)
// Singleton  — jedna instancja przez całe życie aplikacji

builder.Services.AddTransient<IEmailSerwis, SmtpEmailSerwis>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddSingleton<IKonfiguracjaCache, KonfiguracjaCache>();

// Reguły:
// Singleton NIE może zależeć od Scoped/Transient!
// (Scoped żyje krócej — captive dependency problem)
// Scoped może zależeć od Transient (OK — Transient żyje krócej lub tak samo)

// Rejestracja z fabryką
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connStr = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("Default")!;
    return new SqlConnection(connStr);
});

// Rejestracja wielu implementacji jednego interfejsu
builder.Services.AddScoped<INotyfikacjaHandler, EmailNotyfikacja>();
builder.Services.AddScoped<INotyfikacjaHandler, SmsNotyfikacja>();
builder.Services.AddScoped<INotyfikacjaHandler, PushNotyfikacja>();

// Pobierz WSZYSTKIE implementacje
public class NotyfikacjaOrchestrator
{
    private readonly IEnumerable<INotyfikacjaHandler> _handlery;

    public NotyfikacjaOrchestrator(IEnumerable<INotyfikacjaHandler> handlery)
        => _handlery = handlery;

    public async Task WyslijWszystkimiKanalamiAsync(string wiadomosc)
    {
        var zadania = _handlery.Select(h => h.WyslijAsync(wiadomosc));
        await Task.WhenAll(zadania);
    }
}

// Opcje (Options Pattern) — typowana konfiguracja
public class JwtOpcje
{
    public const string Sekcja = "Jwt";

    public string Klucz       { get; set; } = "";
    public string Issuer      { get; set; } = "";
    public string Audience    { get; set; } = "";
    public int    WaznoschMin { get; set; } = 60;
}

// appsettings.json:
// "Jwt": { "Klucz": "super-secret", "Issuer": "api", "Audience": "client", "WaznoschMin": 60 }

builder.Services.Configure<JwtOpcje>(
    builder.Configuration.GetSection(JwtOpcje.Sekcja));

// Użycie przez IOptions<T>
public class TokenSerwis
{
    private readonly JwtOpcje _opcje;

    public TokenSerwis(IOptions<JwtOpcje> opcje) =>
        _opcje = opcje.Value;

    public string GenerujToken(string userId)
    {
        Console.WriteLine($"Klucz: {_opcje.Klucz}, Ważność: {_opcje.WaznoschMin}min");
        return "token";
    }
}

// IOptionsMonitor — "na żywo" reaguje na zmiany pliku konfiguracji
// IOptionsSnapshot — odświeżany na request (Scoped)
// IOptions         — zamrożona wartość (Singleton-friendly)
```

---

### 4. Konfiguracja — hierarchia źródeł

csharp

```csharp
// ASP.NET Core łączy konfigurację z wielu źródeł (priorytet: od najniższego)
// 1. appsettings.json
// 2. appsettings.{Environment}.json  (Development/Production)
// 3. Zmienne środowiskowe
// 4. Argumenty wiersza poleceń
// (każde następne nadpisuje poprzednie!)

// appsettings.json
// {
//   "ConnectionStrings": {
//     "Default": "Server=localhost;Database=Sklep;..."
//   },
//   "Jwt": { "Klucz": "dev-key", "WaznoschMin": 60 },
//   "Features": {
//     "NowyCheckout": true,
//     "BetaApi": false
//   },
//   "Logging": {
//     "LogLevel": { "Default": "Information", "Microsoft": "Warning" }
//   }
// }

// Odczyt konfiguracji
public class StartupSerwis
{
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public StartupSerwis(IConfiguration config, IHostEnvironment env)
    {
        _config = config;
        _env    = env;
    }

    public void Pokaz()
    {
        // Podstawowy odczyt
        string? connStr = _config.GetConnectionString("Default");
        string klucz    = _config["Jwt:Klucz"] ?? "";  // notacja z dwukropkiem
        bool nowyCheckout = _config.GetValue<bool>("Features:NowyCheckout");

        // Sekcja jako obiekt
        var jwt = _config.GetSection("Jwt").Get<JwtOpcje>();

        // Środowisko
        Console.WriteLine($"Środowisko: {_env.EnvironmentName}");
        Console.WriteLine($"Czy Dev: {_env.IsDevelopment()}");
        Console.WriteLine($"Czy Prod: {_env.IsProduction()}");
    }
}

// User Secrets — lokalny dev bez commitowania sekretów
// dotnet user-secrets init
// dotnet user-secrets set "Jwt:Klucz" "super-secret-local-key"
// Przechowywane w: %APPDATA%/Microsoft/UserSecrets/{id}/secrets.json

// Zmienne środowiskowe — nadpisują appsettings
// DOTNET_ENVIRONMENT=Production
// Jwt__Klucz=prod-secret  (__ zamiast : dla zagnieżdżonych)
```

---

### 5. WebApplication — minimal API

csharp

```csharp
// Minimal API (od .NET 6) — bez kontrolerów, lekkie endpointy
var app = builder.Build();

// Prosty endpoint
app.MapGet("/", () => "Witaj w API!");

app.MapGet("/produkty/{id:int}", async (int id, IProduktSerwis serwis) =>
{
    var produkt = await serwis.PobierzPoIdAsync(id);
    return produkt is null
        ? Results.NotFound($"Produkt #{id} nie istnieje")
        : Results.Ok(produkt);
});

app.MapPost("/produkty", async (NowyProduktDto dto, IProduktSerwis serwis) =>
{
    int id = await serwis.DodajAsync(dto);
    return Results.Created($"/produkty/{id}", new { id });
});

app.MapPut("/produkty/{id:int}", async (int id, AktualizujProduktDto dto,
    IProduktSerwis serwis) =>
{
    bool ok = await serwis.AktualizujAsync(id, dto);
    return ok ? Results.NoContent() : Results.NotFound();
});

app.MapDelete("/produkty/{id:int}", async (int id, IProduktSerwis serwis) =>
{
    bool ok = await serwis.UsunAsync(id);
    return ok ? Results.NoContent() : Results.NotFound();
});

// Grupowanie endpointów
var produktyGroup = app.MapGroup("/api/produkty")
    .WithTags("Produkty")
    .RequireAuthorization();     // wszystkie wymagają auth

produktyGroup.MapGet("/", PobierzWszystkie);
produktyGroup.MapGet("/{id:int}", PobierzPoId);
produktyGroup.MapPost("/", Dodaj).DisableRateLimiting();

// Filtry dla endpointów
app.MapGet("/dane", () => "Dane")
    .AddEndpointFilter(async (ctx, next) =>
    {
        Console.WriteLine("Przed");
        var wynik = await next(ctx);
        Console.WriteLine("Po");
        return wynik;
    });

// TypedResults — silnie typowane wyniki (lepsza dokumentacja Swagger)
app.MapGet("/produkt/{id}", async Task<Results<Ok<Produkt>, NotFound>> (int id,
    IProduktSerwis serwis) =>
{
    var produkt = await serwis.PobierzPoIdAsync(id);
    return produkt is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(produkt);
});

// Handlery jako metody statyczne
static async Task<IResult> PobierzWszystkie(IProduktSerwis serwis)
    => Results.Ok(await serwis.PobierzWszystkieAsync());

static async Task<IResult> PobierzPoId(int id, IProduktSerwis serwis)
{
    var p = await serwis.PobierzPoIdAsync(id);
    return p is null ? Results.NotFound() : Results.Ok(p);
}

static async Task<IResult> Dodaj(NowyProduktDto dto, IProduktSerwis serwis)
{
    int id = await serwis.DodajAsync(dto);
    return Results.Created($"/api/produkty/{id}", new { id });
}
```

---

### 6. Środowiska i konfiguracja per-środowisko

csharp

```csharp
// launchSettings.json (tylko dev, nie commituj z sekretami!)
// {
//   "profiles": {
//     "Development": {
//       "commandName": "Project",
//       "dotnetRunMessages": true,
//       "launchBrowser": true,
//       "applicationUrl": "https://localhost:7001;http://localhost:5001",
//       "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
//     }
//   }
// }

// Kod zależny od środowiska
var builder2 = WebApplication.CreateBuilder(args);
var app2 = builder2.Build();

if (app2.Environment.IsDevelopment())
{
    app2.UseSwagger();
    app2.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sklep API v1");
        c.RoutePrefix = "";  // Swagger na stronie głównej
    });
    app2.UseDeveloperExceptionPage();  // szczegółowe błędy
}
else
{
    app2.UseExceptionHandler("/error");  // ogólna strona błędu
    app2.UseHsts();  // HTTP Strict Transport Security
}

// Rejestracja serwisów per-środowisko
if (builder2.Environment.IsDevelopment())
{
    builder2.Services.AddSingleton<IEmailSerwis, FakeEmailSerwis>();  // mock w dev
}
else
{
    builder2.Services.AddSingleton<IEmailSerwis, SmtpEmailSerwis>();  // prawdziwy prod
}

// Customowe środowiska — nie musisz używać tylko Development/Production/Staging
// ASPNETCORE_ENVIRONMENT=Testing → app.Environment.IsEnvironment("Testing")
```

---

### 7. Healthchecks i diagnostyka

csharp

```csharp
// Health checks — monitoring stanu aplikacji
builder.Services.AddHealthChecks()
    .AddSqlServer(                              // sprawdź bazę SQL
        builder.Configuration.GetConnectionString("Default")!,
        name:    "database",
        tags:    new[] { "db", "sql" })
    .AddUrlGroup(                               // sprawdź zewnętrzny serwis
        new Uri("https://api.payment.pl/health"),
        name: "payment-api",
        tags: new[] { "external" })
    .AddCheck<CustomHealthCheck>(               // własny sprawdzacz
        "custom",
        tags: new[] { "business" });

// Własny health check
public class CustomHealthCheck : IHealthCheck
{
    private readonly IUnitOfWork _uow;

    public CustomHealthCheck(IUnitOfWork uow) => _uow = uow;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            int liczbaProd = await _uow.Produkty.LiczAsync(ct);
            return liczbaProd > 0
                ? HealthCheckResult.Healthy($"OK — {liczbaProd} produktów")
                : HealthCheckResult.Degraded("Brak produktów w bazie");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Błąd bazy danych", ex);
        }
    }
}

// Endpointy health check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,  // wszystkie
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains("db")  // tylko baza
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // zawsze OK — aplikacja żyje
});
```

---

### 8. Praktyczny przykład — kompletna konfiguracja API

csharp

```csharp
// Program.cs — produkcyjna konfiguracja

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Serilog — zaawansowane logowanie
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Logowanie przez Serilog
    builder.Host.UseSerilog((ctx, services, config) =>
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7));

    // === SERWISY ===

    // Kontrolery z JSON options
    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
            opt.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // API Explorer + Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Sklep API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new()
        {
            Name   = "Authorization",
            Type   = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In     = Microsoft.OpenApi.Models.ParameterLocation.Header
        });
    });

    // Baza danych
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlServer(
            builder.Configuration.GetConnectionString("Default"),
            sql => sql.EnableRetryOnFailure(3)));

    // CORS
    builder.Services.AddCors(opt =>
        opt.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins("https://sklep.pl", "https://admin.sklep.pl")
                  .AllowAnyMethod()
                  .AllowAnyHeader()));

    // Własne serwisy
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<ZamowieniaApplicationService>();
    builder.Services.AddScoped<ProduktApplicationService>();
    builder.Services.Configure<JwtOpcje>(
        builder.Configuration.GetSection(JwtOpcje.Sekcja));

    // Health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("Default")!);

    // Response compression
    builder.Services.AddResponseCompression(opt =>
    {
        opt.EnableForHttps = true;
    });

    // Rate limiting (od .NET 7)
    builder.Services.AddRateLimiter(opt =>
    {
        opt.AddFixedWindowLimiter("global", o =>
        {
            o.PermitLimit         = 100;
            o.Window              = TimeSpan.FromMinutes(1);
            o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            o.QueueLimit          = 10;
        });
    });

    // === PIPELINE ===
    var app = builder.Build();

    // Migracje przy starcie (tylko dev)
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.Database.MigrateAsync();
    }

    // Middleware (kolejność!)
    app.UseMiddleware<GlobalExceptionMiddleware>();  // 1. błędy globalne
    app.UseSerilogRequestLogging();                  // 2. logowanie requestów
    app.UseResponseCompression();                    // 3. kompresja

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();     // 4. HTTPS
    app.UseCors("AllowFrontend"); // 5. CORS
    app.UseRateLimiter();          // 6. rate limiting
    app.UseAuthentication();       // 7. KTO jesteś
    app.UseAuthorization();        // 8. CZY MOŻESZ

    // Health checks
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = hc => hc.Tags.Contains("db")
    }).AllowAnonymous();

    // Kontrolery
    app.MapControllers().RequireRateLimiting("global");

    Log.Information("Aplikacja uruchomiona na {Urls}",
        string.Join(", ", app.Urls));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplikacja zakończyła się błędem krytycznym");
}
finally
{
    Log.CloseAndFlush();
}
```

---

### Typowe pytania rekrutacyjne

**"Co to middleware i jak działa pipeline w ASP.NET Core?"** Middleware to komponent który przetwarza request HTTP i przekazuje go dalej przez wywołanie `next()`. Pipeline to łańcuch middleware — każdy widzi request przed i response po `next()`. Kolejność rejestracji ma fundamentalne znaczenie: middleware autoryzacji musi być po autentykacji, obsługa błędów powinna być pierwsza. `app.Use` — przekazuje dalej, `app.Run` — kończy pipeline, `app.Map` — rozgałęzia.

**"Jakie są czasy życia serwisów w DI?"** Transient — nowy obiekt za każdym razem gdy pobierasz z kontenera. Scoped — jeden obiekt na request HTTP (np. DbContext). Singleton — jeden obiekt przez cały czas życia aplikacji (np. cache, konfiguracja). Pułapka: Singleton nie może zależeć od Scoped — Scoped żyje krócej i zostanie "uwięziony" w Singletonie (captive dependency). ASP.NET Core wykrywa to w Development.

**"Co to Options Pattern i dlaczego zamiast IConfiguration?"** `IConfiguration["klucz"]` zwraca string — brak type safety, brak walidacji, rozproszony dostęp. Options Pattern (`IOptions<T>`) binduje sekcję konfiguracji do silnie typowanej klasy. Zalety: refactoring-safe, IntelliSense, łatwe testowanie (mockujesz obiekt), walidacja przez DataAnnotations, `IOptionsMonitor<T>` dla live reload. Zawsze preferuj Options Pattern dla konfiguracji biznesowej.

**"Jaka różnica między Minimal API a kontrolerami?"** Minimal API — mniej kodu, brak ceremonii, idealne dla małych API i microservices. Kontrolery — więcej struktury, lepsze dla dużych API z wieloma endpointami, łatwiejsze filtrowanie przez action filtry, convention-based routing. W praktyce: Minimal API dla nowych projektów .NET 7+, kontrolery gdy potrzebujesz dziedziczenia, action filtrów lub masz duże API z setkami endpointów. Można mieszać — controllers i minimal API w jednej aplikacji.