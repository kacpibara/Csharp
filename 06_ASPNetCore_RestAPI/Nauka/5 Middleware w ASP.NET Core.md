### Middleware w ASP.NET Core

Middleware to **komponenty budujące pipeline HTTP** — każdy request przechodzi przez łańcuch middleware jak przez sito, każdy może przetworzyć request i/lub response.

---

### 1. Jak działa pipeline

csharp

```csharp
// Wizualizacja pipeline'u — request i response przechodzą przez każdy middleware
//
// Request →  [MW1]  →  [MW2]  →  [MW3]  →  Endpoint
//             ↓          ↓          ↓
// Response ← [MW1] ←  [MW2]  ←  [MW3]  ←  Endpoint
//
// Każdy middleware:
// 1. Wykonuje KOD PRZED (przed next())     — przetwarza request
// 2. Wywołuje next() — przekazuje do następnego
// 3. Wykonuje KOD PO (po next())           — przetwarza response

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Każde app.Use* dodaje middleware do pipeline'u
// KOLEJNOŚĆ MA FUNDAMENTALNE ZNACZENIE!

app.Use(async (context, next) =>
{
    Console.WriteLine("MW1: przed");
    await next();                    // wywołaj następny
    Console.WriteLine("MW1: po");
});

app.Use(async (context, next) =>
{
    Console.WriteLine("MW2: przed");
    await next();
    Console.WriteLine("MW2: po");
});

app.Run(async context =>            // terminal — nie wywołuje next()
{
    Console.WriteLine("Endpoint");
    await context.Response.WriteAsync("Odpowiedź");
});

// Dla GET / konsola wypisze:
// MW1: przed
// MW2: przed
// Endpoint
// MW2: po
// MW1: po
```

---

### 2. Use, Run, Map — różnice

csharp

```csharp
var app = builder.Build();

// === app.Use — middleware z next() ===
// Może przetwarzać request I response
// Przekazuje dalej przez wywołanie next()
app.Use(async (ctx, next) =>
{
    // Przed — modyfikuj request
    ctx.Items["StartCzas"] = DateTime.UtcNow;

    await next(ctx);    // przekaż dalej

    // Po — modyfikuj response
    var start = (DateTime)ctx.Items["StartCzas"]!;
    var ms = (DateTime.UtcNow - start).TotalMilliseconds;
    ctx.Response.Headers["X-Response-Time"] = $"{ms:F0}ms";
});

// === app.Run — terminal middleware ===
// NIE wywołuje next() — kończy pipeline
// Każdy middleware po Run jest martwy (nigdy nie wywoływany)
app.Run(async ctx =>
{
    await ctx.Response.WriteAsync("Koniec pipeline'u");
    // Żaden kolejny middleware NIE zostanie wywołany!
});

app.Use(async (ctx, next) =>         // ← MARTWY! Run powyżej kończy pipeline
{
    await next(ctx);
});

// === app.Map — rozgałęzienie po ścieżce ===
// Tworzy SUB-PIPELINE dla danego prefiksu ścieżki
app.Map("/admin", adminApp =>
{
    // Ten sub-pipeline działa TYLKO dla /admin/*
    adminApp.Use(async (ctx, next) =>
    {
        Console.WriteLine("Middleware tylko dla /admin");
        await next(ctx);
    });

    adminApp.Run(async ctx =>
        await ctx.Response.WriteAsync("Panel admina"));
});

app.Map("/api", apiApp =>
{
    apiApp.Map("/v1", v1App =>       // zagnieżdżone Map!
    {
        v1App.Run(async ctx =>
            await ctx.Response.WriteAsync("API v1"));
    });

    apiApp.Run(async ctx =>
        await ctx.Response.WriteAsync("API"));
});

// === app.MapWhen — warunkowe rozgałęzienie ===
// Rozgałęzia na podstawie dowolnego warunku (nie tylko ścieżki)
app.MapWhen(
    ctx => ctx.Request.Headers.ContainsKey("X-Api-Key"),
    apiKeyApp =>
    {
        apiKeyApp.Run(async ctx =>
            await ctx.Response.WriteAsync("Klient API z kluczem"));
    });

// MapWhen dla konkretnej metody HTTP
app.MapWhen(
    ctx => ctx.Request.Method == HttpMethods.Options,
    optApp => optApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 204;
        ctx.Response.Headers.Allow = "GET, POST, PUT, DELETE, OPTIONS";
    }));

// === app.UseWhen — warunkowy middleware BEZ rozgałęzienia ===
// Różnica vs MapWhen: po warunkowym middleware pipeline łączy się z powrotem!
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    apiApp => apiApp.Use(async (ctx, next) =>
    {
        Console.WriteLine("Ten middleware tylko dla /api/*");
        await next(ctx);
        // Po next() wracamy do GŁÓWNEGO pipeline'u!
    }));

app.Run(async ctx =>               // to działa dla WSZYSTKICH ścieżek (też /api/*)
    await ctx.Response.WriteAsync("Główny handler"));
```

---

### 3. Własny middleware — klasa

csharp

```csharp
// Konwencja: konstruktor z RequestDelegate, metoda InvokeAsync/Invoke
// InvokeAsync może przyjmować dodatkowe serwisy z DI!

public class RequestLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggerMiddleware> _logger;

    // RequestDelegate i SINGLETON serwisy — przez konstruktor
    public RequestLoggerMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggerMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    // SCOPED i TRANSIENT serwisy — przez parametry InvokeAsync (DI wstrzykuje!)
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,   // Scoped — NIE może być w konstruktorze!
        IMetricsService metrics)           // Transient
    {
        var sw        = System.Diagnostics.Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];

        // Dodaj request ID do kontekstu i nagłówka odpowiedzi
        context.Items["RequestId"] = requestId;
        context.Response.Headers["X-Request-Id"] = requestId;

        // Loguj request
        _logger.LogInformation(
            "[{RequestId}] → {Method} {Path} | User: {User}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            currentUser.UserId ?? "Anonymous");

        try
        {
            await _next(context);  // wywołaj następny middleware

            sw.Stop();

            // Loguj response
            _logger.LogInformation(
                "[{RequestId}] ← {StatusCode} | {ElapsedMs}ms",
                requestId,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);

            // Metryki
            metrics.RecordRequest(
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[{RequestId}] ✗ Wyjątek po {ElapsedMs}ms: {Message}",
                requestId,
                sw.ElapsedMilliseconds,
                ex.Message);
            throw;  // re-throw — niech GlobalExceptionMiddleware obsłuży
        }
    }
}

// Extension method — czysty zapis w Program.cs
public static class RequestLoggerMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogger(
        this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggerMiddleware>();
}

// Użycie:
app.UseRequestLogger();
// zamiast:
app.UseMiddleware<RequestLoggerMiddleware>();
```

---

### 4. GlobalExceptionMiddleware — obsługa błędów

csharp

```csharp
// Powinien być PIERWSZY w pipeline — przechwytuje błędy ze wszystkich kolejnych

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await ObsluzBladAsync(ctx, ex);
        }
    }

    private async Task ObsluzBladAsync(HttpContext ctx, Exception ex)
    {
        // Loguj — zawsze
        _logger.LogError(ex,
            "Nieobsłużony wyjątek: {ExceptionType} — {Message}",
            ex.GetType().Name,
            ex.Message);

        // Jeśli response już częściowo wysłany — nie możemy zmienić statusu!
        if (ctx.Response.HasStarted)
        {
            _logger.LogWarning("Response już zaczęty — nie można zmienić StatusCode");
            return;
        }

        ctx.Response.ContentType = "application/problem+json";

        // Mapuj wyjątki na status codes
        (int status, string tytul, string? szczegoly) = ex switch
        {
            NieznalezionyException e => (404, "Nie znaleziono", e.Message),
            WalidacjaException     e => (400, "Błąd walidacji", e.Message),
            BrakUprawnieniException e => (403, "Brak uprawnień", e.Message),
            KonflikException       e => (409, "Konflikt", e.Message),
            ArgumentException      e => (400, "Nieprawidłowe żądanie", e.Message),
            OperationCanceledException => (499, "Żądanie anulowane", null),
            TimeoutException         e => (504, "Przekroczono czas", e.Message),
            _ => (500, "Wewnętrzny błąd serwera",
                  _env.IsDevelopment() ? ex.ToString() : null)
        };

        ctx.Response.StatusCode = status;

        var problem = new
        {
            type     = $"https://api.sklep.pl/errors/{status}",
            title    = tytul,
            status,
            detail   = szczegoly,
            instance = ctx.Request.Path.ToString(),
            traceId  = ctx.TraceIdentifier
        };

        var json = System.Text.Json.JsonSerializer.Serialize(problem,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        await ctx.Response.WriteAsync(json);
    }
}

// Wyjątki domenowe
public class NieznalezionyException : Exception
{
    public NieznalezionyException(string msg) : base(msg) { }
}
public class WalidacjaException : Exception
{
    public WalidacjaException(string msg) : base(msg) { }
}
public class BrakUprawnieniException : Exception
{
    public BrakUprawnieniException(string msg) : base(msg) { }
}
public class KonflikException : Exception
{
    public KonflikException(string msg) : base(msg) { }
}

// Rejestracja — JAKO PIERWSZY!
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggerMiddleware>();
// ... reszta middleware
```

---

### 5. Middleware z konfiguracją — opcje

csharp

```csharp
// Middleware z konfigurowalnymi opcjami

public class RateLimitingOptions
{
    public int    MaxRequestsPerWindow { get; set; } = 100;
    public TimeSpan Window             { get; set; } = TimeSpan.FromMinutes(1);
    public string[]? WhitelistedIPs    { get; set; }
    public bool   EnablePerEndpoint    { get; set; } = false;
}

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _opcje;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    // Thread-safe counter — Singleton lifetime
    private readonly System.Collections.Concurrent.ConcurrentDictionary
        <string, (int Count, DateTime WindowStart)> _liczniki = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        RateLimitingOptions opcje,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next   = next;
        _opcje  = opcje;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        string ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Whitelist — pomiń limit
        if (_opcje.WhitelistedIPs?.Contains(ip) == true)
        {
            await _next(ctx);
            return;
        }

        // Sprawdź i zaktualizuj licznik
        var teraz = DateTime.UtcNow;
        var (przekroczony, pozostalo) = SprawdzLimit(ip, teraz);

        // Dodaj nagłówki informacyjne
        ctx.Response.Headers["X-RateLimit-Limit"]     = _opcje.MaxRequestsPerWindow.ToString();
        ctx.Response.Headers["X-RateLimit-Remaining"] = pozostalo.ToString();
        ctx.Response.Headers["X-RateLimit-Window"]    = _opcje.Window.TotalSeconds.ToString();

        if (przekroczony)
        {
            _logger.LogWarning("Rate limit przekroczony dla IP: {IP}", ip);
            ctx.Response.StatusCode = 429;
            ctx.Response.Headers["Retry-After"] = _opcje.Window.TotalSeconds.ToString();
            await ctx.Response.WriteAsJsonAsync(new
            {
                error   = "Zbyt wiele żądań",
                retryIn = $"{_opcje.Window.TotalSeconds}s"
            });
            return;  // nie wywołuj next()!
        }

        await _next(ctx);
    }

    private (bool Przekroczony, int Pozostalo) SprawdzLimit(string ip, DateTime teraz)
    {
        var wpis = _liczniki.AddOrUpdate(ip,
            _ => (1, teraz),
            (_, stary) =>
            {
                // Reset okna jeśli minął czas
                if (teraz - stary.WindowStart > _opcje.Window)
                    return (1, teraz);

                return (stary.Count + 1, stary.WindowStart);
            });

        bool przekroczony = wpis.Count > _opcje.MaxRequestsPerWindow;
        int pozostalo = Math.Max(0, _opcje.MaxRequestsPerWindow - wpis.Count);
        return (przekroczony, pozostalo);
    }
}

// Extension method z konfiguracją
public static class RateLimitingExtensions
{
    public static IApplicationBuilder UseRateLimiting(
        this IApplicationBuilder app,
        Action<RateLimitingOptions>? configure = null)
    {
        var opcje = new RateLimitingOptions();
        configure?.Invoke(opcje);
        return app.UseMiddleware<RateLimitingMiddleware>(opcje);
    }
}

// Użycie z konfiguracją
app.UseRateLimiting(opt =>
{
    opt.MaxRequestsPerWindow = 200;
    opt.Window               = TimeSpan.FromMinutes(1);
    opt.WhitelistedIPs       = new[] { "127.0.0.1", "::1" };
});
```

---

### 6. Middleware — zaawansowane wzorce

csharp

```csharp
// === WZORZEC 1: Request body buffering — czytaj body wielokrotnie ===
public class RequestBodyBufferingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestBodyBufferingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Domyślnie body można czytać tylko RAZ (forward-only stream)
        // EnableBuffering() pozwala na wielokrotny odczyt
        ctx.Request.EnableBuffering();

        // Odczytaj body dla logowania
        using var reader = new System.IO.StreamReader(
            ctx.Request.Body,
            leaveOpen: true);   // nie zamykaj stream!

        string body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;  // reset — następny middleware może czytać od początku

        ctx.Items["RequestBody"] = body;

        await _next(ctx);
    }
}

// === WZORZEC 2: Response caching — własna implementacja ===
public class SimpleCacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly System.Collections.Concurrent.ConcurrentDictionary
        <string, (string Body, DateTime Expiry)> _cache = new();

    public SimpleCacheMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Cachuj tylko GET
        if (ctx.Request.Method != HttpMethods.Get)
        {
            await _next(ctx);
            return;
        }

        string klucz = ctx.Request.Path + ctx.Request.QueryString;

        // Cache hit
        if (_cache.TryGetValue(klucz, out var cached)
            && cached.Expiry > DateTime.UtcNow)
        {
            ctx.Response.Headers["X-Cache"] = "HIT";
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(cached.Body);
            return;
        }

        // Cache miss — przechwytuj response
        var oryginalnyStream = ctx.Response.Body;
        using var bufor = new System.IO.MemoryStream();
        ctx.Response.Body = bufor;

        try
        {
            await _next(ctx);

            // Zapisz response do cache (tylko 200 OK)
            if (ctx.Response.StatusCode == 200)
            {
                bufor.Position = 0;
                string responseBody = await new System.IO.StreamReader(bufor).ReadToEndAsync();

                _cache[klucz] = (responseBody, DateTime.UtcNow.AddSeconds(30));
                ctx.Response.Headers["X-Cache"] = "MISS";

                // Skopiuj bufor do oryginalnego stream
                bufor.Position = 0;
                await bufor.CopyToAsync(oryginalnyStream);
            }
        }
        finally
        {
            ctx.Response.Body = oryginalnyStream;
        }
    }
}

// === WZORZEC 3: Correlation ID — śledzenie requestów ===
public class CorrelationIdMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ICurrentUserService user)
    {
        // Pobierz z nagłówka lub wygeneruj nowy
        string correlationId = ctx.Request.Headers[CorrelationHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Ustaw w Response (klient może śledzić)
        ctx.Response.Headers[CorrelationHeader] = correlationId;

        // Ustaw w Items (dostępne w całym pipeline)
        ctx.Items["CorrelationId"] = correlationId;

        // Ustaw w AsyncLocal dla logowania (przez LogContext Serilog)
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        using (Serilog.Context.LogContext.PushProperty("UserId", user.UserId))
        {
            await _next(ctx);
        }
        // Po this block — właściwości usunięte z LogContext
    }
}
```

---

### 7. Właściwa kolejność middleware

csharp

```csharp
// PRAWIDŁOWA kolejność w Program.cs
// (ASP.NET Core docs + dobre praktyki)

var app = builder.Build();

// 1. Obsługa wyjątków — MUSI być pierwsza!
//    Przechwytuje błędy ze WSZYSTKICH kolejnych middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. HSTS — HTTP Strict Transport Security (tylko prod)
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// 3. HTTPS Redirect
app.UseHttpsRedirection();

// 4. Pliki statyczne — przed routingiem, szybka obsługa
app.UseStaticFiles();

// 5. Cookie Policy (RODO)
app.UseCookiePolicy();

// 6. Routing — dopasowanie ścieżki do endpointu
app.UseRouting();

// 7. CORS — musi być PO UseRouting, PRZED UseAuthentication
app.UseCors("MojaPolityka");

// 8. Rate Limiting
app.UseRateLimiter();

// 9. Authentication — KTO jesteś? (JWT, Cookie, itp.)
app.UseAuthentication();

// 10. Authorization — CZY MOŻESZ? (roles, policies)
app.UseAuthorization();

// 11. Custom middleware aplikacyjny
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggerMiddleware>();

// 12. Response Compression — po całej logice, przed wysłaniem
app.UseResponseCompression();

// 13. Endpointy — na końcu!
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<ChatHub>("/hubs/chat");
```

---

### 8. Praktyczny przykład — kompletny zestaw middleware

csharp

```csharp
// Kompletny, produkcyjny zestaw middleware dla API

// === MIDDLEWARE: Security Headers ===
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Nagłówki bezpieczeństwa — przed wysłaniem response
        ctx.Response.OnStarting(() =>
        {
            var headers = ctx.Response.Headers;

            // Zapobiegaj clickjackingowi
            headers["X-Frame-Options"] = "DENY";

            // Zapobiegaj MIME sniffingowi
            headers["X-Content-Type-Options"] = "nosniff";

            // XSS Protection (legacy, ale warto)
            headers["X-XSS-Protection"] = "1; mode=block";

            // Content Security Policy
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "img-src 'self' data: https:; " +
                "script-src 'self';";

            // Referrer Policy
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Ukryj serwer
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}

// === MIDDLEWARE: Timeout ===
public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TimeSpan _timeout;
    private readonly ILogger<RequestTimeoutMiddleware> _logger;

    public RequestTimeoutMiddleware(
        RequestDelegate next,
        ILogger<RequestTimeoutMiddleware> logger,
        TimeSpan? timeout = null)
    {
        _next    = next;
        _logger  = logger;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        using var cts = CancellationTokenSource
            .CreateLinkedTokenSource(ctx.RequestAborted);
        cts.CancelAfter(_timeout);

        var oryginalnyToken = ctx.RequestAborted;
        ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestLifetimeFeature>()!
            .RequestAborted = cts.Token;

        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (!oryginalnyToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request timeout po {Timeout}s: {Path}",
                _timeout.TotalSeconds, ctx.Request.Path);

            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 504;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = "Request timeout",
                    limit = $"{_timeout.TotalSeconds}s"
                });
            }
        }
    }
}

// === Program.cs — pełna konfiguracja produkcyjna ===
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddResponseCompression(opt => opt.EnableForHttps = true);
builder.Services.AddCors(opt => opt.AddPolicy("API", policy =>
    policy.WithOrigins("https://sklep.pl")
          .AllowAnyMethod()
          .AllowAnyHeader()));

var app = builder.Build();

// Pipeline w produkcyjnej kolejności
app.UseMiddleware<GlobalExceptionMiddleware>();     // 1. błędy globalne
app.UseMiddleware<SecurityHeadersMiddleware>();     // 2. nagłówki bezpieczeństwa
app.UseMiddleware<CorrelationIdMiddleware>();       // 3. correlation ID

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();                         // 4. HTTPS
app.UseResponseCompression();                      // 5. kompresja
app.UseStaticFiles();                              // 6. pliki statyczne
app.UseRouting();                                  // 7. routing
app.UseCors("API");                                // 8. CORS
app.UseMiddleware<RateLimitingMiddleware>(          // 9. rate limiting
    new RateLimitingOptions { MaxRequestsPerWindow = 100 });
app.UseAuthentication();                           // 10. autentykacja
app.UseAuthorization();                            // 11. autoryzacja
app.UseMiddleware<RequestLoggerMiddleware>();       // 12. logowanie
app.UseMiddleware<RequestTimeoutMiddleware>(        // 13. timeout
    TimeSpan.FromSeconds(30));

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Placeholder interfaces
public interface ICurrentUserService { string? UserId { get; } }
public interface IMetricsService
{
    void RecordRequest(string method, string path, int status, long ms);
}
```

---

### Typowe pytania rekrutacyjne

**"Co to middleware i jak działa pipeline?"** Middleware to komponent który przetwarza request HTTP i opcjonalnie przekazuje go dalej przez wywołanie `next()`. Pipeline to łańcuch middleware — każdy widzi request przed `next()` i response po `next()`. Przypomina to dekorator — każdy middleware opakowuje kolejny. Kluczowe: kolejność rejestracji w Program.cs określa kolejność wykonywania. Middleware dodany pierwszy wykonuje się pierwszy dla requestu i ostatni dla response.

**"Jaka różnica między `Use`, `Run` i `Map`?"** `Use` — middleware z `next()`, może przetwarzać request i response, przekazuje dalej. `Run` — terminal, nie wywołuje `next()`, kończy pipeline — każdy middleware po `Run` jest martwy. `Map` — tworzy sub-pipeline dla konkretnego prefiksu ścieżki, po dopasowaniu główny pipeline nie kontynuuje. `MapWhen` — warunkowe rozgałęzienie na podstawie dowolnego predykatu. `UseWhen` — warunkowe middleware ale pipeline łączy się z powrotem po warunkowym bloku.

**"Dlaczego Scoped serwisy muszą być w parametrach `InvokeAsync`, a nie w konstruktorze?"** Middleware jest rejestrowany jako Singleton — jeden obiekt na całe życie aplikacji. Konstruktor wywoływany raz przy starcie. Gdyby Scoped serwis (np. DbContext, jeden na request) był w konstruktorze — stałby się efektywnie Singleton i był współdzielony między wszystkimi requestami (captive dependency). Parametry `InvokeAsync` są rozwiązywane przez DI przy każdym wywołaniu — dostają właściwy Scoped serwis z bieżącego scope requestu.

**"Jak czytać body requestu wielokrotnie w middleware?"** Domyślnie `Request.Body` to forward-only stream — można czytać tylko raz. `ctx.Request.EnableBuffering()` zamienia go na buforowalny stream. Po odczytaniu musisz zresetować pozycję: `ctx.Request.Body.Position = 0`. Pamiętaj o `leaveOpen: true` przy `StreamReader` żeby nie zamknąć stream przed następnym middleware. Ta technika jest potrzebna np. do logowania body requestu.