using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace _07_Cryptography_Security_JWT;

// ============================================================
// 1. HTTPS — FUNDAMENTY
// ============================================================
// HTTP  — plaintext, każdy może podsłuchać (man-in-the-middle)
// HTTPS — szyfrowany TLS, niemożliwe podsłuchanie bez klucza

// Co HTTPS chroni:
// ✅ Poufność     — dane szyfrowane (AES session key)
// ✅ Integralność — nikt nie może zmienić danych w transporcie
// ✅ Autentyczność— certyfikat X.509 potwierdza tożsamość serwera
// ✅ Nagłówki     — Authorization: Bearer TOKEN też zaszyfrowany!

// Co HTTPS NIE chroni:
// ❌ Metadane     — adres IP, domena (tylko subdomena ukryta)
// ❌ Timing       — długość request/response widoczna
// ❌ Serwer sam   — jeśli serwer skompromitowany HTTPS nie pomoże

// TLS Handshake (uproszczony):
// 1. Klient → "ClientHello" (TLS version, cipher suites)
// 2. Serwer → "ServerHello" + certyfikat X.509
// 3. Klient → weryfikuje certyfikat (CA chain, data ważności, domena)
// 4. Klient → generuje session key, szyfruje kluczem publicznym serwera
// 5. Serwer → odszyfrowuje session key swoim kluczem prywatnym
// 6. Obie strony → komunikacja szyfrowana AES session key

public static class HttpsKonceptyDemo
{
    public static void OpisKonfiguracji()
    {
        Console.WriteLine("  Program.cs — HTTPS + HSTS pipeline:");
        Console.WriteLine("    if (!IsDevelopment()) { app.UseHsts(); }  // tylko w prod");
        Console.WriteLine("    app.UseHttpsRedirection();                // HTTP→HTTPS redirect");
        Console.WriteLine("    services.AddHttpsRedirection(opt => {");
        Console.WriteLine("        opt.RedirectStatusCode = 307;         // Temporary Redirect");
        Console.WriteLine("        opt.HttpsPort = 443;                  // prod port");
        Console.WriteLine("    });");
    }
}

// ============================================================
// 2. HSTS — HTTP STRICT TRANSPORT SECURITY
// ============================================================
// Nagłówek: Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
// max-age        — sekund przeglądarka pamięta (31536000 = 1 rok)
// includeSubDomains — dotyczy też subdomen (api.sklep.pl, cdn.sklep.pl)
// preload        — wbudowany w Chrome/Firefox (ochrona od pierwszej wizyty!)
// Rejestracja preload: https://hstspreload.org

public static class HstsKonfiguracja
{
    public static void OpisSetup()
    {
        Console.WriteLine("  HSTS setup:");
        Console.WriteLine("    builder.Services.AddHsts(opt => {");
        Console.WriteLine("        opt.MaxAge            = TimeSpan.FromDays(365);");
        Console.WriteLine("        opt.IncludeSubDomains = true;   // subdomeny też HTTPS");
        Console.WriteLine("        opt.Preload           = true;   // wbudowany w przeglądarkach");
        Console.WriteLine("    });");
        Console.WriteLine("  ⚠️ Nie włączaj includeSubDomains jeśli masz HTTP subdomeny!");
        Console.WriteLine("  ⚠️ Po wpisaniu na listę preload — usunięcie trwa miesiące!");
    }
}

// ============================================================
// 3. BASE64 / BASE64URL — KODOWANIE, NIE SZYFROWANIE!
// ============================================================

public static class Base64Przyklady
{
    // Standardowy Base64 → tekst ASCII
    // Używany gdy protokół wymaga tylko ASCII (nagłówki HTTP)
    public static string DoBase64(byte[] bajty) => Convert.ToBase64String(bajty);
    public static byte[] ZBase64(string s) => Convert.FromBase64String(s);

    // Base64URL — wariant dla URL i JWT (bez +, /, = znaków specjalnych)
    // JWT używa Base64Url w header i payload
    public static string DoBase64Url(byte[] bajty) =>
        Convert.ToBase64String(bajty)
            .Replace('+', '-')   // + → - (bezpieczny w URL)
            .Replace('/', '_')   // / → _ (bezpieczny w URL)
            .TrimEnd('=');       // usuń padding

    public static byte[] ZBase64Url(string s)
    {
        string base64 = s.Replace('-', '+').Replace('_', '/');
        int padding = base64.Length % 4;
        if (padding == 2) base64 += "==";
        else if (padding == 3) base64 += "=";
        return Convert.FromBase64String(base64);
    }

    // Demo — plik binarny do Base64
    public static string PlikDoBase64Str(byte[] dane) => Convert.ToBase64String(dane);

    // Nagłówek z danymi binarnymi (np. podpis, certyfikat)
    public static void PokazNaglowekBinarny()
    {
        byte[] podpis = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        string headerValue = Convert.ToBase64String(podpis);
        Console.WriteLine($"    X-Signature: {headerValue[..20]}...");
    }

    public static void Demo()
    {
        string tekst = "jan@test.pl:Tajne123";
        byte[] bajty = Encoding.UTF8.GetBytes(tekst);
        string base64 = DoBase64(bajty);
        string base64url = DoBase64Url(bajty);

        Console.WriteLine($"  [Base64]    '{tekst}' → '{base64}'");
        Console.WriteLine($"  [Base64URL] '{tekst}' → '{base64url}' (bez +/= znaków)");
        Console.WriteLine("  ⚠️ Base64 to KODOWANIE, nie szyfrowanie! Każdy może odkodować!");

        // Roundtrip
        string dekodowany = Encoding.UTF8.GetString(ZBase64(base64));
        string dekodowanyUrl = Encoding.UTF8.GetString(ZBase64Url(base64url));
        Console.WriteLine($"  [Dekod]     '{dekodowany}' = '{dekodowanyUrl}' (identyczne)");
    }
}

// ============================================================
// 4. BASIC AUTH HELPER — PODSTAWY BASE64 W HTTP
// ============================================================
// Authorization: Basic BASE64(login:haslo)
// Authorization: Basic amFuQHRlc3QucGw6VGFqbmUxMjM=

public static class BasicAuthBase64Helper
{
    public static string EnkodujNaglowek(string login, string haslo)
    {
        string combo  = $"{login}:{haslo}";
        byte[] bajty  = Encoding.UTF8.GetBytes(combo);
        return $"Basic {Convert.ToBase64String(bajty)}";
    }

    public static (string Login, string Haslo)? DekodujNaglowek(string naglowek)
    {
        if (!naglowek.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            string base64  = naglowek["Basic ".Length..].Trim();
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            int idx = decoded.IndexOf(':');
            if (idx < 0) return null;
            return (decoded[..idx], decoded[(idx + 1)..]);
        }
        catch (FormatException) { return null; }
    }

    public static void Demo()
    {
        string naglowek = EnkodujNaglowek("jan@test.pl", "Tajne123");
        Console.WriteLine($"  Authorization: {naglowek}");
        var (login, haslo) = DekodujNaglowek(naglowek)!.Value;
        Console.WriteLine($"  Login={login}, Hasło={haslo} ← JAWNE! Dlatego tylko HTTPS!");
    }
}

// ============================================================
// 5. CERTYFIKAT SERWIS — INSPEKCJA X.509
// ============================================================

public class CertyfikatSerwis
{
    // Sprawdź certyfikat zdalnego serwera przez TcpClient + SslStream
    // W demo nie łączymy — pokazujemy tylko strukturę kodu
    public static void OpisSprawdzania(string hostName = "example.com", int port = 443)
    {
        Console.WriteLine($"  Sprawdzanie certyfikatu {hostName}:{port}:");
        Console.WriteLine("    using var tcp = new TcpClient(host, port);");
        Console.WriteLine("    using var ssl = new SslStream(tcp.GetStream());");
        Console.WriteLine("    ssl.AuthenticateAsClient(hostName);");
        Console.WriteLine("    var cert = ssl.RemoteCertificate as X509Certificate2;");
        Console.WriteLine("    // cert.Subject, cert.Issuer, cert.NotBefore, cert.NotAfter");
        Console.WriteLine("    // cert.GetCertHashString(SHA256)");
        Console.WriteLine("    // (cert.NotAfter - DateTime.Now).Days — wygasa za X dni");
    }

    // Typy certyfikatów i ich konfiguracja w Kestrel
    public static void OpisKonfiguracji()
    {
        Console.WriteLine("  Kestrel — certyfikaty:");
        Console.WriteLine("    Dev:  dotnet dev-certs https --trust");
        Console.WriteLine("    Prod: konfiguracja przez appsettings.json → Kestrel:Endpoints:Https:Certificate");
        Console.WriteLine("    PFX:  new X509Certificate2(\"cert.pfx\", password)");
        Console.WriteLine("    PEM:  X509Certificate2.CreateFromPemFile(certPath, keyPath)");
        Console.WriteLine("    WinStore: X509Store → FindBySubjectName");
        Console.WriteLine("    Let's Encrypt: LettuceEncrypt package — auto odnowienie co 90 dni");
        Console.WriteLine("  TLS 1.2+: httpsOpt.SslProtocols = Tls12 | Tls13");
        Console.WriteLine("  kestrel.AddServerHeader = false  // ukryj 'Kestrel' w nagłówku Server");
    }
}

// ============================================================
// 6. SECURITY HEADERS MIDDLEWARE — ROZSZERZONA WERSJA
// ============================================================

public class SecurityHeadersMiddlewareHttps
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddlewareHttps(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // OnStarting — bezpieczne miejsce na nagłówki (przed wysłaniem)
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;

            // HSTS — tylko dla HTTPS requestów
            if (context.Request.IsHttps)
                h["Strict-Transport-Security"] =
                    "max-age=31536000; includeSubDomains; preload";

            // Clickjacking
            h["X-Frame-Options"] = "DENY";
            // DENY=nigdy w iframe, SAMEORIGIN=tylko ta sama domena

            // MIME sniffing — przeglądarka NIE zgaduje Content-Type
            h["X-Content-Type-Options"] = "nosniff";

            // XSS Protection (legacy — CSP jest lepsze dla nowoczesnych przeglądarek)
            h["X-XSS-Protection"] = "1; mode=block";

            // Referrer Policy
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // no-referrer, strict-origin, strict-origin-when-cross-origin

            // Permissions Policy — wyłącz zbędne API przeglądarki
            h["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=(), bluetooth=()";

            // Content Security Policy (CSP)
            h["Content-Security-Policy"] =
                "default-src 'self'; "               +
                "script-src 'self'; "                +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; "      +
                "font-src 'self'; "                  +
                "connect-src 'self'; "               +
                "frame-ancestors 'none'; "           +
                "base-uri 'self'; "                  +
                "form-action 'self'";

            // Ukryj informacje o serwerze
            h.Remove("Server");
            h.Remove("X-Powered-By");
            h.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

// ============================================================
// 7. HTTPCLIENT Z HTTPS — KONFIGURACJA
// ============================================================

public static class HttpClientHttpsKonfiguracja
{
    // Podstawowe HTTPS — automatyczna weryfikacja certyfikatu
    public static HttpClient UtworzStandardowy()
        => new(); // automatyczna weryfikacja CA chain

    // Własna walidacja — TYLKO DEV (self-signed)
    public static HttpClient UtworzDevKlient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
            {
                // ⚠️ TYLKO DEVELOPMENT! Nigdy w produkcji!
                if (errors == SslPolicyErrors.None) return true;
                return msg.RequestUri?.Host == "localhost";
            }
        };
        return new HttpClient(handler);
    }

    // mTLS — mutual TLS (certyfikat klienta)
    public static HttpClient UtworzMtlsKlient(X509Certificate2 certKlienta)
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certKlienta);
        return new HttpClient(handler);
    }

    // IHttpClientFactory — ZALECANE w ASP.NET Core (rejestracja w Program.cs)
    public static void OpisIHttpClientFactory()
    {
        Console.WriteLine("  IHttpClientFactory konfiguracja:");
        Console.WriteLine("    builder.Services.AddHttpClient(\"SklepApi\", client => {");
        Console.WriteLine("        client.BaseAddress = new Uri(\"https://api.sklep.pl\");");
        Console.WriteLine("        client.Timeout = TimeSpan.FromSeconds(30);");
        Console.WriteLine("    })");
        Console.WriteLine("    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler {");
        Console.WriteLine("        SslProtocols = Tls12 | Tls13,  // wymuś TLS 1.2+");
        Console.WriteLine("    });");
    }

    // Weryfikacja HTTPS endpointu
    public static async Task<string> SprawdzHttpsAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await client.GetAsync(url);
            return $"OK: {(int)resp.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"Błąd: {ex.Message[..Math.Min(50, ex.Message.Length)]}";
        }
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class HttpsBezpieczenstwoDemo
{
    public static void Uruchom()
    {
        // HTTPS fundamenty
        Console.WriteLine("  HTTP vs HTTPS:");
        Console.WriteLine("    HTTP:  plaintext — widoczny dla ISP, routera, man-in-the-middle");
        Console.WriteLine("    HTTPS: TLS szyfrowanie — AES session key, niemożliwe podsłuchanie");
        Console.WriteLine("    Chroni: poufność, integralność, autentyczność (certyfikat X.509)");
        Console.WriteLine("    TLS Handshake: ClientHello → ServerHello+cert → KeyExchange → AES");
        HttpsKonceptyDemo.OpisKonfiguracji();

        // HSTS
        HstsKonfiguracja.OpisSetup();
        Console.WriteLine("  HSTS chroni przed downgrade attack:");
        Console.WriteLine("    Przeglądarka pamięta 'ten serwer tylko HTTPS' przez max-age sekund");
        Console.WriteLine("    Preload = wbudowany w Chrome/Firefox przed pierwszą wizytą!");

        // Base64 i Base64URL
        Base64Przyklady.Demo();
        Console.WriteLine("  [Base64URL] JWT używa Base64URL (bez +/= znaków) w header i payload");
        Base64Przyklady.PokazNaglowekBinarny();

        // Basic Auth Base64
        BasicAuthBase64Helper.Demo();

        // Certyfikaty
        CertyfikatSerwis.OpisSprawdzania();
        CertyfikatSerwis.OpisKonfiguracji();

        // Security Headers
        Console.WriteLine("  SecurityHeadersMiddleware (response.OnStarting):");
        Console.WriteLine("    Strict-Transport-Security (HSTS)");
        Console.WriteLine("    X-Frame-Options: DENY (clickjacking)");
        Console.WriteLine("    X-Content-Type-Options: nosniff (MIME sniffing)");
        Console.WriteLine("    X-XSS-Protection: 1; mode=block (legacy)");
        Console.WriteLine("    Referrer-Policy: strict-origin-when-cross-origin");
        Console.WriteLine("    Permissions-Policy: camera=(), microphone=(), geolocation=()");
        Console.WriteLine("    Content-Security-Policy: default-src 'self'; script-src 'self'; ...");
        Console.WriteLine("    Usuń: Server, X-Powered-By, X-AspNet-Version");

        // HttpClient HTTPS
        HttpClientHttpsKonfiguracja.OpisIHttpClientFactory();
        Console.WriteLine("  ServerCertificateCustomValidationCallback: tylko dev/self-signed!");
        Console.WriteLine("  mTLS: HttpClientHandler.ClientCertificates.Add(certKlienta)");
        Console.WriteLine("  SslProtocols = Tls12 | Tls13  ← wymuszaj nowoczesny TLS");

        // Produkcyjna konfiguracja Kestrel
        Console.WriteLine("  Kestrel prod config:");
        Console.WriteLine("    builder.WebHost.ConfigureKestrel((ctx, k) => {");
        Console.WriteLine("        k.AddServerHeader = false;       // ukryj Server: Kestrel");
        Console.WriteLine("        k.Listen(IPAddress.Any, 80);    // HTTP (tylko redirect)");
        Console.WriteLine("        k.Listen(IPAddress.Any, 443, opt => opt.UseHttps(httpsOpt => {");
        Console.WriteLine("            httpsOpt.ServerCertificate = new X509Certificate2(pfxPath, pass);");
        Console.WriteLine("            httpsOpt.SslProtocols = Tls12 | Tls13;");
        Console.WriteLine("        }));");
        Console.WriteLine("    });");

        // /security-check endpoint
        Console.WriteLine("  Demo endpoint /security-check: IsHttps, Protocol, Scheme, TlsVersion, CipherAlgorithm");
    }
}
