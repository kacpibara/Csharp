using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace _07_Cryptography_Security_JWT;

// ============================================================
// 1. OPCJE JWT Z WALIDACJĄ
// ============================================================

public class JwtOpcje
{
    public const string Sekcja = "Jwt";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(32,
        ErrorMessage = "SecretKey musi mieć min. 32 znaki")]
    public string SecretKey { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Issuer { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Audience { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Range(1, 1440)]
    public int AccessTokenMin { get; set; } = 15;

    [System.ComponentModel.DataAnnotations.Range(1, 365)]
    public int RefreshTokenDni { get; set; } = 7;

    // Computed properties — nie z konfiguracji
    public byte[] KluczBytes => System.Text.Encoding.UTF8.GetBytes(SecretKey);
    public SymmetricSecurityKey KluczSymetryczny => new(KluczBytes);
}

// ============================================================
// 2. STRUKTURA JWT — DEKODOWANIE BASE64URL
// ============================================================
// JWT = Header.Payload.Signature (trzy sekcje oddzielone kropkami)
// Każda zakodowana Base64Url (nie Base64!)
// JWT jest JAWNY — każdy może odczytać payload!
// NIE wkładaj haseł, kluczy API, sekretów do JWT!

public static class JwtStrukturaDekoder
{
    // === HEADER ===  { "alg": "HS256", "typ": "JWT" }
    // === PAYLOAD === { "sub":"42", "jti":"...", "iat":..., "nbf":..., "exp":...,
    //                   "iss":"https://api.pl", "aud":"https://sklep.pl", "email":"...", "role":["Admin"] }
    // === SIGNATURE === HMACSHA256(base64url(header)+"."+base64url(payload), secretKey)
    // Podpis gwarantuje że token nie był modyfikowany!

    public static void PokażZawartośćTokenu(string jwt)
    {
        var czesci = jwt.Split('.');
        if (czesci.Length != 3)
        {
            Console.WriteLine("  Nieprawidłowy format JWT (oczekiwano 3 sekcji)");
            return;
        }

        string header  = DecodeBase64Url(czesci[0]);
        string payload = DecodeBase64Url(czesci[1]);
        Console.WriteLine($"  Header:  {header}");
        Console.WriteLine($"  Payload: {payload}");
        Console.WriteLine($"  Signature: [bajty kryptograficzne — nie dekodujemy]");
    }

    private static string DecodeBase64Url(string base64url)
    {
        // Zamień Base64URL → Base64 i dodaj padding
        string base64 = base64url.Replace('-', '+').Replace('_', '/');
        int padding = base64.Length % 4;
        if (padding == 2) base64 += "==";
        else if (padding == 3) base64 += "=";

        byte[] bajty = Convert.FromBase64String(base64);
        return System.Text.Encoding.UTF8.GetString(bajty);
    }

    // Base64URL — wariant dla URL/JWT (bez +, /, =)
    public static string DoBase64Url(byte[] bajty) =>
        Convert.ToBase64String(bajty)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    public static byte[] ZBase64Url(string s)
    {
        string base64 = s.Replace('-', '+').Replace('_', '/');
        int padding = base64.Length % 4;
        if (padding == 2) base64 += "==";
        else if (padding == 3) base64 += "=";
        return Convert.FromBase64String(base64);
    }
}

// ============================================================
// 3. MODELE
// ============================================================

public record UzytkownikInfo(
    int           Id,
    string        Login,
    string        Email,
    List<string>  Role,
    string?       TenantId    = null,
    string?       Plan        = null,
    List<string>? Uprawnienia = null)
{
    public List<string> Uprawnienia { get; init; } = Uprawnienia ?? [];
}

public record TokenPara(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessWygasa,
    DateTime RefreshWygasa);

// Encja Refresh Token z pełnymi metadanymi audytu
public class RefreshTokenEntity
{
    public int      Id              { get; set; }
    public int      UzytkownikId    { get; set; }
    public string   Token           { get; set; } = "";
    public DateTime Wygasa          { get; set; }
    public DateTime Utworzono       { get; set; } = DateTime.UtcNow;
    public bool     Odwolany        { get; set; } = false;
    public DateTime? DataOdwolania  { get; set; }
    public string?  PowodOdwolania  { get; set; }
    public string?  ZastapionoPrzez { get; set; }  // dla audytu rotacji
    public string?  AdresIP         { get; set; }
    public string?  UserAgent       { get; set; }

    // Computed: aktywny gdy nie odwołany i nie wygasł
    public bool Aktywny => !Odwolany && DateTime.UtcNow < Wygasa;
}

// ============================================================
// 4. INTERFEJS SERWISU TOKENÓW
// ============================================================

public interface ITokenSerwis
{
    TokenPara      GenerujTokenPare(UzytkownikInfo info);
    string         GenerujRefreshToken();
    ClaimsPrincipal? WalidujAccessToken(string token);
}

// ============================================================
// 5. IMPLEMENTACJA JWT TOKEN SERWIS
// ============================================================

public class JwtTokenSerwis : ITokenSerwis
{
    private readonly JwtOpcje _opcje;

    public JwtTokenSerwis(IOptions<JwtOpcje> opcje)
        => _opcje = opcje.Value;

    public TokenPara GenerujTokenPare(UzytkownikInfo info)
    {
        string accessToken   = GenerujAccessToken(info);
        string refreshToken  = GenerujRefreshToken();
        DateTime accessWygasa  = DateTime.UtcNow.AddMinutes(_opcje.AccessTokenMin);
        DateTime refreshWygasa = DateTime.UtcNow.AddDays(_opcje.RefreshTokenDni);

        return new TokenPara(accessToken, refreshToken, accessWygasa, refreshWygasa);
    }

    private string GenerujAccessToken(UzytkownikInfo info)
    {
        var claims = BudujClaims(info);
        var poswiadczenia = new SigningCredentials(
            _opcje.KluczSymetryczny,
            SecurityAlgorithms.HmacSha256);

        // JwtSecurityToken: issuer, audience, claims, notBefore, expires, signingCredentials
        var token = new JwtSecurityToken(
            issuer:             _opcje.Issuer,
            audience:           _opcje.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_opcje.AccessTokenMin),
            signingCredentials: poswiadczenia);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<Claim> BudujClaims(UzytkownikInfo info)
    {
        var claims = new List<Claim>
        {
            // Standardowe JWT claims (RFC 7519)
            new(JwtRegisteredClaimNames.Sub,   info.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString("N")),  // unikatowy ID tokenu
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Email, info.Email),

            // ASP.NET Core claims
            new(ClaimTypes.NameIdentifier, info.Id.ToString()),
            new(ClaimTypes.Name,           info.Login),
            new(ClaimTypes.Email,          info.Email),
        };

        // Role — można dodać wiele
        claims.AddRange(info.Role.Select(r => new Claim(ClaimTypes.Role, r)));

        // Własne claims biznesowe (multi-tenancy, plan subskrypcji, uprawnienia)
        if (info.TenantId is not null)
            claims.Add(new Claim("tenant_id", info.TenantId));

        if (info.Plan is not null)
            claims.Add(new Claim("sub_tier", info.Plan));

        if (info.Uprawnienia.Any())
            claims.AddRange(info.Uprawnienia.Select(u => new Claim("permission", u)));

        return claims;
    }

    // Kryptograficznie bezpieczny losowy refresh token (64 bajty → Base64)
    public string GenerujRefreshToken()
    {
        var bajty = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bajty);
        return Convert.ToBase64String(bajty);
    }

    // Walidacja BEZ sprawdzania wygaśnięcia — używana przy refresh!
    public ClaimsPrincipal? WalidujAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parametry = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = false,  // NIE sprawdzaj exp — token mógł wygasnąć
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _opcje.Issuer,
            ValidAudience            = _opcje.Audience,
            IssuerSigningKey         = _opcje.KluczSymetryczny,
            ClockSkew                = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(token, parametry, out SecurityToken validatedToken);

            // Obrona przed atakiem "alg:none" — sprawdź algorytm podpisu
            if (validatedToken is JwtSecurityToken jwt
                && jwt.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return principal;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}

// ============================================================
// 6. REFRESH TOKEN SERWIS (IN-MEMORY)
// ============================================================

public class InMemoryRefreshTokenSerwis
{
    private readonly List<RefreshTokenEntity> _store = [];
    private int _nextId = 1;

    public Task<RefreshTokenEntity> UtworzAsync(int userId, string token, DateTime wygasa,
        string? ip = null, string? userAgent = null)
    {
        var rt = new RefreshTokenEntity
        {
            Id           = _nextId++,
            UzytkownikId = userId,
            Token        = token,
            Wygasa       = wygasa,
            AdresIP      = ip,
            UserAgent    = userAgent
        };
        _store.Add(rt);
        return Task.FromResult(rt);
    }

    public Task<RefreshTokenEntity?> ZnajdzAktywnyAsync(int userId, string token)
        => Task.FromResult(
            _store.FirstOrDefault(rt =>
                rt.UzytkownikId == userId
                && rt.Token     == token
                && rt.Aktywny));

    // Token rotation — unieważnij stary, zapisz nowy (audyt przez ZastapionoPrzez)
    public Task<string> ZrotujAsync(RefreshTokenEntity stary, string nowyToken, DateTime nowaWygasa)
    {
        stary.Odwolany        = true;
        stary.DataOdwolania   = DateTime.UtcNow;
        stary.PowodOdwolania  = "Rotacja";
        stary.ZastapionoPrzez = nowyToken;  // audyt łańcucha rotacji

        _store.Add(new RefreshTokenEntity
        {
            Id           = _nextId++,
            UzytkownikId = stary.UzytkownikId,
            Token        = nowyToken,
            Wygasa       = nowaWygasa,
            AdresIP      = stary.AdresIP,
            UserAgent    = stary.UserAgent
        });

        return Task.FromResult(nowyToken);
    }

    // Wykryj ponowne użycie odwołanego tokenu — potencjalna kradzież!
    public async Task WykryjPonowneUzycieAsync(int userId, string token)
    {
        var uzyty = _store.FirstOrDefault(rt =>
            rt.UzytkownikId == userId && rt.Token == token && rt.Odwolany);

        if (uzyty is not null)
        {
            // Ktoś używa już zrotowanego tokenu — unieważnij WSZYSTKIE!
            await OdwolajWszystkieAsync(userId, "Podejrzenie kradzieży tokenu");
            Console.WriteLine($"  [RefreshToken] ALERT: wykryto ponowne użycie! Unieważniono wszystkie tokeny userId={userId}");
        }
    }

    public Task OdwolajWszystkieAsync(int userId, string powod)
    {
        var aktywne = _store.Where(rt => rt.UzytkownikId == userId && !rt.Odwolany).ToList();
        foreach (var rt in aktywne)
        {
            rt.Odwolany       = true;
            rt.DataOdwolania  = DateTime.UtcNow;
            rt.PowodOdwolania = powod;
        }
        return Task.CompletedTask;
    }
}

// ============================================================
// 7. AUTH CONTROLLER DEMO (LOGIKA BEZ HTTP)
// ============================================================

public record LoginDto(string Email, string Haslo);
public record RefreshDto(string AccessToken, string RefreshToken);
public record LogoutDto(string? RefreshToken = null);
public record OdpowiedzTokenuDto(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    string TokenType,
    string Email,
    List<string> Role);

public class AuthLogikaSerwis
{
    private readonly ITokenSerwis              _token;
    private readonly InMemoryRefreshTokenSerwis _rtSerwis;
    // Symulacja bazy użytkowników
    private readonly List<UzytkownikInfo> _users =
    [
        new(1, "jan.kowalski", "jan@test.pl", ["Admin", "User"], "sklep-abc", "premium"),
        new(2, "anna.nowak",   "anna@test.pl", ["User"], null, "free"),
    ];

    public AuthLogikaSerwis(ITokenSerwis token, InMemoryRefreshTokenSerwis rtSerwis)
    {
        _token    = token;
        _rtSerwis = rtSerwis;
    }

    public async Task<OdpowiedzTokenuDto?> LoginAsync(LoginDto dto)
    {
        var user = _users.FirstOrDefault(u => u.Email == dto.Email);
        if (user is null || dto.Haslo != "TajneHaslo123!")  // uproszczona weryfikacja
            return null;

        var tokeny = _token.GenerujTokenPare(user);
        await _rtSerwis.UtworzAsync(user.Id, tokeny.RefreshToken, tokeny.RefreshWygasa, "127.0.0.1");

        return new OdpowiedzTokenuDto(
            tokeny.AccessToken,
            tokeny.RefreshToken,
            (int)(tokeny.AccessWygasa - DateTime.UtcNow).TotalSeconds,
            "Bearer",
            user.Email,
            user.Role);
    }

    public async Task<OdpowiedzTokenuDto?> RefreshAsync(RefreshDto dto)
    {
        // 1. Waliduj access token (bez sprawdzania exp — token wygasł, o to chodzi!)
        var principal = _token.WalidujAccessToken(dto.AccessToken);
        if (principal is null) return null;

        string? userIdStr = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return null;

        // 2. Wykryj ponowne użycie
        await _rtSerwis.WykryjPonowneUzycieAsync(userId, dto.RefreshToken);

        // 3. Znajdź aktywny refresh token
        var rt = await _rtSerwis.ZnajdzAktywnyAsync(userId, dto.RefreshToken);
        if (rt is null) return null;

        // 4. Pobierz użytkownika (role mogły się zmienić!)
        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user is null) return null;

        // 5. Wygeneruj nowe tokeny
        var nowePara = _token.GenerujTokenPare(user);

        // 6. Rotuj refresh token
        await _rtSerwis.ZrotujAsync(rt, nowePara.RefreshToken, nowePara.RefreshWygasa);

        return new OdpowiedzTokenuDto(
            nowePara.AccessToken,
            nowePara.RefreshToken,
            (int)(nowePara.AccessWygasa - DateTime.UtcNow).TotalSeconds,
            "Bearer",
            user.Email,
            user.Role);
    }

    public async Task LogoutAsync(int userId, string? refreshToken = null)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var rt = await _rtSerwis.ZnajdzAktywnyAsync(userId, refreshToken);
            if (rt is not null)
                await _rtSerwis.ZrotujAsync(rt, "", DateTime.MinValue);
        }
        else
        {
            // Wyloguj ze WSZYSTKICH urządzeń
            await _rtSerwis.OdwolajWszystkieAsync(userId, "Wylogowanie użytkownika");
        }
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class JwtDemoSerwis
{
    public static void Uruchom()
    {
        // Opcje JWT (ValidateOnStart — walidacja przy starcie aplikacji)
        var opcje = new JwtOpcje
        {
            SecretKey      = "super-tajny-klucz-produkcyjny-min32znaki!",
            Issuer         = "https://api.demo.pl",
            Audience       = "https://demo.pl",
            AccessTokenMin = 15,
            RefreshTokenDni= 7
        };

        // Demonstracja struktury JWT
        Console.WriteLine("  JWT = Header.Payload.Signature (Base64Url)");
        Console.WriteLine("  Header:  {\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        Console.WriteLine("  Payload: {\"sub\":\"42\",\"jti\":\"...\",\"iat\":...,\"nbf\":...,\"exp\":...,\"iss\":\"...\",\"aud\":\"...\"}");
        Console.WriteLine("  JWT jest JAWNY — NIE wkładaj sekretów do claims!");

        var tokenSerwis = new JwtTokenSerwis(Options.Create(opcje));
        var rtSerwis    = new InMemoryRefreshTokenSerwis();

        // Generowanie tokenu
        var user = new UzytkownikInfo(42, "jan.kowalski", "jan@test.pl",
            ["Admin", "User"], "sklep-abc", "premium",
            ["orders:read", "products:write"]);

        var para = tokenSerwis.GenerujTokenPare(user);
        Console.WriteLine($"  [JWT] Access token (15min): {para.AccessToken[..40]}...");
        Console.WriteLine($"  [JWT] Refresh token (64B losowe): {para.RefreshToken[..20]}...");
        Console.WriteLine($"  [JWT] Access wygasa: {para.AccessWygasa:HH:mm:ss}");

        // Dekodowanie struktury
        Console.WriteLine("  [JWT] Dekodowanie Base64Url:");
        JwtStrukturaDekoder.PokażZawartośćTokenu(para.AccessToken);

        // Walidacja
        var principal = tokenSerwis.WalidujAccessToken(para.AccessToken);
        string? userId   = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email    = principal?.FindFirstValue(ClaimTypes.Email);
        var role         = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        string? tenantId = principal?.FindFirstValue("tenant_id");
        string? plan     = principal?.FindFirstValue("sub_tier");
        var uprawnienia  = principal?.FindAll("permission").Select(c => c.Value).ToList();
        Console.WriteLine($"  [JWT] Walidacja: userId={userId}, email={email}");
        Console.WriteLine($"  [JWT] Role: [{string.Join(", ", role ?? [])}], tenant={tenantId}, plan={plan}");
        Console.WriteLine($"  [JWT] Uprawnienia: [{string.Join(", ", uprawnienia ?? [])}]");

        // "alg:none" atak — weryfikacja algorytmu podpisu
        Console.WriteLine("  [JWT] Obrona przed 'alg:none': sprawdzamy SecurityAlgorithms.HmacSha256");

        // Refresh token flow
        var authLogika = new AuthLogikaSerwis(tokenSerwis, rtSerwis);
        var loginResult = authLogika.LoginAsync(new LoginDto("jan@test.pl", "TajneHaslo123!")).Result;
        Console.WriteLine($"  [Login] AccessToken: {loginResult!.AccessToken[..20]}..., ExpiresIn={loginResult.ExpiresIn}s");

        var refreshResult = authLogika.RefreshAsync(
            new RefreshDto(loginResult.AccessToken, loginResult.RefreshToken)).Result;
        Console.WriteLine($"  [Refresh] Nowy AccessToken: {refreshResult!.AccessToken[..20]}...");
        Console.WriteLine("  [Refresh] Stary refresh token → ODWOŁANY (rotacja)");

        // Token rotation — próba ponownego użycia
        var reuseResult = authLogika.RefreshAsync(
            new RefreshDto(loginResult.AccessToken, loginResult.RefreshToken)).Result;
        Console.WriteLine($"  [Token rotation] Ponowne użycie: {reuseResult is null} null = wykryto kradzież!");

        // Logout
        authLogika.LogoutAsync(42).Wait();
        Console.WriteLine("  [Logout] Wylogowano ze wszystkich urządzeń — wszystkie RT odwołane");

        // Konfiguracja w Program.cs
        Console.WriteLine("  [Config] AddAuthentication(JwtBearerDefaults.AuthenticationScheme)");
        Console.WriteLine("  [Config] TokenValidationParameters: ValidateIssuer/Audience/Lifetime/IssuerSigningKey");
        Console.WriteLine("  [Config] ClockSkew=30s, NameClaimType, RoleClaimType");
        Console.WriteLine("  [Config] JwtBearerEvents: OnChallenge, OnForbidden, OnAuthenticationFailed, OnTokenValidated, OnMessageReceived");
        Console.WriteLine("  [Config] OnMessageReceived: SignalR/WebSocket token z query string ?access_token=");
        Console.WriteLine("  [Config] FallbackPolicy: RequireAuthenticatedUser (wymaga [AllowAnonymous] żeby ominąć)");
    }
}
