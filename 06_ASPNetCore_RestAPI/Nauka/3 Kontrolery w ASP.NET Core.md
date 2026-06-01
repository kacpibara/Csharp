### Kontrolery w ASP.NET Core

---

### 1. Anatomia kontrolera

csharp

```csharp
using Microsoft.AspNetCore.Mvc;

// [ApiController] — zestaw konwencji dla API (szczegóły w sekcji 4)
// [Route]         — bazowy routing dla całego kontrolera
[ApiController]
[Route("api/[controller]")]  // [controller] = nazwa klasy bez "Controller" → "produkty"
public class ProduktyController : ControllerBase
//                               ↑ ControllerBase — bez wsparcia dla Views
//                               (Controller dziedziczy ControllerBase + View support)
{
    // Wstrzykiwanie przez konstruktor — DI
    private readonly IProduktSerwis   _serwis;
    private readonly ILogger<ProduktyController> _logger;

    public ProduktyController(
        IProduktSerwis serwis,
        ILogger<ProduktyController> logger)
    {
        _serwis  = serwis;
        _logger  = logger;
    }

    // Akcja = publiczna metoda w kontrolerze
    [HttpGet]
    public async Task<IActionResult> PobierzWszystkie()
    {
        var produkty = await _serwis.PobierzWszystkieAsync();
        return Ok(produkty);  // 200 OK + JSON
    }
}

// ControllerBase daje Ci:
// Ok(), Created(), NoContent(), BadRequest(), NotFound(), Conflict()...
// Request, Response, HttpContext, RouteData, ModelState
// User (ClaimsPrincipal), Url (IUrlHelper)
```

---

### 2. Attribute Routing — szczegółowo

csharp

```csharp
// Routing na poziomie KONTROLERA
[Route("api/v1/sklep/produkty")]          // stała ścieżka
[Route("api/v{version:apiVersion}/[controller]")]  // z wersją API
[Route("[controller]")]                    // nazwa kontrolera lowercase

// Routing na poziomie AKCJI
[ApiController]
[Route("api/produkty")]
public class RoutingController : ControllerBase
{
    // Prosta ścieżka
    [HttpGet]                              // GET /api/produkty
    [HttpGet("")]                          // to samo
    [HttpGet("/api/produkty")]             // absolutna (nadpisuje bazę kontrolera!)
    public IActionResult Lista() => Ok();

    // Parametry w ścieżce
    [HttpGet("{id}")]                      // GET /api/produkty/42
    [HttpGet("{id:int}")]                  // tylko int
    [HttpGet("{id:int:min(1)}")]           // int, min=1
    [HttpGet("{id:int:range(1,1000)}")]    // int, zakres
    [HttpGet("{slug:alpha}")]              // tylko litery
    [HttpGet("{slug:regex(^[a-z-]+$)}")]  // regex
    [HttpGet("{kod:length(5)}")]           // dokładnie 5 znaków
    [HttpGet("{kod:minlength(3):maxlength(10)}")]  // zakres długości
    [HttpGet("{id:guid}")]                 // GUID
    [HttpGet("{data:datetime}")]           // datetime
    public IActionResult PoId(int id) => Ok(id);

    // Wiele parametrów
    [HttpGet("{kategoriaId:int}/produkty/{id:int}")]
    // GET /api/produkty/5/produkty/42
    public IActionResult PoKategoriiIId(int kategoriaId, int id) => Ok();

    // Opcjonalne parametry
    [HttpGet("{id:int?}")]
    public IActionResult OpcjonalnyId(int? id = null) =>
        id.HasValue ? Ok($"produkt {id}") : Ok("wszystkie");

    // Wiele tras dla jednej akcji
    [HttpGet("aktywne")]
    [HttpGet("dostepne")]
    [HttpGet("w-sprzedazy")]
    public IActionResult Aktywne() => Ok("aktywne");

    // Nadpisanie bazowej ścieżki przez /
    [HttpGet("/health")]                   // GET /health (nie /api/produkty/health!)
    public IActionResult Health() => Ok("OK");

    // Named routes — do generowania URL
    [HttpGet("{id:int}", Name = "PobierzProdukt")]
    public IActionResult PobierzPoId(int id)
    {
        // Generuj URL do innej akcji
        string url = Url.Link("PobierzProdukt", new { id = 42 })!;
        // https://localhost/api/produkty/42
        return Ok(url);
    }

    // Catch-all — przechwytuje wszystko
    [HttpGet("{**sciezka}")]
    public IActionResult Fallback(string sciezka) =>
        NotFound($"Nie znaleziono: {sciezka}");
}
```

---

### 3. Model Binding — wszystkie źródła danych

csharp

```csharp
[ApiController]
[Route("api/demo")]
public class ModelBindingController : ControllerBase
{
    // [FromRoute]  — z URI path
    // [FromQuery]  — z query string (?klucz=wartość)
    // [FromBody]   — z request body (JSON/XML)
    // [FromHeader] — z HTTP nagłówka
    // [FromForm]   — z form-data (pliki, formularze)
    // [FromServices] — z DI kontenera

    // ===== FromRoute =====
    [HttpGet("{id:int}")]
    public IActionResult ZRoute([FromRoute] int id)
    {
        Console.WriteLine($"ID z URI: {id}");
        return Ok(id);
    }

    // ===== FromQuery =====
    // GET /api/demo/szukaj?fraza=laptop&minCena=100&maxCena=5000&strona=2
    [HttpGet("szukaj")]
    public IActionResult ZQuery(
        [FromQuery] string?  fraza,
        [FromQuery] decimal? minCena,
        [FromQuery] decimal? maxCena,
        [FromQuery] int      strona  = 1,
        [FromQuery] int      rozmiar = 20)
    {
        return Ok(new { fraza, minCena, maxCena, strona, rozmiar });
    }

    // Kompleksowy obiekt z query — binding automatyczny
    public class ProduktFiltr
    {
        [FromQuery(Name = "kat")]
        public string?  Kategoria { get; set; }

        [FromQuery(Name = "min")]
        public decimal? MinCena   { get; set; }

        [FromQuery(Name = "max")]
        public decimal? MaxCena   { get; set; }

        public int Strona  { get; set; } = 1;
        public int Rozmiar { get; set; } = 20;
    }

    // GET /api/demo/filtry?kat=IT&min=100&max=5000&Strona=2
    [HttpGet("filtry")]
    public IActionResult ZKompleksowymQuery([FromQuery] ProduktFiltr filtr)
        => Ok(filtr);

    // Kolekcje z query — ?ids=1&ids=2&ids=3
    [HttpGet("wiele")]
    public IActionResult PobierzWiele([FromQuery] int[] ids)
        => Ok(ids);

    // ===== FromBody =====
    public record NowyProduktDto(
        string  Nazwa,
        decimal Cena,
        int     KategoriaId,
        int     StanMagazynu = 0);

    [HttpPost]
    public IActionResult ZBody([FromBody] NowyProduktDto dto)
    {
        Console.WriteLine($"Z body: {dto.Nazwa}, {dto.Cena}");
        return Ok(dto);
    }

    // ===== FromHeader =====
    [HttpGet("naglowki")]
    public IActionResult ZNaglowka(
        [FromHeader(Name = "X-Api-Key")]  string? apiKey,
        [FromHeader(Name = "User-Agent")] string? userAgent,
        [FromHeader(Name = "Accept-Language")] string? lang)
    {
        return Ok(new { apiKey, userAgent, lang });
    }

    // ===== FromForm =====
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] string      nazwa,
        [FromForm] IFormFile   plik,
        [FromForm] IFormFileCollection wszystkiePliki)
    {
        Console.WriteLine($"Plik: {plik.FileName}, rozmiar: {plik.Length}");

        using var stream = plik.OpenReadStream();
        // Zapisz stream...

        return Ok(new { nazwa, plik.FileName, plik.Length });
    }

    // ===== FromServices =====
    [HttpGet("report")]
    public async Task<IActionResult> Raport(
        [FromServices] IRaportSerwis raportSerwis,
        [FromQuery]    DateTime      od,
        [FromQuery]    DateTime      do_)
    {
        // Wstrzyknięcie bezpośrednio do akcji — bez konstruktora
        var wynik = await raportSerwis.GenerujAsync(od, do_);
        return Ok(wynik);
    }

    // ===== MIESZANE ŹRÓDŁA =====
    [HttpPost("{kategoriaId:int}/produkty")]
    public IActionResult Mieszane(
        [FromRoute]  int            kategoriaId,   // z URI
        [FromQuery]  bool           aktywny,        // z query
        [FromBody]   NowyProduktDto dto,            // z body
        [FromHeader(Name = "X-Trace")] string? traceId)  // z nagłówka
    {
        return Ok(new { kategoriaId, aktywny, dto, traceId });
    }
}
```

---

### 4. [ApiController] — co robi

csharp

```csharp
// [ApiController] dodaje automatycznie:

// 1. AUTOMATIC 400 BAD REQUEST — ModelState validation
//    Bez [ApiController] musisz ręcznie sprawdzać ModelState:
//    if (!ModelState.IsValid) return BadRequest(ModelState);
//    Z [ApiController] — automatycznie 400 gdy walidacja nie przejdzie

// 2. BINDING SOURCE INFERENCE
//    Bez [ApiController] musisz pisać [FromBody], [FromRoute], [FromQuery] wszędzie
//    Z [ApiController] — komplex typy z body, prymitywy z route/query

// 3. PROBLEM DETAILS dla błędów 4xx i 5xx (RFC 7807)
// 4. MULTIPART/FORM-DATA inference

// Przykład bez [ApiController]:
public class BezApiControllerController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Test(
        [FromRoute]  int           id,     // MUSI być explicite
        [FromQuery]  string?       szukaj, // MUSI być explicite
        [FromBody]   NowyDto?      dto)    // MUSI być explicite
    {
        if (!ModelState.IsValid)           // MUSI być ręcznie!
            return BadRequest(ModelState);

        return Ok();
    }
}

// Z [ApiController] — inference działa automatycznie:
[ApiController]
[Route("api/[controller]")]
public class ZApiControllerController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Test(
        int     id,       // inference: [FromRoute] (pasuje do {id})
        string? szukaj,   // inference: [FromQuery] (nie ma w route)
        NowyDto dto)      // inference: [FromBody]  (kompleksowy typ)
    {
        // ModelState sprawdzane AUTOMATYCZNIE — brak return BadRequest!
        return Ok();
    }
}

// Wyłączenie automatycznej walidacji gdy potrzebujesz custom zachowania
[ApiController]
[Route("api/custom")]
public class CustomValidController : ControllerBase
{
    [HttpPost]
    public IActionResult Dodaj(NowyDto dto)
    {
        // ModelState jest sprawdzany przed wejściem do akcji przez filtr
        // Jeśli chcesz ręcznie — wyłącz filtr:
        // builder.Services.Configure<ApiBehaviorOptions>(opt =>
        //     opt.SuppressModelStateInvalidFilter = true);

        // Manual check po wyłączeniu:
        if (!ModelState.IsValid)
        {
            var bledy = ModelState
                .Where(m => m.Value!.Errors.Any())
                .ToDictionary(
                    m => m.Key,
                    m => m.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return BadRequest(new { bledy });
        }

        return Ok();
    }
}

record NowyDto(string Nazwa, decimal Cena);
```

---

### 5. Action Results — pełna lista

csharp

```csharp
[ApiController]
[Route("api/rezultaty")]
public class ActionResultsController : ControllerBase
{
    // ===== 2xx SUKCES =====
    [HttpGet("ok")]
    public IActionResult ZwrocOk()
    {
        return Ok(new { data = "wartość" });     // 200 + JSON body
    }

    [HttpGet("ok-typed")]
    public ActionResult<Produkt> ZwrocTyped()
    {
        var p = new Produkt { Id = 1, Nazwa = "Laptop" };
        return p;                                // niejawna konwersja do 200 OK
    }

    [HttpPost("created")]
    public IActionResult ZwrocCreated()
    {
        var nowy = new { Id = 42, Nazwa = "Test" };

        // Created z URL
        return Created("/api/produkty/42", nowy);          // 201 + Location + body

        // CreatedAtAction — generuje URL przez nazwę akcji
        return CreatedAtAction(
            nameof(PobierzPoId),        // nazwa akcji
            new { id = 42 },            // parametry routingu
            nowy);                      // body

        // CreatedAtRoute — przez nazwę route
        return CreatedAtRoute("PobierzProdukt", new { id = 42 }, nowy);
    }

    [HttpPut("{id}")]
    public IActionResult ZwrocNoContent(int id)
        => NoContent();                          // 204 — brak body

    [HttpGet("accepted")]
    public IActionResult ZwrocAccepted()
        => Accepted("/api/jobs/123");            // 202 — operacja async przyjęta

    // ===== 4xx BŁĘDY KLIENTA =====
    [HttpGet("bad-request")]
    public IActionResult ZwrocBadRequest()
    {
        // Różne formy BadRequest
        return BadRequest();                     // 400 — puste
        return BadRequest("Zły format danych");  // 400 + string
        return BadRequest(new                    // 400 + obiekt
        {
            blad   = "Nieprawidłowa cena",
            pole   = "cena",
            wartosc = -100
        });
        return BadRequest(ModelState);           // 400 + błędy walidacji

        // Problem Details (RFC 7807)
        return Problem(
            title:      "Błąd walidacji",
            detail:     "Cena musi być dodatnia",
            statusCode: 400,
            type:       "https://api.sklep.pl/errors/validation");

        return ValidationProblem(ModelState);   // 400 + ValidationProblemDetails
    }

    [HttpGet("not-found")]
    public IActionResult ZwrocNotFound(int id)
    {
        return NotFound();                       // 404
        return NotFound($"Produkt #{id} nie istnieje");
        return NotFound(new { id, blad = "Nie znaleziono" });

        // Problem Details
        return Problem(
            title:      "Nie znaleziono",
            detail:     $"Produkt #{id} nie istnieje",
            statusCode: 404);
    }

    [HttpGet("unauthorized")]
    public IActionResult ZwrocUnauthorized()
        => Unauthorized();                       // 401

    [HttpGet("forbidden")]
    public IActionResult ZwrocForbidden()
        => Forbid();                             // 403

    [HttpGet("conflict")]
    public IActionResult ZwrocConflict()
        => Conflict(new { blad = "Email już istnieje" }); // 409

    // ===== CUSTOM STATUS CODE =====
    [HttpGet("custom/{code:int}")]
    public IActionResult Custom(int code)
    {
        return StatusCode(code);                 // dowolny kod
        return StatusCode(418, "I'm a teapot"); // 418 z body
    }

    // ===== PRZEKIEROWANIA =====
    [HttpGet("redirect")]
    public IActionResult Redirect()
    {
        return RedirectToAction("ZwrocOk");                         // do akcji
        return RedirectToRoute("PobierzProdukt", new { id = 1 });   // do route
        return Redirect("https://google.pl");                        // zewnętrzny
        return LocalRedirect("/api/produkty");                       // lokalny
        return RedirectPermanent("https://nowy.pl");                 // 301
    }

    // ===== PLIKI =====
    [HttpGet("plik")]
    public IActionResult ZwrocPlik()
    {
        byte[] bajty = System.IO.File.ReadAllBytes("raport.pdf");
        return File(bajty, "application/pdf", "raport.pdf");        // download

        return PhysicalFile("/var/files/raport.pdf", "application/pdf");

        return new FileStreamResult(
            System.IO.File.OpenRead("raport.pdf"),
            "application/pdf");
    }

    // ===== CONTENT =====
    [HttpGet("text")]
    public IActionResult ZwrocText()
        => Content("Zwykły tekst", "text/plain");

    [HttpGet("html")]
    public IActionResult ZwrocHtml()
        => Content("<h1>Witaj</h1>", "text/html");

    [HttpGet("json-raw")]
    public IActionResult ZwrocJsonRaw()
        => new JsonResult(new { pole = "wartość" });  // zawsze JSON niezależnie od Accept

    // ===== TypedResults — Minimal API (silne typowanie) =====
    [HttpGet("typed")]
    public Results<Ok<Produkt>, NotFound, BadRequest<string>> Typed(int id)
    {
        if (id < 0) return TypedResults.BadRequest("ID ujemne");
        if (id == 0) return TypedResults.NotFound();
        return TypedResults.Ok(new Produkt { Id = id, Nazwa = "Test" });
    }

    void PobierzPoId() { } // placeholder
}

public class Produkt { public int Id { get; set; } public string Nazwa { get; set; } = ""; }
```

---

### 6. Walidacja modeli

csharp

```csharp
// Data Annotations — atrybuty walidacyjne
public class NowyProduktRequest
{
    [Required(ErrorMessage = "Nazwa jest wymagana")]
    [StringLength(200, MinimumLength = 2,
        ErrorMessage = "Nazwa musi mieć od 2 do 200 znaków")]
    public string Nazwa { get; set; } = "";

    [Required]
    [Range(0.01, 999_999.99, ErrorMessage = "Cena musi być między 0.01 a 999999.99")]
    public decimal Cena { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "KategoriaId musi być > 0")]
    public int KategoriaId { get; set; }

    [Range(0, 10_000)]
    public int StanMagazynu { get; set; } = 0;

    [Url(ErrorMessage = "Nieprawidłowy URL zdjęcia")]
    public string? ZdjecieUrl { get; set; }

    [EmailAddress]
    public string? KontaktEmail { get; set; }

    [RegularExpression(@"^[A-Z]{2}\d{4}$",
        ErrorMessage = "Kod SKU musi mieć format: XX0000")]
    public string? SKU { get; set; }

    [Compare(nameof(Cena), ErrorMessage = "Cena promocyjna musi być <= Cenie")]
    public decimal? CenaPromocyjna { get; set; }

    [MinLength(1, ErrorMessage = "Podaj przynajmniej jeden tag")]
    [MaxLength(10, ErrorMessage = "Maksymalnie 10 tagów")]
    public List<string> Tagi { get; set; } = new();
}

// Własny atrybut walidacyjny
public class NiePrzyszlosctAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value, ValidationContext ctx)
    {
        if (value is DateTime data && data > DateTime.Now)
            return new ValidationResult(
                "Data nie może być z przyszłości",
                new[] { ctx.MemberName! });

        return ValidationResult.Success;
    }
}

public class ZamowienieRequest
{
    [NiePrzyszlosc]
    public DateTime DataDostawy { get; set; }

    // Walidacja między polami — IValidatableObject
}

public class RezerwacjaRequest : IValidatableObject
{
    public DateTime Od { get; set; }
    public DateTime Do { get; set; }
    public int      LiczbaNoc { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (Do <= Od)
            yield return new ValidationResult(
                "Data 'Do' musi być po dacie 'Od'",
                new[] { nameof(Do) });

        if ((Do - Od).Days != LiczbaNoc)
            yield return new ValidationResult(
                "Liczba nocy nie zgadza się z datami",
                new[] { nameof(LiczbaNoc) });
    }
}

// Dostęp do błędów ModelState w kontrolerze
[ApiController]
[Route("api/walidacja")]
public class WalidacjaController : ControllerBase
{
    [HttpPost]
    public IActionResult Dodaj([FromBody] NowyProduktRequest request)
    {
        // Z [ApiController] — automatyczny 400 gdy ModelState.IsValid == false
        // Poniżej tylko gdy wyłączymy SuppressModelStateInvalidFilter

        if (!ModelState.IsValid)
        {
            // Wyciągnij wszystkie błędy
            var bledy = ModelState
                .Where(m => m.Value!.Errors.Count > 0)
                .SelectMany(m => m.Value!.Errors
                    .Select(e => new { Pole = m.Key, Blad = e.ErrorMessage }))
                .ToList();

            return BadRequest(new { bledy });
        }

        return Ok("Walidacja przeszła");
    }

    // Ręczne dodawanie błędów do ModelState
    [HttpPost("custom")]
    public async Task<IActionResult> Custom([FromBody] NowyProduktRequest req,
        [FromServices] IProduktSerwis serwis)
    {
        // Sprawdź unikalność nazwy — logika biznesowa, nie atrybuty
        bool nazwaZajeta = await serwis.CzyNazwaZajetaAsync(req.Nazwa);
        if (nazwaZajeta)
            ModelState.AddModelError(nameof(req.Nazwa),
                $"Produkt o nazwie '{req.Nazwa}' już istnieje");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);  // RFC 7807 format

        return Ok();
    }
}
```

---

### 7. Filtry akcji

csharp

```csharp
// Filtry — kod wykonywany przed/po akcji
// Kolejność: Authorization → Resource → Action → Result → Exception

// Własny filtr akcji
public class LogujAkcjeAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var kontroler = context.Controller.GetType().Name;
        var akcja     = context.ActionDescriptor.DisplayName;

        Console.WriteLine($"→ {kontroler}.{akcja}");
        Console.WriteLine($"  Parametry: {string.Join(", ",
            context.ActionArguments.Select(a => $"{a.Key}={a.Value}"))}");
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        var kod = (context.Result as ObjectResult)?.StatusCode ?? 200;
        Console.WriteLine($"← Status: {kod}");

        if (context.Exception != null)
            Console.WriteLine($"  Wyjątek: {context.Exception.Message}");
    }
}

// Filtr autoryzacji — sprawdza API Key
public class ApiKeyFilter : IActionFilter
{
    private readonly string _klucz;

    public ApiKeyFilter(IConfiguration config)
        => _klucz = config["ApiKey"]!;

    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (!ctx.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var key)
            || key != _klucz)
        {
            ctx.Result = new UnauthorizedObjectResult(
                new { blad = "Nieprawidłowy klucz API" });
        }
    }

    public void OnActionExecuted(ActionExecutedContext ctx) { }
}

// Async filtr
public class ValidacjaIdFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Przed akcją
        if (context.ActionArguments.TryGetValue("id", out object? idObj)
            && idObj is int id && id <= 0)
        {
            context.Result = new BadRequestObjectResult(
                new { blad = "ID musi być > 0" });
            return;  // nie wywołuj akcji!
        }

        // Wywołaj akcję
        var executed = await next();

        // Po akcji
        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            Console.WriteLine($"Nieobsłużony błąd: {executed.Exception.Message}");
        }
    }
}

// Rejestracja filtrów
// Globalnie (dla wszystkich kontrolerów):
builder.Services.AddControllers(opt =>
{
    opt.Filters.Add<LogujAkcjeAttribute>();
    opt.Filters.Add(new ApiKeyFilter(/* ... */));
});

// Na kontrolerze:
[ApiController]
[LogujAkcje]           // filtr jako atrybut
[ServiceFilter(typeof(ApiKeyFilter))]  // filtr z DI
[Route("api/[controller]")]
public class ZFiltramiController : ControllerBase
{
    // Na konkretnej akcji:
    [HttpGet]
    [LogujAkcje]
    [ServiceFilter(typeof(ValidacjaIdFilter))]
    public IActionResult Test() => Ok();

    // Wyłącz globalny filtr dla tej akcji
    [HttpGet("wylaczony")]
    [SkipStatusCodePages]
    public IActionResult BezFiltrow() => Ok();
}
```

---

### 8. Praktyczny przykład — kompletny kontroler

csharp

```csharp
// Kompletny kontroler CRUD z walidacją, filtrami, HATEOAS

[ApiController]
[Route("api/v1/produkty")]
[Produces("application/json")]
public class KompletnyProduktyController : ControllerBase
{
    private readonly IProduktSerwis _serwis;
    private readonly ILogger<KompletnyProduktyController> _logger;

    public KompletnyProduktyController(
        IProduktSerwis serwis,
        ILogger<KompletnyProduktyController> logger)
    {
        _serwis = serwis;
        _logger = logger;
    }

    /// <summary>Pobierz listę produktów z filtrami i paginacją</summary>
    [HttpGet(Name = "ListaProduktow")]
    [ProducesResponseType(typeof(StronicowanaOdpowiedz<ProduktListDto>), 200)]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<StronicowanaOdpowiedz<ProduktListDto>>> PobierzListe(
        [FromQuery] ProduktFiltrDto filtr,
        CancellationToken ct)
    {
        _logger.LogInformation("Pobieranie listy produktów. Filtr: {@Filtr}", filtr);

        var (dane, lacznie) = await _serwis.SzukajAsync(filtr, ct);

        var odpowiedz = new StronicowanaOdpowiedz<ProduktListDto>
        {
            Dane        = dane,
            Strona      = filtr.Strona,
            Rozmiar     = filtr.Rozmiar,
            LacznaIlosc = lacznie,
            Links       = BudujLinkiListy(filtr, lacznie)
        };

        return Ok(odpowiedz);
    }

    /// <summary>Pobierz szczegóły produktu</summary>
    [HttpGet("{id:int}", Name = "SzczegolyProduktu")]
    [ProducesResponseType(typeof(ProduktDetailDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<ProduktDetailDto>> PobierzSzczegoly(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var produkt = await _serwis.PobierzPoIdAsync(id, ct);

        if (produkt is null)
        {
            _logger.LogWarning("Produkt #{Id} nie znaleziony", id);
            return Problem(
                title:      "Produkt nie znaleziony",
                detail:     $"Produkt o id={id} nie istnieje",
                statusCode: 404,
                instance:   Request.Path);
        }

        var odpowiedz = new ProduktDetailDto(produkt)
        {
            Links = new List<Link>
            {
                new(Url.Link("SzczegolyProduktu", new { id })!, "self",      "GET"),
                new(Url.Link("AktualizujProdukt", new { id })!, "update",    "PUT"),
                new(Url.Link("UsunProdukt",        new { id })!, "delete",   "DELETE"),
                new(Url.Link("ListaProduktow",     null)!,       "products", "GET"),
            }
        };

        return Ok(odpowiedz);
    }

    /// <summary>Utwórz nowy produkt</summary>
    [HttpPost(Name = "UtworzProdukt")]
    [ProducesResponseType(typeof(ProduktDetailDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<ProduktDetailDto>> Utworz(
        [FromBody] NowyProduktRequest request,
        CancellationToken ct)
    {
        // Sprawdź unikalność nazwy
        if (await _serwis.CzyNazwaZajetaAsync(request.Nazwa, ct))
        {
            ModelState.AddModelError(nameof(request.Nazwa),
                $"Produkt '{request.Nazwa}' już istnieje");
            return ValidationProblem(ModelState);
        }

        int id = await _serwis.DodajAsync(request, ct);
        var utworzony = await _serwis.PobierzPoIdAsync(id, ct);

        _logger.LogInformation("Utworzono produkt #{Id}: {Nazwa}", id, request.Nazwa);

        return CreatedAtRoute(
            "SzczegolyProduktu",
            new { id },
            new ProduktDetailDto(utworzony!));
    }

    /// <summary>Zastąp cały produkt</summary>
    [HttpPut("{id:int}", Name = "AktualizujProdukt")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Aktualizuj(
        [FromRoute] int                  id,
        [FromBody]  ZastapProduktRequest request,
        CancellationToken                ct)
    {
        bool ok = await _serwis.ZastapAsync(id, request, ct);

        if (!ok) return Problem(
            title: "Nie znaleziono", statusCode: 404,
            detail: $"Produkt #{id} nie istnieje");

        _logger.LogInformation("Zaktualizowano produkt #{Id}", id);
        return NoContent();
    }

    /// <summary>Usuń produkt</summary>
    [HttpDelete("{id:int}", Name = "UsunProdukt")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> Usun(
        [FromRoute] int id,
        CancellationToken ct)
    {
        try
        {
            bool ok = await _serwis.UsunAsync(id, ct);
            if (!ok) return Problem(title: "Nie znaleziono", statusCode: 404,
                detail: $"Produkt #{id} nie istnieje");

            _logger.LogInformation("Usunięto produkt #{Id}", id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Nie można usunąć", statusCode: 409,
                detail: ex.Message);
        }
    }

    // Helpers
    private List<Link> BudujLinkiListy(ProduktFiltrDto filtr, int lacznie)
    {
        var links = new List<Link>
        {
            new(Url.Link("ListaProduktow", new { filtr.Strona, filtr.Rozmiar })!, "self", "GET"),
            new(Url.Link("UtworzProdukt", null)!, "create", "POST")
        };

        int totalPages = (int)Math.Ceiling((double)lacznie / filtr.Rozmiar);

        if (filtr.Strona > 1)
            links.Add(new(Url.Link("ListaProduktow",
                new { strona = filtr.Strona - 1, filtr.Rozmiar })!, "prev", "GET"));

        if (filtr.Strona < totalPages)
            links.Add(new(Url.Link("ListaProduktow",
                new { strona = filtr.Strona + 1, filtr.Rozmiar })!, "next", "GET"));

        return links;
    }
}

// DTOs i modele pomocnicze
public class StronicowanaOdpowiedz<T>
{
    public IReadOnlyList<T> Dane        { get; set; } = new List<T>();
    public int              Strona      { get; set; }
    public int              Rozmiar     { get; set; }
    public int              LacznaIlosc { get; set; }
    public List<Link>       Links       { get; set; } = new();
    public int LaczneStrony => (int)Math.Ceiling((double)LacznaIlosc / Rozmiar);
}

public class ProduktDetailDto
{
    public int     Id           { get; set; }
    public string  Nazwa        { get; set; } = "";
    public decimal Cena         { get; set; }
    public string  Kategoria    { get; set; } = "";
    public int     StanMagazynu { get; set; }
    public List<Link> Links     { get; set; } = new();

    public ProduktDetailDto() { }
    public ProduktDetailDto(object? src) { /* mapowanie */ }
}

public record ProduktListDto(int Id, string Nazwa, decimal Cena, string Kategoria);
public record Link(string Href, string Rel, string Method);

public class ProduktFiltrDto
{
    [FromQuery] public string?  Kategoria { get; set; }
    [FromQuery] public decimal? MinCena   { get; set; }
    [FromQuery] public decimal? MaxCena   { get; set; }
    [FromQuery] public int      Strona    { get; set; } = 1;
    [FromQuery] public int      Rozmiar   { get; set; } = 20;
}

public class NowyProduktRequest
{
    [Required][StringLength(200, MinimumLength = 2)]
    public string  Nazwa        { get; set; } = "";
    [Range(0.01, 999999)] public decimal Cena { get; set; }
    [Range(1, int.MaxValue)] public int  KategoriaId { get; set; }
    [Range(0, 10000)]        public int  StanMagazynu { get; set; }
}

public class ZastapProduktRequest : NowyProduktRequest { }

// Interfejsy serwisów (stubs)
public interface IProduktSerwis
{
    Task<(IReadOnlyList<ProduktListDto> Dane, int Lacznie)> SzukajAsync(
        ProduktFiltrDto filtr, CancellationToken ct);
    Task<ProduktDetailDto?> PobierzPoIdAsync(int id, CancellationToken ct);
    Task<int> DodajAsync(NowyProduktRequest req, CancellationToken ct);
    Task<bool> ZastapAsync(int id, ZastapProduktRequest req, CancellationToken ct);
    Task<bool> UsunAsync(int id, CancellationToken ct);
    Task<bool> CzyNazwaZajetaAsync(string nazwa, CancellationToken ct);
}
```

---

### Typowe pytania rekrutacyjne

**"Co robi atrybut `[ApiController]`?"** Dodaje cztery automatyzacje: (1) Automatic model validation — 400 Bad Request bez ręcznego `if (!ModelState.IsValid)`, (2) Binding source inference — `[FromBody]` dla kompleksowych typów, `[FromRoute/Query]` dla prymitywów bez pisania atrybutów, (3) Problem Details — RFC 7807 format dla błędów 4xx/5xx, (4) Multipart form-data inference. Można wyłączyć każdą z nich przez `ApiBehaviorOptions`.

**"Jaka różnica między `Controller` a `ControllerBase`?"** `ControllerBase` — bazowa klasa dla API, zawiera HttpContext, Request, Response, ModelState, metody wynikowe (Ok, NotFound, ...). `Controller` — dziedziczy po `ControllerBase` i dodaje wsparcie dla Razor Views (View(), PartialView(), Json(), ...). Dla REST API zawsze używaj `ControllerBase` — Controller jest cięższy i dodaje funkcjonalność której nie potrzebujesz.

**"Jak działa Model Binding — skąd ASP.NET wie skąd brać wartości?"** Bez `[ApiController]`: sprawdza kolejno RouteData → QueryString → Body (tylko jeśli `[FromBody]`). Z `[ApiController]`: kompleksowe typy (klasy) binduje z Body, prymitywy i stringi z Route/Query w zależności od obecności w szablonie routingu. Można nadpisać przez explicite atrybuty `[FromRoute]`, `[FromQuery]`, `[FromBody]`, `[FromHeader]`, `[FromForm]`, `[FromServices]`.

**"Kiedy `IActionResult` a kiedy `ActionResult<T>`?"** `IActionResult` — elastyczny, możesz zwrócić dowolny typ wyniku, ale Swagger nie może wywnioskować typu danych odpowiedzi. `ActionResult<T>` — silnie typowany, Swagger automatycznie dokumentuje typ odpowiedzi 200 OK, możesz zwrócić `T` bezpośrednio (niejawna konwersja) lub `IActionResult` (NotFound, BadRequest). Zalecane: `ActionResult<T>` dla dokumentacji i type safety, chyba że akcja zwraca różne typy zależnie od przypadku.

---

Następny temat to **Dependency Injection w ASP.NET Core** albo napisz "dalej" 🚀