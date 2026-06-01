namespace _06_ASPNetCore_RestAPI;

// ============================================================
// HATEOAS — HYPERMEDIA AS THE ENGINE OF APPLICATION STATE
// ============================================================

public class Link
{
    public string Href { get; init; } = "";
    public string Rel { get; init; } = "";
    public string Method { get; init; } = "GET";

    public Link() { }

    public Link(string href, string rel, string method = "GET")
    {
        Href = href;
        Rel = rel;
        Method = method;
    }
}

public class HateoasOdpowiedz<T>
{
    public T Dane { get; init; }
    public List<Link> Linki { get; init; } = [];

    public HateoasOdpowiedz(T dane, List<Link> linki)
    {
        Dane = dane;
        Linki = linki;
    }
}

public class StronicowanaHateoas<T>
{
    public List<T> Elementy { get; init; } = [];
    public int Strona { get; init; }
    public int RozmiarStrony { get; init; }
    public int LacznaLiczba { get; init; }
    public int LacznaLiczbaStron => (int)Math.Ceiling((double)LacznaLiczba / RozmiarStrony);
    public List<Link> Linki { get; init; } = [];

    public StronicowanaHateoas(List<T> elementy, int strona, int rozmiarStrony, int lacznaLiczba, List<Link> linki)
    {
        Elementy = elementy;
        Strona = strona;
        RozmiarStrony = rozmiarStrony;
        LacznaLiczba = lacznaLiczba;
        Linki = linki;
    }
}

// ============================================================
// RICHARDSON MATURITY MODEL
// ============================================================

// Level 0 — Plain Old XML / JSON over HTTP (jeden endpoint, jeden verb)
// Level 1 — Resources (osobne URL dla każdego zasobu)
// Level 2 — HTTP Verbs (GET/POST/PUT/DELETE + kody statusu)
// Level 3 — Hypermedia Controls (HATEOAS — klient odkrywa akcje z odpowiedzi)

public static class RichardsonMaturityModel
{
    public static void Opisz()
    {
        Console.WriteLine("Richardson Maturity Model:");
        Console.WriteLine("  L0: POST /api -> {akcja:'pobierzProdukty'} (POX/POJO over HTTP)");
        Console.WriteLine("  L1: GET /api/produkty, GET /api/zamowienia (osobne zasoby)");
        Console.WriteLine("  L2: GET /produkty, POST /produkty, PUT /produkty/1, DELETE /produkty/1 + kody HTTP");
        Console.WriteLine("  L3: HATEOAS — odpowiedź zawiera linki do możliwych akcji");
    }
}

// ============================================================
// HTTP STATUS CODES
// ============================================================

public static class HttpStatusCodeGuide
{
    // 2xx — Sukces
    public const int Ok = 200;                 // GET, PUT — zasób zwrócony/zaktualizowany
    public const int Created = 201;            // POST — zasób utworzony, Location header
    public const int Accepted = 202;           // Async — operacja przyjęta, nie ukończona
    public const int NoContent = 204;          // DELETE, PUT — brak ciała odpowiedzi

    // 3xx — Przekierowania
    public const int MovedPermanently = 301;   // Trwałe przekierowanie
    public const int Found = 302;              // Tymczasowe przekierowanie
    public const int NotModified = 304;        // ETag pasuje — użyj cache

    // 4xx — Błędy klienta
    public const int BadRequest = 400;         // Nieprawidłowe dane wejściowe
    public const int Unauthorized = 401;       // Brak/nieprawidłowy token auth
    public const int Forbidden = 403;          // Uwierzytelniony, ale brak uprawnień
    public const int NotFound = 404;           // Zasób nie istnieje
    public const int MethodNotAllowed = 405;   // HTTP method niedozwolona
    public const int Conflict = 409;           // Konflikt stanu (np. duplikat)
    public const int Gone = 410;               // Zasób trwale usunięty
    public const int UnprocessableEntity = 422; // Walidacja semantyczna
    public const int TooManyRequests = 429;    // Rate limiting

    // 5xx — Błędy serwera
    public const int InternalServerError = 500; // Nieobsługiwany wyjątek
    public const int NotImplemented = 501;      // Metoda nie zaimplementowana
    public const int BadGateway = 502;          // Upstream error
    public const int ServiceUnavailable = 503;  // Serwis tymczasowo niedostępny
    public const int GatewayTimeout = 504;      // Upstream timeout
}

// ============================================================
// RFC 7807 — PROBLEM DETAILS
// ============================================================

public class ProblemDetails
{
    public string Type { get; set; } = "about:blank";
    public string Title { get; set; } = "";
    public int Status { get; set; }
    public string Detail { get; set; } = "";
    public string Instance { get; set; } = "";
    public Dictionary<string, object> Extensions { get; set; } = new();
}

// ============================================================
// ETAG — CACHE VALIDATION
// ============================================================

public static class ETagHelper
{
    // Generuje słaby ETag na podstawie hash zawartości
    public static string Generuj(object dane)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(dane);
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json));
        return $"W/\"{Convert.ToBase64String(hash)[..16]}\"";
    }

    // Walidacja: If-None-Match -> 304, If-Match -> 412
    public static bool CzyPasuje(string? ifNoneMatch, string etag) =>
        string.Equals(ifNoneMatch, etag, StringComparison.OrdinalIgnoreCase);
}

// ============================================================
// PAGINACJA
// ============================================================

public class ParametryStronicowania
{
    private int _rozmiarStrony = 10;

    public int Strona { get; set; } = 1;
    public int RozmiarStrony
    {
        get => _rozmiarStrony;
        set => _rozmiarStrony = Math.Min(value, 100); // max 100
    }
    public string? Sortowanie { get; set; }
    public string? Kierunek { get; set; } = "asc";
}

public class WynikStronicowania<T>
{
    public List<T> Elementy { get; init; } = [];
    public int Strona { get; init; }
    public int RozmiarStrony { get; init; }
    public int LacznaLiczba { get; init; }
    public bool MaPoprzednia => Strona > 1;
    public bool MaNastepna => Strona < (int)Math.Ceiling((double)LacznaLiczba / RozmiarStrony);
}

// ============================================================
// API VERSIONING
// ============================================================

// Strategie wersjonowania:
// 1. URI: /api/v1/produkty, /api/v2/produkty
// 2. Query: /api/produkty?api-version=1.0
// 3. Header: X-Api-Version: 1.0
// 4. Accept: application/vnd.mojapi.v1+json

public static class ApiVersioningDemo
{
    public static void Opisz()
    {
        Console.WriteLine("API Versioning strategie:");
        Console.WriteLine("  URI: /api/v1/produkty (najprostsze, SEO-friendly)");
        Console.WriteLine("  Query: ?api-version=1.0 (łatwe testowanie)");
        Console.WriteLine("  Header: X-Api-Version: 1.0 (clean URL)");
        Console.WriteLine("  Accept: application/vnd.api.v1+json (RFC-compliant)");
        Console.WriteLine("  services.AddApiVersioning(o => { o.DefaultApiVersion = new(1,0); o.AssumeDefaultVersionWhenUnspecified = true; })");
        Console.WriteLine("  [ApiVersion(\"1.0\")], [ApiVersion(\"2.0\"), [MapToApiVersion(\"2.0\")]");
    }
}

// ============================================================
// CONTENT NEGOTIATION
// ============================================================

// Accept: application/json  -> JSON (domyślnie)
// Accept: application/xml   -> XML (wymaga AddXmlSerializerFormatters())
// Accept: text/csv          -> CSV (własny OutputFormatter)
// Produces/Consumes atrybuty ograniczają akceptowane formaty

public static class ContentNegotiationDemo
{
    public static void Opisz()
    {
        Console.WriteLine("Content Negotiation:");
        Console.WriteLine("  Request: Accept: application/json lub application/xml");
        Console.WriteLine("  Response: Content-Type: application/json; charset=utf-8");
        Console.WriteLine("  services.AddControllers().AddXmlSerializerFormatters()");
        Console.WriteLine("  [Produces(\"application/json\")], [Consumes(\"application/json\")]");
        Console.WriteLine("  406 Not Acceptable — gdy serwer nie obsługuje żądanego formatu");
    }
}

// ============================================================
// HELPER — BUDOWANIE LINKÓW HATEOAS
// ============================================================

public static class HateoasBuilder
{
    public static List<Link> BudujLinkiProduktu(int id, string baseUrl)
    {
        return
        [
            new Link($"{baseUrl}/api/produkty/{id}", "self"),
            new Link($"{baseUrl}/api/produkty/{id}", "update", "PUT"),
            new Link($"{baseUrl}/api/produkty/{id}", "delete", "DELETE"),
            new Link($"{baseUrl}/api/produkty", "collection"),
        ];
    }

    public static List<Link> BudujLinkiKolekcji(string baseUrl, int strona, int rozmiar, int lacznie)
    {
        var linki = new List<Link>
        {
            new Link($"{baseUrl}/api/produkty?strona=1&rozmiar={rozmiar}", "first"),
            new Link($"{baseUrl}/api/produkty?strona={strona}&rozmiar={rozmiar}", "self"),
        };

        if (strona > 1)
            linki.Add(new Link($"{baseUrl}/api/produkty?strona={strona - 1}&rozmiar={rozmiar}", "prev"));

        int ostatniaStrona = (int)Math.Ceiling((double)lacznie / rozmiar);
        if (strona < ostatniaStrona)
            linki.Add(new Link($"{baseUrl}/api/produkty?strona={strona + 1}&rozmiar={rozmiar}", "next"));

        linki.Add(new Link($"{baseUrl}/api/produkty?strona={ostatniaStrona}&rozmiar={rozmiar}", "last"));

        return linki;
    }
}
