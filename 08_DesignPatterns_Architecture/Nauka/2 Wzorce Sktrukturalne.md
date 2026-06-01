### Wzorce Strukturalne w C#

Wzorce strukturalne rozwiązują **jak składać klasy i obiekty** w większe struktury, zachowując elastyczność i wydajność.

---

### 1. Adapter — mostek między interfejsami

csharp

```csharp
// Adapter — konwertuje interfejs klasy na inny interfejs
// którego oczekuje klient. Pozwala współpracować klasom
// o niekompatybilnych interfejsach.

// === PROBLEM ===
// Mamy stary system płatności i nowy interfejs
// Nie możemy zmienić starego kodu (zewnętrzna biblioteka)

// Istniejący kod (nie możemy zmienić — zewnętrzna biblioteka)
public class StaraSystemPlatnosci
{
    public bool WykonajTransakcje(
        string numerKarty,
        string dataWaz,
        string cvv,
        double kwota,           // double — stary format
        string waluta)
    {
        Console.WriteLine($"Stary system: {kwota} {waluta} kartą {numerKarty[..4]}****");
        return true;
    }

    public string SprawdzSaldo(string numerKarty)
    {
        return "5000.00 PLN";   // zwraca string, nie decimal!
    }
}

// Drugi zewnętrzny system (PayPal)
public class PayPalAPI
{
    public void AuthorizePayment(string email, decimal amount)
        => Console.WriteLine($"PayPal: {amount} PLN dla {email}");

    public bool CapturePayment(string transactionId)
        => true;
}

// Nasz interfejs — tego oczekuje aplikacja
public interface ISerwisaPlatnosci
{
    Task<WynikPlatnosci> PobierzPlatnosc(
        DanePlatnosci dane,
        CancellationToken ct = default);

    Task<decimal> SprawdzSaldoAsync(
        string identyfikator,
        CancellationToken ct = default);
}

public record DanePlatnosci(
    string Identyfikator,   // karta lub email PayPal
    string? DodatkoweDane,  // data ważności, CVV
    decimal Kwota,
    string Waluta = "PLN");

public record WynikPlatnosci(
    bool    Sukces,
    string? TransakcjaId,
    string? KomunikatBledu);

// === ADAPTER dla starego systemu kartowego ===
public class AdapterStaregoSystemu : ISerwisaPlatnosci
{
    private readonly StaraSystemPlatnosci _starySystem;

    public AdapterStaregoSystemu(StaraSystemPlatnosci starySystem)
        => _starySystem = starySystem;

    public async Task<WynikPlatnosci> PobierzPlatnosc(
        DanePlatnosci dane, CancellationToken ct = default)
    {
        await Task.Yield();  // symulacja async I/O

        // Adaptuj interfejs — zamień decimal na double, rozbij dane karty
        var cześciKarty = dane.DodatkoweDane?.Split('|') ?? Array.Empty<string>();
        string dataWaz = cześciKarty.ElementAtOrDefault(0) ?? "";
        string cvv     = cześciKarty.ElementAtOrDefault(1) ?? "";

        bool wynik = _starySystem.WykonajTransakcje(
            dane.Identyfikator,
            dataWaz,
            cvv,
            (double)dane.Kwota,     // ← decimal → double (adaptacja!)
            dane.Waluta);

        return wynik
            ? new WynikPlatnosci(true,  Guid.NewGuid().ToString("N"), null)
            : new WynikPlatnosci(false, null, "Transakcja odrzucona");
    }

    public async Task<decimal> SprawdzSaldoAsync(
        string identyfikator, CancellationToken ct = default)
    {
        await Task.Yield();

        string saldoStr = _starySystem.SprawdzSaldo(identyfikator);
        // Adaptuj string → decimal
        string liczba = saldoStr.Split(' ')[0];  // "5000.00 PLN" → "5000.00"
        return decimal.Parse(liczba,
            System.Globalization.CultureInfo.InvariantCulture);
    }
}

// === ADAPTER dla PayPal ===
public class AdapterPayPal : ISerwisaPlatnosci
{
    private readonly PayPalAPI _paypal;
    private readonly Dictionary<string, string> _transakcje = new();

    public AdapterPayPal(PayPalAPI paypal) => _paypal = paypal;

    public async Task<WynikPlatnosci> PobierzPlatnosc(
        DanePlatnosci dane, CancellationToken ct = default)
    {
        await Task.Yield();

        // Adaptuj — PayPal wymaga email i działa w 2 krokach
        _paypal.AuthorizePayment(dane.Identyfikator, dane.Kwota);

        string transId = Guid.NewGuid().ToString("N");
        bool captured = _paypal.CapturePayment(transId);

        return captured
            ? new WynikPlatnosci(true,  transId, null)
            : new WynikPlatnosci(false, null, "PayPal capture failed");
    }

    public Task<decimal> SprawdzSaldoAsync(
        string identyfikator, CancellationToken ct = default)
        => Task.FromResult(999.99m);  // PayPal nie daje salda przez API
}

// === FABRYKA ADAPTERÓW ===
public class FabrykaAdapterowPlatnosci
{
    public static ISerwisaPlatnosci Pobierz(string provider)
        => provider.ToLower() switch
        {
            "karta"  => new AdapterStaregoSystemu(new StaraSystemPlatnosci()),
            "paypal" => new AdapterPayPal(new PayPalAPI()),
            _ => throw new ArgumentException($"Nieznany provider: {provider}")
        };
}

// === UŻYCIE — klient nie wie z czego korzysta ===
var platnosc = FabrykaAdapterowPlatnosci.Pobierz("karta");
var wynik = await platnosc.PobierzPlatnosc(
    new DanePlatnosci("4111111111111111", "12/26|123", 299.99m));

Console.WriteLine($"Platnosc: {wynik.Sukces}, ID: {wynik.TransakcjaId}");

var platPayPal = FabrykaAdapterowPlatnosci.Pobierz("paypal");
var wynikPP = await platPayPal.PobierzPlatnosc(
    new DanePlatnosci("jan@test.pl", null, 150m));

Console.WriteLine($"PayPal: {wynikPP.Sukces}");
```

---

### 2. Decorator — dodawanie odpowiedzialności dynamicznie

csharp

```csharp
// Decorator — dynamicznie dodaje zachowania do obiektu
// Alternatywa dla dziedziczenia — kompozycja zamiast hierarchii

// === PROBLEM — logger z wieloma funkcjami ===
// Chcemy: logowanie + cache + metryki + retry
// Nie chcemy: PersonLogger extends CachingLogger extends RetryLogger...

public interface IProduktRepo
{
    Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool> UsunAsync(int id, CancellationToken ct = default);
}

public record Produkt(int Id, string Nazwa, decimal Cena, bool Aktywny = true);

// Konkretna implementacja — prosta, bez żadnych cross-cutting concerns
public class ProduktRepoDb : IProduktRepo
{
    private static readonly List<Produkt> _baza = new()
    {
        new(1, "Laptop",   3500m),
        new(2, "Mysz",      150m),
        new(3, "Klawiatura",250m)
    };

    public Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
    {
        Console.WriteLine($"[DB] SELECT * FROM Produkty WHERE Id = {id}");
        var prod = _baza.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(prod);
    }

    public Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
    {
        Console.WriteLine("[DB] SELECT * FROM Produkty");
        return Task.FromResult(_baza.ToList());
    }

    public Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default)
    {
        int noweId = _baza.Max(p => p.Id) + 1;
        _baza.Add(produkt with { Id = noweId });
        Console.WriteLine($"[DB] INSERT Produkt #{noweId}: {produkt.Nazwa}");
        return Task.FromResult(noweId);
    }

    public Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        bool usunieto = _baza.RemoveAll(p => p.Id == id) > 0;
        Console.WriteLine($"[DB] DELETE Produkt #{id}: {usunieto}");
        return Task.FromResult(usunieto);
    }
}

// === DECORATOR 1 — Logowanie ===
public class LogowanyProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;           // dekorowany obiekt
    private readonly ILogger<LogowanyProduktRepo> _logger;

    public LogowanyProduktRepo(
        IProduktRepo inner,
        ILogger<LogowanyProduktRepo> logger)
    {
        _inner  = inner;
        _logger = logger;
    }

    public async Task<Produkt?> PobierzPoIdAsync(
        int id, CancellationToken ct = default)
    {
        _logger.LogDebug("Pobieranie produktu #{Id}", id);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var wynik = await _inner.PobierzPoIdAsync(id, ct);
            sw.Stop();
            _logger.LogInformation(
                "Pobrano produkt #{Id} [{Ms}ms]: {Status}",
                id, sw.ElapsedMilliseconds,
                wynik is null ? "NotFound" : wynik.Nazwa);
            return wynik;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Błąd pobierania produktu #{Id} [{Ms}ms]",
                id, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync(
        CancellationToken ct = default)
    {
        _logger.LogDebug("Pobieranie wszystkich produktów");
        var wynik = await _inner.PobierzWszystkieAsync(ct);
        _logger.LogInformation("Pobrano {Count} produktów", wynik.Count);
        return wynik;
    }

    public async Task<int> DodajAsync(
        Produkt produkt, CancellationToken ct = default)
    {
        _logger.LogInformation("Dodawanie produktu: {Nazwa}", produkt.Nazwa);
        int id = await _inner.DodajAsync(produkt, ct);
        _logger.LogInformation("Dodano produkt #{Id}", id);
        return id;
    }

    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        _logger.LogInformation("Usuwanie produktu #{Id}", id);
        bool wynik = await _inner.UsunAsync(id, ct);
        _logger.LogInformation("Usunięcie produktu #{Id}: {Wynik}", id, wynik);
        return wynik;
    }
}

// === DECORATOR 2 — Cache ===
public class CachedProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public CachedProduktRepo(
        IProduktRepo inner,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Produkt?> PobierzPoIdAsync(
        int id, CancellationToken ct = default)
    {
        string klucz = $"produkt:{id}";

        if (_cache.TryGetValue(klucz, out Produkt? cached))
        {
            Console.WriteLine($"[CACHE HIT] produkt:{id}");
            return cached;
        }

        Console.WriteLine($"[CACHE MISS] produkt:{id}");
        var produkt = await _inner.PobierzPoIdAsync(id, ct);

        if (produkt is not null)
            _cache.Set(klucz, produkt, _ttl);

        return produkt;
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync(
        CancellationToken ct = default)
    {
        const string klucz = "produkty:wszystkie";

        if (_cache.TryGetValue(klucz, out List<Produkt>? cached))
        {
            Console.WriteLine("[CACHE HIT] produkty:wszystkie");
            return cached!;
        }

        var lista = await _inner.PobierzWszystkieAsync(ct);
        _cache.Set(klucz, lista, _ttl);
        return lista;
    }

    public async Task<int> DodajAsync(
        Produkt produkt, CancellationToken ct = default)
    {
        int id = await _inner.DodajAsync(produkt, ct);
        // Unieważnij cache po dodaniu
        _cache.Remove("produkty:wszystkie");
        return id;
    }

    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        bool wynik = await _inner.UsunAsync(id, ct);
        if (wynik)
        {
            // Unieważnij cache po usunięciu
            _cache.Remove($"produkt:{id}");
            _cache.Remove("produkty:wszystkie");
        }
        return wynik;
    }
}

// === DECORATOR 3 — Retry ===
public class RetryProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;
    private readonly int _maxProb;
    private readonly TimeSpan _opoznienie;

    public RetryProduktRepo(
        IProduktRepo inner,
        int maxProb = 3,
        int opoznienieMs = 200)
    {
        _inner     = inner;
        _maxProb   = maxProb;
        _opoznienie = TimeSpan.FromMilliseconds(opoznienieMs);
    }

    private async Task<T> WykonajZRetryAsync<T>(
        Func<Task<T>> operacja, string nazwaOp)
    {
        for (int proba = 1; proba <= _maxProb; proba++)
        {
            try
            {
                return await operacja();
            }
            catch (Exception ex) when (proba < _maxProb)
            {
                Console.WriteLine(
                    $"[RETRY] {nazwaOp} — próba {proba}: {ex.Message}");
                await Task.Delay(_opoznienie * proba);
            }
        }
        return await operacja();  // ostatnia próba — rzuć jeśli błąd
    }

    public Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
        => WykonajZRetryAsync(
            () => _inner.PobierzPoIdAsync(id, ct),
            $"PobierzPoId({id})");

    public Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
        => WykonajZRetryAsync(
            () => _inner.PobierzWszystkieAsync(ct),
            "PobierzWszystkie");

    public Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default)
        => WykonajZRetryAsync(
            () => _inner.DodajAsync(produkt, ct),
            $"Dodaj({produkt.Nazwa})");

    public Task<bool> UsunAsync(int id, CancellationToken ct = default)
        => WykonajZRetryAsync(
            () => _inner.UsunAsync(id, ct),
            $"Usun({id})");
}

// === SKŁADANIE DECORATORÓW ===
// Kolejność: Retry → Cache → Logging → DB
// Request:  Retry → Cache → Logging → DB
// Response: DB → Logging → Cache → Retry

IProduktRepo repo =
    new RetryProduktRepo(             // zewnętrzny — retry całej operacji
        new CachedProduktRepo(         // środkowy — cache po retry
            new LogowanyProduktRepo(   // wewnętrzny — loguje rzeczywiste operacje
                new ProduktRepoDb(),   // rdzeń — faktyczna baza
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<LogowanyProduktRepo>.Instance),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));

// Użycie — klient widzi tylko IProduktRepo!
var produkt = await repo.PobierzPoIdAsync(1);
Console.WriteLine($"Produkt: {produkt?.Nazwa}");

// Rejestracja w DI (ASP.NET Core Scrutor)
// dotnet add package Scrutor
// builder.Services.AddScoped<IProduktRepo, ProduktRepoDb>();
// builder.Services.Decorate<IProduktRepo, LogowanyProduktRepo>();
// builder.Services.Decorate<IProduktRepo, CachedProduktRepo>();
// builder.Services.Decorate<IProduktRepo, RetryProduktRepo>();
```

---

### 3. Facade — uproszczony interfejs

csharp

```csharp
// Facade — dostarcza uproszczony interfejs do złożonego podsystemu
// Klient nie musi znać wszystkich komponentów

// === ZŁOŻONY PODSYSTEM — wiele serwisów ===
public class WalidatorZamowienia
{
    public WynikWalidacji Waliduj(ZamowienieFacade z)
    {
        if (z.KlientId <= 0)
            return WynikWalidacji.Blad("Nieprawidłowy ID klienta");
        if (!z.Pozycje.Any())
            return WynikWalidacji.Blad("Brak pozycji");
        return WynikWalidacji.OK();
    }
}

public class InwentarzSerwis
{
    private readonly Dictionary<int, int> _stany = new()
        { [1] = 10, [2] = 5, [3] = 0 };

    public bool SprawdzDostepnosc(int produktId, int ilosc)
    {
        Console.WriteLine($"[Inwentarz] Sprawdzam produkt #{produktId}: {ilosc} szt.");
        return _stany.TryGetValue(produktId, out int stan) && stan >= ilosc;
    }

    public void ZarezerwujTowar(int produktId, int ilosc)
    {
        _stany[produktId] -= ilosc;
        Console.WriteLine($"[Inwentarz] Zarezerwowano #{produktId}: -{ilosc}");
    }
}

public class PrzelicznikCen
{
    public decimal ObliczSume(List<PozycjaFacade> pozycje)
    {
        decimal suma = pozycje.Sum(p => p.Cena * p.Ilosc);
        Console.WriteLine($"[Ceny] Suma: {suma:C}");
        return suma;
    }

    public decimal ObliczRabat(int klientId, decimal suma)
    {
        decimal rabat = klientId > 100 ? suma * 0.1m : 0;  // stali klienci -10%
        Console.WriteLine($"[Ceny] Rabat: {rabat:C}");
        return rabat;
    }
}

public class BramkaPlatnosci
{
    public async Task<string> PobierzAsync(
        int klientId, decimal kwota, string metodaPlatnosci)
    {
        await Task.Delay(100);  // symulacja API
        string transId = $"TXN-{DateTime.Now:yyyyMMddHHmmss}";
        Console.WriteLine($"[Płatność] {kwota:C} — transakcja: {transId}");
        return transId;
    }
}

public class MagazynSerwis
{
    public void ZlecWysylke(int zamId, int klientId, AdresFacade adres)
    {
        Console.WriteLine(
            $"[Magazyn] Zlecam wysyłkę zamówienia #{zamId} do: {adres.Miasto}");
    }
}

public class EmailNotyfikator
{
    public async Task WyslijPotwierdzenieAsync(
        string email, int zamId, decimal suma)
    {
        await Task.Delay(50);
        Console.WriteLine(
            $"[Email] Wysłano potwierdzenie zamówienia #{zamId} na {email}");
    }
}

public class AuditLog
{
    public void Zapisz(string zdarzenie, object dane)
    {
        Console.WriteLine(
            $"[Audit] {zdarzenie}: {System.Text.Json.JsonSerializer.Serialize(dane)}");
    }
}

// === FACADE — uproszcza składanie zamówienia ===
public class ZamowienieFacade
{
    // Dane zamówienia
    public int              KlientId { get; init; }
    public string           Email    { get; init; } = "";
    public List<PozycjaFacade> Pozycje  { get; init; } = new();
    public AdresFacade      Adres    { get; init; } = null!;
    public string           Platnosc { get; init; } = "karta";
}

public record PozycjaFacade(int ProduktId, int Ilosc, decimal Cena);
public record AdresFacade(string Ulica, string Miasto, string Kod);
public record WynikWalidacji(bool OK, string? Blad)
{
    public static WynikWalidacji OK() => new(true, null);
    public static WynikWalidacji Blad(string b) => new(false, b);
}

public class SklepFacade
{
    // Wszystkie serwisy podsystemu
    private readonly WalidatorZamowienia _walidator;
    private readonly InwentarzSerwis     _inwentarz;
    private readonly PrzelicznikCen      _ceny;
    private readonly BramkaPlatnosci     _platnosc;
    private readonly MagazynSerwis       _magazyn;
    private readonly EmailNotyfikator    _email;
    private readonly AuditLog            _audit;

    public SklepFacade(
        WalidatorZamowienia walidator,
        InwentarzSerwis     inwentarz,
        PrzelicznikCen      ceny,
        BramkaPlatnosci     platnosc,
        MagazynSerwis       magazyn,
        EmailNotyfikator    email,
        AuditLog            audit)
    {
        _walidator = walidator;
        _inwentarz = inwentarz;
        _ceny      = ceny;
        _platnosc  = platnosc;
        _magazyn   = magazyn;
        _email     = email;
        _audit     = audit;
    }

    // === JEDNA METODA — zastępuje orchestrację 7 serwisów ===
    public async Task<WynikZlozenia> ZlozZamowienieAsync(
        ZamowienieFacade zamowienie,
        CancellationToken ct = default)
    {
        Console.WriteLine("\n=== Składanie zamówienia ===");

        // 1. Walidacja
        var walidacja = _walidator.Waliduj(zamowienie);
        if (!walidacja.OK)
            return WynikZlozenia.Blad(walidacja.Blad!);

        // 2. Sprawdź dostępność wszystkich produktów
        foreach (var poz in zamowienie.Pozycje)
        {
            if (!_inwentarz.SprawdzDostepnosc(poz.ProduktId, poz.Ilosc))
                return WynikZlozenia.Blad(
                    $"Produkt #{poz.ProduktId} niedostępny w ilości {poz.Ilosc}");
        }

        // 3. Oblicz cenę
        decimal suma   = _ceny.ObliczSume(zamowienie.Pozycje);
        decimal rabat  = _ceny.ObliczRabat(zamowienie.KlientId, suma);
        decimal kwota  = suma - rabat;

        // 4. Pobierz płatność
        string transId = await _platnosc.PobierzAsync(
            zamowienie.KlientId, kwota, zamowienie.Platnosc);

        // 5. Zarezerwuj towar
        foreach (var poz in zamowienie.Pozycje)
            _inwentarz.ZarezerwujTowar(poz.ProduktId, poz.Ilosc);

        int zamId = new Random().Next(10000, 99999);

        // 6. Zlec wysyłkę
        _magazyn.ZlecWysylke(zamId, zamowienie.KlientId, zamowienie.Adres);

        // 7. Wyślij email
        await _email.WyslijPotwierdzenieAsync(zamowienie.Email, zamId, kwota);

        // 8. Zapisz audit
        _audit.Zapisz("ZamowienieZlozone", new { zamId, kwota, transId });

        Console.WriteLine($"=== Zamówienie #{zamId} przyjęte ===\n");
        return WynikZlozenia.Sukces(zamId, kwota, transId);
    }
}

public record WynikZlozenia(
    bool    OK,
    int?    ZamId,
    decimal Kwota,
    string? TransakcjaId,
    string? BladKomunikat)
{
    public static WynikZlozenia Sukces(int id, decimal k, string t)
        => new(true, id, k, t, null);
    public static WynikZlozenia Blad(string b)
        => new(false, null, 0, null, b);
}

// === UŻYCIE — jeden call zamiast orchestracji 7 serwisów ===
var facade = new SklepFacade(
    new WalidatorZamowienia(),
    new InwentarzSerwis(),
    new PrzelicznikCen(),
    new BramkaPlatnosci(),
    new MagazynSerwis(),
    new EmailNotyfikator(),
    new AuditLog());

var wynik = await facade.ZlozZamowienieAsync(new ZamowienieFacade
{
    KlientId = 101,
    Email    = "jan@test.pl",
    Pozycje  = new List<PozycjaFacade>
    {
        new(1, 2, 3500m),
        new(2, 1,  150m)
    },
    Adres   = new AdresFacade("ul. Testowa 1", "Warszawa", "00-001"),
    Platnosc = "karta"
});

Console.WriteLine(wynik.OK
    ? $"Zamówienie #{wynik.ZamId}: {wynik.Kwota:C}"
    : $"Błąd: {wynik.BladKomunikat}");
```

---

### 4. Proxy — pośrednik z kontrolą dostępu

csharp

```csharp
// Proxy — dostarcza zastępnik lub placeholder dla innego obiektu
// Kontroluje dostęp, dodaje lazy loading, logowanie, cache

public interface ISerwisRaportow
{
    Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_,
        CancellationToken ct = default);

    Task<byte[]> EksportujDoPdfAsync(
        RaportDto raport,
        CancellationToken ct = default);
}

public record RaportDto(
    string Typ, DateTime Od, DateTime Do_,
    int LiczbaRekordow, decimal SumaSprzedazy);

// Prawdziwy serwis — ciężki, wymaga uprawnień
public class SerwisRaportow : ISerwisRaportow
{
    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Raport] Generuję {typ} od {od:d} do {do_:d}...");
        await Task.Delay(500, ct);  // ciężka operacja
        return new RaportDto(typ, od, do_, 1500, 250_000m);
    }

    public async Task<byte[]> EksportujDoPdfAsync(
        RaportDto raport, CancellationToken ct = default)
    {
        Console.WriteLine($"[Raport] Eksportuję do PDF...");
        await Task.Delay(200, ct);
        return System.Text.Encoding.UTF8.GetBytes($"PDF:{raport.Typ}");
    }
}

// === PROXY 1 — Access Control Proxy ===
public class ProxyKontroliDostepu : ISerwisRaportow
{
    private readonly ISerwisRaportow    _inner;
    private readonly ICurrentUserService _user;

    public ProxyKontroliDostepu(
        ISerwisRaportow inner,
        ICurrentUserService user)
    {
        _inner = inner;
        _user  = user;
    }

    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_,
        CancellationToken ct = default)
    {
        // Sprawdź uprawnienia
        SprawdzDostep(typ);

        // Ogranicz zakres dat dla nie-adminów
        if (!_user.IsAdmin)
        {
            var maxOkres = TimeSpan.FromDays(90);
            if ((do_ - od) > maxOkres)
                throw new UnauthorizedAccessException(
                    "Zwykli użytkownicy mogą generować raporty max 90 dni");
        }

        return await _inner.GenerujRaportAsync(typ, od, do_, ct);
    }

    public async Task<byte[]> EksportujDoPdfAsync(
        RaportDto raport, CancellationToken ct = default)
    {
        if (!_user.HasClaim("permission", "export:pdf"))
            throw new UnauthorizedAccessException(
                "Brak uprawnienia do eksportu PDF");

        return await _inner.EksportujDoPdfAsync(raport, ct);
    }

    private void SprawdzDostep(string typ)
    {
        bool maUprawnienie = typ switch
        {
            "finansowy"   => _user.IsAdmin || _user.HasClaim("role", "Finanse"),
            "sprzedazowy" => _user.IsAdmin || _user.HasClaim("role", "Sprzedaz"),
            "magazynowy"  => _user.IsAdmin || _user.HasClaim("role", "Magazyn"),
            _             => _user.IsAdmin
        };

        if (!maUprawnienie)
            throw new UnauthorizedAccessException(
                $"Brak uprawnień do raportu: {typ}");
    }
}

// === PROXY 2 — Cache Proxy ===
public class ProxyCache : ISerwisRaportow
{
    private readonly ISerwisRaportow _inner;
    private readonly Dictionary<string, (RaportDto Raport, DateTime Wygasa)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public ProxyCache(ISerwisRaportow inner) => _inner = inner;

    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_,
        CancellationToken ct = default)
    {
        string klucz = $"{typ}:{od:yyyyMMdd}:{do_:yyyyMMdd}";

        if (_cache.TryGetValue(klucz, out var cached)
            && cached.Wygasa > DateTime.UtcNow)
        {
            Console.WriteLine($"[Cache] HIT: {klucz}");
            return cached.Raport;
        }

        Console.WriteLine($"[Cache] MISS: {klucz}");
        var raport = await _inner.GenerujRaportAsync(typ, od, do_, ct);
        _cache[klucz] = (raport, DateTime.UtcNow.Add(_ttl));
        return raport;
    }

    public Task<byte[]> EksportujDoPdfAsync(
        RaportDto raport, CancellationToken ct = default)
        => _inner.EksportujDoPdfAsync(raport, ct);  // PDF nie cache'ujemy
}

// === PROXY 3 — Lazy Loading Proxy ===
public class ProxyLazyLoading : ISerwisRaportow
{
    private ISerwisRaportow? _inner;
    private readonly Func<ISerwisRaportow> _fabryka;

    public ProxyLazyLoading(Func<ISerwisRaportow> fabryka)
        => _fabryka = fabryka;

    // Inicjalizacja serwisu dopiero przy pierwszym użyciu
    private ISerwisRaportow PobierzSerwis()
        => _inner ??= _fabryka();

    public Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_,
        CancellationToken ct = default)
        => PobierzSerwis().GenerujRaportAsync(typ, od, do_, ct);

    public Task<byte[]> EksportujDoPdfAsync(
        RaportDto raport, CancellationToken ct = default)
        => PobierzSerwis().EksportujDoPdfAsync(raport, ct);
}

// === SKŁADANIE PROXY ===
// Access Control → Cache → LazyLoading → RealService
ISerwisRaportow serwis =
    new ProxyKontroliDostepu(
        new ProxyCache(
            new ProxyLazyLoading(
                () => new SerwisRaportow())),  // inicjalizacja lazy!
        new MockCurrentUser());

// Użycie
try
{
    var raport = await serwis.GenerujRaportAsync(
        "sprzedazowy",
        DateTime.Today.AddMonths(-1),
        DateTime.Today);
    Console.WriteLine($"Raport: {raport.LiczbaRekordow} rekordów");

    var pdf = await serwis.EksportujDoPdfAsync(raport);
    Console.WriteLine($"PDF: {pdf.Length} bajtów");
}
catch (UnauthorizedAccessException ex)
{
    Console.WriteLine($"Brak dostępu: {ex.Message}");
}

// Mock CurrentUser do przykładu
public class MockCurrentUser : ICurrentUserService
{
    public int UserId => 1;
    public string Email => "admin@test.pl";
    public bool IsAdmin => true;
    public string? TenantId => null;
    public IEnumerable<string> Roles => new[] { "Admin" };
    public bool HasClaim(string type, string value) => true;
}

public interface ICurrentUserService
{
    int UserId { get; }
    string Email { get; }
    bool IsAdmin { get; }
    string? TenantId { get; }
    IEnumerable<string> Roles { get; }
    bool HasClaim(string type, string value);
}
```

---

### 5. Porównanie wzorców

csharp

```csharp
// ADAPTER vs DECORATOR vs FACADE vs PROXY
// Wszystkie "owijają" obiekt — różnią się celem!

// ADAPTER — zmienia interfejs
// Stary interfejs → Nowy interfejs
// "Chcę użyć tego, ale ma zły interfejs"
// Przykład: StaraSystemPlatnosci → ISerwisaPlatnosci

// DECORATOR — dodaje zachowanie
// Interfejs IN = Interfejs OUT (ten sam!)
// "Chcę dodać funkcję bez zmiany klasy"
// Przykład: IProduktRepo + logging + cache + retry

// FACADE — upraszcza podsystem
// Wiele interfejsów → Jeden prosty interfejs
// "Chcę ukryć złożoność"
// Przykład: 7 serwisów → ZlozZamowienie()

// PROXY — kontroluje dostęp
// Interfejs IN = Interfejs OUT (ten sam co Decorator!)
// "Chcę kontrolować kiedy/jak obiekt jest używany"
// Różnica vs Decorator:
//   Decorator DODAJE funkcje (open-ended)
//   Proxy KONTROLUJE dostęp (zamknięty — znany obiekt)
// Przykład: Access control, lazy init, cache

// Mnemonic:
// Adapter  = "tłumacz"  — zmienia język
// Decorator = "ozdobnik" — dodaje warstwę
// Facade   = "recepcja" — jedno okienko
// Proxy    = "ochroniarz" — kontroluje dostęp
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między Decorator a Proxy?"** Oba implementują ten sam interfejs i owijają inny obiekt — stąd podobieństwo. Różni je intencja: Decorator dodaje nowe zachowania (logowanie, cache, retry) — jest open-ended, można stackować wiele dekoratorów. Proxy kontroluje dostęp do konkretnego obiektu — zna docelowy obiekt z góry, zarządza jego cyklem życia (lazy init), dostępem (auth) lub lokalizacją (remote proxy). Decorator = "co robić więcej?", Proxy = "czy i kiedy dać dostęp?".

**"Kiedy Facade zamiast bezpośredniego użycia serwisów?"** Facade gdy: masz złożony podsystem z wieloma klasami które klient musi orchestrować w określonej kolejności. Upraszcza API dla częstych przypadków użycia. Nie zabrania bezpośredniego dostępu do podsystemu gdy potrzeba zaawansowanego użycia. W ASP.NET Core — Facade często wygląda jak Application Service który koordynuje Domain Services, Repository, EmailService itp. Nie używaj Facade żeby "ukryć bałagan" — to nie rozwiązuje problemu projektowego.

**"Jak implementujesz Decorator w ASP.NET Core DI?"** Natywny DI nie wspiera Decoratora bezpośrednio. Opcje: (1) Ręcznie: `services.AddScoped<IRepo>(sp => new LoggingDecorator(new CachingDecorator(new RepoImpl())))`, (2) Biblioteka Scrutor: `services.AddScoped<IRepo, RepoImpl>(); services.Decorate<IRepo, CachingDecorator>(); services.Decorate<IRepo, LoggingDecorator>()` — ostatni Decorate jest "zewnętrzną" warstwą. Scrutor jest najpopularniejszym rozwiązaniem dla Decorator pattern w .NET DI.

**"Jaka różnica między Adapter a Facade?"** Adapter działa na poziomie jednej klasy/interfejsu — konwertuje jeden interfejs na inny. Jeden adaptowany obiekt. Facade działa na poziomie podsystemu — ukrywa wiele klas za jednym prostym interfejsem. Wiele obiektów wewnętrznych. Adapter gdy: "mam tę klasę ale ma zły interfejs". Facade gdy: "mam 5 klas które zawsze używam razem w tej samej kolejności".