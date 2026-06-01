### HTTPS w ASP.NET Core

---

### 1. Dlaczego HTTPS — fundamenty

csharp

```csharp
// HTTP  — plaintext, każdy może podsłuchać (man-in-the-middle)
// HTTPS — szyfrowany TLS, niemożliwe podsłuchanie bez certyfikatu

// Co HTTPS chroni:
// ✅ Poufność     — dane szyfrowane, ISP/router nie widzi treści
// ✅ Integralność — nikt nie może zmienić danych w transporcie
// ✅ Autentyczność— certyfikat potwierdza że serwer jest tym za kogo się podaje
// ✅ Nagłówki     — włącznie z Authorization: Bearer TOKEN!

// Co HTTPS NIE chroni:
// ❌ Metadane     — adres IP, domena (tylko subdomena ukryta)
// ❌ Timing       — długość requestu/response widoczna
// ❌ Serwer sam   — jeśli serwer skompromitowany HTTPS nie pomoże

// TLS Handshake (uproszczony):
// 1. Klient → "ClientHello" (obsługiwane wersje TLS, cipher suites)
// 2. Serwer → "ServerHello" + certyfikat X.509
// 3. Klient → weryfikuje certyfikat (CA chain, data ważności, domena)
// 4. Klient → generuje session key, szyfruje kluczem publicznym serwera
// 5. Serwer → odszyfrowuje session key swoim kluczem prywatnym
// 6. Obie strony → komunikacja szyfrowana AES session key

// Konfiguracja w Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Wymuszaj HTTPS w produkcji
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();            // nagłówek Strict-Transport-Security
}
app.UseHttpsRedirection();    // przekieruj HTTP → HTTPS

// Porty
// HTTP:  5000 (dev) lub 80 (prod)
// HTTPS: 5001 (dev) lub 443 (prod)
```

---

### 2. HTTPS Redirection — konfiguracja

csharp

```csharp
// Program.cs — szczegółowa konfiguracja

builder.Services.AddHttpsRedirection(opt =>
{
    // Status code przekierowania
    // 307 Temporary Redirect — metoda HTTP zachowana (POST → POST)
    // 301 Permanent Redirect — przeglądarka cachuje (ostrożnie!)
    opt.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;

    // Port HTTPS (gdy inny niż 443)
    opt.HttpsPort = 443;
});

// Warunkowe przekierowanie — różne env
if (app.Environment.IsDevelopment())
{
    // W dev — port developerski
    builder.Services.AddHttpsRedirection(opt =>
        opt.HttpsPort = 5001);
}
else
{
    // W prod — standardowy port 443
    builder.Services.AddHttpsRedirection(opt =>
    {
        opt.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
        opt.HttpsPort          = 443;
    });
}

// Konfiguracja przez launchSettings.json (tylko dev):
// {
//   "profiles": {
//     "https": {
//       "commandName": "Project",
//       "applicationUrl": "https://localhost:5001;http://localhost:5000",
//       "environmentVariables": {
//         "ASPNETCORE_ENVIRONMENT": "Development"
//       }
//     }
//   }
// }

// appsettings.json — wymuszenie HTTPS portów
// {
//   "Kestrel": {
//     "Endpoints": {
//       "Http": {
//         "Url": "http://0.0.0.0:80"
//       },
//       "Https": {
//         "Url": "https://0.0.0.0:443",
//         "Certificate": {
//           "Path": "/certs/cert.pfx",
//           "Password": "tajne_haslo"
//         }
//       }
//     }
//   }
// }
```

---

### 3. HSTS — HTTP Strict Transport Security

csharp

```csharp
// HSTS — przeglądarka pamięta że serwer wymaga HTTPS
// Przy kolejnych wizytach — nawet jeśli wpisujesz "http://"
// przeglądarka automatycznie wysyła HTTPS!

// Nagłówek HSTS:
// Strict-Transport-Security: max-age=31536000; includeSubDomains; preload

// max-age        — ile sekund przeglądarka pamięta (31536000 = 1 rok)
// includeSubDomains — dotyczy też subdomen (api.sklep.pl, cdn.sklep.pl)
// preload        — zgłoszenie do listy preload przeglądarek (Chrome, Firefox)

// Konfiguracja HSTS
builder.Services.AddHsts(opt =>
{
    opt.MaxAge            = TimeSpan.FromDays(365);   // 1 rok
    opt.IncludeSubDomains = true;                      // subdomeny też
    opt.Preload           = true;                      // lista preload

    // Wyklucz hosty które nie obsługują HTTPS
    // opt.ExcludedHosts.Add("localhost");             // domyślnie już wykluczone
    // opt.ExcludedHosts.Add("internal.firma.pl");
});

// W pipeline:
if (!app.Environment.IsDevelopment())
{
    // HSTS tylko w produkcji! W dev certyfikat self-signed nie działa z HSTS
    app.UseHsts();
}
app.UseHttpsRedirection();

// Sprawdź nagłówek HSTS w odpowiedzi:
// HTTP/2 200 OK
// strict-transport-security: max-age=31536000; includeSubDomains; preload

// HSTS Preload — jak się zapisać:
// 1. Ustaw max-age >= 31536000 (1 rok)
// 2. Ustaw includeSubDomains
// 3. Ustaw preload
// 4. Zgłoś domenę na: https://hstspreload.org
// Po zgłoszeniu — Chrome, Firefox, Safari znają Twoją domenę ZANIM
// pierwszy raz ją odwiedzą — zero ryzyka downgrade attack!

// OSTRZEŻENIA:
// ❌ Nie włączaj HSTS z includeSubDomains jeśli masz subdomeny HTTP
// ❌ Nie włączaj preload jeśli nie jesteś gotowy na 100% HTTPS
// ❌ Po wpisaniu na listę preload — usunięcie trwa miesiące!
```

---

### 4. Certyfikaty — dev i produkcja

csharp

```csharp
// === DEV CERTIFICATE ===
// .NET SDK automatycznie tworzy certyfikat developerski
// dotnet dev-certs https --trust  ← zaufaj certyfikatowi (Windows/Mac)
// dotnet dev-certs https --check  ← sprawdź status
// dotnet dev-certs https --clean  ← usuń i regeneruj

// Konfiguracja certyfikatu w kodzie
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // HTTP
    kestrel.Listen(System.Net.IPAddress.Any, 80);

    // HTTPS z certyfikatem z pliku
    kestrel.Listen(System.Net.IPAddress.Any, 443, listenOpt =>
    {
        listenOpt.UseHttps(httpsOpt =>
        {
            if (app.Environment.IsDevelopment())
            {
                // Dev — domyślny certyfikat .NET
                httpsOpt.UseLettuceEncrypt(null!);  // lub pomiń
            }
            else
            {
                // Prod — certyfikat z pliku PFX
                httpsOpt.ServerCertificate = new System.Security.Cryptography
                    .X509Certificates.X509Certificate2(
                        "/certs/cert.pfx",
                        builder.Configuration["Kestrel:Cert:Password"]);
            }
        });
    });
});

// Alternatywa — przez appsettings
// {
//   "Kestrel": {
//     "Endpoints": {
//       "Https": {
//         "Url": "https://0.0.0.0:443",
//         "Certificate": {
//           "Path": "/etc/ssl/certs/cert.pem",
//           "KeyPath": "/etc/ssl/private/key.pem"
//         }
//       }
//     }
//   }
// }

// Załaduj certyfikat z magazynu Windows
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Listen(System.Net.IPAddress.Any, 443, listenOpt =>
    {
        listenOpt.UseHttps(httpsOpt =>
        {
            // Załaduj z Windows Certificate Store
            var store = new System.Security.Cryptography
                .X509Certificates.X509Store(
                    System.Security.Cryptography.X509Certificates.StoreName.My,
                    System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser);
            store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadOnly);

            var cert = store.Certificates
                .Find(
                    System.Security.Cryptography.X509Certificates.X509FindType.FindBySubjectName,
                    "api.sklep.pl",
                    validOnly: true)
                .FirstOrDefault();

            store.Close();

            if (cert != null)
                httpsOpt.ServerCertificate = cert;
        });
    });
});

// Let's Encrypt — automatyczne certyfikaty (darmowe!)
// dotnet add package Certes  lub  LettuceEncrypt
// builder.Services.AddLettuceEncrypt(opt =>
// {
//     opt.EmailAddress = "admin@sklep.pl";
//     opt.DomainNames  = new[] { "sklep.pl", "api.sklep.pl" };
//     opt.AcceptTermsOfService = true;
// });

// Sprawdzanie certyfikatu w kodzie
public class CertyfikatSerwis
{
    public void SprawdzCertyfikat(string hostName, int port = 443)
    {
        using var client = new System.Net.Sockets.TcpClient(hostName, port);
        using var ssl    = new System.Net.Security.SslStream(client.GetStream());

        ssl.AuthenticateAsClient(hostName);

        var cert = ssl.RemoteCertificate as System.Security.Cryptography
            .X509Certificates.X509Certificate2;

        if (cert is null) return;

        Console.WriteLine($"Podmiot:      {cert.Subject}");
        Console.WriteLine($"Wystawca:     {cert.Issuer}");
        Console.WriteLine($"Ważny od:     {cert.NotBefore}");
        Console.WriteLine($"Ważny do:     {cert.NotAfter}");
        Console.WriteLine($"Odcisk SHA256:{cert.GetCertHashString(
            System.Security.Cryptography.HashAlgorithmName.SHA256)}");
        Console.WriteLine($"Wygasa za:    " +
            $"{(cert.NotAfter - DateTime.Now).Days} dni");
    }
}
```

---

### 5. Base64 w nagłówkach HTTP

csharp

```csharp
// Base64 — kodowanie binarne → tekst ASCII
// NIE jest szyfrowaniem — każdy może odkodować!
// Używane gdy nagłówek musi zawierać tylko znaki ASCII

// === BASIC AUTH === (najczęstsze użycie Base64 w nagłówkach)
// Authorization: Basic BASE64(login:haslo)
// Authorization: Basic amFuQHRlc3QucGw6VGFqbmUxMjM=
//                      ↑ = Base64("jan@test.pl:Tajne123")

public class BasicAuthHelper
{
    // Enkoduj credentials → nagłówek
    public static string EnkodujCredentials(string login, string haslo)
    {
        string combo  = $"{login}:{haslo}";
        byte[] bajty  = System.Text.Encoding.UTF8.GetBytes(combo);
        string base64 = Convert.ToBase64String(bajty);
        return $"Basic {base64}";
    }

    // Dekoduj nagłówek → credentials
    public static (string Login, string Haslo)? DekodujCredentials(
        string naglowek)
    {
        if (!naglowek.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            string base64  = naglowek["Basic ".Length..].Trim();
            byte[] bajty   = Convert.FromBase64String(base64);
            string decoded = System.Text.Encoding.UTF8.GetString(bajty);

            // Znajdź pierwsze ':' — hasło może zawierać ':'
            int idx = decoded.IndexOf(':');
            if (idx < 0) return null;

            return (decoded[..idx], decoded[(idx + 1)..]);
        }
        catch (FormatException)
        {
            return null;  // nieprawidłowy Base64
        }
    }

    // Demo
    public static void Demo()
    {
        string nagłowek = EnkodujCredentials("jan@test.pl", "Tajne123");
        Console.WriteLine($"Nagłówek: {nagłowek}");
        // Authorization: Basic amFuQHRlc3QucGw6VGFqbmUxMjM=

        var (login, haslo) = DekodujCredentials(nagłowek)!.Value;
        Console.WriteLine($"Login: {login}");   // jan@test.pl
        Console.WriteLine($"Hasło: {haslo}");   // Tajne123
        // ⚠️ Hasło JAWNE — dlatego Basic Auth tylko przez HTTPS!
    }
}

// === BASE64 W INNYCH KONTEKSTACH ===

public class Base64Przyklady
{
    // Przesyłanie pliku binarnego w nagłówku
    public static string PlikDoBase64(string sciezka)
    {
        byte[] bajty = System.IO.File.ReadAllBytes(sciezka);
        return Convert.ToBase64String(bajty);
    }

    // Base64URL — wariant dla URL/JWT (bez +, /, =)
    public static string DoBase64Url(byte[] bajty)
    {
        return Convert.ToBase64String(bajty)
            .Replace('+', '-')   // + → - (bezpieczny w URL)
            .Replace('/', '_')   // / → _ (bezpieczny w URL)
            .TrimEnd('=');       // usuń padding
    }

    public static byte[] ZBase64Url(string base64url)
    {
        // Przywróć standardowy Base64
        string base64 = base64url
            .Replace('-', '+')
            .Replace('_', '/');

        // Dodaj padding jeśli brakuje
        int padding = base64.Length % 4;
        if (padding == 2) base64 += "==";
        else if (padding == 3) base64 += "=";

        return Convert.FromBase64String(base64);
    }

    // Nagłówek z danymi binarnymi (np. podpis)
    public static void PrzykladNaglowek(HttpClient client)
    {
        byte[] podpis = System.Security.Cryptography.RandomNumberGenerator
            .GetBytes(32);

        // Dodaj podpis jako Base64 w nagłówku
        client.DefaultRequestHeaders.Add(
            "X-Signature",
            Convert.ToBase64String(podpis));

        // Dane binarne jako Base64
        byte[] dane = System.Text.Encoding.UTF8.GetBytes("payload danych");
        client.DefaultRequestHeaders.Add(
            "X-Payload",
            Convert.ToBase64String(dane));
    }
}
```

---

### 6. Bezpieczne nagłówki HTTP

csharp

```csharp
// Middleware dodający nagłówki bezpieczeństwa

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Dodaj nagłówki przed wysłaniem response
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;

            // === TRANSPORT SECURITY ===
            // HSTS — dodawany też przez app.UseHsts(), ale można ręcznie
            if (ctx.Request.IsHttps)
                h["Strict-Transport-Security"] =
                    "max-age=31536000; includeSubDomains; preload";

            // === CLICKJACKING ===
            h["X-Frame-Options"] = "DENY";
            // DENY     — nigdy w iframe
            // SAMEORIGIN — tylko ta sama domena
            // ALLOW-FROM — konkretna domena (przestarzałe)

            // === MIME SNIFFING ===
            h["X-Content-Type-Options"] = "nosniff";
            // Przeglądarka NIE zgaduje Content-Type — używa tylko deklarowanego

            // === XSS PROTECTION (legacy) ===
            h["X-XSS-Protection"] = "1; mode=block";
            // Nowoczesne przeglądarki ignorują — CSP jest lepsze

            // === REFERRER POLICY ===
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // no-referrer               — nigdy nie wysyłaj Referer
            // strict-origin             — tylko domena (nie ścieżka)
            // strict-origin-when-cross-origin — pełny URL dla same-origin

            // === PERMISSIONS POLICY ===
            h["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), " +
                "payment=(), usb=(), bluetooth=()";
            // Wyłącz zbędne API przeglądarki

            // === CONTENT SECURITY POLICY ===
            h["Content-Security-Policy"] =
                "default-src 'self'; "               +  // domyślnie tylko własna domena
                "script-src 'self'; "                +  // JS tylko z własnej domeny
                "style-src 'self' 'unsafe-inline'; " +  // CSS + inline style OK
                "img-src 'self' data: https:; "      +  // obrazki z HTTPS
                "font-src 'self'; "                  +  // fonty z własnej domeny
                "connect-src 'self' https://api.sklep.pl; " + // XHR/fetch
                "frame-ancestors 'none'; "           +  // brak iframes (jak X-Frame-Options)
                "base-uri 'self'; "                  +  // ogranicz <base>
                "form-action 'self'";                   // formularze tylko do własnej domeny

            // === UKRYJ INFORMACJE O SERWERZE ===
            h.Remove("Server");           // usuń "nginx/1.24.0" lub "Kestrel"
            h.Remove("X-Powered-By");     // usuń "ASP.NET"
            h.Remove("X-AspNet-Version"); // usuń wersję

            return Task.CompletedTask;
        });

        await _next(ctx);
    }
}

// Rejestracja
app.UseMiddleware<SecurityHeadersMiddleware>();

// Weryfikacja nagłówków — securityheaders.com lub ręcznie:
// curl -I https://api.sklep.pl | grep -E "strict|content-security|x-frame"
```

---

### 7. HttpClient z HTTPS

csharp

```csharp
// HttpClient — konfiguracja HTTPS po stronie klienta

// Podstawowe użycie — automatyczna weryfikacja certyfikatu
using var client = new HttpClient();
var response = await client.GetAsync("https://api.sklep.pl/produkty");

// Własna walidacja certyfikatu — dla self-signed (TYLKO DEV!)
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
    {
        // ⚠️ TYLKO W DEVELOPMENT! Nigdy w produkcji!
        if (errors == System.Net.Security.SslPolicyErrors.None)
            return true;

        // Zezwól na self-signed tylko dla localhost
        return message.RequestUri?.Host == "localhost";
    }
};

using var devClient = new HttpClient(handler);

// Certyfikat klienta (mTLS — mutual TLS)
var certKlienta = new System.Security.Cryptography.X509Certificates
    .X509Certificate2("/certs/client.pfx", "haslo");

var mTlsHandler = new HttpClientHandler();
mTlsHandler.ClientCertificates.Add(certKlienta);

using var mTlsClient = new HttpClient(mTlsHandler);

// Przez IHttpClientFactory — ZALECANE w ASP.NET Core
builder.Services.AddHttpClient("SklepApi", client =>
{
    client.BaseAddress = new Uri("https://api.sklep.pl");
    client.DefaultRequestHeaders.Add("User-Agent", "Sklep/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Wymuszaj TLS 1.2+
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12
                 | System.Security.Authentication.SslProtocols.Tls13,
});

// Użycie przez DI
public class ApiKlient
{
    private readonly HttpClient _client;

    public ApiKlient(IHttpClientFactory factory)
        => _client = factory.CreateClient("SklepApi");

    public async Task<string> PobierzDaneAsync(
        string endpoint, CancellationToken ct = default)
    {
        using var response = await _client.GetAsync(endpoint, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
```

---

### 8. Praktyczny przykład — kompletna konfiguracja produkcyjna

csharp

```csharp
// Program.cs — produkcyjna konfiguracja HTTPS + bezpieczeństwo

var builder = WebApplication.CreateBuilder(args);

// === KESTREL — serwer HTTP ===
builder.WebHost.ConfigureKestrel((ctx, kestrel) =>
{
    var certCfg = ctx.Configuration.GetSection("Kestrel:Certificate");

    kestrel.AddServerHeader = false;  // ukryj "Kestrel" w Server header

    kestrel.Listen(System.Net.IPAddress.Any, 80);   // HTTP (tylko redirect)

    kestrel.Listen(System.Net.IPAddress.Any, 443, listenOpt =>
    {
        listenOpt.UseHttps(httpsOpt =>
        {
            if (ctx.HostingEnvironment.IsDevelopment())
            {
                // Dev cert — brak konfiguracji = użyj domyślnego
                return;
            }

            // Prod — certyfikat z pliku lub zmiennej środowiskowej
            string? certPath = certCfg["Path"];
            string? keyPath  = certCfg["KeyPath"];
            string? pfxPath  = certCfg["PfxPath"];
            string? pfxPass  = certCfg["PfxPassword"];

            if (!string.IsNullOrEmpty(pfxPath) && File.Exists(pfxPath))
            {
                httpsOpt.ServerCertificate =
                    new System.Security.Cryptography.X509Certificates
                        .X509Certificate2(pfxPath, pfxPass);
            }
            else if (!string.IsNullOrEmpty(certPath))
            {
                httpsOpt.ServerCertificate =
                    System.Security.Cryptography.X509Certificates.X509Certificate2
                    .CreateFromPemFile(certPath, keyPath);
            }

            // Wymuś TLS 1.2+
            httpsOpt.SslProtocols =
                System.Security.Authentication.SslProtocols.Tls12 |
                System.Security.Authentication.SslProtocols.Tls13;
        });
    });
});

// === HSTS ===
builder.Services.AddHsts(opt =>
{
    opt.MaxAge            = TimeSpan.FromDays(365);
    opt.IncludeSubDomains = true;
    opt.Preload           = true;
});

// === HTTPS REDIRECT ===
builder.Services.AddHttpsRedirection(opt =>
{
    opt.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    opt.HttpsPort          = 443;
});

// === MIDDLEWARE PIPELINE ===
var app = builder.Build();

// Kolejność MA ZNACZENIE!
app.UseMiddleware<SecurityHeadersMiddleware>();  // 1. nagłówki bezpieczeństwa

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();                               // 2. HSTS header
    app.UseExceptionHandler("/error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();                       // 3. redirect HTTP → HTTPS

app.UseStaticFiles();                            // 4. pliki statyczne
app.UseRouting();                                // 5. routing
app.UseCors();                                   // 6. CORS
app.UseAuthentication();                         // 7. autentykacja
app.UseAuthorization();                          // 8. autoryzacja
app.MapControllers();

// Prosty endpoint sprawdzający HTTPS
app.MapGet("/security-check", (HttpContext ctx) => new
{
    IsHttps       = ctx.Request.IsHttps,
    Protocol      = ctx.Request.Protocol,
    Scheme        = ctx.Request.Scheme,
    Host          = ctx.Request.Host.ToString(),
    TlsVersion    = ctx.Features.Get<Microsoft.AspNetCore.Http.Features
                        .ITlsHandshakeFeature>()?.Protocol.ToString(),
    CipherAlgorithm = ctx.Features.Get<Microsoft.AspNetCore.Http.Features
                        .ITlsHandshakeFeature>()?.CipherAlgorithm.ToString()
}).WithMetadata(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());

app.Run();
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między HTTP a HTTPS?"** HTTP — plaintext, dane widoczne w sieci dla każdego (ISP, router, atakujący w tej samej sieci Wi-Fi). HTTPS — szyfrowany TLS, dane nieczytelne bez klucza prywatnego serwera. HTTPS zapewnia trzy gwarancje: poufność (dane zaszyfrowane), integralność (nikt nie zmienił danych w transporcie), autentyczność (certyfikat potwierdza tożsamość serwera). Szczególnie ważne dla: tokenów JWT, haseł, danych osobowych — przez HTTP są jawne.

**"Co to HSTS i przed czym chroni?"** HSTS (HTTP Strict Transport Security) to nagłówek który mówi przeglądarce "Ten serwer obsługuje tylko HTTPS — zapamiętaj to na max-age sekund". Przy kolejnych wizytach przeglądarka automatycznie używa HTTPS nawet jeśli użytkownik wpisze `http://`. Chroni przed downgrade attack — atakujący w tej samej sieci nie może przekierować użytkownika na HTTP żeby podsłuchać ruch. `preload` to lista wbudowana w przeglądarki — ochrona od pierwszej wizyty.

**"Czym jest Base64 i dlaczego to NIE jest szyfrowanie?"** Base64 to kodowanie które zamienia bajty binarne na znaki ASCII (A-Z, a-z, 0-9, +, /). Każdy może to odkodować — nie ma klucza, nie ma sekretu. Używamy go gdy protokół wymaga tekstu ASCII (nagłówki HTTP mogą zawierać tylko ASCII). Basic Auth wysyła `Base64(login:hasło)` — to jest JAWNE, dlatego Basic Auth musi być zawsze przez HTTPS. JWT też używa Base64Url w header i payload — każdy może odczytać claims, dlatego nie wkładamy sekretów do JWT.

**"Jak skonfigurować własny certyfikat SSL dla ASP.NET Core w produkcji?"** Trzy opcje: (1) Certyfikat z pliku PFX/PEM — konfiguracja w `appsettings.json` pod `Kestrel:Endpoints:Https:Certificate`, (2) Let's Encrypt przez LettuceEncrypt — automatyczne darmowe certyfikaty, odnawiane automatycznie co 90 dni, (3) Reverse proxy (nginx/Apache/Caddy) — terminuje TLS przed aplikacją, aplikacja dostaje zwykły HTTP na localhost. Opcja 3 jest najczęstsza w produkcji — nginx świetnie obsługuje SSL/TLS, można go konfigurować niezależnie od aplikacji .NET.