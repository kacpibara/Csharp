using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace _06_ASPNetCore_RestAPI;

// ============================================================
// GLOBAL EXCEPTION MIDDLEWARE
// ============================================================

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nieobsługiwany wyjątek: {Message}", ex.Message);

            // Jeśli odpowiedź już częściowo wysłana — nie można zmienić nagłówków
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response.HasStarted=true — nie można zmienić odpowiedzi");
                throw;
            }

            await ObsluzWyjatek(context, ex);
        }
    }

    private static async Task ObsluzWyjatek(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/problem+json";

        // Mapowanie typów wyjątków na kody HTTP (RFC 7807 Problem Details)
        var (status, title) = ex switch
        {
            ArgumentException => (400, "Nieprawidłowe dane wejściowe"),
            UnauthorizedAccessException => (401, "Brak autoryzacji"),
            KeyNotFoundException => (404, "Zasób nie istnieje"),
            InvalidOperationException => (409, "Konflikt stanu"),
            NotImplementedException => (501, "Nie zaimplementowano"),
            _ => (500, "Wewnętrzny błąd serwera")
        };

        context.Response.StatusCode = status;

        var problem = new
        {
            type = $"https://httpstatuses.com/{status}",
            title,
            status,
            detail = ex.Message,
            instance = context.Request.Path.Value
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}

// ============================================================
// REQUEST LOGGER MIDDLEWARE
// ============================================================

public class RequestLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggerMiddleware> _logger;

    public RequestLoggerMiddleware(RequestDelegate next, ILogger<RequestLoggerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // Scoped serwisy mogą być wstrzykiwane przez parametry InvokeAsync (nie konstruktor!)
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        _logger.LogInformation("{Method} {Path} -> {Status} [{ElapsedMs}ms]",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
}

// ============================================================
// SECURITY HEADERS MIDDLEWARE
// ============================================================

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Zapobiega clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Zapobiega MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Content Security Policy
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'";

        // Referrer Policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy (dawniej Feature-Policy)
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        await _next(context);
    }
}

// ============================================================
// CORRELATION ID MIDDLEWARE
// ============================================================

public class CorrelationIdMiddleware
{
    private const string NazwaHeadera = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Pobierz istniejący lub wygeneruj nowy Correlation ID
        var correlationId = context.Request.Headers.TryGetValue(NazwaHeadera, out var existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString();

        // Dodaj do response headers
        context.Response.Headers[NazwaHeadera] = correlationId;

        // Dostępny w całym pipeline przez Items
        context.Items[NazwaHeadera] = correlationId;

        await _next(context);
    }
}

// ============================================================
// RATE LIMITING MIDDLEWARE
// ============================================================

public class RateLimitingOpcje
{
    public int MaksZapytanNaOkno { get; set; } = 100;
    public TimeSpan OknoTimeSpan { get; set; } = TimeSpan.FromMinutes(1);
}

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOpcje _opcje;
    private readonly Dictionary<string, (int Licznik, DateTime Reset)> _klienci = new();
    private readonly object _lock = new();

    // Opcje wstrzykiwane przez konstruktor (Singleton middleware)
    public RateLimitingMiddleware(RequestDelegate next, IOptions<RateLimitingOpcje> opcje)
    {
        _next = next;
        _opcje = opcje.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var teraz = DateTime.UtcNow;

        bool przekroczono;
        lock (_lock)
        {
            if (_klienci.TryGetValue(ip, out var info))
            {
                if (teraz > info.Reset)
                {
                    _klienci[ip] = (1, teraz.Add(_opcje.OknoTimeSpan));
                    przekroczono = false;
                }
                else
                {
                    przekroczono = info.Licznik >= _opcje.MaksZapytanNaOkno;
                    if (!przekroczono)
                        _klienci[ip] = (info.Licznik + 1, info.Reset);
                }
            }
            else
            {
                _klienci[ip] = (1, teraz.Add(_opcje.OknoTimeSpan));
                przekroczono = false;
            }
        }

        if (przekroczono)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = _opcje.OknoTimeSpan.TotalSeconds.ToString();
            await context.Response.WriteAsync("Too Many Requests");
            return;
        }

        await _next(context);
    }
}

// ============================================================
// REQUEST BODY BUFFERING MIDDLEWARE
// ============================================================

public class RequestBodyBufferingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestBodyBufferingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // EnableBuffering pozwala na wielokrotny odczyt body (np. przez middleware i kontroler)
        context.Request.EnableBuffering();

        // Opcjonalnie: odczytaj body i przewiń
        if (context.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            // var bodyText = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Przewiń do początku
        }

        await _next(context);
    }
}

// ============================================================
// REQUEST TIMEOUT MIDDLEWARE
// ============================================================

public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TimeSpan _timeout;

    public RequestTimeoutMiddleware(RequestDelegate next, TimeSpan timeout)
    {
        _next = next;
        _timeout = timeout;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        cts.CancelAfter(_timeout);

        var originalToken = context.RequestAborted;
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (!originalToken.IsCancellationRequested)
        {
            context.Response.StatusCode = 408; // Request Timeout
            await context.Response.WriteAsync("Request timed out");
        }
    }
}

// ============================================================
// RESPONSE CACHING MIDDLEWARE DEMO
// ============================================================
// Uwaga: ResponseCaching jest wbudowany w ASP.NET Core
// services.AddResponseCaching() + app.UseResponseCaching()
// [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "kategoria" })]

// ============================================================
// EXTENSION METHODS DLA MIDDLEWARE
// ============================================================

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UzyjGlobalExceptionMiddleware(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();

    public static IApplicationBuilder UzyjRequestLoggera(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggerMiddleware>();

    public static IApplicationBuilder UzyjSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();

    public static IApplicationBuilder UzyjCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();

    public static IApplicationBuilder UzyjRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitingMiddleware>();

    public static IApplicationBuilder UzyjRequestBodyBuffering(this IApplicationBuilder app)
        => app.UseMiddleware<RequestBodyBufferingMiddleware>();

    public static IApplicationBuilder UzyjRequestTimeout(this IApplicationBuilder app, TimeSpan timeout)
        => app.UseMiddleware<RequestTimeoutMiddleware>(timeout);

    // Use/Run/Map/MapWhen/UseWhen demonstracja (inline)
    public static void DodajDemoMiddleware(this IApplicationBuilder app)
    {
        // Use — wykonuje i przekazuje do następnego
        app.Use(async (context, next) =>
        {
            // przed
            await next.Invoke();
            // po
        });

        // MapWhen — warunkowe rozgałęzienie pipeline
        app.MapWhen(
            ctx => ctx.Request.Headers.ContainsKey("X-Debug"),
            branch => branch.Run(async ctx =>
                await ctx.Response.WriteAsync("Debug mode active")));

        // UseWhen — warunkowe, ale wraca do głównego pipeline
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/api"),
            branch => branch.Use(async (ctx, next) =>
            {
                ctx.Items["IsApi"] = true;
                await next.Invoke();
            }));
    }
}
