### REST w ASP.NET Core

REST (Representational State Transfer) to **styl architektoniczny** — zbiór zasad projektowania API, nie protokół ani standard.

---

### 1. Zasady REST — 6 ograniczeń

csharp

```csharp
// 1. CLIENT-SERVER — separacja klienta od serwera
// Klient nie wie jak dane są przechowywane
// Serwer nie wie jak dane są wyświetlane

// 2. STATELESS — bezstanowość
// Każdy request zawiera WSZYSTKIE potrzebne informacje
// Serwer nie przechowuje stanu sesji klienta
// ŹLE:
// GET /zamowienia/nastepna-strona  ← serwer pamięta którą stronę byłeś
// DOBRZE:
// GET /zamowienia?strona=2&rozmiar=20  ← wszystko w requeście

// 3. CACHEABLE — możliwość cache'owania
// Odpowiedzi mogą być oznaczone jako cache'owalne

// 4. UNIFORM INTERFACE — jednolity interfejs
// Zasoby identyfikowane przez URI
// Operacje przez HTTP methods
// Reprezentacje (JSON, XML)

// 5. LAYERED SYSTEM — warstwowa architektura
// Klient nie wie czy mówi z serwerem czy proxy/load balancer

// 6. CODE ON DEMAND (opcjonalne)
// Serwer może wysyłać wykonywalny kod (np. JavaScript)
```

---

### 2. Zasoby i URI — projektowanie

csharp

```csharp
// ZASÓB = rzeczownik, nie czasownik!
// URI identyfikuje CO, nie JAK

// ŹLE — czasowniki w URI (RPC style)
// POST /pobierzProdukty
// POST /aktualizujKlienta
// GET  /usunZamowienie/5
// POST /zamknijKonto

// DOBRZE — rzeczowniki, hierarchia zasobów
// GET    /produkty          — lista produktów
// GET    /produkty/42       — produkt #42
// POST   /produkty          — utwórz nowy produkt
// PUT    /produkty/42       — zastąp produkt #42
// PATCH  /produkty/42       — częściowo zaktualizuj
// DELETE /produkty/42       — usuń produkt #42

// Zagnieżdżone zasoby — relacje
// GET /klienci/5/zamowienia          — zamówienia klienta #5
// GET /klienci/5/zamowienia/101      — zamówienie #101 klienta #5
// POST /klienci/5/zamowienia         — utwórz zamówienie dla klienta #5
// GET /zamowienia/101/pozycje        — pozycje zamówienia #101
// GET /zamowienia/101/pozycje/3      — pozycja #3 zamówienia #101

// Nie zagnieżdżaj głębiej niż 2-3 poziomy!
// Zbyt głęboko: /klienci/5/zamowienia/101/pozycje/3/produkt/kategoria — ZŁE

// Filtrowanie, sortowanie, paginacja — query parameters
// GET /produkty?kategoria=IT&minCena=100&maxCena=5000
// GET /produkty?sortuj=cena&kierunek=desc
// GET /produkty?strona=2&rozmiar=20
// GET /produkty?pola=id,nazwa,cena     — sparse fieldsets

// Akcje które nie pasują do CRUD — sub-resource
// POST /zamowienia/101/anuluj         — anuluj zamówienie
// POST /konta/5/aktywuj               // aktywuj konto
// POST /produkty/42/zdjecia           — dodaj zdjęcie produktu
// DELETE /produkty/42/zdjecia/7       — usuń zdjęcie #7

// Wersjonowanie API
// URI versioning:    /api/v1/produkty  /api/v2/produkty
// Header versioning: X-API-Version: 2
// Query param:       /api/produkty?version=2
// Accept header:     Accept: application/vnd.sklep.v2+json
```

---

### 3. HTTP Metody — semantyka

csharp

```csharp
// IDEMPOTENTNOŚĆ — wywołanie N razy = ten sam efekt co wywołanie raz
// BEZPIECZEŃSTWO — nie modyfikuje zasobu

// GET    — bezpieczna, idempotentna — odczyt
// HEAD   — bezpieczna, idempotentna — tylko nagłówki (bez body)
// OPTIONS— bezpieczna, idempotentna — jakie metody obsługuje
// PUT    — NIEbezpieczna, idempotentna — zastąp cały zasób
// DELETE — NIEbezpieczna, idempotentna — usuń zasób
// POST   — NIEbezpieczna, NIEidempotentna — utwórz / akcja
// PATCH  — NIEbezpieczna, NIEidempotentna — częściowa aktualizacja

[ApiController]
[Route("api/produkty")]
public class ProduktyController : ControllerBase
{
    private readonly IProduktSerwis _serwis;
    public ProduktyController(IProduktSerwis serwis) => _serwis = serwis;

    // GET /api/produkty
    // GET /api/produkty?kategoria=IT&minCena=100&strona=1&rozmiar=20
    [HttpGet]
    [ProducesResponseType(typeof(StronicowanaLista<ProduktListDto>), 200)]
    public async Task<IActionResult> PobierzWszystkie(
        [FromQuery] ProduktFiltrDto filtr,
        CancellationToken ct)
    {
        var wynik = await _serwis.SzukajAsync(filtr, ct);
        return Ok(wynik);
    }

    // GET /api/produkty/42
    [HttpGet("{id:int}", Name = "PobierzProdukt")]
    [ProducesResponseType(typeof(ProduktDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PobierzPoId(int id, CancellationToken ct)
    {
        var produkt = await _serwis.PobierzPoIdAsync(id, ct);
        return produkt is null ? NotFound() : Ok(produkt);
    }

    // POST /api/produkty — utwórz nowy zasób
    [HttpPost]
    [ProducesResponseType(typeof(ProduktDetailDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<IActionResult> Dodaj(
        [FromBody] NowyProduktDto dto,
        CancellationToken ct)
    {
        int id = await _serwis.DodajAsync(dto, ct);
        var utworzony = await _serwis.PobierzPoIdAsync(id, ct);

        // 201 Created + Location header wskazujący na nowy zasób
        return CreatedAtRoute("PobierzProdukt", new { id }, utworzony);
        // Location: /api/produkty/42
    }

    // PUT /api/produkty/42 — ZASTĄP CAŁY zasób (idempotentne)
    // Klient wysyła KOMPLETNĄ reprezentację
    [HttpPut("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Zastap(
        int id,
        [FromBody] ZastapProduktDto dto,  // WSZYSTKIE pola wymagane!
        CancellationToken ct)
    {
        bool ok = await _serwis.ZastapAsync(id, dto, ct);
        return ok ? NoContent() : NotFound();
    }

    // PATCH /api/produkty/42 — częściowa aktualizacja
    // RFC 6902 — JSON Patch
    [HttpPatch("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CzesciowaAktualizacja(
        int id,
        [FromBody] Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<AktualizujProduktDto> patch,
        CancellationToken ct)
    {
        var produkt = await _serwis.PobierzDoEdycjiAsync(id, ct);
        if (produkt is null) return NotFound();

        patch.ApplyTo(produkt, ModelState);  // zastosuj operacje patch
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await _serwis.ZapiszAsync(id, produkt, ct);
        return NoContent();
    }

    // DELETE /api/produkty/42
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Usun(int id, CancellationToken ct)
    {
        bool ok = await _serwis.UsunAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // HEAD /api/produkty/42 — sprawdź czy istnieje bez pobierania body
    [HttpHead("{id:int}")]
    public async Task<IActionResult> SprawdzIstnienie(int id, CancellationToken ct)
    {
        bool istnieje = await _serwis.IstniejeAsync(id, ct);
        return istnieje ? Ok() : NotFound();
        // Response: nagłówki + pusty body
    }
}
```

---

### 4. HTTP Status Codes — pełna lista

csharp

```csharp
// 2xx — SUKCES
// 200 OK            — GET, PUT (z body), POST (gdy nie tworzysz zasobu)
// 201 Created       — POST, PUT (gdy tworzysz zasób) + Location header
// 202 Accepted      — operacja przyjęta, będzie przetworzona asynchronicznie
// 204 No Content    — PUT, PATCH, DELETE (sukces bez body)
// 206 Partial Content — streaming, range requests

// 3xx — PRZEKIEROWANIA
// 301 Moved Permanently — zmiana URI na stałe
// 302 Found             — tymczasowe przekierowanie
// 304 Not Modified      — cache aktualny (ETag / Last-Modified)
// 307 Temporary Redirect— zachowaj metodę HTTP przy redirect
// 308 Permanent Redirect— jak 301 ale zachowaj metodę HTTP

// 4xx — BŁĘDY KLIENTA
// 400 Bad Request       — nieprawidłowe dane, walidacja
// 401 Unauthorized      — brak/nieprawidłowy token (authentication)
// 403 Forbidden         — brak uprawnień (authorization)
// 404 Not Found         — zasób nie istnieje
// 405 Method Not Allowed— metoda HTTP niedozwolona
// 408 Request Timeout   — klient za wolno wysyła request
// 409 Conflict          — konflikt (np. duplikat, optimistic concurrency)
// 410 Gone              — zasób usunięty na stałe (w odróżnieniu od 404)
// 415 Unsupported Media Type — zły Content-Type (np. XML zamiast JSON)
// 422 Unprocessable Entity  — walidacja biznesowa (dane poprawne składniowo ale nie logicznie)
// 429 Too Many Requests     — rate limiting
// 451 Unavailable For Legal Reasons — zablokowane prawnie

// 5xx — BŁĘDY SERWERA
// 500 Internal Server Error — nieoczekiwany błąd
// 501 Not Implemented       — endpoint niezaimplementowany
// 502 Bad Gateway           — błąd upstream
// 503 Service Unavailable   — serwis niedostępny (maintenance, overload)
// 504 Gateway Timeout       — upstream nie odpowiedział w czasie

// Implementacja spójnych odpowiedzi błędów — RFC 7807 Problem Details
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult NieZnaleziono(string zasob, object id) =>
        Problem(
            title:       "Nie znaleziono",
            detail:      $"{zasob} o id={id} nie istnieje",
            statusCode:  404,
            type:        "https://api.sklep.pl/errors/not-found");

    protected IActionResult KonflikDanych(string szczegoly) =>
        Problem(
            title:       "Konflikt",
            detail:      szczegoly,
            statusCode:  409,
            type:        "https://api.sklep.pl/errors/conflict");

    protected IActionResult BladWalidacjiBiznesowej(string szczegoly) =>
        Problem(
            title:       "Błąd walidacji",
            detail:      szczegoly,
            statusCode:  422,
            type:        "https://api.sklep.pl/errors/validation");
}

// RFC 7807 Problem Details — standardowy format błędu
// {
//   "type": "https://api.sklep.pl/errors/not-found",
//   "title": "Nie znaleziono",
//   "status": 404,
//   "detail": "Produkt o id=42 nie istnieje",
//   "instance": "/api/produkty/42"
// }
```

---

### 5. Routing — atrybuty i konwencje

csharp

```csharp
// Attribute Routing — deklaratywny, czytelny

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]  // api/v1/klienci
public class KlienciController : ControllerBase
{
    // Ograniczenia w routingu — type constraints
    [HttpGet("{id:int:min(1)}")]     // int, minimum 1
    [HttpGet("{slug:alpha}")]        // tylko litery
    [HttpGet("{kod:length(5)}")]     // dokładnie 5 znaków
    [HttpGet("{id:guid}")]           // GUID
    [HttpGet("{data:datetime}")]     // data
    [HttpGet("{cena:decimal}")]      // decimal

    // Wiele tras dla jednego akcji
    [HttpGet("")]
    [HttpGet("lista")]
    [HttpGet("wszystkie")]
    public IActionResult Lista() => Ok("lista klientów");

    // Opcjonalne segmenty
    [HttpGet("{id:int?}")]           // id opcjonalne
    public IActionResult PobierzLubListe(int? id = null) =>
        id.HasValue ? Ok($"klient {id}") : Ok("lista");

    // Catch-all
    [HttpGet("{**sciezka}")]         // przechwytuje /a/b/c/d
    public IActionResult Fallback(string sciezka) => NotFound(sciezka);
}

// Model Binding — skąd pobierać parametry
public class ProduktyV2Controller : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Pobierz(
        [FromRoute]  int id,           // z URI: /produkty/42
        [FromQuery]  string? szukaj,   // z query: ?szukaj=laptop
        [FromHeader] string? apiKey,   // z nagłówka: X-Api-Key: abc
        [FromBody]   FiltrDto? filtr,  // z body (JSON)
        [FromForm]   IFormFile? plik)  // z form-data
        => Ok();

    // [FromServices] — wstrzyknięcie z DI bezpośrednio do akcji
    [HttpGet("raport")]
    public async Task<IActionResult> Raport(
        [FromServices] IRaportSerwis serwis,
        [FromQuery]    DateTime od,
        [FromQuery]    DateTime do_)
        => Ok(await serwis.GenerujAsync(od, do_));
}

// Route Groups — Minimal API (od .NET 7)
var api = app.MapGroup("/api/v1").RequireAuthorization();

var produktyGr = api.MapGroup("/produkty").WithTags("Produkty");
produktyGr.MapGet("/", PobierzProdukty);
produktyGr.MapGet("/{id:int}", PobierzProdukt);
produktyGr.MapPost("/", DodajProdukt).RequireAuthorization("Admin");
```

---

### 6. Content Negotiation i nagłówki

csharp

```csharp
// Content Negotiation — klient mówi w czym chce odpowiedź
// Accept: application/json  → JSON
// Accept: application/xml   → XML
// Accept: text/csv          → CSV

// Konfiguracja w Program.cs
builder.Services.AddControllers(opt =>
{
    opt.RespectBrowserAcceptHeader = true;   // honoruj Accept header
    opt.ReturnHttpNotAcceptable    = true;   // 406 gdy nie obsługujemy formatu
})
.AddXmlSerializerFormatters()               // dodaj XML formatter
.AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Własny formatter — np. CSV
public class CsvOutputFormatter : TextOutputFormatter
{
    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add("text/csv");
        SupportedEncodings.Add(System.Text.Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type) =>
        typeof(IEnumerable<object>).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context,
        System.Text.Encoding selectedEncoding)
    {
        var response = context.HttpContext.Response;
        var sb = new System.Text.StringBuilder();

        if (context.Object is IEnumerable<ProduktListDto> produkty)
        {
            sb.AppendLine("Id,Nazwa,Cena,Kategoria");
            foreach (var p in produkty)
                sb.AppendLine($"{p.Id},{p.Nazwa},{p.Cena},{p.Kategoria}");
        }

        await response.WriteAsync(sb.ToString(), selectedEncoding);
    }
}

// Caching — nagłówki HTTP
[HttpGet("{id:int}")]
[ResponseCache(Duration = 300,              // cache 5 minut
               Location = ResponseCacheLocation.Any,
               VaryByQueryKeys = new[] { "lang" })]
public async Task<IActionResult> PobierzProdukt(int id)
{
    // ETag — fingerprint odpowiedzi
    var produkt = await _serwis.PobierzPoIdAsync(id);
    if (produkt is null) return NotFound();

    string etag = $"\"{produkt.GetHashCode()}\"";
    Response.Headers.ETag = etag;
    Response.Headers.LastModified = produkt.DataModyfikacji.ToString("R");

    // Sprawdź czy klient ma aktualną wersję
    if (Request.Headers.IfNoneMatch == etag)
        return StatusCode(304);  // 304 Not Modified — użyj cache!

    return Ok(produkt);
}
```

---

### 7. HATEOAS — Hypermedia As The Engine Of Application State

csharp

```csharp
// HATEOAS = odpowiedź zawiera linki do powiązanych akcji
// Klient nie musi znać URL-i z góry — odkrywa je z odpowiedzi
// Najwyższy poziom dojrzałości REST (Richardson Maturity Model Level 3)

// Bez HATEOAS — klient musi "znać" URL-e
// { "id": 1, "status": "Nowe", "suma": 500 }

// Z HATEOAS — odpowiedź zawiera linki do możliwych akcji
// {
//   "id": 1,
//   "status": "Nowe",
//   "suma": 500,
//   "_links": {
//     "self":    { "href": "/api/zamowienia/1",         "method": "GET"    },
//     "anuluj":  { "href": "/api/zamowienia/1/anuluj",  "method": "POST"   },
//     "pozycje": { "href": "/api/zamowienia/1/pozycje", "method": "GET"    },
//     "klient":  { "href": "/api/klienci/5",            "method": "GET"    }
//   }
// }

// Implementacja HATEOAS w ASP.NET Core

public class Link
{
    public string Href   { get; set; }
    public string Rel    { get; set; }   // relacja: self, next, prev, itp.
    public string Method { get; set; }

    public Link(string href, string rel, string method)
    {
        Href   = href;
        Rel    = rel;
        Method = method;
    }
}

public class HateoasOdpowiedz<T>
{
    public T                Data   { get; set; }
    public List<Link>       Links  { get; set; } = new();

    public HateoasOdpowiedz(T data) => Data = data;

    public HateoasOdpowiedz<T> DodajLink(string href, string rel, string method)
    {
        Links.Add(new Link(href, rel, method));
        return this;
    }
}

public class StronicowanaHateoas<T>
{
    public IReadOnlyList<T> Dane        { get; set; }
    public int              Strona      { get; set; }
    public int              Rozmiar     { get; set; }
    public int              LacznaIlosc { get; set; }
    public int              LaczneStrony=> (int)Math.Ceiling((double)LacznaIlosc / Rozmiar);
    public List<Link>       Links       { get; set; } = new();

    public StronicowanaHateoas(IReadOnlyList<T> dane, int strona, int rozmiar, int laczna)
    {
        Dane        = dane;
        Strona      = strona;
        Rozmiar     = rozmiar;
        LacznaIlosc = laczna;
    }
}

// Kontroler z HATEOAS
[ApiController]
[Route("api/zamowienia")]
public class ZamowieniaHateoasController : ControllerBase
{
    private readonly IZamowienieSerwis _serwis;
    private readonly IUrlHelper        _url;

    public ZamowieniaHateoasController(
        IZamowienieSerwis serwis,
        IUrlHelper url)
    {
        _serwis = serwis;
        _url    = url;
    }

    [HttpGet(Name = "PobierzZamowienia")]
    public async Task<IActionResult> PobierzWszystkie(
        [FromQuery] int strona = 1,
        [FromQuery] int rozmiar = 20)
    {
        var (dane, laczna) = await _serwis.PobierzAsync(strona, rozmiar);

        var odpowiedz = new StronicowanaHateoas<ZamowienieDto>(
            dane, strona, rozmiar, laczna);

        // Link do siebie
        odpowiedz.Links.Add(new Link(
            _url.Link("PobierzZamowienia", new { strona, rozmiar })!,
            "self", "GET"));

        // Paginacja
        if (strona > 1)
            odpowiedz.Links.Add(new Link(
                _url.Link("PobierzZamowienia", new { strona = strona - 1, rozmiar })!,
                "prev", "GET"));

        if (strona < odpowiedz.LaczneStrony)
            odpowiedz.Links.Add(new Link(
                _url.Link("PobierzZamowienia", new { strona = strona + 1, rozmiar })!,
                "next", "GET"));

        // Akcja tworzenia
        odpowiedz.Links.Add(new Link(
            _url.Link("UtworzZamowienie", null)!,
            "create", "POST"));

        return Ok(odpowiedz);
    }

    [HttpGet("{id:int}", Name = "PobierzZamowienie")]
    public async Task<IActionResult> PobierzPoId(int id)
    {
        var zamowienie = await _serwis.PobierzPoIdAsync(id);
        if (zamowienie is null) return NotFound();

        var odpowiedz = new HateoasOdpowiedz<ZamowienieDto>(zamowienie)
            .DodajLink(
                _url.Link("PobierzZamowienie", new { id })!,
                "self", "GET")
            .DodajLink(
                _url.Link("PobierzKlienta", new { id = zamowienie.KlientId })!,
                "klient", "GET")
            .DodajLink(
                _url.Link("PobierzPozycjeZamowienia", new { zamowienieId = id })!,
                "pozycje", "GET");

        // Linki zależne od stanu — tylko gdy dozwolone
        if (zamowienie.Status == "Nowe" || zamowienie.Status == "Potwierdzone")
            odpowiedz.DodajLink(
                _url.Link("AnulujZamowienie", new { id })!,
                "anuluj", "POST");

        if (zamowienie.Status == "Nowe")
            odpowiedz.DodajLink(
                _url.Link("PotwierdZamowienie", new { id })!,
                "potwierdz", "POST");

        if (zamowienie.Status == "Potwierdzone")
            odpowiedz.DodajLink(
                _url.Link("WyslijZamowienie", new { id })!,
                "wyslij", "POST");

        return Ok(odpowiedz);
    }

    [HttpPost(Name = "UtworzZamowienie")]
    public async Task<IActionResult> Utworz([FromBody] NoweZamowienieDto dto)
    {
        int id = await _serwis.UtworzAsync(dto);
        var zamowienie = await _serwis.PobierzPoIdAsync(id);

        var odpowiedz = new HateoasOdpowiedz<ZamowienieDto>(zamowienie!)
            .DodajLink(
                _url.Link("PobierzZamowienie", new { id })!,
                "self", "GET")
            .DodajLink(
                _url.Link("AnulujZamowienie", new { id })!,
                "anuluj", "POST");

        return CreatedAtRoute("PobierzZamowienie", new { id }, odpowiedz);
    }

    [HttpPost("{id:int}/anuluj", Name = "AnulujZamowienie")]
    public async Task<IActionResult> Anuluj(int id, [FromBody] AnulujDto dto)
    {
        bool ok = await _serwis.AnulujAsync(id, dto.Powod);
        if (!ok) return NotFound();

        var zamowienie = await _serwis.PobierzPoIdAsync(id);
        var odpowiedz = new HateoasOdpowiedz<ZamowienieDto>(zamowienie!)
            .DodajLink(
                _url.Link("PobierzZamowienie", new { id })!,
                "self", "GET");
        // Brak linku "anuluj" — już anulowane!

        return Ok(odpowiedz);
    }

    [HttpPost("{id:int}/potwierdz", Name = "PotwierdZamowienie")]
    public async Task<IActionResult> Potwierdz(int id)
    {
        bool ok = await _serwis.PotwierdAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/wyslij", Name = "WyslijZamowienie")]
    public async Task<IActionResult> Wyslij(int id,
        [FromBody] WysylkaDto dto)
    {
        bool ok = await _serwis.WyslijAsync(id, dto);
        return ok ? NoContent() : NotFound();
    }
}
```

---

### 8. Richardson Maturity Model — poziomy dojrzałości REST

csharp

```csharp
// POZIOM 0 — "The Swamp of POX" — jeden endpoint, POST dla wszystkiego
// POST /api  body: { "akcja": "pobierzProdukt", "id": 42 }
// POST /api  body: { "akcja": "usunProdukt", "id": 42 }
// To nie jest REST — to RPC przez HTTP

// POZIOM 1 — Zasoby — osobne URI dla każdego zasobu
// POST /produkty     body: { "akcja": "pobierz", "id": 42 }
// POST /zamowienia   body: { "akcja": "usun", "id": 5 }
// Lepiej — ale metody HTTP ciągle złe

// POZIOM 2 — HTTP Verbs — właściwe metody + status codes
// GET    /produkty/42     → 200 OK + produkt
// DELETE /produkty/42     → 204 No Content
// POST   /produkty        → 201 Created + Location
// To minimum dla "REST API" w praktyce

// POZIOM 3 — HATEOAS — odpowiedź zawiera linki
// GET /zamowienia/1 →
// {
//   "id": 1, "status": "Nowe",
//   "_links": {
//     "self":    { "href": "/api/zamowienia/1",        "method": "GET"  },
//     "anuluj":  { "href": "/api/zamowienia/1/anuluj", "method": "POST" },
//     "klient":  { "href": "/api/klienci/5",           "method": "GET"  }
//   }
// }
// Klient może nawigować po API bez dokumentacji URL-i

// W PRAKTYCE:
// Poziom 2 — standard branżowy ("REST API" które wszyscy znają)
// Poziom 3 — rzadki w praktyce, wymagany przez czyste REST
// Większość "REST API" to tak naprawdę poziom 2

// Praktyczny przykład — odpowiedź API na GET /api/produkty/1

// Odpowiedź Poziom 2:
// {
//   "id": 1,
//   "nazwa": "Laptop",
//   "cena": 3500.00,
//   "kategoria": "IT",
//   "stanMagazynu": 10
// }

// Odpowiedź Poziom 3:
// {
//   "id": 1,
//   "nazwa": "Laptop",
//   "cena": 3500.00,
//   "kategoria": "IT",
//   "stanMagazynu": 10,
//   "_links": {
//     "self":       { "href": "/api/produkty/1",          "method": "GET"    },
//     "aktualizuj": { "href": "/api/produkty/1",          "method": "PUT"    },
//     "usun":       { "href": "/api/produkty/1",          "method": "DELETE" },
//     "kategoria":  { "href": "/api/kategorie/it",        "method": "GET"    },
//     "opinie":     { "href": "/api/produkty/1/opinie",   "method": "GET"    },
//     "dodaj-opinię":{ "href": "/api/produkty/1/opinie",  "method": "POST"   },
//     "zdjecia":    { "href": "/api/produkty/1/zdjecia",  "method": "GET"    }
//   }
// }
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między PUT a PATCH?"** PUT zastępuje CAŁY zasób — klient wysyła kompletną reprezentację, pola nieobecne w body są resetowane do domyślnych. Jest idempotentne — wywołanie N razy = ten sam efekt. PATCH aktualizuje CZĘŚĆ zasobu — klient wysyła tylko zmieniające się pola (RFC 6902 JSON Patch lub własny format). Formalnie PATCH nie jest idempotentny, choć w praktyce implementuje się go idempotentnie. Użyj PUT gdy chcesz zastąpić całość, PATCH gdy aktualizujesz jedno-dwa pola.

**"Kiedy 404 a kiedy 400?"** 400 Bad Request — dane które klient wysłał są nieprawidłowe (walidacja formatu, brak wymaganych pól, złe typy). 404 Not Found — dane poprawne ale zasób o podanym id nie istnieje. 422 Unprocessable Entity — dane poprawne składniowo ale naruszają reguły biznesowe (np. data końca przed datą startu). Klasyfikacja: 400 = "nie wiem co chcesz", 404 = "wiem czego chcesz, ale tego nie ma", 422 = "wiem czego chcesz, nie da się tego zrobić".

**"Co to HATEOAS i czy warto implementować?"** HATEOAS (Hypermedia As The Engine Of Application State) to zasada gdzie odpowiedź API zawiera linki do możliwych następnych akcji — klient nie musi znać URL-i z góry, odkrywa je z odpowiedzi. Zalety: luźne powiązanie klienta z serwerem, łatwość ewolucji API. Wady: złożoność implementacji, większe response body, klienci rzadko z tego korzystają. W praktyce: większość API produkcyjnych implementuje poziom 2 (HTTP verbs + status codes), HATEOAS tylko gdy budujesz publiczne API dla zewnętrznych developerów lub wymagania tego explicite wymagają.

**"Jak wersjonować REST API?"** Cztery podejścia: URI versioning (`/api/v1/produkty`) — najprostsze, widoczne, łamie zasadę że URI identyfikuje zasób. Header versioning (`X-API-Version: 2`) — czysty URI, ale niewidoczny, trudniejszy do testowania przez przeglądarkę. Accept header (`Accept: application/vnd.sklep.v2+json`) — najbardziej RESTful, trudny w implementacji. Query param (`?api-version=2`) — wygodny, ale zaśmieca URL. W praktyce: URI versioning wygrywa prostotą — używa go GitHub, Stripe, większość dużych API.