### Swagger / OpenAPI w ASP.NET Core

Swagger to **interfejs webowy** który automatycznie dokumentuje API i pozwala je testować bezpośrednio z przeglądarki. OpenAPI to standard specyfikacji, Swashbuckle to biblioteka generująca go dla ASP.NET Core.

---

### 1. Setup i podstawowa konfiguracja

csharp

```csharp
// dotnet add package Swashbuckle.AspNetCore

// Program.cs — minimalna konfiguracja
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();  // wymagane dla Minimal API

builder.Services.AddSwaggerGen(opt =>
{
    // Podstawowe informacje o API
    opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title          = "Sklep API",
        Version        = "v1",
        Description    = "REST API dla sklepu internetowego",
        Contact        = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name  = "Zespół Backend",
            Email = "api@sklep.pl",
            Url   = new Uri("https://sklep.pl/kontakt")
        },
        License        = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT",
            Url  = new Uri("https://opensource.org/licenses/MIT")
        },
        TermsOfService = new Uri("https://sklep.pl/regulamin")
    });

    // Włącz komentarze XML jako dokumentację
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    opt.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Swagger UI — domyślnie tylko w Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // generuje /swagger/v1/swagger.json
    app.UseSwaggerUI(opt =>
    {
        opt.SwaggerEndpoint("/swagger/v1/swagger.json", "Sklep API v1");
        opt.RoutePrefix = "";          // UI dostępne na / zamiast /swagger
        opt.DocumentTitle = "Sklep API";
    });
}

app.UseAuthorization();
app.MapControllers();
app.Run();

// WYMAGANE w .csproj — generuj plik XML z komentarzami
// <PropertyGroup>
//   <GenerateDocumentationFile>true</GenerateDocumentationFile>
//   <NoWarn>$(NoWarn);1591</NoWarn>  ← wyłącz warning o brakujących komentarzach
// </PropertyGroup>
```

---

### 2. Dokumentowanie kontrolerów przez atrybuty

csharp

```csharp
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

/// <summary>
/// Zarządzanie produktami w katalogu sklepu.
/// Endpointy wymagają autentykacji JWT oprócz odczytu.
/// </summary>
[ApiController]
[Route("api/v1/produkty")]
[Produces("application/json")]          // domyślny Content-Type response
[Consumes("application/json")]          // domyślny Content-Type request
[Tags("Produkty")]                      // grupowanie w Swagger UI
public class ProduktyController : ControllerBase
{
    private readonly IProduktSerwis _serwis;
    private readonly ILogger<ProduktyController> _logger;

    public ProduktyController(
        IProduktSerwis serwis,
        ILogger<ProduktyController> logger)
    {
        _serwis = serwis;
        _logger = logger;
    }

    /// <summary>
    /// Pobierz listę produktów z filtrowaniem i paginacją.
    /// </summary>
    /// <param name="filtr">Parametry filtrowania i paginacji</param>
    /// <param name="ct">Token anulowania</param>
    /// <returns>Stronicowana lista produktów</returns>
    /// <remarks>
    /// Przykładowe zapytanie:
    ///
    ///     GET /api/v1/produkty?kategoria=IT&amp;minCena=100&amp;strona=1&amp;rozmiar=20
    ///
    /// Zwraca maksymalnie 100 produktów na stronę.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(StronicowanaOdpowiedz<ProduktListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ResponseCache(Duration = 60)]
    public async Task<ActionResult<StronicowanaOdpowiedz<ProduktListDto>>> PobierzListe(
        [FromQuery] ProduktFiltrDto filtr,
        CancellationToken ct)
    {
        var wynik = await _serwis.SzukajAsync(filtr, ct);
        return Ok(wynik);
    }

    /// <summary>
    /// Pobierz szczegóły konkretnego produktu.
    /// </summary>
    /// <param name="id">Identyfikator produktu (liczba całkowita > 0)</param>
    /// <returns>Szczegóły produktu wraz z kategorią i zdjęciami</returns>
    /// <response code="200">Produkt znaleziony i zwrócony</response>
    /// <response code="404">Produkt o podanym ID nie istnieje</response>
    [HttpGet("{id:int}", Name = "PobierzProdukt")]
    [ProducesResponseType(typeof(ProduktDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProduktDetailDto>> PobierzPoId(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var produkt = await _serwis.PobierzPoIdAsync(id, ct);
        return produkt is null ? NotFound() : Ok(produkt);
    }

    /// <summary>
    /// Utwórz nowy produkt w katalogu.
    /// </summary>
    /// <param name="dto">Dane nowego produktu</param>
    /// <returns>Utworzony produkt z przydzielonym ID</returns>
    /// <response code="201">Produkt utworzony pomyślnie</response>
    /// <response code="400">Dane wejściowe nieprawidłowe (walidacja)</response>
    /// <response code="401">Brak autoryzacji — wymagany token JWT</response>
    /// <response code="409">Produkt o tej nazwie już istnieje</response>
    [HttpPost]
    [Authorize]                           // wymaga JWT
    [ProducesResponseType(typeof(ProduktDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProduktDetailDto>> Dodaj(
        [FromBody] NowyProduktDto dto,
        CancellationToken ct)
    {
        int id = await _serwis.DodajAsync(dto, ct);
        var nowy = await _serwis.PobierzPoIdAsync(id, ct);
        return CreatedAtRoute("PobierzProdukt", new { id }, nowy);
    }

    /// <summary>
    /// Zaktualizuj istniejący produkt.
    /// Operacja zastępuje CAŁY zasób (PUT semantics).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Aktualizuj(
        [FromRoute] int id,
        [FromBody]  AktualizujProduktDto dto,
        CancellationToken ct)
    {
        bool ok = await _serwis.AktualizujAsync(id, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Usuń produkt z katalogu (soft delete — produkt jest deaktywowany).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Usun(
        [FromRoute] int id,
        CancellationToken ct)
    {
        bool ok = await _serwis.UsunAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
```

---

### 3. Dokumentowanie modeli przez XML i atrybuty

csharp

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Dane nowego produktu do dodania do katalogu.
/// </summary>
public class NowyProduktDto
{
    /// <summary>Nazwa produktu. Musi być unikalna w katalogu.</summary>
    /// <example>Laptop Dell XPS 15</example>
    [Required(ErrorMessage = "Nazwa jest wymagana")]
    [StringLength(200, MinimumLength = 2)]
    public string Nazwa { get; set; } = "";

    /// <summary>Cena brutto w PLN.</summary>
    /// <example>3499.99</example>
    [Required]
    [Range(0.01, 999_999.99)]
    public decimal Cena { get; set; }

    /// <summary>ID kategorii produktu.</summary>
    /// <example>5</example>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Wybierz kategorię")]
    public int KategoriaId { get; set; }

    /// <summary>Dostępna ilość w magazynie. Domyślnie 0.</summary>
    /// <example>10</example>
    [Range(0, 100_000)]
    [DefaultValue(0)]
    public int StanMagazynu { get; set; } = 0;

    /// <summary>Krótki opis produktu (opcjonalny, max 1000 znaków).</summary>
    /// <example>Wydajny laptop z ekranem OLED 15.6 cala, procesor Intel i7</example>
    [StringLength(1000)]
    public string? Opis { get; set; }

    /// <summary>Lista tagów ułatwiających wyszukiwanie.</summary>
    /// <example>["laptop", "dell", "oled", "premium"]</example>
    public List<string> Tagi { get; set; } = new();

    /// <summary>Czy produkt jest od razu widoczny w sklepie.</summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool Aktywny { get; set; } = true;
}

/// <summary>
/// Szczegóły produktu zwracane przez API.
/// </summary>
public class ProduktDetailDto
{
    /// <example>42</example>
    public int Id { get; set; }

    /// <example>Laptop Dell XPS 15</example>
    public string Nazwa { get; set; } = "";

    /// <example>3499.99</example>
    public decimal Cena { get; set; }

    /// <summary>Kategoria produktu.</summary>
    public KategoriaDto Kategoria { get; set; } = null!;

    /// <example>10</example>
    public int StanMagazynu { get; set; }

    /// <example>true</example>
    public bool Aktywny { get; set; }

    /// <example>2024-03-15T10:30:00Z</example>
    public DateTime DataDodania { get; set; }

    public List<string> Tagi { get; set; } = new();
}

/// <summary>Skrócone informacje o kategorii.</summary>
public class KategoriaDto
{
    /// <example>5</example>
    public int Id { get; set; }

    /// <example>Laptopy i komputery</example>
    public string Nazwa { get; set; } = "";
}

/// <summary>Skrócona informacja o produkcie dla listy.</summary>
public class ProduktListDto
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public string  Kategoria    { get; set; } = "";
    public int     StanMagazynu { get; set; }
}

/// <summary>Parametry filtrowania listy produktów.</summary>
public class ProduktFiltrDto
{
    /// <summary>Filtruj po nazwie kategorii.</summary>
    /// <example>IT</example>
    public string?  Kategoria { get; set; }

    /// <summary>Minimalna cena.</summary>
    /// <example>100</example>
    public decimal? MinCena   { get; set; }

    /// <summary>Maksymalna cena.</summary>
    /// <example>5000</example>
    public decimal? MaxCena   { get; set; }

    /// <summary>Numer strony (od 1).</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)] public int Strona  { get; set; } = 1;

    /// <summary>Liczba wyników na stronę (1–100).</summary>
    /// <example>20</example>
    [Range(1, 100)] public int Rozmiar { get; set; } = 20;
}

/// <summary>Stronicowana odpowiedź API.</summary>
public class StronicowanaOdpowiedz<T>
{
    /// <summary>Lista wyników na bieżącej stronie.</summary>
    public IReadOnlyList<T> Dane        { get; set; } = new List<T>();

    /// <example>1</example>
    public int Strona      { get; set; }

    /// <example>20</example>
    public int Rozmiar     { get; set; }

    /// <example>150</example>
    public int LacznaIlosc { get; set; }

    /// <example>8</example>
    public int LaczneStrony => (int)Math.Ceiling((double)LacznaIlosc / Rozmiar);
}
```

---

### 4. Swagger z JWT — autoryzacja

csharp

```csharp
// Konfiguracja Swagger z obsługą JWT Bearer token

builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title   = "Sklep API",
        Version = "v1"
    });

    // Definicja schematu bezpieczeństwa JWT
    opt.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = """
            Wpisz TOKEN JWT bez prefiksu 'Bearer'.
            Przykład: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
            Token uzyskasz przez POST /api/auth/login
            """
    });

    // Wymagaj JWT dla wszystkich endpointów domyślnie
    opt.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Komentarze XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    opt.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    // Enum jako string zamiast liczby
    opt.UseInlineDefinitionsForEnums();
});
```

---

### 5. Wiele wersji API

csharp

```csharp
// dotnet add package Microsoft.AspNetCore.Mvc.Versioning
// dotnet add package Microsoft.AspNetCore.Mvc.Versioning.ApiExplorer

// Program.cs
builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion               = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ReportApiVersions               = true;  // nagłówki api-supported-versions
    opt.ApiVersionReader                = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),           // /api/v1/...
        new HeaderApiVersionReader("X-Api-Version"), // nagłówek
        new QueryStringApiVersionReader("version")); // query ?version=1
});

builder.Services.AddVersionedApiExplorer(opt =>
{
    opt.GroupNameFormat           = "'v'VVV";
    opt.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen();

// Konfiguracja Swagger per wersja
builder.Services.ConfigureOptions<SwaggerKonfiguracjaOpcji>();

// Klasa konfiguracji
public class SwaggerKonfiguracjaOpcji
    : IConfigureNamedOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public SwaggerKonfiguracjaOpcji(IApiVersionDescriptionProvider provider)
        => _provider = provider;

    public void Configure(string? name, SwaggerGenOptions opt) => Configure(opt);

    public void Configure(SwaggerGenOptions opt)
    {
        foreach (var opis in _provider.ApiVersionDescriptions)
        {
            opt.SwaggerDoc(opis.GroupName, TworzInfo(opis));
        }
    }

    private static Microsoft.OpenApi.Models.OpenApiInfo TworzInfo(
        ApiVersionDescription opis)
    {
        var info = new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title   = $"Sklep API {opis.GroupName.ToUpperInvariant()}",
            Version = opis.ApiVersion.ToString()
        };

        if (opis.IsDeprecated)
            info.Description = "⚠️ Ta wersja API jest przestarzała.";

        return info;
    }
}

// Swagger UI z wieloma wersjami
app.UseSwaggerUI(opt =>
{
    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
    foreach (var opis in provider.ApiVersionDescriptions.Reverse())
    {
        opt.SwaggerEndpoint(
            $"/swagger/{opis.GroupName}/swagger.json",
            $"Sklep API {opis.GroupName.ToUpperInvariant()}");
    }
    opt.RoutePrefix = "";
});

// Kontroler z wersjonowaniem
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/produkty")]
public class ProduktyV1V2Controller : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public IActionResult PobierzV1() => Ok("API v1 — stary format");

    [HttpGet]
    [MapToApiVersion("2.0")]
    public IActionResult PobierzV2() => Ok("API v2 — nowy format z HATEOAS");
}

[ApiController]
[ApiVersion("1.0", Deprecated = true)]  // oznacz jako przestarzały
[Route("api/v{version:apiVersion}/zamowienia")]
public class ZamowieniaV1Controller : ControllerBase
{
    [HttpGet]
    public IActionResult Pobierz() => Ok("Stare API — użyj v2");
}
```

---

### 6. Filtry i customizacja Swagger

csharp

```csharp
// Własny filtr dokumentu — dodaje globalne nagłówki

public class GlobalNaglowkiFilter : IOperationFilter
{
    public void Apply(
        Microsoft.OpenApi.Models.OpenApiOperation operation,
        OperationFilterContext context)
    {
        operation.Parameters ??= new List<Microsoft.OpenApi.Models.OpenApiParameter>();

        // Dodaj X-Correlation-Id do każdego endpointu
        operation.Parameters.Add(new Microsoft.OpenApi.Models.OpenApiParameter
        {
            Name        = "X-Correlation-Id",
            In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Required    = false,
            Description = "Opcjonalny ID korelacji do śledzenia requestu",
            Schema      = new Microsoft.OpenApi.Models.OpenApiSchema
            {
                Type    = "string",
                Format  = "uuid",
                Example = new Microsoft.OpenApi.Any.OpenApiString(
                    Guid.NewGuid().ToString())
            }
        });

        // Dodaj X-Api-Key gdy endpoint wymaga ApiKey
        var wymagaApiKey = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<WymagaApiKeyAttribute>()
            .Any();

        if (wymagaApiKey)
        {
            operation.Parameters.Add(new Microsoft.OpenApi.Models.OpenApiParameter
            {
                Name        = "X-Api-Key",
                In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Required    = true,
                Description = "Klucz API do autoryzacji",
                Schema      = new Microsoft.OpenApi.Models.OpenApiSchema
                    { Type = "string" }
            });
        }
    }
}

public class WymagaApiKeyAttribute : Attribute { }

// Filtr pomijający endpointy z [ApiExplorerSettings(IgnoreApi = true)]
public class UkryjWewnetrzneFilter : IDocumentFilter
{
    public void Apply(
        Microsoft.OpenApi.Models.OpenApiDocument swaggerDoc,
        DocumentFilterContext context)
    {
        // Usuń ścieżki oznaczone jako wewnętrzne
        var doUsuniecia = swaggerDoc.Paths
            .Where(p => p.Key.Contains("/internal/"))
            .Select(p => p.Key)
            .ToList();

        foreach (var sciezka in doUsuniecia)
            swaggerDoc.Paths.Remove(sciezka);
    }
}

// Filtr przykładów — własne przykłady odpowiedzi
public class ProduktPrzykladyFilter : IOperationFilter
{
    public void Apply(
        Microsoft.OpenApi.Models.OpenApiOperation operation,
        OperationFilterContext context)
    {
        if (context.MethodInfo.Name != "PobierzPoId") return;

        operation.Responses["200"].Content["application/json"].Examples =
            new Dictionary<string, Microsoft.OpenApi.Models.OpenApiExample>
            {
                ["laptop"] = new Microsoft.OpenApi.Models.OpenApiExample
                {
                    Summary = "Przykład — Laptop",
                    Value   = new Microsoft.OpenApi.Any.OpenApiObject
                    {
                        ["id"]     = new Microsoft.OpenApi.Any.OpenApiInteger(1),
                        ["nazwa"]  = new Microsoft.OpenApi.Any.OpenApiString("Laptop Dell XPS"),
                        ["cena"]   = new Microsoft.OpenApi.Any.OpenApiDouble(3499.99),
                        ["aktywny"]= new Microsoft.OpenApi.Any.OpenApiBoolean(true)
                    }
                },
                ["mysz"] = new Microsoft.OpenApi.Models.OpenApiExample
                {
                    Summary = "Przykład — Mysz",
                    Value   = new Microsoft.OpenApi.Any.OpenApiObject
                    {
                        ["id"]    = new Microsoft.OpenApi.Any.OpenApiInteger(2),
                        ["nazwa"] = new Microsoft.OpenApi.Any.OpenApiString("Logitech MX Master"),
                        ["cena"]  = new Microsoft.OpenApi.Any.OpenApiDouble(399.00),
                        ["aktywny"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true)
                    }
                }
            };
    }
}

// Rejestracja filtrów
builder.Services.AddSwaggerGen(opt =>
{
    opt.OperationFilter<GlobalNaglowkiFilter>();
    opt.OperationFilter<ProduktPrzykladyFilter>();
    opt.DocumentFilter<UkryjWewnetrzneFilter>();
});
```

---

### 7. Swagger UI — customizacja wyglądu

csharp

```csharp
// Customizacja UI Swagger

app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "Sklep API v1");
    opt.RoutePrefix = "docs";   // dostępne pod /docs

    // Wygląd
    opt.DocumentTitle     = "Sklep API — Dokumentacja";
    opt.DisplayRequestDuration();      // pokaż czas wykonania
    opt.EnableDeepLinking();           // linkowanie bezpośrednio do endpointu
    opt.EnableFilter();                // pole wyszukiwania
    opt.ShowExtensions();              // pokaż rozszerzenia OpenAPI
    opt.EnableValidator();             // waliduj spec po stronie UI

    // Domyślne rozwinięcie
    opt.DefaultModelExpandDepth(2);    // głębokość rozwinięcia modelu
    opt.DefaultModelsExpandDepth(-1);  // -1 = zwiń wszystkie modele
    opt.DefaultModelRendering(
        Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);

    // Własne CSS i JS
    opt.InjectStylesheet("/swagger-custom.css");
    opt.InjectJavascript("/swagger-custom.js");

    // Ukryj przycisk "Authorize" gdy używasz auth przez nagłówek
    opt.ConfigObject.AdditionalItems["persistAuthorization"] = true;
});

// Własny CSS (wwwroot/swagger-custom.css)
// .swagger-ui .topbar { background-color: #1a1a2e; }
// .swagger-ui .topbar-wrapper img { display: none; }
// .swagger-ui .topbar-wrapper::after { content: "Sklep API"; color: white; }

// Kondycjonalna ekspozycja Swagger (np. przez feature flag)
if (app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enable"))
{
    app.UseSwagger(opt =>
    {
        // Customizuj generowany JSON
        opt.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            // Dynamicznie ustaw serwer na podstawie hosta
            swaggerDoc.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
            {
                new()
                {
                    Url         = $"{httpReq.Scheme}://{httpReq.Host}",
                    Description = app.Environment.EnvironmentName
                }
            };
        });
    });
    app.UseSwaggerUI();
}
```

---

### 8. Praktyczny przykład — kompletna konfiguracja

csharp

```csharp
// Program.cs — produkcyjna konfiguracja Swagger

using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(opt =>
{
    // === PODSTAWOWE INFO ===
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Sklep API",
        Version     = "v1",
        Description = """
            ## REST API sklepu internetowego

            ### Autentykacja
            Większość endpointów wymaga tokenu JWT w nagłówku `Authorization`.

            Uzyskaj token przez: `POST /api/auth/login`

            ### Konwencje
            - Daty w formacie ISO 8601 (UTC): `2024-03-15T10:30:00Z`
            - Ceny w PLN jako `decimal`
            - Paginacja: parametry `strona` i `rozmiar`
            - Błędy: format RFC 7807 Problem Details
            """,
        Contact = new OpenApiContact
        {
            Name  = "Zespół API",
            Email = "api@sklep.pl",
            Url   = new Uri("https://sklep.pl/kontakt")
        }
    });

    // === JWT ===
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Wprowadź token JWT. Przykład: `eyJhbG...`"
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // === API KEY (drugi schemat bezpieczeństwa) ===
    opt.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name        = "X-Api-Key",
        Type        = SecuritySchemeType.ApiKey,
        In          = ParameterLocation.Header,
        Description = "Klucz API dla serwisów zewnętrznych"
    });

    // === XML KOMENTARZE ===
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        opt.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    // === FILTRY ===
    opt.OperationFilter<GlobalNaglowkiFilter>();

    // === OPCJE ===
    opt.EnableAnnotations();                   // [SwaggerOperation], [SwaggerResponse]
    opt.UseInlineDefinitionsForEnums();        // enum jako string
    opt.OrderActionsBy(a => a.RelativePath);   // sortuj endpointy
    opt.SchemaFilter<EnumSchemaFilter>();      // opisy dla enum

    // Customowe mapowanie typów
    opt.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    opt.MapType<TimeOnly>(() => new OpenApiSchema { Type = "string", Format = "time" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opt =>
    {
        opt.SwaggerEndpoint("/swagger/v1/swagger.json", "Sklep API v1");
        opt.RoutePrefix            = "";
        opt.DocumentTitle          = "Sklep API";
        opt.DisplayRequestDuration();
        opt.EnableDeepLinking();
        opt.EnableFilter();
        opt.DefaultModelsExpandDepth(-1);
        opt.ConfigObject.AdditionalItems["persistAuthorization"] = true;
    });
}

// Przykładowy EnumSchemaFilter
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(
        Microsoft.OpenApi.Models.OpenApiSchema schema,
        SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        schema.Enum.Clear();
        schema.Type = "string";

        foreach (var name in Enum.GetNames(context.Type))
        {
            schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(name));
        }

        // Dodaj opis z wartościami
        schema.Description = "Możliwe wartości: " +
            string.Join(", ", Enum.GetNames(context.Type));
    }
}

// Minimal API z Swagger
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", czas = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithSummary("Sprawdź stan API")
    .WithDescription("Zwraca status zdrowia aplikacji i aktualny czas serwera")
    .WithTags("System")
    .Produces<object>(200)
    .AllowAnonymous();

app.MapPost("/api/auth/login", async (LoginDto dto, IAuthSerwis auth) =>
{
    var token = await auth.LoginAsync(dto);
    return token is null ? Results.Unauthorized() : Results.Ok(token);
})
    .WithName("Login")
    .WithSummary("Zaloguj się i uzyskaj token JWT")
    .WithTags("Auth")
    .Produces<TokenDto>(200)
    .Produces(401)
    .AllowAnonymous();

// Stub interfaces
public interface IProduktSerwis
{
    Task<StronicowanaOdpowiedz<ProduktListDto>> SzukajAsync(ProduktFiltrDto f, CancellationToken ct);
    Task<ProduktDetailDto?> PobierzPoIdAsync(int id, CancellationToken ct);
    Task<int> DodajAsync(NowyProduktDto dto, CancellationToken ct);
    Task<bool> AktualizujAsync(int id, AktualizujProduktDto dto, CancellationToken ct);
    Task<bool> UsunAsync(int id, CancellationToken ct);
}
public interface IAuthSerwis { Task<TokenDto?> LoginAsync(LoginDto dto); }
public record LoginDto(string Login, string Haslo);
public record TokenDto(string Token, DateTime Wygasa);
public record AktualizujProduktDto(string Nazwa, decimal Cena, int KategoriaId, int Stan);
public class AuthorizeAttribute : Attribute { public string? Roles { get; set; } }
```

---

### Typowe pytania rekrutacyjne

**"Co to Swagger i jaka jest różnica między Swagger a OpenAPI?"** OpenAPI to standard specyfikacji REST API (YAML/JSON opisujący endpointy, modele, autoryzację). Swagger to zestaw narzędzi do pracy z OpenAPI — Swagger UI (interfejs webowy), Swagger Editor, Swagger Codegen. Swashbuckle to biblioteka .NET która generuje specyfikację OpenAPI z kodu C# i serwuje Swagger UI. W codziennym języku "Swagger" oznacza cały ekosystem, choć technicznie to narzędzia a nie standard.

**"Jak dokumentować endpointy żeby Swagger był użyteczny?"** Trzy warstwy: (1) `[ProducesResponseType]` — jawne kody HTTP i typy odpowiedzi, (2) komentarze XML (`<summary>`, `<param>`, `<returns>`, `<example>`) — opis w interfejsie, (3) atrybuty na modelach (`[Required]`, `[Range]`, `[StringLength]`) — walidacja widoczna w schemacie. Najważniejsze: opisz co zwraca każdy kod HTTP i podaj przykłady wartości przez `<example>` — to czyni Swagger użytecznym narzędziem testowym.

**"Jak dodać obsługę JWT w Swagger UI?"** `AddSecurityDefinition("Bearer", ...)` — definiuje schemat JWT w dokumentacji. `AddSecurityRequirement(...)` — mówi że endpointy wymagają tego schematu. W UI pojawi się przycisk "Authorize" gdzie wpisujesz token. Opcjonalnie `[AllowAnonymous]` wyłącza wymóg dla konkretnych endpointów. Ustaw `persistAuthorization: true` żeby token był pamiętany po odświeżeniu strony.

**"Czy Swagger powinien być dostępny w produkcji?"** Zależy od kontekstu. Zalety produkcyjnego Swagger: zawsze aktualna dokumentacja, łatwe testowanie dla partnerów. Wady: ujawnia strukturę API atakującym, overhead. Kompromis: wymagaj autentykacji dla Swagger UI w produkcji (`app.UseSwaggerUI()` za `[Authorize]` lub przez middleware), albo publikuj tylko specyfikację JSON bez UI, albo generuj statyczny HTML offline. W wewnętrznych API enterprise — Swagger w prod jest normalny i wygodny.