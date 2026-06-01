using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;

namespace _06_ASPNetCore_RestAPI;

// ============================================================
// DTOs
// ============================================================

public record ProduktDto(int Id, string Nazwa, decimal Cena, string Kategoria, bool Aktywny);

public class NowyProduktDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Nazwa { get; set; } = "";

    [Range(0.01, 999999.99)]
    public decimal Cena { get; set; }

    [Required]
    [StringLength(50)]
    public string Kategoria { get; set; } = "";
}

public class AktualizujProduktDto
{
    [StringLength(100, MinimumLength = 2)]
    public string? Nazwa { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? Cena { get; set; }

    public bool? Aktywny { get; set; }
}

// ============================================================
// ACTION FILTER
// ============================================================

public class LogowanieAkcjiFilter : ActionFilterAttribute
{
    // Wykona się przed akcją
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Console.WriteLine($"[Filter] OnActionExecuting: {context.ActionDescriptor.DisplayName}");
    }

    // Wykona się po akcji
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        Console.WriteLine($"[Filter] OnActionExecuted: status={context.HttpContext.Response.StatusCode}");
    }

    // Asynchroniczna wersja
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        Console.WriteLine($"[Filter] Before async action");
        var result = await next();
        Console.WriteLine($"[Filter] After async action, exception={result.Exception?.Message ?? "none"}");
    }
}

public class WalidacjaModelu : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new UnprocessableEntityObjectResult(context.ModelState);
        }
    }
}

// ============================================================
// PRODUKTY CONTROLLER — PEŁNE CRUD + HATEOAS
// ============================================================

/// <summary>
/// Zarządzanie produktami — pełne CRUD z HATEOAS
/// </summary>
[ApiController]                    // Auto ModelState, binding inference, Problem Details
[Route("api/[controller]")]
[Produces("application/json")]
[LogowanieAkcjiFilter]
public class ProduktyController : ControllerBase
{
    // In-memory store dla demonstracji
    private static readonly List<ProduktDto> _produkty =
    [
        new(1, "Laptop Dell XPS", 4999.99m, "Elektronika", true),
        new(2, "Mysz bezprzewodowa", 149.99m, "Akcesoria", true),
        new(3, "Monitor 27\"", 1299.99m, "Elektronika", true),
        new(4, "Klawiatura mechaniczna", 299.99m, "Akcesoria", false),
    ];
    private static int _nextId = 5;

    private readonly ILogger<ProduktyController> _logger;

    public ProduktyController(ILogger<ProduktyController> logger)
    {
        _logger = logger;
    }

    /// <summary>Pobiera listę produktów z opcjonalnym filtrowaniem</summary>
    /// <param name="kategoria">Opcjonalny filtr kategorii</param>
    /// <param name="tylkoAktywne">Czy zwracać tylko aktywne produkty</param>
    /// <param name="strona">Numer strony (domyślnie 1)</param>
    /// <param name="rozmiar">Rozmiar strony (domyślnie 10)</param>
    [HttpGet]
    [ProducesResponseType(typeof(StronicowanaHateoas<ProduktDto>), 200)]
    public IActionResult PobierzWszystkie(
        [FromQuery] string? kategoria,
        [FromQuery] bool tylkoAktywne = false,
        [FromQuery] int strona = 1,
        [FromQuery] int rozmiar = 10)
    {
        var query = _produkty.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(kategoria))
            query = query.Where(p => p.Kategoria.Equals(kategoria, StringComparison.OrdinalIgnoreCase));

        if (tylkoAktywne)
            query = query.Where(p => p.Aktywny);

        var lacznie = query.Count();
        var elementy = query.Skip((strona - 1) * rozmiar).Take(rozmiar).ToList();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var wynik = new StronicowanaHateoas<ProduktDto>(
            elementy, strona, rozmiar, lacznie,
            HateoasBuilder.BudujLinkiKolekcji(baseUrl, strona, rozmiar, lacznie));

        return Ok(wynik);
    }

    /// <summary>Pobiera produkt po ID</summary>
    /// <param name="id">ID produktu</param>
    [HttpGet("{id:int}", Name = "PobierzProdukt")]
    [ProducesResponseType(typeof(HateoasOdpowiedz<ProduktDto>), 200)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), 404)]
    public IActionResult PobierzPoId([FromRoute] int id)
    {
        var produkt = _produkty.FirstOrDefault(p => p.Id == id);
        if (produkt is null)
            return NotFound(new { message = $"Produkt {id} nie istnieje" });

        // ETag
        var etag = ETagHelper.Generuj(produkt);
        Response.Headers.ETag = etag;

        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch)
            && ETagHelper.CzyPasuje(ifNoneMatch, etag))
            return StatusCode(304);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var wynik = new HateoasOdpowiedz<ProduktDto>(produkt,
            HateoasBuilder.BudujLinkiProduktu(id, baseUrl));

        return Ok(wynik);
    }

    /// <summary>Tworzy nowy produkt</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProduktDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public IActionResult Stworz([FromBody] NowyProduktDto dto)
    {
        // [ApiController] automatycznie zwraca 400 gdy ModelState.IsValid == false
        var nowyProdukt = new ProduktDto(_nextId++, dto.Nazwa, dto.Cena, dto.Kategoria, true);
        _produkty.Add(nowyProdukt);

        _logger.LogInformation("Stworzono produkt {Id}: {Nazwa}", nowyProdukt.Id, nowyProdukt.Nazwa);

        // CreatedAtRoute zwraca 201 z Location header
        return CreatedAtRoute("PobierzProdukt", new { id = nowyProdukt.Id }, nowyProdukt);
    }

    /// <summary>Aktualizuje produkt (pełna zamiana)</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProduktDto), 200)]
    [ProducesResponseType(404)]
    public IActionResult Aktualizuj([FromRoute] int id, [FromBody] NowyProduktDto dto)
    {
        var index = _produkty.FindIndex(p => p.Id == id);
        if (index < 0) return NotFound();

        var zaktualizowany = new ProduktDto(id, dto.Nazwa, dto.Cena, dto.Kategoria, true);
        _produkty[index] = zaktualizowany;
        return Ok(zaktualizowany);
    }

    /// <summary>Częściowa aktualizacja produktu</summary>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(ProduktDto), 200)]
    [ProducesResponseType(404)]
    public IActionResult PartialUpdate([FromRoute] int id, [FromBody] AktualizujProduktDto dto)
    {
        var index = _produkty.FindIndex(p => p.Id == id);
        if (index < 0) return NotFound();

        var stary = _produkty[index];
        var zaktualizowany = stary with
        {
            Nazwa = dto.Nazwa ?? stary.Nazwa,
            Cena = dto.Cena ?? stary.Cena,
            Aktywny = dto.Aktywny ?? stary.Aktywny
        };
        _produkty[index] = zaktualizowany;
        return Ok(zaktualizowany);
    }

    /// <summary>Usuwa produkt</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult Usun([FromRoute] int id)
    {
        var produkt = _produkty.FirstOrDefault(p => p.Id == id);
        if (produkt is null) return NotFound();

        _produkty.Remove(produkt);
        return NoContent(); // 204
    }

    // HEAD — jak GET ale bez ciała odpowiedzi
    [HttpHead("{id:int}")]
    public IActionResult Sprawdz([FromRoute] int id)
    {
        var istnieje = _produkty.Any(p => p.Id == id);
        return istnieje ? Ok() : NotFound();
    }

    // OPTIONS — informuje o dostępnych metodach
    [HttpOptions]
    public IActionResult Opcje()
    {
        Response.Headers.Allow = "GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS";
        return Ok();
    }
}

// ============================================================
// MODEL BINDING CONTROLLER — WSZYSTKIE [FROM*]
// ============================================================

[ApiController]
[Route("api/[controller]")]
public class ModelBindingController : ControllerBase
{
    // [FromRoute] — z segmentu URL
    [HttpGet("route/{id:int}/{name:alpha}")]
    public IActionResult ZRoute([FromRoute] int id, [FromRoute] string name)
        => Ok(new { id, name, zrodlo = "FromRoute" });

    // [FromQuery] — z query string
    [HttpGet("query")]
    public IActionResult ZQuery(
        [FromQuery] string? szukaj,
        [FromQuery] int strona = 1,
        [FromQuery(Name = "sort")] string? sortowanie = "asc")
        => Ok(new { szukaj, strona, sortowanie, zrodlo = "FromQuery" });

    // [FromBody] — z ciała żądania (JSON)
    [HttpPost("body")]
    public IActionResult ZBody([FromBody] NowyProduktDto dto)
        => Ok(new { dto.Nazwa, dto.Cena, zrodlo = "FromBody" });

    // [FromHeader] — z nagłówka HTTP
    [HttpGet("header")]
    public IActionResult ZHeader(
        [FromHeader(Name = "X-Correlation-Id")] string? correlationId,
        [FromHeader(Name = "Accept-Language")] string? jezyk)
        => Ok(new { correlationId, jezyk, zrodlo = "FromHeader" });

    // [FromServices] — wstrzyknięty serwis (alternatywa do konstruktora)
    [HttpGet("service")]
    public IActionResult ZService([FromServices] ILogger<ModelBindingController> logger)
    {
        logger.LogInformation("FromServices demo");
        return Ok(new { message = "Serwis wstrzyknięty przez [FromServices]", zrodlo = "FromServices" });
    }

    // [FromForm] — z formularza multipart
    [HttpPost("form")]
    [Consumes("multipart/form-data")]
    public IActionResult ZForm([FromForm] string nazwa, [FromForm] IFormFile? plik)
        => Ok(new { nazwa, plikNazwa = plik?.FileName, zrodlo = "FromForm" });
}

// ============================================================
// ACTION RESULTS CONTROLLER — WSZYSTKIE TYPY WYNIKÓW
// ============================================================

[ApiController]
[Route("api/[controller]")]
public class ActionResultsController : ControllerBase
{
    [HttpGet("ok")] public IActionResult ZwrocOk() => Ok(new { status = "OK", kod = 200 });
    [HttpGet("created")] public IActionResult ZwrocCreated() => Created("/api/produkty/99", new { id = 99 });
    [HttpGet("createdatroute")] public IActionResult ZwrocCreatedAtRoute() => CreatedAtRoute("PobierzProdukt", new { id = 1 }, new { id = 1 });
    [HttpGet("nocontent")] public IActionResult ZwrocNoContent() => NoContent();
    [HttpGet("accepted")] public IActionResult ZwrocAccepted() => Accepted("/api/jobs/123", new { jobId = "123" });
    [HttpGet("badrequest")] public IActionResult ZwrocBadRequest() => BadRequest(new { error = "Nieprawidłowe dane" });
    [HttpGet("notfound")] public IActionResult ZwrocNotFound() => NotFound(new { error = "Nie znaleziono zasobu" });
    [HttpGet("conflict")] public IActionResult ZwrocConflict() => Conflict(new { error = "Zasób już istnieje" });
    [HttpGet("statuscode/{code:int}")] public IActionResult ZwrocStatusCode(int code) => StatusCode(code);
    [HttpGet("problem")] public IActionResult ZwrocProblem() => Problem("Coś poszło nie tak", statusCode: 500, title: "Server Error");
    [HttpGet("validationproblem")] public IActionResult ZwrocValidationProblem()
    {
        ModelState.AddModelError("Pole", "Pole jest wymagane");
        return ValidationProblem();
    }
    [HttpGet("content")] public IActionResult ZwrocContent() => Content("<h1>HTML</h1>", "text/html");

    // TypedResults — silnie typowane (kompiluje typy odpowiedzi)
    [HttpGet("typed/{id:int}")]
    public Results<Ok<ProduktDto>, NotFound> TypedResult(int id)
    {
        if (id <= 0) return TypedResults.NotFound();
        return TypedResults.Ok(new ProduktDto(id, "Produkt", 9.99m, "Kat", true));
    }
}

// ============================================================
// ROUTING CONTROLLER — WSZYSTKIE OGRANICZENIA TRASY
// ============================================================

[ApiController]
[Route("api/[controller]")]
public class RoutingController : ControllerBase
{
    // Ograniczenia typów
    [HttpGet("int/{id:int}")] public IActionResult IntConstraint([FromRoute] int id) => Ok(new { id, typ = "int" });
    [HttpGet("guid/{id:guid}")] public IActionResult GuidConstraint([FromRoute] Guid id) => Ok(new { id, typ = "guid" });
    [HttpGet("datetime/{data:datetime}")] public IActionResult DateConstraint([FromRoute] DateTime data) => Ok(new { data, typ = "datetime" });
    [HttpGet("bool/{flaga:bool}")] public IActionResult BoolConstraint([FromRoute] bool flaga) => Ok(new { flaga, typ = "bool" });

    // Ograniczenia ciągów
    [HttpGet("alpha/{name:alpha}")] public IActionResult AlphaConstraint([FromRoute] string name) => Ok(new { name, typ = "alpha" });
    [HttpGet("length/{code:length(3,10)}")] public IActionResult LengthConstraint([FromRoute] string code) => Ok(new { code, typ = "length(3,10)" });
    [HttpGet("minlength/{text:minlength(3)}")] public IActionResult MinLengthConstraint([FromRoute] string text) => Ok(new { text, typ = "minlength(3)" });
    [HttpGet("maxlength/{text:maxlength(10)}")] public IActionResult MaxLengthConstraint([FromRoute] string text) => Ok(new { text, typ = "maxlength(10)" });

    // Ograniczenia zakresów
    [HttpGet("min/{val:min(1)}")] public IActionResult MinConstraint([FromRoute] int val) => Ok(new { val, typ = "min(1)" });
    [HttpGet("max/{val:max(100)}")] public IActionResult MaxConstraint([FromRoute] int val) => Ok(new { val, typ = "max(100)" });
    [HttpGet("range/{val:range(1,100)}")] public IActionResult RangeConstraint([FromRoute] int val) => Ok(new { val, typ = "range(1,100)" });

    // Regex
    [HttpGet("regex/{kod:regex(^[[A-Z]]{{2}}\\d{{4}}$)}")]
    public IActionResult RegexConstraint([FromRoute] string kod) => Ok(new { kod, typ = "regex" });

    // Optional + default
    [HttpGet("optional/{id:int?}")] public IActionResult Optional([FromRoute] int? id) => Ok(new { id, hasValue = id.HasValue });
    [HttpGet("default/{strona:int=1}")] public IActionResult Default([FromRoute] int strona) => Ok(new { strona });
}

// ============================================================
// WALIDACJA CONTROLLER
// ============================================================

[ApiController]
[Route("api/[controller]")]
public class WalidacjaController : ControllerBase
{
    private readonly ILogger<WalidacjaController> _logger;

    public WalidacjaController(ILogger<WalidacjaController> logger)
    {
        _logger = logger;
    }

    // [ApiController] automatycznie waliduje ModelState i zwraca 400
    [HttpPost("rejestruj")]
    [ProducesResponseType(typeof(RejestrujKlientaDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public IActionResult Rejestruj([FromBody] RejestrujKlientaDto dto)
    {
        // ModelState.IsValid gwarantowane przez [ApiController]
        return Ok(new
        {
            message = "Rejestracja pomyślna",
            email = dto.Email,
            imie = dto.Imie,
            nazwisko = dto.Nazwisko
        });
    }

    // Ręczna walidacja ModelState
    [HttpPost("reczna")]
    public IActionResult RecznaWalidacja([FromBody] NowyProduktDto dto)
    {
        if (!ModelState.IsValid)
        {
            // ValidationProblem tworzy RFC 7807 response
            return ValidationProblem(ModelState);
        }

        // Własny błąd
        if (dto.Nazwa.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(dto.Nazwa), "Nazwa nie może zawierać słowa 'test'");
            return ValidationProblem(ModelState);
        }

        return Ok(dto);
    }

    // FluentValidation — validator wstrzyknięty przez DI
    [HttpPost("fluent")]
    public async Task<IActionResult> FluentWalidacja(
        [FromBody] RejestrujKlientaDto dto,
        [FromServices] IValidator<RejestrujKlientaDto> validator)
    {
        var wynik = await validator.ValidateAsync(dto);
        if (!wynik.IsValid)
        {
            var bledy = wynik.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return BadRequest(new { bledy });
        }
        return Ok(new { message = "FV walidacja przeszła", email = dto.Email });
    }
}
