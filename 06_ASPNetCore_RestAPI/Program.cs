using _06_ASPNetCore_RestAPI;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

// ============================================================
// FAZA 1 — KONFIGURACJA USŁUG (Services)
// WebApplication.CreateBuilder: 3-fazowy wzorzec
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// Konfiguracja in-memory (zastępuje appsettings.json dla demonstracji)
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Smtp:Host"] = "smtp.example.com",
    ["Smtp:Port"] = "587",
    ["Smtp:Uzytkownik"] = "noreply@example.com",
    ["Smtp:UzyjSsl"] = "true",
    ["Jwt:Klucz"] = "super-secret-key-min32chars-padding!",
    ["Jwt:Wydawca"] = "MojeAPI",
    ["Jwt:Odbiorca"] = "MojeAPI.Klienci",
    ["Jwt:WygasaPoMinutach"] = "60",
    ["Limity:MaksymalnaLiczbaPrzed"] = "500",
    ["Limity:MaksymalnyRozmiarStrony"] = "50",
});

// Kontrolery z opcjami
builder.Services.AddControllers(options =>
{
    // Zwróć 406 gdy klient żąda nieobsługiwanego formatu
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;
})
.AddXmlSerializerFormatters()   // Content negotiation: XML
.ConfigureApiBehaviorOptions(options =>
{
    // Własna fabryka odpowiedzi dla błędów walidacji
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Błąd walidacji",
            Status = 400,
            Instance = context.HttpContext.Request.Path
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problemDetails);
    };
});

// Serwisy aplikacji + Health Checks
builder.Services.DodajSerwisyAplikacji(builder.Configuration);
builder.Services.DodajHealthChecks();

// Swagger / OpenAPI
builder.Services.DodajSwagger();

// Response Caching
builder.Services.AddResponseCaching();

// Rate Limiting opcje
builder.Services.Configure<RateLimitingOpcje>(options =>
{
    options.MaksZapytanNaOkno = 1000;
    options.OknoTimeSpan = TimeSpan.FromMinutes(1);
});

// HttpClient dla SelfTestService
builder.Services.AddHttpClient("self", client =>
{
    client.BaseAddress = new Uri("http://localhost:5099");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<RejestrujKlientaDtoValidator>();

// Background service — self-test
builder.Services.AddHostedService<SelfTestService>();

// Logging
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

// ============================================================
// FAZA 2 — BUDOWANIE APLIKACJI (Build)
// ============================================================

var app = builder.Build();

// ============================================================
// FAZA 3 — KONFIGURACJA PIPELINE (Middleware)
// Kolejność jest krytyczna!
// ============================================================

// 1. Globalny handler wyjątków — MUSI być pierwszy
app.UzyjGlobalExceptionMiddleware();

// 2. Security Headers
app.UzyjSecurityHeaders();

// 3. HSTS + HTTPS Redirection
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// 4. Static Files
app.UseStaticFiles();

// 5. Correlation ID
app.UzyjCorrelationId();

// 6. Request Body Buffering
app.UzyjRequestBodyBuffering();

// 7. Request Logger
app.UzyjRequestLoggera();

// 8. Response Caching
app.UseResponseCaching();

// 9. Routing
app.UseRouting();

// 10. Rate Limiting
app.UzyjRateLimiting();

// 11. Swagger (tylko w dev, ale tu zawsze dla demonstracji)
app.DodajDemoMiddleware();
app.UzyjSwagger();

// ============================================================
// ENDPOINTY
// ============================================================

// Ping — najprostszy Minimal API endpoint
app.MapGet("/ping", () => Results.Ok("pong"))
    .WithName("Ping")
    .WithSummary("Health ping")
    .WithTags("System");

// Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = 200,
        [HealthStatus.Degraded] = 200,
        [HealthStatus.Unhealthy] = 503
    }
});

app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var wynik = new
        {
            status = report.Status.ToString(),
            sprawdzenia = report.Entries.Select(e => new
            {
                nazwa = e.Key,
                status = e.Value.Status.ToString(),
                opis = e.Value.Description,
                czas_ms = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(wynik));
    }
});

// Minimal API endpoints z Swagger
app.MapMinimalApiEndpointy();

// Minimal API — demo Map branch
app.Map("/api/debug", debugApp =>
{
    debugApp.Run(async ctx =>
        await ctx.Response.WriteAsync("Debug branch (Map)"));
});

// Controllers
app.MapControllers();

// ============================================================
// URUCHOMIENIE
// ============================================================

app.Run("http://localhost:5099");
