using System.Security.Cryptography;

namespace _07_Cryptography_Security_JWT;

// ============================================================
// 1. ZŁEPRAKTYKI — NIGDY NIE RÓB TEGO
// ============================================================

public static class ZlePraktyki
{
    // ❌ Plaintext — katastrofa przy wycieku bazy!
    public static string ZapiszHasloJawne(string haslo) => haslo;

    // ❌ MD5 — kryptograficznie złamany, tęczowe tablice
    public static string MD5Hash(string haslo)
    {
        using var md5 = MD5.Create();
        byte[] bajty = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(haslo));
        return Convert.ToHexString(bajty).ToLower();
    }

    // ❌ SHA-1 — kryptograficznie złamany
    public static string SHA1Hash(string haslo)
    {
        byte[] bajty = SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(haslo));
        return Convert.ToHexString(bajty).ToLower();
    }

    // ❌ SHA-256 BEZ SOLI — tęczowe tablice, ataki słownikowe
    public static string SHA256BezSoli(string haslo)
    {
        byte[] bajty = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(haslo));
        return Convert.ToBase64String(bajty);
        // 1000 użytkowników z "Tajne123" → IDENTYCZNE hashe!
    }
}

// ============================================================
// 2. BCRYPT — DEDYKOWANY DO HASEŁ, WBUDOWANY SALT
// ============================================================

public class BCryptDemo
{
    // Haszowanie: automatyczny losowy salt, cost factor
    // Format: $2a$12$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy
    //         wersja  cost  salt(22)     hash(31)
    public string HashujHaslo(string haslo)
    {
        // WorkFactor 10=~60ms, 11=~120ms, 12=~250ms (ZALECANE), 13=~500ms, 14=~1s
        return BCrypt.Net.BCrypt.HashPassword(haslo, workFactor: 12);
    }

    public bool WeryfikujHaslo(string haslo, string hash)
    {
        // Stałoczasowe porównanie — nie podatne na timing attack
        return BCrypt.Net.BCrypt.Verify(haslo, hash);
    }

    // Sprawdź czy hash wymaga aktualizacji (zwiększono cost factor)
    public bool CzyWymagaRehash(string hash, int minCostFactor = 12)
        => BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, minCostFactor);

    // Rehash przy logowaniu — stopniowa aktualizacja starych haseł
    public string RehashujJesliPotrzeba(string hasloWpisane, string obecnyHash)
    {
        if (!CzyWymagaRehash(obecnyHash)) return obecnyHash;
        Console.WriteLine("  [BCrypt] Hash przestarzały — aktualizacja przy logowaniu");
        return HashujHaslo(hasloWpisane);
    }

    // Benchmark cost factor
    public static void BenchmarkCostFactor(string haslo = "TestoweHaslo123!")
    {
        Console.WriteLine("  BCrypt benchmark:");
        for (int cost = 10; cost <= 12; cost++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string hash = BCrypt.Net.BCrypt.HashPassword(haslo, workFactor: cost);
            sw.Stop();
            Console.WriteLine($"    Cost {cost}: {sw.ElapsedMilliseconds}ms | {hash[..20]}...");
        }
        Console.WriteLine("  (cost 13≈500ms, cost 14≈1s — produkcja: min. 12)");
    }
}

// ============================================================
// 3. PBKDF2 — WBUDOWANY W .NET, STANDARD NIST
// ============================================================

public class PBKDF2Hasher
{
    // Konfiguracja — OWASP 2024: 600 000 iteracji HMAC-SHA256
    private const int SaltRozmiar = 32;
    private const int HashRozmiar = 32;
    private const int Iteracje = 600_000;
    private static readonly HashAlgorithmName Algorytm = HashAlgorithmName.SHA256;

    // Format: $pbkdf2-sha256$iteracje$salt_b64$hash_b64
    public string HashujHaslo(string haslo)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltRozmiar);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(haslo),
            salt, Iteracje, Algorytm, HashRozmiar);

        return $"$pbkdf2-sha256${Iteracje}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool WeryfikujHaslo(string haslo, string zapisanyHash)
    {
        var czesci = zapisanyHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (czesci.Length != 4) throw new ArgumentException("Nieprawidłowy format hasha PBKDF2");

        int iteracje   = int.Parse(czesci[1]);
        byte[] salt    = Convert.FromBase64String(czesci[2]);
        byte[] zapisany = Convert.FromBase64String(czesci[3]);

        byte[] obliczony = Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(haslo),
            salt, iteracje, Algorytm, HashRozmiar);

        // Porównanie w czasie stałym — zapobiegaj timing attack!
        return CryptographicOperations.FixedTimeEquals(obliczony, zapisany);
    }

    // Sprawdź czy hash ma wystarczającą liczbę iteracji
    public bool CzyWymagaAktualizacji(string hash)
    {
        var czesci = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (czesci.Length < 2 || !int.TryParse(czesci[1], out int iter))
            return true;
        return iter < Iteracje;
    }
}

// ============================================================
// 4. IDENTITY PASSWORD HASHER — ASP.NET CORE
// ============================================================

public class IdentityPasswordHasherDemo
{
    private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<ModelUzytkownika> _hasher;

    public IdentityPasswordHasherDemo()
    {
        // V3 = PBKDF2 z HMACSHA512, domyślnie 350_000 iteracji
        _hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<ModelUzytkownika>(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.AspNetCore.Identity.PasswordHasherOptions
                {
                    // CompatibilityMode: V3 = HMACSHA512, V2 = HMACSHA1 (legacy)
                    CompatibilityMode = Microsoft.AspNetCore.Identity.PasswordHasherCompatibilityMode.IdentityV3,
                    IterationCount = 600_000
                }));
    }

    public string Hash(string haslo) => _hasher.HashPassword(new(), haslo);

    public bool Verify(string haslo, string hash)
    {
        var wynik = _hasher.VerifyHashedPassword(new(), hash, haslo);
        return wynik is Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                     or Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded;
    }

    // SuccessRehashNeeded — hash jest poprawny, ale używa starszego algorytmu
    public bool WymagaRehash(string haslo, string hash)
        => _hasher.VerifyHashedPassword(new(), hash, haslo)
           == Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded;
}

public class ModelUzytkownika { public int Id { get; set; } }

// ============================================================
// 5. SHA-256 DLA TOKENÓW API + HMAC-SHA256 (WEBHOOKI)
// ============================================================

public static class TokenHasher
{
    // SHA-256 dla tokenów API — token jest losowy i długi, nie potrzebuje BCrypt
    public static string HashujTokenAPI(string token)
    {
        byte[] bajty = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bajty).ToLower();
    }

    // HMAC-SHA256 — podpis webhooka (GitHub, Stripe, itp.)
    public static string GenerujPodpisWebhooka(string payload, string sekret)
    {
        var kluczBytes   = System.Text.Encoding.UTF8.GetBytes(sekret);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        using var hmac   = new HMACSHA256(kluczBytes);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLower();
    }

    // Weryfikacja podpisu webhooka — FixedTimeEquals!
    public static bool WeryfikujWebhook(string payload, string podpis, string sekret)
    {
        string oczekiwany = GenerujPodpisWebhooka(payload, sekret);
        // Stałoczasowe porównanie — nie podatne na timing attack
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(podpis),
            System.Text.Encoding.UTF8.GetBytes(oczekiwany));
    }
}

// ============================================================
// 6. SALT DEMO — DLACZEGO SALT JEST KONIECZNY
// ============================================================

public static class SaltDemo
{
    // Problem bez soli: identyczne hasła → identyczne hashe
    public static void PokazProblemBezSoli()
    {
        string haslo = "Tajne123";
        string hash1 = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(haslo)));
        string hash2 = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(haslo)));
        Console.WriteLine($"  Hash1 (bez soli): {hash1[..16]}...");
        Console.WriteLine($"  Hash2 (bez soli): {hash2[..16]}... ← identyczne! Tęczowe tablice działają!");
    }

    // Rozwiązanie z solą: każdy hash unikalny
    public static void PokazRozwiazanieZSola()
    {
        string haslo = "Tajne123";
        string hash1 = HashujZSola(haslo, GenerujSol());
        string hash2 = HashujZSola(haslo, GenerujSol());
        Console.WriteLine($"  Hash1 (z solą): {hash1[..16]}...");
        Console.WriteLine($"  Hash2 (z solą): {hash2[..16]}... ← różne! Tęczowe tablice bezużyteczne!");
    }

    private static string GenerujSol() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashujZSola(string haslo, string sol)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sol + haslo));
        return sol + ":" + Convert.ToBase64String(hash);
    }
}

// ============================================================
// 7. SERWIS WALIDACJI SIŁY HASŁA
// ============================================================

public class HasloSerwis
{
    private const int WorkFactor = 12;
    private static readonly HashSet<string> PopularneHasla =
        ["password", "123456", "qwerty", "admin", "letmein", "welcome", "monkey", "dragon"];

    public WalidacjaHaslaWynik WalidujSile(string haslo)
    {
        var bledy = new List<string>();

        if (haslo.Length < 8)
            bledy.Add("Hasło musi mieć min. 8 znaków");
        if (!haslo.Any(char.IsUpper))
            bledy.Add("Hasło musi zawierać wielką literę");
        if (!haslo.Any(char.IsLower))
            bledy.Add("Hasło musi zawierać małą literę");
        if (!haslo.Any(char.IsDigit))
            bledy.Add("Hasło musi zawierać cyfrę");
        if (!haslo.Any(c => !char.IsLetterOrDigit(c)))
            bledy.Add("Hasło musi zawierać znak specjalny (!@#$%...)");
        if (PopularneHasla.Contains(haslo.ToLower()))
            bledy.Add("Hasło jest zbyt popularne");

        return new WalidacjaHaslaWynik(bledy.Count == 0, bledy, ObliczSile(haslo));
    }

    private static int ObliczSile(string haslo)
    {
        int wynik = 0;
        if (haslo.Length >= 8)  wynik++;
        if (haslo.Length >= 12) wynik++;
        if (haslo.Length >= 16) wynik++;
        if (haslo.Any(char.IsUpper))                wynik++;
        if (haslo.Any(char.IsLower))                wynik++;
        if (haslo.Any(char.IsDigit))                wynik++;
        if (haslo.Any(c => !char.IsLetterOrDigit(c))) wynik++;
        return wynik; // 0-7: słabe → bardzo mocne
    }

    public string HashujHaslo(string haslo)
        => BCrypt.Net.BCrypt.HashPassword(haslo, WorkFactor);

    public bool WeryfikujHaslo(string haslo, string hash)
        => BCrypt.Net.BCrypt.Verify(haslo, hash);

    public bool CzyWymagaRehash(string hash)
        => BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, WorkFactor);
}

public record WalidacjaHaslaWynik(bool Poprawne, List<string> Bledy, int Sila);

// ============================================================
// 8. KOMPLETNY AUTH SERWIS Z OCHRONĄ PRZED BRUTE FORCE
// ============================================================

public class UzytkownikEntity
{
    public int      Id             { get; set; }
    public string   Email          { get; set; } = "";
    public string   Login          { get; set; } = "";
    public string   HashHasla      { get; set; } = "";
    public bool     Aktywny        { get; set; } = true;
    public int      NieudaneProby  { get; set; } = 0;
    public DateTime? ZablokDo      { get; set; }
    public DateTime? OstatniLogin  { get; set; }
    public List<string> Role       { get; set; } = [];
}

public interface IUzytkownikRepo
{
    Task<UzytkownikEntity?> FindByEmailAsync(string email, CancellationToken ct);
    Task<UzytkownikEntity?> FindByIdAsync(int id, CancellationToken ct);
    Task<bool> EmailIstniejeAsync(string email, CancellationToken ct);
    Task<int> DodajAsync(UzytkownikEntity user, CancellationToken ct);
    Task AktualizujAsync(UzytkownikEntity user, CancellationToken ct);
    Task<UzytkownikEntity?> SaveAsync(UzytkownikEntity user, CancellationToken ct);
}

public class InMemoryUzytkownikRepo : IUzytkownikRepo
{
    private readonly List<UzytkownikEntity> _db = [];
    private int _nextId = 1;

    public Task<UzytkownikEntity?> FindByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(_db.FirstOrDefault(u => u.Email == email));

    public Task<UzytkownikEntity?> FindByIdAsync(int id, CancellationToken ct)
        => Task.FromResult(_db.FirstOrDefault(u => u.Id == id));

    public Task<bool> EmailIstniejeAsync(string email, CancellationToken ct)
        => Task.FromResult(_db.Any(u => u.Email == email));

    public Task<int> DodajAsync(UzytkownikEntity user, CancellationToken ct)
    {
        user.Id = _nextId++;
        _db.Add(user);
        return Task.FromResult(user.Id);
    }

    public Task AktualizujAsync(UzytkownikEntity user, CancellationToken ct)
        => Task.CompletedTask;

    public Task<UzytkownikEntity?> SaveAsync(UzytkownikEntity user, CancellationToken ct)
        => Task.FromResult<UzytkownikEntity?>(user);
}

public class WynikLogowania
{
    public bool              Udane      { get; private set; }
    public UzytkownikEntity? Uzytkownik { get; private set; }
    public string?           Komunikat  { get; private set; }
    public bool              Zablokowane { get; private set; }

    public static WynikLogowania Sukces(UzytkownikEntity u) =>
        new() { Udane = true, Uzytkownik = u };
    public static WynikLogowania Niepowodzenie(string msg) =>
        new() { Udane = false, Komunikat = msg };
    public static WynikLogowania Zablok(string msg) =>
        new() { Udane = false, Komunikat = msg, Zablokowane = true };
}

public class KompletnyAuthSerwis
{
    private readonly IUzytkownikRepo _repo;
    private readonly HasloSerwis     _hasloSerwis;

    public KompletnyAuthSerwis(IUzytkownikRepo repo, HasloSerwis hasloSerwis)
    {
        _repo = repo;
        _hasloSerwis = hasloSerwis;
    }

    // Rejestracja z walidacją siły hasła
    public async Task<int> ZarejestujAsync(string email, string haslo, CancellationToken ct = default)
    {
        var walidacja = _hasloSerwis.WalidujSile(haslo);
        if (!walidacja.Poprawne)
            throw new ArgumentException(string.Join("; ", walidacja.Bledy));

        if (await _repo.EmailIstniejeAsync(email, ct))
            throw new InvalidOperationException("Email już zajęty");

        var user = new UzytkownikEntity
        {
            Email     = email.ToLower().Trim(),
            Login     = email.Split('@')[0],
            HashHasla = _hasloSerwis.HashujHaslo(haslo),
            Role      = ["User"]
        };

        return await _repo.DodajAsync(user, ct);
    }

    // Logowanie z ochroną przed brute force
    public async Task<WynikLogowania> ZalogujAsync(string email, string haslo, CancellationToken ct = default)
    {
        // Celowe opóźnienie — zapobiegaj timing attacks przy enumeracji kont
        await Task.Delay(Random.Shared.Next(50, 150), ct);

        var user = await _repo.FindByEmailAsync(email.ToLower().Trim(), ct);

        if (user is null)
        {
            // Wykonaj dummy hash — czas odpowiedzi taki sam jak przy błędnym haśle
            BCrypt.Net.BCrypt.Verify(haslo, BCrypt.Net.BCrypt.HashPassword("dummy", workFactor: 4));
            return WynikLogowania.Niepowodzenie("Nieprawidłowy email lub hasło");
        }

        // Sprawdź blokadę konta
        if (user.ZablokDo.HasValue && user.ZablokDo > DateTime.UtcNow)
        {
            var pozostalo = (int)(user.ZablokDo.Value - DateTime.UtcNow).TotalMinutes;
            return WynikLogowania.Zablok($"Konto zablokowane. Spróbuj za {pozostalo} min.");
        }

        // Weryfikuj hasło
        if (!_hasloSerwis.WeryfikujHaslo(haslo, user.HashHasla))
        {
            user.NieudaneProby++;
            // Zablokuj po 5 nieudanych próbach na 15 minut
            if (user.NieudaneProby >= 5)
                user.ZablokDo = DateTime.UtcNow.AddMinutes(15);
            await _repo.AktualizujAsync(user, ct);
            return WynikLogowania.Niepowodzenie("Nieprawidłowy email lub hasło");
        }

        // Logowanie udane
        user.NieudaneProby = 0;
        user.ZablokDo      = null;
        user.OstatniLogin  = DateTime.UtcNow;

        // Rehash jeśli przestarzały
        if (_hasloSerwis.CzyWymagaRehash(user.HashHasla))
            user.HashHasla = _hasloSerwis.HashujHaslo(haslo);

        await _repo.AktualizujAsync(user, ct);
        return WynikLogowania.Sukces(user);
    }

    // Zmiana hasła z weryfikacją starego + walidacją nowego
    public async Task<bool> ZmienHasloAsync(int userId, string obecne, string nowe, CancellationToken ct = default)
    {
        var user = await _repo.FindByIdAsync(userId, ct);
        if (user is null) return false;

        if (!_hasloSerwis.WeryfikujHaslo(obecne, user.HashHasla))
            return false;

        var walidacja = _hasloSerwis.WalidujSile(nowe);
        if (!walidacja.Poprawne)
            throw new ArgumentException(string.Join("; ", walidacja.Bledy));

        // Nowe hasło musi różnić się od starego
        if (_hasloSerwis.WeryfikujHaslo(nowe, user.HashHasla))
            throw new ArgumentException("Nowe hasło musi różnić się od obecnego");

        user.HashHasla = _hasloSerwis.HashujHaslo(nowe);
        await _repo.AktualizujAsync(user, ct);
        return true;
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class HaszowanieDemo
{
    public static async Task Uruchom()
    {
        // --- Złe praktyki ---
        Console.WriteLine("  [ZlePraktyki] MD5 'Tajne123': " + ZlePraktyki.MD5Hash("Tajne123")[..16] + "...");
        Console.WriteLine("  [ZlePraktyki] SHA256 bez soli: zawsze ten sam hash!");
        Console.WriteLine("  Dobre algo: BCrypt, PBKDF2, Argon2 — celowo wolne, auto-salt");

        // --- BCrypt ---
        var bcryptDemo = new BCryptDemo();
        string bcHash = bcryptDemo.HashujHaslo("MojeHaslo123!");
        bool bcOk     = bcryptDemo.WeryfikujHaslo("MojeHaslo123!", bcHash);
        bool bcZly    = bcryptDemo.WeryfikujHaslo("ZleHaslo", bcHash);
        Console.WriteLine($"  [BCrypt] Hash: {bcHash[..20]}...");
        Console.WriteLine($"  [BCrypt] Weryfikacja OK={bcOk}, złe={bcZly}");
        Console.WriteLine($"  [BCrypt] CzyWymagaRehash(cost=10 hash): {bcryptDemo.CzyWymagaRehash(BCrypt.Net.BCrypt.HashPassword("x", 10))}");
        BCryptDemo.BenchmarkCostFactor();

        // --- Salt demo ---
        Console.WriteLine("  [Salt] Problem bez soli:");
        SaltDemo.PokazProblemBezSoli();
        Console.WriteLine("  [Salt] Rozwiązanie z solą:");
        SaltDemo.PokazRozwiazanieZSola();

        // --- PBKDF2 ---
        var pbkdf2 = new PBKDF2Hasher();
        string pbHash = pbkdf2.HashujHaslo("MojeHaslo123!");
        bool pbOk = pbkdf2.WeryfikujHaslo("MojeHaslo123!", pbHash);
        Console.WriteLine($"  [PBKDF2] Hash: {pbHash[..30]}...");
        Console.WriteLine($"  [PBKDF2] Weryfikacja: {pbOk}, FixedTimeEquals: chroni przed timing attack");
        Console.WriteLine($"  [PBKDF2] WymagaAktualizacji: {pbkdf2.CzyWymagaAktualizacji(pbHash)}");

        // --- Identity PasswordHasher ---
        var idHasher = new IdentityPasswordHasherDemo();
        string idHash = idHasher.Hash("MojeHaslo123!");
        bool idOk = idHasher.Verify("MojeHaslo123!", idHash);
        Console.WriteLine($"  [Identity] Hash(PBKDF2 V3, 600k iter): {idHash[..25]}...");
        Console.WriteLine($"  [Identity] Weryfikacja: {idOk}, WymagaRehash: {idHasher.WymagaRehash("MojeHaslo123!", idHash)}");

        // --- HMAC-SHA256 webhook ---
        string podpis = TokenHasher.GenerujPodpisWebhooka("{\"event\":\"payment\"}", "secret_key");
        bool webhookOk = TokenHasher.WeryfikujWebhook("{\"event\":\"payment\"}", podpis, "secret_key");
        bool webhookZly = TokenHasher.WeryfikujWebhook("{\"event\":\"payment\"}", podpis, "zly_klucz");
        Console.WriteLine($"  [HMAC-SHA256] Podpis: {podpis}");
        Console.WriteLine($"  [HMAC-SHA256] Weryfikacja OK={webhookOk}, zły klucz={webhookZly}");

        // --- SHA-256 dla tokenu API ---
        string apiToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string apiHash  = TokenHasher.HashujTokenAPI(apiToken);
        Console.WriteLine($"  [SHA-256 API token] Token: {apiToken[..12]}... Hash: {apiHash[..16]}...");

        // --- HasloSerwis ---
        var serwis = new HasloSerwis();
        var strong = serwis.WalidujSile("Mocne@Haslo999");
        var weak   = serwis.WalidujSile("abc");
        Console.WriteLine($"  [HasloSerwis] 'Mocne@Haslo999': poprawne={strong.Poprawne}, siła={strong.Sila}/7");
        Console.WriteLine($"  [HasloSerwis] 'abc': poprawne={weak.Poprawne}, błędy: {weak.Bledy.Count}");

        // --- Kompletny AuthSerwis z brute force ---
        var repo     = new InMemoryUzytkownikRepo();
        var authSerwis = new KompletnyAuthSerwis(repo, serwis);
        int id = await authSerwis.ZarejestujAsync("jan@test.pl", "Zarejestr@cja1", CancellationToken.None);
        Console.WriteLine($"  [AuthSerwis] Zarejestrowano ID={id}");

        var wynik = await authSerwis.ZalogujAsync("jan@test.pl", "Zarejestr@cja1", CancellationToken.None);
        Console.WriteLine($"  [AuthSerwis] Logowanie: udane={wynik.Udane}, login={wynik.Uzytkownik?.Login}");

        var wynikZly = await authSerwis.ZalogujAsync("jan@test.pl", "zlehaslo", CancellationToken.None);
        Console.WriteLine($"  [AuthSerwis] Złe hasło: {wynikZly.Komunikat}");

        // --- Porównanie algorytmów ---
        Console.WriteLine("  [Algorytmy] SHA-256: <1ms — ZA SZYBKI (miliardy iteracji/s GPU)");
        Console.WriteLine("  [Algorytmy] PBKDF2:  ~100ms — OK, standard NIST");
        Console.WriteLine("  [Algorytmy] BCrypt:  ~250ms — DOBRY, dedykowany hasłom");
        Console.WriteLine("  [Algorytmy] Argon2:  ~100ms — NAJLEPSZY (PHC 2015, nie w stdlib)");
    }
}
