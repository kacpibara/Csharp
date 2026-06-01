### Basic Auth i Haszowanie Haseł w C#

---

### 1. Dlaczego haszowanie — nie szyfrowanie

csharp

```csharp
// SZYFROWANIE — dwukierunkowe, można odszyfrować (złe dla haseł!)
// HASZOWANIE  — jednokierunkowe, nie można odwrócić (dobre dla haseł!)

// NIGDY nie rób tego:
public class ZlePraktyki
{
    // ❌ Plaintext — katastrofa przy wycieku bazy!
    public void ZapiszHasloJawne(string haslo)
    {
        var user = new { Haslo = haslo };  // "Tajne123!" w bazie — ZŁE!
    }

    // ❌ Szyfrowanie — można odszyfrować jeśli klucz wycieknie
    public string SzyfrowanieSym(string haslo)
    {
        // AES jest odwracalny — jeśli klucz wycieknie, wszystkie hasła skompromitowane
        return Convert.ToBase64String(
            System.Security.Cryptography.Aes.Create()
                .CreateEncryptor().TransformFinalBlock(
                    System.Text.Encoding.UTF8.GetBytes(haslo), 0,
                    haslo.Length));  // uproszczone, złe!
    }

    // ❌ MD5 / SHA-1 — kryptograficznie złamane, tęczowe tablice
    public string MD5Hash(string haslo)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] bajty = md5.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(haslo));
        return Convert.ToHexString(bajty).ToLower();
        // "Tajne123" → "d8578edf8458ce06fbc5bb76a58c5ca4"
        // Ten hash jest w tęczowych tablicach — crackowany natychmiast!
    }

    // ❌ SHA-256 BEZ SOLI — podatny na tęczowe tablice i ataki słownikowe
    public string SHA256BezSoli(string haslo)
    {
        byte[] bajty = System.Security.Cryptography.SHA256
            .HashData(System.Text.Encoding.UTF8.GetBytes(haslo));
        return Convert.ToBase64String(bajty);
        // 1000 użytkowników z hasłem "Tajne123" → identyczne hashe!
        // Atakujący łamie jedno → łamie wszystkie!
    }
}

// DOBRE podejście:
// 1. BCrypt    — dedykowany do haseł, wbudowany salt, powolny celowo
// 2. Argon2   — zwycięzca Password Hashing Competition 2015, najlepszy
// 3. PBKDF2   — standard NIST, wbudowany w .NET, dobry kompromis
// 4. scrypt   — odporny na sprzętowe ataki GPU

// Wymagania dobrego algorytmu haszowania haseł:
// ✅ Wolny — utrudnia brute force (koszt obliczeniowy)
// ✅ Unikalny salt — każde hasło ma inny hash mimo identycznej wartości
// ✅ Niemożliwy do odwrócenia — jednokierunkowy
// ✅ Skalowalny — można zwiększyć koszt z czasem
```

---

### 2. BCrypt — najprostszy i najczęstszy wybór

csharp

```csharp
// dotnet add package BCrypt.Net-Next

using BCrypt.Net;

// BCrypt automatycznie:
// 1. Generuje losowy salt (22 znaki)
// 2. Hashuje hasło z solą
// 3. Zwraca hash zawierający wersję, cost factor i salt

// Format hasha BCrypt:
// $2a$12$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
// $2a$ — wersja algorytmu
// $12$ — cost factor (2^12 = 4096 iteracji)
// N9qo8uLOickgx2ZMRZoMye — salt (22 znaki Base64)
// IjZAgcfl7p92ldGxad68LJZdL17lhWy — hash (31 znaki)

public class BCryptPrzyklad
{
    // Haszowanie hasła
    public string HashujHaslo(string haslo)
    {
        // WorkFactor (cost factor) — 2^n iteracji
        // 10 = 1024 iter  — szybkie,  OK dla dev
        // 11 = 2048 iter  — wolniejsze
        // 12 = 4096 iter  — ZALECANE dla produkcji (~250ms)
        // 13 = 8192 iter  — ~500ms
        // 14 = 16384 iter — ~1s, dla systemów z niską liczbą logowań
        return BCrypt.HashPassword(haslo, workFactor: 12);
    }

    // Weryfikacja hasła
    public bool WeryfikujHaslo(string haslo, string hash)
    {
        // BCrypt porównuje w czasie stałym — nie podatne na timing attack
        return BCrypt.Verify(haslo, hash);
    }

    // Sprawdź czy hash wymaga aktualizacji (np. zwiększono cost factor)
    public bool CzyWymagaRehash(string hash, int minCostFactor = 12)
    {
        return BCrypt.PasswordNeedsRehash(hash, minCostFactor);
    }

    // Rehash przy logowaniu — stopniowa aktualizacja istniejących haseł
    public async Task<string?> LogujIRehashujAsync(
        string email, string hasloWpisane,
        IUzytkownikRepo repo, CancellationToken ct)
    {
        var user = await repo.FindByEmailAsync(email, ct);
        if (user is null) return null;

        // Weryfikuj hasło
        if (!BCrypt.Verify(hasloWpisane, user.HashHasla))
            return null;

        // Sprawdź czy hash jest wystarczająco mocny
        if (BCrypt.PasswordNeedsRehash(user.HashHasla, minNewHashWorkFactor: 12))
        {
            // Przy każdym udanym logowaniu zaktualizuj hash w tle
            user.HashHasla = BCrypt.HashPassword(hasloWpisane, workFactor: 12);
            await repo.SaveAsync(user, ct);
            Console.WriteLine($"Hash zaktualizowany dla: {email}");
        }

        return GenerujToken(user);
    }

    // Demo — pokaz różnych cost factor
    public void BenchmarkCostFactor()
    {
        var haslo = "TestoweHaslo123!";

        for (int cost = 10; cost <= 14; cost++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string hash = BCrypt.HashPassword(haslo, workFactor: cost);
            sw.Stop();

            Console.WriteLine($"Cost {cost}: {sw.ElapsedMilliseconds}ms | {hash[..20]}...");
        }
        // Cost 10:  ~60ms
        // Cost 11: ~120ms
        // Cost 12: ~250ms  ← sweet spot
        // Cost 13: ~500ms
        // Cost 14: ~1000ms
    }

    private string GenerujToken(object user) => "token";
}
```

---

### 3. PBKDF2 — wbudowany w .NET

csharp

```csharp
// PBKDF2 — Password-Based Key Derivation Function 2
// Standard NIST SP 800-132, rekomendowany przez OWASP
// Wbudowany w .NET — zero zewnętrznych zależności

using System.Security.Cryptography;

public class PBKDF2Hasher
{
    // Konfiguracja — zgodna z OWASP 2024
    private const int SaltRozmiar   = 32;   // bajty (256 bitów)
    private const int HashRozmiar   = 32;   // bajty (256 bitów)
    private const int Iteracje      = 600_000;  // OWASP 2024 dla HMAC-SHA256
    private static readonly HashAlgorithmName Algorytm =
        HashAlgorithmName.SHA256;

    // Format przechowywania: $pbkdf2-sha256$iteracje$salt_base64$hash_base64
    public string HashujHaslo(string haslo)
    {
        // 1. Wygeneruj losowy salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltRozmiar);

        // 2. Wylicz hash
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(haslo),
            salt,
            Iteracje,
            Algorytm,
            HashRozmiar);

        // 3. Zakoduj do stringa — format własny
        string saltStr = Convert.ToBase64String(salt);
        string hashStr = Convert.ToBase64String(hash);

        return $"$pbkdf2-sha256${Iteracje}${saltStr}${hashStr}";
    }

    public bool WeryfikujHaslo(string haslo, string zapisanyHash)
    {
        // Parsuj format
        var czesci = zapisanyHash.Split('$',
            StringSplitOptions.RemoveEmptyEntries);

        if (czesci.Length != 4)
            throw new ArgumentException("Nieprawidłowy format hasha");

        string algorytmStr = czesci[0];  // pbkdf2-sha256
        int iteracje       = int.Parse(czesci[1]);
        byte[] salt        = Convert.FromBase64String(czesci[2]);
        byte[] zapisanyH   = Convert.FromBase64String(czesci[3]);

        // Wylicz hash z podanym hasłem i oryginalną solą
        byte[] obliczonyH = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(haslo),
            salt,
            iteracje,
            Algorytm,
            HashRozmiar);

        // Porównanie w czasie stałym — zapobiegaj timing attack!
        return CryptographicOperations.FixedTimeEquals(
            obliczonyH, zapisanyH);
    }

    // Sprawdź czy hash wymaga aktualizacji (stare iteracje)
    public bool CzyWymagaAktualizacji(string hash)
    {
        var czesci = hash.Split('$',
            StringSplitOptions.RemoveEmptyEntries);

        if (czesci.Length < 2 || !int.TryParse(czesci[1], out int iter))
            return true;

        return iter < Iteracje;
    }
}

// Wersja z ASP.NET Core Identity PasswordHasher
public class IdentityPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity
        .IPasswordHasher<UzytkownikModel> _hasher;

    public IdentityPasswordHasher()
    {
        _hasher = new Microsoft.AspNetCore.Identity
            .PasswordHasher<UzytkownikModel>(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.AspNetCore.Identity
                    .PasswordHasherOptions
                {
                    // V3 = PBKDF2 z HMACSHA512, 350_000 iteracji (.NET 8+)
                    // V2 = PBKDF2 z HMACSHA1,   10_000 iteracji (legacy)
                    CompatibilityMode = Microsoft.AspNetCore.Identity
                        .PasswordHasherCompatibilityMode.IdentityV3,
                    IterationCount = 600_000  // nadpisz domyślne
                }));
    }

    public string Hash(string haslo)
    {
        var user = new UzytkownikModel();
        return _hasher.HashPassword(user, haslo);
    }

    public bool Verify(string haslo, string hash)
    {
        var user = new UzytkownikModel();
        var wynik = _hasher.VerifyHashedPassword(user, hash, haslo);

        return wynik is
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
            or Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded;
    }

    public bool WymagaRehash(string haslo, string hash)
    {
        var user = new UzytkownikModel();
        return _hasher.VerifyHashedPassword(user, hash, haslo)
            == Microsoft.AspNetCore.Identity.PasswordVerificationResult
                .SuccessRehashNeeded;
    }
}

public class UzytkownikModel { public int Id { get; set; } }
```

---

### 4. SHA-256 z solą — kiedy używać

csharp

```csharp
// SHA-256 z solą — NIE dla haseł użytkowników!
// Używaj do: tokenów API, linków weryfikacyjnych, HMAC, checksum

// SHA-256 dla tokenów API (nie haseł!)
public class TokenHasher
{
    // Hashuj token API przed zapisem do bazy
    // Token API jest losowy i długi — nie potrzebuje drogiego BCrypt
    public string HashujTokenAPI(string token)
    {
        byte[] bajty = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bajty).ToLower();
        // "sk_live_abc123..." → "a1b2c3d4e5..."
    }

    // HMAC-SHA256 — haszowanie z kluczem (np. webhook signatures)
    public string GenerujPodpisWebhooka(string payload, string sekret)
    {
        var kluczBytes    = System.Text.Encoding.UTF8.GetBytes(sekret);
        var payloadBytes  = System.Text.Encoding.UTF8.GetBytes(payload);

        using var hmac = new System.Security.Cryptography.HMACSHA256(kluczBytes);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLower();
    }

    // Weryfikuj podpis webhooka (np. GitHub, Stripe)
    public bool WeryfikujWebhook(string payload, string podpis, string sekret)
    {
        string oczekiwany = GenerujPodpisWebhooka(payload, sekret);

        // Porównanie w czasie stałym!
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(podpis),
            System.Text.Encoding.UTF8.GetBytes(oczekiwany));
    }
}

// Salt ręcznie — demonstracja konceptu
public class SaltDemo
{
    // Dlaczego salt jest konieczny?
    public void PokazProblemBezSoli()
    {
        string haslo = "Tajne123";
        string hash  = Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(haslo)));

        // 1000 użytkowników z tym samym hasłem → identyczne hashe!
        // Atakujący łamie jeden → łamie wszystkich
        // Tęczowe tablice — precomputed hash → plaintext mappings

        Console.WriteLine($"Hash: {hash}");
        // Hash: pVwGSo/KNYFmMUiI5qkJo9CItU0... — zawsze ten sam!
    }

    public void PokazRozwiazanieZSola()
    {
        string haslo = "Tajne123";

        // Każdy użytkownik dostaje INNĄ sól
        string hash1 = HashujZSola(haslo, GenerujSol());
        string hash2 = HashujZSola(haslo, GenerujSol());

        Console.WriteLine($"Hash1: {hash1[..20]}...");
        Console.WriteLine($"Hash2: {hash2[..20]}...");
        // Zupełnie inne! Mimo identycznego hasła!
    }

    private string GenerujSol()
        => Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

    private string HashujZSola(string haslo, string sol)
    {
        string combo = sol + haslo;  // lub haslo + sol
        byte[] hash  = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combo));
        return sol + ":" + Convert.ToBase64String(hash);
    }
}
```

---

### 5. Basic Authentication w ASP.NET Core

csharp

```csharp
// Basic Auth = Base64(login:haslo) w nagłówku Authorization
// Authorization: Basic amFuQHRlc3QucGw6VGFqbmUxMjM=
// ZAWSZE przez HTTPS — Base64 to nie szyfrowanie!

// Własny middleware Basic Auth
public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BasicAuthMiddleware> _logger;

    public BasicAuthMiddleware(
        RequestDelegate next,
        ILogger<BasicAuthMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext ctx,
        IAuthSerwis authSerwis)
    {
        string? nagłowek = ctx.Request.Headers.Authorization;

        if (string.IsNullOrEmpty(nagłowek)
            || !nagłowek.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        try
        {
            // Dekoduj Base64
            string base64 = nagłowek["Basic ".Length..].Trim();
            string decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(base64));

            // Rozdziel login:haslo (tylko pierwsze ':' — hasło może zawierać ':')
            int dwukropek = decoded.IndexOf(':');
            if (dwukropek < 0)
            {
                ZwrocUnauthorized(ctx);
                return;
            }

            string email = decoded[..dwukropek];
            string haslo  = decoded[(dwukropek + 1)..];

            // Waliduj
            var user = await authSerwis.WalidujAsync(email, haslo);
            if (user is null)
            {
                _logger.LogWarning(
                    "Nieudane Basic Auth: {Email}", email);
                ZwrocUnauthorized(ctx);
                return;
            }

            // Ustaw Principal
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name,           user.Email),
                new Claim(ClaimTypes.Email,          user.Email),
            };
            claims = claims.Concat(user.Role
                .Select(r => new Claim(ClaimTypes.Role, r)))
                .ToArray();

            var identity  = new ClaimsIdentity(claims, "Basic");
            ctx.User      = new ClaimsPrincipal(identity);

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

// Handler dla ASP.NET Core Authentication
public class BasicAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthSerwis _authSerwis;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> opt,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthSerwis authSerwis)
        : base(opt, logger, encoder)
    {
        _authSerwis = authSerwis;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        string? nagłowek = Request.Headers.Authorization;
        if (!nagłowek!.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        try
        {
            string base64  = nagłowek["Basic ".Length..].Trim();
            string decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(base64));

            int idx = decoded.IndexOf(':');
            if (idx < 0)
                return AuthenticateResult.Fail("Nieprawidłowy format");

            string email = decoded[..idx];
            string haslo  = decoded[(idx + 1)..];

            var user = await _authSerwis.WalidujAsync(email, haslo);
            if (user is null)
                return AuthenticateResult.Fail("Nieprawidłowe dane logowania");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name,           user.Email),
                new(ClaimTypes.Email,          user.Email),
            };
            claims.AddRange(user.Role
                .Select(r => new Claim(ClaimTypes.Role, r)));

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

    protected override Task HandleChallengeAsync(AuthenticationProperties props)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Basic realm=\"API\", charset=\"UTF-8\"";
        return Task.CompletedTask;
    }
}

// Rejestracja Basic Auth w Program.cs
builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(
        "Basic", null);
```

---

### 6. Porównanie algorytmów

csharp

```csharp
// Benchmark i porównanie algorytmów

public class AlgorytmyPorownanie
{
    public static async Task PokazPorownanie()
    {
        string haslo = "Tajne123!@#";
        Console.WriteLine($"Hasło: {haslo}\n");

        // BCrypt
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string bcryptHash = BCrypt.Net.BCrypt.HashPassword(haslo, workFactor: 12);
        sw.Stop();
        Console.WriteLine($"BCrypt (cost=12):    {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Hash: {bcryptHash}\n");

        // PBKDF2
        sw.Restart();
        var pbkdf2 = new PBKDF2Hasher();
        string pbkdf2Hash = pbkdf2.HashujHaslo(haslo);
        sw.Stop();
        Console.WriteLine($"PBKDF2 (600k iter): {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Hash: {pbkdf2Hash}\n");

        // Identity PasswordHasher (PBKDF2 v3)
        sw.Restart();
        var idHasher = new IdentityPasswordHasher();
        string idHash = idHasher.Hash(haslo);
        sw.Stop();
        Console.WriteLine($"Identity PBKDF2:    {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Hash: {idHash[..30]}...\n");

        // SHA-256 (tylko demonstracja — NIE dla haseł!)
        sw.Restart();
        byte[] sha = SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(haslo));
        string shaHash = Convert.ToHexString(sha);
        sw.Stop();
        Console.WriteLine($"SHA-256 (bez soli): {sw.ElapsedMilliseconds}ms [UNSAFE!]");
        Console.WriteLine($"Hash: {shaHash}\n");

        // Weryfikacja
        Console.WriteLine("=== Weryfikacja ===");
        Console.WriteLine($"BCrypt OK: {BCrypt.Net.BCrypt.Verify(haslo, bcryptHash)}");
        Console.WriteLine($"PBKDF2 OK: {pbkdf2.WeryfikujHaslo(haslo, pbkdf2Hash)}");
        Console.WriteLine($"Identity OK: {idHasher.Verify(haslo, idHash)}");

        // Porównanie:
        // SHA-256:  < 1ms    — ZA SZYBKI, podatny na brute force
        // PBKDF2:   ~100ms   — OK, standard NIST
        // BCrypt:   ~250ms   — DOBRY, dedykowany do haseł
        // Argon2:   ~100ms   — NAJLEPSZY (nie w .NET stdlib)

        // ┌────────────────┬──────────┬──────────┬─────────┬────────────┐
        // │ Algorytm       │ Szybkość │ Salt     │ .NET    │ Zalecenie  │
        // ├────────────────┼──────────┼──────────┼─────────┼────────────┤
        // │ MD5/SHA-1      │ Bardzo   │ Brak     │ Tak     │ NIGDY      │
        // │                │ szybki   │ natywny  │         │            │
        // │ SHA-256 bez    │ Szybki   │ Brak     │ Tak     │ NIGDY dla  │
        // │ soli           │          │ natywny  │         │ haseł      │
        // │ SHA-256 z solą │ Szybki   │ Ręczny   │ Tak     │ Nie dla    │
        // │                │          │          │         │ haseł      │
        // │ PBKDF2         │ ~100ms   │ Auto     │ Tak     │ Dobry      │
        // │ BCrypt         │ ~250ms   │ Auto     │ Nuget   │ Zalecany   │
        // │ Argon2         │ ~100ms   │ Auto     │ Nuget   │ Najlepszy  │
        // └────────────────┴──────────┴──────────┴─────────┴────────────┘
        await Task.CompletedTask;
    }
}
```

---

### 7. Praktyczny przykład — kompletny AuthSerwis

csharp

```csharp
// Pełna implementacja z BCrypt, weryfikacją i rehashowaniem

public class UzytkownikEntity
{
    public int      Id           { get; set; }
    public string   Email        { get; set; } = "";
    public string   Login        { get; set; } = "";
    public string   HashHasla    { get; set; } = "";
    public bool     Aktywny      { get; set; } = true;
    public int      NieudaneProby{ get; set; } = 0;
    public DateTime? ZablokDo    { get; set; }
    public DateTime? OstatniLogin{ get; set; }
    public List<string> Role     { get; set; } = new();
}

public class HasloSerwis
{
    private const int WorkFactor       = 12;
    private const int MinWymaganaMin   = 8;
    private const int MaksNieudanych   = 5;
    private static readonly TimeSpan   CzasBlokady = TimeSpan.FromMinutes(15);

    // Walidacja siły hasła
    public WalidacjaHaslaWynik WalidujSileHasla(string haslo)
    {
        var bledy = new List<string>();

        if (haslo.Length < MinWymaganaMin)
            bledy.Add($"Hasło musi mieć min. {MinWymaganaMin} znaków");

        if (!haslo.Any(char.IsUpper))
            bledy.Add("Hasło musi zawierać wielką literę");

        if (!haslo.Any(char.IsLower))
            bledy.Add("Hasło musi zawierać małą literę");

        if (!haslo.Any(char.IsDigit))
            bledy.Add("Hasło musi zawierać cyfrę");

        if (!haslo.Any(c => !char.IsLetterOrDigit(c)))
            bledy.Add("Hasło musi zawierać znak specjalny (!@#$%...)");

        // Sprawdź czy hasło nie jest na liście popularnych
        var popularneHasla = new HashSet<string>
        {
            "password", "123456", "qwerty", "admin",
            "letmein", "welcome", "monkey", "dragon"
        };

        if (popularneHasla.Contains(haslo.ToLower()))
            bledy.Add("Hasło jest zbyt popularne — wybierz inne");

        return new WalidacjaHaslaWynik(
            bledy.Count == 0,
            bledy,
            ObliczSileHasla(haslo));
    }

    private static int ObliczSileHasla(string haslo)
    {
        int wynik = 0;
        if (haslo.Length >= 8)  wynik += 1;
        if (haslo.Length >= 12) wynik += 1;
        if (haslo.Length >= 16) wynik += 1;
        if (haslo.Any(char.IsUpper))                wynik += 1;
        if (haslo.Any(char.IsLower))                wynik += 1;
        if (haslo.Any(char.IsDigit))                wynik += 1;
        if (haslo.Any(c => !char.IsLetterOrDigit(c))) wynik += 1;
        return wynik;  // 0-7 → słabe-bardzo mocne
    }

    // Hashuj hasło
    public string HashujHaslo(string haslo)
        => BCrypt.Net.BCrypt.HashPassword(haslo, WorkFactor);

    // Weryfikuj hasło
    public bool WeryfikujHaslo(string haslo, string hash)
        => BCrypt.Net.BCrypt.Verify(haslo, hash);

    // Czy hash wymaga aktualizacji
    public bool CzyWymagaRehash(string hash)
        => BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, WorkFactor);
}

public record WalidacjaHaslaWynik(
    bool         Poprawne,
    List<string> Bledy,
    int          Sila);  // 0-7

// Kompletny serwis autentykacji
public class KompletnyAuthSerwis
{
    private readonly IUzytkownikRepo _repo;
    private readonly HasloSerwis     _hasloSerwis;
    private readonly ILogger<KompletnyAuthSerwis> _logger;

    public KompletnyAuthSerwis(
        IUzytkownikRepo repo,
        HasloSerwis hasloSerwis,
        ILogger<KompletnyAuthSerwis> logger)
    {
        _repo        = repo;
        _hasloSerwis = hasloSerwis;
        _logger      = logger;
    }

    // Rejestracja nowego użytkownika
    public async Task<int> ZarejestujAsync(
        string email, string haslo,
        CancellationToken ct = default)
    {
        // 1. Waliduj siłę hasła
        var walidacja = _hasloSerwis.WalidujSileHasla(haslo);
        if (!walidacja.Poprawne)
            throw new ArgumentException(
                string.Join("; ", walidacja.Bledy));

        // 2. Sprawdź unikalność emaila
        bool emailIstnieje = await _repo.EmailIstniejeAsync(email, ct);
        if (emailIstnieje)
            throw new InvalidOperationException("Email już zajęty");

        // 3. Hashuj hasło
        string hash = _hasloSerwis.HashujHaslo(haslo);

        // 4. Zapisz użytkownika
        var user = new UzytkownikEntity
        {
            Email     = email.ToLower().Trim(),
            Login     = email.Split('@')[0],
            HashHasla = hash,
            Role      = new List<string> { "User" }
        };

        int id = await _repo.DodajAsync(user, ct);

        _logger.LogInformation(
            "Zarejestrowano nowego użytkownika: {Email}", email);

        return id;
    }

    // Logowanie z ochroną przed brute force
    public async Task<WynikLogowania> ZalogujAsync(
        string email, string haslo,
        CancellationToken ct = default)
    {
        // Celowe opóźnienie — utrudnia timing attacks
        // (nawet gdy użytkownik nie istnieje czas jest podobny)
        await Task.Delay(
            Random.Shared.Next(50, 150), ct);

        var user = await _repo.FindByEmailAsync(
            email.ToLower().Trim(), ct);

        // Użytkownik nie istnieje — hashuj "fałszywe" hasło
        // żeby czas odpowiedzi był taki sam (zapobiegaj enumeracji kont)
        if (user is null)
        {
            // Celowo wykonaj hash żeby czas odpowiedzi był taki sam
            BCrypt.Net.BCrypt.Verify(haslo, BCrypt.Net.BCrypt.HashPassword(
                "dummy", workFactor: 4));  // szybszy hash dla dummy

            _logger.LogWarning(
                "Próba logowania na nieistniejące konto: {Email}", email);
            return WynikLogowania.Niepowodzenie("Nieprawidłowy email lub hasło");
        }

        // Sprawdź blokadę
        if (user.ZablokDo.HasValue && user.ZablokDo > DateTime.UtcNow)
        {
            var pozostalo = (int)(user.ZablokDo.Value - DateTime.UtcNow)
                .TotalMinutes;

            _logger.LogWarning(
                "Próba logowania na zablokowane konto: {Email}", email);
            return WynikLogowania.Zablokowane(
                $"Konto zablokowane. Spróbuj za {pozostalo} min.");
        }

        // Weryfikuj hasło
        if (!_hasloSerwis.WeryfikujHaslo(haslo, user.HashHasla))
        {
            user.NieudaneProby++;

            // Zablokuj po zbyt wielu próbach
            if (user.NieudaneProby >= 5)
            {
                user.ZablokDo = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning(
                    "Konto zablokowane po {N} próbach: {Email}",
                    user.NieudaneProby, email);
            }

            await _repo.AktualizujAsync(user, ct);
            return WynikLogowania.Niepowodzenie("Nieprawidłowy email lub hasło");
        }

        // Logowanie udane — resetuj licznik nieudanych prób
        user.NieudaneProby = 0;
        user.ZablokDo      = null;
        user.OstatniLogin  = DateTime.UtcNow;

        // Rehash jeśli hash jest przestarzały
        if (_hasloSerwis.CzyWymagaRehash(user.HashHasla))
        {
            user.HashHasla = _hasloSerwis.HashujHaslo(haslo);
            _logger.LogInformation(
                "Hash zaktualizowany dla: {Email}", email);
        }

        await _repo.AktualizujAsync(user, ct);

        _logger.LogInformation("Zalogowano: {Email}", email);
        return WynikLogowania.Sukces(user);
    }

    // Zmiana hasła
    public async Task<bool> ZmienHasloAsync(
        int userId, string obecne, string nowe,
        CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(userId, ct);
        if (user is null) return false;

        // Weryfikuj obecne hasło
        if (!_hasloSerwis.WeryfikujHaslo(obecne, user.HashHasla))
        {
            _logger.LogWarning(
                "Błędne obecne hasło przy zmianie: {Id}", userId);
            return false;
        }

        // Waliduj nowe hasło
        var walidacja = _hasloSerwis.WalidujSileHasla(nowe);
        if (!walidacja.Poprawne)
            throw new ArgumentException(
                string.Join("; ", walidacja.Bledy));

        // Sprawdź czy nowe hasło różni się od starego
        if (_hasloSerwis.WeryfikujHaslo(nowe, user.HashHasla))
            throw new ArgumentException(
                "Nowe hasło musi różnić się od obecnego");

        user.HashHasla = _hasloSerwis.HashujHaslo(nowe);
        await _repo.AktualizujAsync(user, ct);

        _logger.LogInformation(
            "Hasło zmienione dla: {Id}", userId);
        return true;
    }
}

// Wynik logowania
public class WynikLogowania
{
    public bool              Udane      { get; private set; }
    public UzytkownikEntity? Uzytkownik { get; private set; }
    public string?           Komunikat  { get; private set; }
    public bool              Zablok     { get; private set; }

    public static WynikLogowania Sukces(UzytkownikEntity u) =>
        new() { Udane = true, Uzytkownik = u };

    public static WynikLogowania Niepowodzenie(string msg) =>
        new() { Udane = false, Komunikat = msg };

    public static WynikLogowania Zablokowane(string msg) =>
        new() { Udane = false, Komunikat = msg, Zablok = true };
}

// Interfejsy repozytorium
public interface IUzytkownikRepo
{
    Task<UzytkownikEntity?> FindByEmailAsync(string email, CancellationToken ct);
    Task<UzytkownikEntity?> FindByIdAsync(int id, CancellationToken ct);
    Task<bool> EmailIstniejeAsync(string email, CancellationToken ct);
    Task<int> DodajAsync(UzytkownikEntity user, CancellationToken ct);
    Task AktualizujAsync(UzytkownikEntity user, CancellationToken ct);
    Task<UzytkownikEntity?> SaveAsync(UzytkownikEntity user, CancellationToken ct);
}

public interface IAuthSerwis
{
    Task<UzytkownikEntity?> WalidujAsync(string email, string haslo,
        CancellationToken ct = default);
}

// Stub AppDbContext i IdentityPasswordHasher
public class IdentityPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<UzytkownikModel>
        _hasher = new();
    public string Hash(string p)  => _hasher.HashPassword(new(), p);
    public bool Verify(string p, string h)
        => _hasher.VerifyHashedPassword(new(), h, p)
           != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
    public bool WymagaRehash(string p, string h)
        => _hasher.VerifyHashedPassword(new(), h, p)
           == Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded;
}
```

---

### Typowe pytania rekrutacyjne

**"Dlaczego używamy BCrypt zamiast SHA-256 do haseł?"** SHA-256 jest zaprojektowany żeby być szybki — może wykonać miliardy operacji na sekundę na GPU, co czyni brute force trywialnym. BCrypt jest celowo wolny (0.25s przy cost=12) — atakujący może sprawdzić tylko ~4 hasła na sekundę zamiast miliardów. Poza tym BCrypt automatycznie generuje i przechowuje losowy salt (każde hasło ma inny hash), jest odporny na tęczowe tablice, a cost factor można zwiększyć w przyszłości zachowując kompatybilność.

**"Co to salt i dlaczego jest niezbędny?"** Salt to losowa wartość dołączana do hasła przed hashowaniem. Bez soli — wszyscy użytkownicy z hasłem "Tajne123" mają identyczny hash. Atakujący łamie jeden hash → ma dostęp do wszystkich kont z tym hasłem. Tęczowe tablice (precomputed hash→plaintext mappings) działają natychmiast. Z solą — każde hasło ma unikalny hash mimo identycznej wartości. Tęczowe tablice bezużyteczne. Atakujący musi atakować każde konto oddzielnie.

**"Jaka różnica między Basic Auth a JWT?"** Basic Auth wysyła login:hasło przy każdym requeście (zakodowane Base64, nie zaszyfrowane) — serwer musi weryfikować w bazie przy każdym zapytaniu. JWT wysyłany raz przy logowaniu, potem klient przesyła podpisany token — serwer weryfikuje podpis kryptograficznie bez zapytania do bazy. Basic Auth prostszy w implementacji, JWT skalowalny (stateless). Basic Auth wymaga HTTPS bez wyjątków — credentials jawne w nagłówku.

**"Co to timing attack i jak bronić?"** Timing attack — atakujący mierzy czas odpowiedzi serwera. Zwykłe porównanie stringów `a == b` kończy się przy pierwszej różnicy — krótszy czas gdy więcej znaków się zgadza. Atakujący może odgadywać hasło znak po znaku. Obrona: `CryptographicOperations.FixedTimeEquals()` — zawsze porównuje wszystkie bajty bez early exit. BCrypt i PBKDF2 mają wbudowane stałoczasowe porównanie. Dodatkowa obrona: sztuczne opóźnienie przy nieudanym logowaniu.