### JWT w ASP.NET Core — kompletny przewodnik

---

### 1. Struktura tokenu JWT

csharp

```csharp
// JWT = trzy sekcje oddzielone kropkami: Header.Payload.Signature
// Każda sekcja zakodowana Base64Url (nie Base64!)
//
// eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9          ← Header
// .eyJzdWIiOiI0MiIsImVtYWlsIjoiamFuQHRlc3QucGwi  ← Payload
// .SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c   ← Signature

// === HEADER ===
// {
//   "alg": "HS256",   ← algorytm podpisu
//   "typ": "JWT"      ← typ tokenu
// }

// === PAYLOAD (Claims) ===
// {
//   "sub":   "42",              ← Subject (ID użytkownika)
//   "jti":   "abc-123",         ← JWT ID (unikalny ID tokenu)
//   "iat":   1710000000,        ← Issued At (kiedy wystawiony)
//   "nbf":   1710000000,        ← Not Before (od kiedy ważny)
//   "exp":   1710003600,        ← Expiration (kiedy wygasa)
//   "iss":   "https://api.pl",  ← Issuer (kto wystawił)
//   "aud":   "https://sklep.pl",← Audience (dla kogo)
//   "email": "jan@test.pl",     ← claim użytkownika
//   "role":  ["Admin", "User"]  ← role
// }

// === SIGNATURE ===
// HMACSHA256(
//   base64url(header) + "." + base64url(payload),
//   secretKey
// )
// Podpis gwarantuje że token nie był modyfikowany!
// JWT jest JAWNY — każdy może odczytać payload (Base64 to nie szyfrowanie!)
// NIE wkładaj haseł, kluczy API ani innych sekretów do JWT!

// Demonstracja dekodowania (bez weryfikacji — tylko edukacyjna)
public static void PokażZawartośćTokenu(string jwt)
{
    var czesci = jwt.Split('.');
    if (czesci.Length != 3) return;

    // Dodaj padding jeśli brakuje
    static string DodajPadding(string s)
    {
        return (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
    }

    string header  = System.Text.Encoding.UTF8.GetString(
        Convert.FromBase64String(
            DodajPadding(czesci[0]
                .Replace('-', '+')
                .Replace('_', '/'))));

    string payload = System.Text.Encoding.UTF8.GetString(
        Convert.FromBase64String(
            DodajPadding(czesci[1]
                .Replace('-', '+')
                .Replace('_', '/'))));

    Console.WriteLine($"Header:  {header}");
    Console.WriteLine($"Payload: {payload}");
    // Podpisu nie dekodujemy — to bajty kryptograficzne
}
```

---

### 2. Konfiguracja — appsettings i opcje

csharp

```csharp
// appsettings.json
// {
//   "Jwt": {
//     "SecretKey":       "minimum-32-znaki-super-tajny-klucz-produkcyjny!",
//     "Issuer":          "https://api.sklep.pl",
//     "Audience":        "https://sklep.pl",
//     "AccessTokenMin":  15,
//     "RefreshTokenDni": 7
//   }
// }

// Klasa opcji z walidacją
public class JwtOpcje
{
    public const string Sekcja = "Jwt";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(32,
        ErrorMessage = "SecretKey musi mieć min. 32 znaki")]
    public string SecretKey { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Issuer    { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Audience  { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Range(1, 1440)]
    public int AccessTokenMin  { get; set; } = 15;

    [System.ComponentModel.DataAnnotations.Range(1, 365)]
    public int RefreshTokenDni { get; set; } = 7;

    // Wyliczane — nie z konfiguracji
    public byte[] KluczBytes =>
        System.Text.Encoding.UTF8.GetBytes(SecretKey);

    public SymmetricSecurityKey KluczSymetryczny =>
        new(KluczBytes);
}
```

---

### 3. Program.cs — pełna konfiguracja JWT

csharp

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// === Opcje JWT z walidacją przy starcie ===
builder.Services
    .AddOptions<JwtOpcje>()
    .BindConfiguration(JwtOpcje.Sekcja)
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtOpcje = builder.Configuration
    .GetSection(JwtOpcje.Sekcja)
    .Get<JwtOpcje>()!;

// === Autentykacja JWT ===
builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultForbidScheme       = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            // Co walidować
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,

            // Wartości
            ValidIssuer              = jwtOpcje.Issuer,
            ValidAudience            = jwtOpcje.Audience,
            IssuerSigningKey         = jwtOpcje.KluczSymetryczny,

            // Tolerancja zegara między serwerami (max 5 min)
            ClockSkew                = TimeSpan.FromSeconds(30),

            // Mapowanie claims — WAŻNE dla User.FindFirstValue!
            // .NET domyślnie mapuje "sub" → ClaimTypes.NameIdentifier
            // NameClaimType i RoleClaimType mówią gdzie szukać nazwy/roli
            NameClaimType            = ClaimTypes.Name,
            RoleClaimType            = ClaimTypes.Role
        };

        opt.Events = new JwtBearerEvents
        {
            // Token niepoprawny lub brak
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();  // zapobiegaj domyślnej odpowiedzi
                ctx.Response.StatusCode  = 401;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsJsonAsync(new
                {
                    blad    = "Brak autoryzacji — wymagany token JWT",
                    kod     = "UNAUTHORIZED",
                    sciezka = ctx.Request.Path.ToString()
                });
            },

            // Brak uprawnień (token OK, ale nie ma dostępu)
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode  = 403;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsJsonAsync(new
                {
                    blad = "Brak uprawnień do tego zasobu",
                    kod  = "FORBIDDEN"
                });
            },

            // Token wygasł — dodaj nagłówek dla klienta
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers["X-Token-Expired"] = "true";

                return Task.CompletedTask;
            },

            // Token zwalidowany — możesz dodać dodatkową logikę
            OnTokenValidated = async ctx =>
            {
                // Opcjonalnie: sprawdź czy token nie jest na blackliście
                var tokenId = ctx.Principal!
                    .FindFirstValue(JwtRegisteredClaimNames.Jti);

                // var blacklist = ctx.HttpContext.RequestServices
                //     .GetRequiredService<ITokenBlacklist>();
                // if (await blacklist.CzyUnieważnionyAsync(tokenId!))
                //     ctx.Fail("Token unieważniony");

                await Task.CompletedTask;
            },

            // SignalR / WebSocket — token z query string
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"].ToString();
                var path  = ctx.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(token)
                    && path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// === Autoryzacja ===
builder.Services.AddAuthorization(opt =>
{
    // Wymagaj autentykacji domyślnie — [AllowAnonymous] wyłącza
    opt.FallbackPolicy = new Microsoft.AspNetCore.Authorization
        .AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    opt.AddPolicy("Admin",         p => p.RequireRole("Admin"));
    opt.AddPolicy("AdminManager",  p => p.RequireRole("Admin", "Manager"));
    opt.AddPolicy("Premium",       p => p.RequireClaim("sub_tier", "premium"));
});

// === Serwisy ===
builder.Services.AddScoped<ITokenSerwis, JwtTokenSerwis>();
builder.Services.AddScoped<IAuthSerwis,  AuthSerwis>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

---

### 4. Serwis generowania tokenów

csharp

```csharp
public interface ITokenSerwis
{
    TokenPara GenerujTokenPare(UzytkownikInfo info);
    ClaimsPrincipal? WalidujAccessToken(string token);
    string GenerujRefreshToken();
}

public class JwtTokenSerwis : ITokenSerwis
{
    private readonly JwtOpcje _opcje;

    public JwtTokenSerwis(IOptions<JwtOpcje> opcje)
        => _opcje = opcje.Value;

    public TokenPara GenerujTokenPare(UzytkownikInfo info)
    {
        string accessToken  = GenerujAccessToken(info);
        string refreshToken = GenerujRefreshToken();
        DateTime accessWygasa = DateTime.UtcNow
            .AddMinutes(_opcje.AccessTokenMin);
        DateTime refreshWygasa = DateTime.UtcNow
            .AddDays(_opcje.RefreshTokenDni);

        return new TokenPara(
            accessToken,
            refreshToken,
            accessWygasa,
            refreshWygasa);
    }

    private string GenerujAccessToken(UzytkownikInfo info)
    {
        var claims = BudujClaims(info);
        var poswiadczenia = new SigningCredentials(
            _opcje.KluczSymetryczny,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _opcje.Issuer,
            audience:           _opcje.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow
                                    .AddMinutes(_opcje.AccessTokenMin),
            signingCredentials: poswiadczenia);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<Claim> BudujClaims(UzytkownikInfo info)
    {
        var claims = new List<Claim>
        {
            // Standardowe JWT claims
            new(JwtRegisteredClaimNames.Sub,   info.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Email, info.Email),

            // ASP.NET Core claims
            new(ClaimTypes.NameIdentifier, info.Id.ToString()),
            new(ClaimTypes.Name,           info.Login),
            new(ClaimTypes.Email,          info.Email),
        };

        // Role — wiele
        claims.AddRange(info.Role.Select(r =>
            new Claim(ClaimTypes.Role, r)));

        // Własne claims biznesowe
        if (info.TenantId is not null)
            claims.Add(new Claim("tenant_id", info.TenantId));

        if (info.Plan is not null)
            claims.Add(new Claim("sub_tier", info.Plan));

        if (info.Uprawnienia.Any())
            claims.AddRange(info.Uprawnienia.Select(u =>
                new Claim("permission", u)));

        return claims;
    }

    public string GenerujRefreshToken()
    {
        // Kryptograficznie bezpieczny losowy token
        var bajty = new byte[64];
        using var rng =
            System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bajty);
        return Convert.ToBase64String(bajty);
    }

    public ClaimsPrincipal? WalidujAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        // Parametry BEZ sprawdzania wygaśnięcia — używamy do refresh!
        var parametry = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = false,  // NIE sprawdzaj exp!
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _opcje.Issuer,
            ValidAudience            = _opcje.Audience,
            IssuerSigningKey         = _opcje.KluczSymetryczny,
            ClockSkew                = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(
                token, parametry, out SecurityToken validatedToken);

            // Sprawdź algorytm — zapobiegaj atakowi "alg:none"
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

// Modele
public record UzytkownikInfo(
    int           Id,
    string        Login,
    string        Email,
    List<string>  Role,
    string?       TenantId     = null,
    string?       Plan         = null,
    List<string>? Uprawnienia  = null)
{
    public List<string> Uprawnienia { get; init; }
        = Uprawnienia ?? new();
}

public record TokenPara(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessWygasa,
    DateTime RefreshWygasa);
```

---

### 5. Refresh Token — pełna implementacja

csharp

```csharp
// Encja w bazie
public class RefreshToken
{
    public int      Id             { get; set; }
    public int      UzytkownikId   { get; set; }
    public string   Token          { get; set; } = "";
    public DateTime Wygasa         { get; set; }
    public DateTime Utworzono      { get; set; } = DateTime.UtcNow;
    public bool     Odwolany       { get; set; } = false;
    public DateTime? DataOdwolania { get; set; }
    public string?  PowodOdwolania { get; set; }
    public string?  ZastapionoPrzez{ get; set; }  // dla audytu rotacji
    public string?  AdresIP        { get; set; }
    public string?  UserAgent      { get; set; }

    public bool Aktywny =>
        !Odwolany && DateTime.UtcNow < Wygasa;
}

// Serwis refresh tokenów
public class RefreshTokenSerwis
{
    private readonly AppDbContext _ctx;
    private readonly IHttpContextAccessor _http;

    public RefreshTokenSerwis(
        AppDbContext ctx,
        IHttpContextAccessor http)
    {
        _ctx  = ctx;
        _http = http;
    }

    public async Task<RefreshToken> UtworzAsync(
        int userId, string token, DateTime wygasa,
        CancellationToken ct = default)
    {
        var httpCtx = _http.HttpContext;

        var rt = new RefreshToken
        {
            UzytkownikId = userId,
            Token        = token,
            Wygasa       = wygasa,
            AdresIP      = httpCtx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent    = httpCtx?.Request.Headers.UserAgent.ToString()
        };

        _ctx.RefreshTokeny.Add(rt);
        await _ctx.SaveChangesAsync(ct);
        return rt;
    }

    public async Task<RefreshToken?> ZnajdzAktywnyAsync(
        int userId, string token, CancellationToken ct = default)
    {
        return await _ctx.RefreshTokeny
            .FirstOrDefaultAsync(rt =>
                rt.UzytkownikId == userId
                && rt.Token     == token
                && !rt.Odwolany
                && rt.Wygasa    > DateTime.UtcNow, ct);
    }

    // Token rotation — unieważnij stary, zapisz nowy
    public async Task<string> ZrotujAsync(
        RefreshToken stary, string nowyToken, DateTime nowaWygasa,
        CancellationToken ct = default)
    {
        // Unieważnij stary
        stary.Odwolany        = true;
        stary.DataOdwolania   = DateTime.UtcNow;
        stary.PowodOdwolania  = "Rotacja";
        stary.ZastapionoPrzez = nowyToken;

        // Utwórz nowy z tymi samymi metadanymi
        var nowy = new RefreshToken
        {
            UzytkownikId = stary.UzytkownikId,
            Token        = nowyToken,
            Wygasa       = nowaWygasa,
            AdresIP      = stary.AdresIP,
            UserAgent    = stary.UserAgent
        };

        _ctx.RefreshTokeny.Add(nowy);
        await _ctx.SaveChangesAsync(ct);
        return nowyToken;
    }

    // Wykryj ponowne użycie — potencjalna kradzież!
    public async Task WykryjPonowneUzycieAsync(
        int userId, string token, CancellationToken ct = default)
    {
        var uzytyToken = await _ctx.RefreshTokeny
            .FirstOrDefaultAsync(rt =>
                rt.UzytkownikId == userId
                && rt.Token     == token
                && rt.Odwolany, ct);

        if (uzytyToken is not null)
        {
            // Ktoś próbuje użyć już zrotowanego tokenu!
            // To może oznaczać kradzież — unieważnij WSZYSTKIE tokeny!
            await OdwolajWszystkieAsync(
                userId, "Podejrzenie kradzieży tokenu", ct);
        }
    }

    public async Task OdwolajWszystkieAsync(
        int userId, string powod, CancellationToken ct = default)
    {
        var aktywne = await _ctx.RefreshTokeny
            .Where(rt => rt.UzytkownikId == userId && !rt.Odwolany)
            .ToListAsync(ct);

        var teraz = DateTime.UtcNow;
        foreach (var rt in aktywne)
        {
            rt.Odwolany      = true;
            rt.DataOdwolania = teraz;
            rt.PowodOdwolania = powod;
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
```

---

### 6. Kontroler Auth — kompletny

csharp

```csharp
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthSerwis        _auth;
    private readonly ITokenSerwis       _token;
    private readonly RefreshTokenSerwis _rtSerwis;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthSerwis auth,
        ITokenSerwis token,
        RefreshTokenSerwis rtSerwis,
        ILogger<AuthController> logger)
    {
        _auth     = auth;
        _token    = token;
        _rtSerwis = rtSerwis;
        _logger   = logger;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<OdpowiedzTokenuDto>> Login(
        [FromBody] LoginDto dto,
        CancellationToken ct)
    {
        var uzytkownik = await _auth.WalidujAsync(
            dto.Email, dto.Haslo, ct);

        if (uzytkownik is null)
        {
            _logger.LogWarning(
                "Nieudane logowanie: {Email} z {IP}",
                dto.Email,
                HttpContext.Connection.RemoteIpAddress);

            return Unauthorized(new
            {
                blad = "Nieprawidłowy email lub hasło"
            });
        }

        var info = new UzytkownikInfo(
            uzytkownik.Id,
            uzytkownik.Login,
            uzytkownik.Email,
            uzytkownik.Role);

        var tokeny = _token.GenerujTokenPare(info);

        await _rtSerwis.UtworzAsync(
            uzytkownik.Id,
            tokeny.RefreshToken,
            tokeny.RefreshWygasa,
            ct);

        _logger.LogInformation(
            "Zalogowano: {Email}", dto.Email);

        return Ok(new OdpowiedzTokenuDto(
            tokeny.AccessToken,
            tokeny.RefreshToken,
            (int)(tokeny.AccessWygasa - DateTime.UtcNow).TotalSeconds,
            "Bearer",
            uzytkownik.Email,
            uzytkownik.Role));
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<ActionResult<OdpowiedzTokenuDto>> Refresh(
        [FromBody] RefreshDto dto,
        CancellationToken ct)
    {
        // 1. Waliduj access token (bez sprawdzania exp!)
        var principal = _token.WalidujAccessToken(dto.AccessToken);
        if (principal is null)
            return Unauthorized(new { blad = "Nieprawidłowy access token" });

        // 2. Pobierz ID użytkownika z tokenu
        string? userIdStr = principal.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized(new { blad = "Nieprawidłowy token" });

        // 3. Sprawdź czy refresh token nie był już używany
        await _rtSerwis.WykryjPonowneUzycieAsync(userId, dto.RefreshToken, ct);

        // 4. Znajdź aktywny refresh token
        var refreshToken = await _rtSerwis.ZnajdzAktywnyAsync(
            userId, dto.RefreshToken, ct);

        if (refreshToken is null)
            return Unauthorized(new
            {
                blad = "Refresh token nieważny lub wygasł"
            });

        // 5. Pobierz aktualne dane użytkownika (role mogły się zmienić!)
        var uzytkownik = await _auth.PobierzPoIdAsync(userId, ct);
        if (uzytkownik is null || !uzytkownik.Aktywny)
            return Unauthorized(new { blad = "Konto nieaktywne" });

        var info = new UzytkownikInfo(
            uzytkownik.Id,
            uzytkownik.Login,
            uzytkownik.Email,
            uzytkownik.Role);

        // 6. Generuj nowe tokeny
        var nowePara = _token.GenerujTokenPare(info);

        // 7. Rotuj refresh token — unieważnij stary, zapisz nowy
        await _rtSerwis.ZrotujAsync(
            refreshToken,
            nowePara.RefreshToken,
            nowePara.RefreshWygasa,
            ct);

        _logger.LogInformation(
            "Token odświeżony dla użytkownika: {Id}", userId);

        return Ok(new OdpowiedzTokenuDto(
            nowePara.AccessToken,
            nowePara.RefreshToken,
            (int)(nowePara.AccessWygasa - DateTime.UtcNow).TotalSeconds,
            "Bearer",
            uzytkownik.Email,
            uzytkownik.Role));
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutDto dto,
        CancellationToken ct)
    {
        string userIdStr = User.FindFirstValue(
            ClaimTypes.NameIdentifier)!;
        int userId = int.Parse(userIdStr);

        if (!string.IsNullOrEmpty(dto.RefreshToken))
        {
            // Wyloguj z jednego urządzenia
            var rt = await _rtSerwis.ZnajdzAktywnyAsync(
                userId, dto.RefreshToken, ct);

            if (rt is not null)
                await _rtSerwis.ZrotujAsync(
                    rt, "", DateTime.MinValue, ct);
        }
        else
        {
            // Wyloguj ze WSZYSTKICH urządzeń
            await _rtSerwis.OdwolajWszystkieAsync(
                userId, "Wylogowanie użytkownika", ct);
        }

        return NoContent();
    }

    // GET /api/auth/me — informacje o zalogowanym
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            id       = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email    = User.FindFirstValue(ClaimTypes.Email),
            login    = User.FindFirstValue(ClaimTypes.Name),
            role     = User.FindAll(ClaimTypes.Role)
                           .Select(c => c.Value),
            tenantId = User.FindFirstValue("tenant_id"),
            plan     = User.FindFirstValue("sub_tier")
        });
    }
}

// DTOs
public record LoginDto(string Email, string Haslo);
public record RefreshDto(string AccessToken, string RefreshToken);
public record LogoutDto(string? RefreshToken = null);

public record OdpowiedzTokenuDto(
    string        AccessToken,
    string        RefreshToken,
    int           ExpiresIn,
    string        TokenType,
    string        Email,
    List<string>  Role);
```

---

### 7. Zabezpieczanie endpointów i pobieranie danych

csharp

```csharp
// Różne sposoby zabezpieczania

[ApiController]
[Route("api/profil")]
[Authorize]                       // wymagaj JWT dla całego kontrolera
public class ProfilController : ControllerBase
{
    private readonly IProfilSerwis       _serwis;
    private readonly ICurrentUserService _user;

    public ProfilController(
        IProfilSerwis serwis,
        ICurrentUserService user)
    {
        _serwis = serwis;
        _user   = user;
    }

    // Dostępne dla każdego zalogowanego
    [HttpGet]
    public async Task<IActionResult> PobierzProfil(
        CancellationToken ct)
    {
        var profil = await _serwis.PobierzAsync(
            _user.UserId, ct);
        return profil is null ? NotFound() : Ok(profil);
    }

    // Tylko Admin lub właściciel profilu
    [HttpGet("{id:int}")]
    public async Task<IActionResult> PobierzCudzProfil(
        int id, CancellationToken ct)
    {
        // Sprawdź czy Admin LUB właściciel
        bool jestAdmin = User.IsInRole("Admin");
        bool jestWlascicielem = _user.UserId == id;

        if (!jestAdmin && !jestWlascicielem)
            return Forbid();

        var profil = await _serwis.PobierzAsync(id, ct);
        return profil is null ? NotFound() : Ok(profil);
    }

    // Tylko Admin
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Usun(
        int id, CancellationToken ct)
    {
        await _serwis.UsunAsync(id, ct);
        return NoContent();
    }

    // Dostępne bez logowania
    [HttpGet("publiczny/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicznyProfil(
        int id, CancellationToken ct)
    {
        var profil = await _serwis.PobierzPublicznyAsync(id, ct);
        return profil is null ? NotFound() : Ok(profil);
    }
}

// ICurrentUserService — dostęp do danych z tokenu
public interface ICurrentUserService
{
    int     UserId   { get; }
    string  Email    { get; }
    string? TenantId { get; }
    bool    IsAdmin  { get; }
    IEnumerable<string> Roles { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http)
        => _http = http;

    private ClaimsPrincipal User =>
        _http.HttpContext?.User
        ?? throw new InvalidOperationException("Brak kontekstu HTTP");

    public int UserId => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException());

    public string Email =>
        User.FindFirstValue(ClaimTypes.Email) ?? "";

    public string? TenantId =>
        User.FindFirstValue("tenant_id");

    public bool IsAdmin => User.IsInRole("Admin");

    public IEnumerable<string> Roles =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value);
}
```

---

### 8. Pełny przykład — stub interfaces i modele

csharp

```csharp
// Kompletny zestaw — wszystko razem

// Modele domeny
public class AppUzytkownik
{
    public int           Id      { get; set; }
    public string        Login   { get; set; } = "";
    public string        Email   { get; set; } = "";
    public string        Hash    { get; set; } = "";
    public bool          Aktywny { get; set; } = true;
    public List<string>  Role    { get; set; } = new();
}

// Interfejsy serwisów
public interface IAuthSerwis
{
    Task<AppUzytkownik?> WalidujAsync(
        string email, string haslo, CancellationToken ct);
    Task<AppUzytkownik?> PobierzPoIdAsync(
        int id, CancellationToken ct);
}

public interface IProfilSerwis
{
    Task<object?> PobierzAsync(int id, CancellationToken ct);
    Task<object?> PobierzPublicznyAsync(int id, CancellationToken ct);
    Task UsunAsync(int id, CancellationToken ct);
}

// DbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }

    public DbSet<AppUzytkownik> Uzytkownicy  => Set<AppUzytkownik>();
    public DbSet<RefreshToken>  RefreshTokeny => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AppUzytkownik>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role)
             .HasConversion(
                 v => string.Join(',', v),
                 v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .ToList());
        });

        mb.Entity<RefreshToken>(e =>
        {
            e.HasIndex(rt => rt.Token).IsUnique();
            e.HasIndex(rt => new { rt.UzytkownikId, rt.Odwolany });
        });
    }
}

// Implementacja AuthSerwis
public class AuthSerwis : IAuthSerwis
{
    private readonly AppDbContext _ctx;
    private readonly Microsoft.AspNetCore.Identity
        .IPasswordHasher<AppUzytkownik> _hasher;

    public AuthSerwis(
        AppDbContext ctx,
        Microsoft.AspNetCore.Identity.IPasswordHasher<AppUzytkownik> hasher)
    {
        _ctx    = ctx;
        _hasher = hasher;
    }

    public async Task<AppUzytkownik?> WalidujAsync(
        string email, string haslo, CancellationToken ct)
    {
        var user = await _ctx.Uzytkownicy
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Email == email && u.Aktywny, ct);

        if (user is null) return null;

        var wynik = _hasher.VerifyHashedPassword(user, user.Hash, haslo);

        return wynik is Microsoft.AspNetCore.Identity
            .PasswordVerificationResult.Failed
            ? null
            : user;
    }

    public async Task<AppUzytkownik?> PobierzPoIdAsync(
        int id, CancellationToken ct)
        => await _ctx.Uzytkownicy
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
}

// Rejestracja w Program.cs (dokończenie)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<RefreshTokenSerwis>();
builder.Services.AddScoped<IAuthSerwis, AuthSerwis>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITokenSerwis, JwtTokenSerwis>();
builder.Services.AddSingleton(
    Microsoft.AspNetCore.Identity
        .PasswordHasherOptions.DefaultIterationCount > 0
    ? (Microsoft.AspNetCore.Identity
        .IPasswordHasher<AppUzytkownik>)
       new Microsoft.AspNetCore.Identity
           .PasswordHasher<AppUzytkownik>()
    : new Microsoft.AspNetCore.Identity
           .PasswordHasher<AppUzytkownik>());
```

---

### Typowe pytania rekrutacyjne

**"Wyjaśnij strukturę JWT i dlaczego jest bezpieczny mimo że jest jawny?"** JWT ma trzy części: Header (algorytm), Payload (claims — dane użytkownika) i Signature (podpis kryptograficzny). Header i Payload to Base64Url — każdy może je odkodować, dlatego nigdy nie wkładamy sekretów do JWT. Bezpieczeństwo zapewnia Signature — HMACSHA256(header + payload + secretKey). Bez znajomości secretKey nie można wygenerować ważnej sygnatury. Serwer weryfikuje podpis przy każdym requeście — zmodyfikowany token ma inny podpis i jest odrzucany.

**"Dlaczego access token ma krótki czas życia, a refresh token długi?"** Access token żyje 15-60 minut — gdy wycieknie, okno ataku jest ograniczone. Nie można go unieważnić bez blacklisty (stateless). Refresh token żyje 7-30 dni i jest przechowywany w bazie — można go natychmiast unieważnić. Przy refresh używasz obu tokenów: serwer sprawdza refresh token w bazie i rotuje go (stary staje się nieważny). Jeśli atakujący ukradnie access token — może go używać tylko przez kilka minut. Jeśli ukradnie refresh token — rotacja wykryje ponowne użycie i unieważni wszystkie tokeny konta.

**"Co to token rotation i jak chroni przed kradzieżą?"** Token rotation = przy każdym refresh stary refresh token jest unieważniany, wystawiany nowy. W bazie zapisujesz `ZastapionoPrzez` — można prześledzić historię rotacji. Jeśli atakujący ukradnie refresh token i użyje go PO legalnym kliencie — serwer wykryje że już unieważniony token jest używany i unieważnia WSZYSTKIE tokeny użytkownika. Legitymny klient dostaje błąd 401 i musi się zalogować ponownie — to sygnał że konto mogło być skompromitowane.

**"Jaka różnica między `ValidateLifetime = true` przy normalnej walidacji a `false` przy refresh?"** Przy normalnych requestach `ValidateLifetime = true` — token wygasły = 401 Unauthorized. Przy refresh endpoincie `ValidateLifetime = false` — celowo akceptujemy wygasły access token, bo klient chce go właśnie wymienić na nowy. Weryfikujemy tylko podpis i strukturę (issuer, audience, algorytm) — sprawdzamy tylko czy token był kiedyś wystawiony przez nas. Następnie walidujemy refresh token w bazie i wystawiamy nową parę.