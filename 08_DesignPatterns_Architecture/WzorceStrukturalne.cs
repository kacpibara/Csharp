namespace _08_DesignPatterns_Architecture;

// ============================================================
// WZORCE STRUKTURALNE — Adapter, Decorator, Facade, Proxy
// ============================================================

// ============================================================
// WSPÓLNE TYPY (używane w wielu wzorcach poniżej)
// ============================================================

public record Produkt(int Id, string Nazwa, decimal Cena, bool Aktywny = true);

public interface IProduktRepo
{
    Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default);
    Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default);
    Task<int> DodajAsync(Produkt produkt, CancellationToken ct = default);
    Task<bool> UsunAsync(int id, CancellationToken ct = default);
    Task<List<Produkt>> PobierzStroneAsync(int strona, int rozmiar, CancellationToken ct = default);
}

// ============================================================
// 1. ADAPTER — mostek między interfejsami
// ============================================================
// "Tłumacz" — zmienia stary interfejs na nowy, bez modyfikacji starego kodu

// Stary system zewnętrzny — nie możemy go zmienić
public class StaraSystemPlatnosci
{
    public bool WykonajTransakcje(string numerKarty, string dataWaz,
        string cvv, double kwota, string waluta)
    {
        Console.WriteLine($"  [StarySys] {kwota} {waluta} kartą {numerKarty[..4]}****");
        return true;
    }
    public string SprawdzSaldo(string numerKarty) => "5000.00 PLN";
}

// Drugi zewnętrzny system
public class PayPalAPI
{
    public void AuthorizePayment(string email, decimal amount)
        => Console.WriteLine($"  [PayPal] {amount} PLN dla {email}");
    public bool CapturePayment(string transactionId) => true;
}

// Nasz interfejs — tego oczekuje aplikacja
public interface ISerwisaPlatnosci
{
    Task<WynikPlatnosci> PobierzPlatnosc(DanePlatnosci dane, CancellationToken ct = default);
    Task<decimal> SprawdzSaldoAsync(string identyfikator, CancellationToken ct = default);
}

public record DanePlatnosci(
    string Identyfikator, string? DodatkoweDane,
    decimal Kwota, string Waluta = "PLN");

public record WynikPlatnosci(bool Sukces, string? TransakcjaId, string? KomunikatBledu);

// Adapter starego systemu kartowego → ISerwisaPlatnosci
public class AdapterStaregoSystemu : ISerwisaPlatnosci
{
    private readonly StaraSystemPlatnosci _stary;
    public AdapterStaregoSystemu(StaraSystemPlatnosci stary) => _stary = stary;

    public async Task<WynikPlatnosci> PobierzPlatnosc(DanePlatnosci dane, CancellationToken ct = default)
    {
        await Task.Yield();
        var czesci = dane.DodatkoweDane?.Split('|') ?? Array.Empty<string>();
        bool ok = _stary.WykonajTransakcje(
            dane.Identyfikator,
            czesci.ElementAtOrDefault(0) ?? "",
            czesci.ElementAtOrDefault(1) ?? "",
            (double)dane.Kwota,       // decimal → double (adaptacja!)
            dane.Waluta);
        return ok
            ? new WynikPlatnosci(true,  Guid.NewGuid().ToString("N"), null)
            : new WynikPlatnosci(false, null, "Transakcja odrzucona");
    }

    public async Task<decimal> SprawdzSaldoAsync(string id, CancellationToken ct = default)
    {
        await Task.Yield();
        string saldoStr = _stary.SprawdzSaldo(id);
        return decimal.Parse(saldoStr.Split(' ')[0],
            System.Globalization.CultureInfo.InvariantCulture);
    }
}

// Adapter PayPal → ISerwisaPlatnosci
public class AdapterPayPal : ISerwisaPlatnosci
{
    private readonly PayPalAPI _paypal;
    public AdapterPayPal(PayPalAPI paypal) => _paypal = paypal;

    public async Task<WynikPlatnosci> PobierzPlatnosc(DanePlatnosci dane, CancellationToken ct = default)
    {
        await Task.Yield();
        _paypal.AuthorizePayment(dane.Identyfikator, dane.Kwota);
        string transId = Guid.NewGuid().ToString("N");
        bool captured = _paypal.CapturePayment(transId);
        return captured
            ? new WynikPlatnosci(true,  transId, null)
            : new WynikPlatnosci(false, null, "PayPal capture failed");
    }

    public Task<decimal> SprawdzSaldoAsync(string id, CancellationToken ct = default)
        => Task.FromResult(999.99m);
}

// Fabryka adapterów
public static class FabrykaAdapterowPlatnosci
{
    public static ISerwisaPlatnosci Pobierz(string provider) =>
        provider.ToLower() switch
        {
            "karta"  => new AdapterStaregoSystemu(new StaraSystemPlatnosci()),
            "paypal" => new AdapterPayPal(new PayPalAPI()),
            _ => throw new ArgumentException($"Nieznany provider: {provider}")
        };
}

// ============================================================
// 2. DECORATOR — dodawanie odpowiedzialności dynamicznie
// ============================================================
// "Ozdobnik" — dodaje warstwę nie zmieniając interfejsu

// Rdzeń — prosta implementacja bez cross-cutting concerns
public class ProduktRepoDb : IProduktRepo
{
    private static readonly List<Produkt> _baza = new()
    {
        new(1, "Laptop",    3500m),
        new(2, "Mysz",       150m),
        new(3, "Klawiatura", 250m)
    };

    public Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
    {
        Console.WriteLine($"  [DB] SELECT WHERE Id={id}");
        return Task.FromResult(_baza.FirstOrDefault(p => p.Id == id));
    }

    public Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
    {
        Console.WriteLine("  [DB] SELECT *");
        return Task.FromResult(_baza.ToList());
    }

    public Task<int> DodajAsync(Produkt p, CancellationToken ct = default)
    {
        int newId = _baza.Max(x => x.Id) + 1;
        _baza.Add(p with { Id = newId });
        Console.WriteLine($"  [DB] INSERT #{newId}: {p.Nazwa}");
        return Task.FromResult(newId);
    }

    public Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        bool ok = _baza.RemoveAll(p => p.Id == id) > 0;
        Console.WriteLine($"  [DB] DELETE #{id}: {ok}");
        return Task.FromResult(ok);
    }

    public Task<List<Produkt>> PobierzStroneAsync(int strona, int rozmiar, CancellationToken ct = default)
    {
        var wynik = _baza.Skip((strona - 1) * rozmiar).Take(rozmiar).ToList();
        Console.WriteLine($"  [DB] SELECT strona={strona}, rozmiar={rozmiar}: {wynik.Count} rekordów");
        return Task.FromResult(wynik);
    }
}

// Decorator 1 — Logowanie (zastępuje ILogger<T> zwykłym Console)
public class LogowanyProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;
    public LogowanyProduktRepo(IProduktRepo inner) => _inner = inner;

    public async Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var wynik = await _inner.PobierzPoIdAsync(id, ct);
        sw.Stop();
        Console.WriteLine($"  [LOG] PobierzPoId({id}) [{sw.ElapsedMilliseconds}ms]: {(wynik is null ? "NotFound" : wynik.Nazwa)}");
        return wynik;
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
    {
        var wynik = await _inner.PobierzWszystkieAsync(ct);
        Console.WriteLine($"  [LOG] PobierzWszystkie: {wynik.Count} produktów");
        return wynik;
    }

    public async Task<int> DodajAsync(Produkt p, CancellationToken ct = default)
    {
        int id = await _inner.DodajAsync(p, ct);
        Console.WriteLine($"  [LOG] Dodaj → #{id}");
        return id;
    }

    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        bool ok = await _inner.UsunAsync(id, ct);
        Console.WriteLine($"  [LOG] Usun(#{id}) → {ok}");
        return ok;
    }

    public Task<List<Produkt>> PobierzStroneAsync(int s, int r, CancellationToken ct = default)
        => _inner.PobierzStroneAsync(s, r, ct);
}

// Decorator 2 — Cache (prosty Dictionary zamiast IMemoryCache)
public class CachedProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;
    private readonly Dictionary<string, (object Val, DateTime Wygasa)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public CachedProduktRepo(IProduktRepo inner) => _inner = inner;

    private bool TryGet<T>(string klucz, out T? val)
    {
        if (_cache.TryGetValue(klucz, out var e) && e.Wygasa > DateTime.UtcNow)
        {
            Console.WriteLine($"  [CACHE HIT] {klucz}");
            val = (T)e.Val;
            return true;
        }
        Console.WriteLine($"  [CACHE MISS] {klucz}");
        val = default;
        return false;
    }

    public async Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
    {
        string k = $"produkt:{id}";
        if (TryGet<Produkt?>(k, out var cached)) return cached;
        var p = await _inner.PobierzPoIdAsync(id, ct);
        if (p is not null) _cache[k] = (p, DateTime.UtcNow.Add(_ttl));
        return p;
    }

    public async Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
    {
        const string k = "produkty:wszystkie";
        if (TryGet<List<Produkt>>(k, out var cached)) return cached!;
        var lista = await _inner.PobierzWszystkieAsync(ct);
        _cache[k] = (lista, DateTime.UtcNow.Add(_ttl));
        return lista;
    }

    public async Task<int> DodajAsync(Produkt p, CancellationToken ct = default)
    {
        int id = await _inner.DodajAsync(p, ct);
        _cache.Remove("produkty:wszystkie");
        return id;
    }

    public async Task<bool> UsunAsync(int id, CancellationToken ct = default)
    {
        bool ok = await _inner.UsunAsync(id, ct);
        if (ok) { _cache.Remove($"produkt:{id}"); _cache.Remove("produkty:wszystkie"); }
        return ok;
    }

    public Task<List<Produkt>> PobierzStroneAsync(int s, int r, CancellationToken ct = default)
        => _inner.PobierzStroneAsync(s, r, ct);
}

// Decorator 3 — Retry
public class RetryProduktRepo : IProduktRepo
{
    private readonly IProduktRepo _inner;
    private readonly int _maxProb;
    private readonly TimeSpan _opoznienie;

    public RetryProduktRepo(IProduktRepo inner, int maxProb = 3, int opoznienieMs = 200)
    {
        _inner     = inner;
        _maxProb   = maxProb;
        _opoznienie = TimeSpan.FromMilliseconds(opoznienieMs);
    }

    private async Task<T> WykonajZRetryAsync<T>(Func<Task<T>> op, string nazwa)
    {
        for (int proba = 1; proba <= _maxProb; proba++)
        {
            try { return await op(); }
            catch (Exception ex) when (proba < _maxProb)
            {
                Console.WriteLine($"  [RETRY] {nazwa} próba {proba}: {ex.Message}");
                await Task.Delay(_opoznienie * proba);
            }
        }
        return await op();
    }

    public Task<Produkt?> PobierzPoIdAsync(int id, CancellationToken ct = default)
        => WykonajZRetryAsync(() => _inner.PobierzPoIdAsync(id, ct), $"PobierzPoId({id})");
    public Task<List<Produkt>> PobierzWszystkieAsync(CancellationToken ct = default)
        => WykonajZRetryAsync(() => _inner.PobierzWszystkieAsync(ct), "PobierzWszystkie");
    public Task<int> DodajAsync(Produkt p, CancellationToken ct = default)
        => WykonajZRetryAsync(() => _inner.DodajAsync(p, ct), $"Dodaj({p.Nazwa})");
    public Task<bool> UsunAsync(int id, CancellationToken ct = default)
        => WykonajZRetryAsync(() => _inner.UsunAsync(id, ct), $"Usun({id})");
    public Task<List<Produkt>> PobierzStroneAsync(int s, int r, CancellationToken ct = default)
        => _inner.PobierzStroneAsync(s, r, ct);
}

// ============================================================
// 3. FACADE — uproszczony interfejs
// ============================================================
// "Recepcja" — ukrywa złożony podsystem za jednym okienkiem

public class WalidatorZamowieniaFacade
{
    public WynikWalidacji Waliduj(ZamowienieFacade z)
    {
        if (z.KlientId <= 0) return WynikWalidacji.Niepoprawny("Nieprawidłowy ID klienta");
        if (!z.Pozycje.Any()) return WynikWalidacji.Niepoprawny("Brak pozycji");
        return WynikWalidacji.Poprawny();
    }
}

public class InwentarzSerwis
{
    private readonly Dictionary<int, int> _stany = new() { [1] = 10, [2] = 5, [3] = 0 };

    public bool SprawdzDostepnosc(int produktId, int ilosc)
    {
        Console.WriteLine($"  [Inwentarz] sprawdzam #{produktId}: {ilosc} szt.");
        return _stany.TryGetValue(produktId, out int stan) && stan >= ilosc;
    }

    public void ZarezerwujTowar(int produktId, int ilosc)
    {
        _stany[produktId] -= ilosc;
        Console.WriteLine($"  [Inwentarz] zarezerwowano #{produktId}: -{ilosc}");
    }
}

public class PrzelicznikCen
{
    public decimal ObliczSume(List<PozycjaFacade> pozycje)
    {
        decimal suma = pozycje.Sum(p => p.Cena * p.Ilosc);
        Console.WriteLine($"  [Ceny] suma: {suma:C}");
        return suma;
    }

    public decimal ObliczRabat(int klientId, decimal suma)
    {
        decimal rabat = klientId > 100 ? suma * 0.1m : 0;
        Console.WriteLine($"  [Ceny] rabat: {rabat:C}");
        return rabat;
    }
}

public class BramkaPlatnosci
{
    public async Task<string> PobierzAsync(int klientId, decimal kwota, string metoda)
    {
        await Task.Delay(10);
        string transId = $"TXN-{DateTime.Now:yyyyMMddHHmmss}";
        Console.WriteLine($"  [Platnosc] {kwota:C} — {transId}");
        return transId;
    }
}

public class MagazynSerwis
{
    public void ZlecWysylke(int zamId, int klientId, AdresFacade adres)
        => Console.WriteLine($"  [Magazyn] wysylka #{zamId} do: {adres.Miasto}");
}

public class EmailNotyfikator
{
    public async Task WyslijPotwierdzenieAsync(string email, int zamId, decimal suma)
    {
        await Task.Delay(5);
        Console.WriteLine($"  [Email] potwierdzenie #{zamId} na {email}");
    }
}

public class AuditLog
{
    public void Zapisz(string zdarzenie, object dane)
        => Console.WriteLine($"  [Audit] {zdarzenie}: {System.Text.Json.JsonSerializer.Serialize(dane)}");
}

public class ZamowienieFacade
{
    public int                  KlientId { get; init; }
    public string               Email    { get; init; } = "";
    public List<PozycjaFacade>  Pozycje  { get; init; } = new();
    public AdresFacade          Adres    { get; init; } = null!;
    public string               Platnosc { get; init; } = "karta";
}

public record PozycjaFacade(int ProduktId, int Ilosc, decimal Cena);
public record AdresFacade(string Ulica, string Miasto, string Kod);

public record WynikWalidacji(bool CzyOk, string? Komunikat)
{
    public static WynikWalidacji Poprawny()       => new(true,  null);
    public static WynikWalidacji Niepoprawny(string b) => new(false, b);
}

public record WynikZlozenia(bool CzyOk, int? ZamId, decimal Kwota, string? TransakcjaId, string? Blad)
{
    public static WynikZlozenia Sukces(int id, decimal k, string t) => new(true,  id, k, t,    null);
    public static WynikZlozenia NieUdane(string b)                   => new(false, null, 0, null, b);
}

// FACADE — jedna metoda zamiast orchestracji 7 serwisów
public class SklepFacade
{
    private readonly WalidatorZamowieniaFacade _walidator;
    private readonly InwentarzSerwis           _inwentarz;
    private readonly PrzelicznikCen            _ceny;
    private readonly BramkaPlatnosci           _platnosc;
    private readonly MagazynSerwis             _magazyn;
    private readonly EmailNotyfikator          _email;
    private readonly AuditLog                  _audit;

    public SklepFacade(WalidatorZamowieniaFacade w, InwentarzSerwis i, PrzelicznikCen c,
        BramkaPlatnosci p, MagazynSerwis m, EmailNotyfikator e, AuditLog a)
    {
        _walidator = w; _inwentarz = i; _ceny = c;
        _platnosc = p; _magazyn = m; _email = e; _audit = a;
    }

    public async Task<WynikZlozenia> ZlozZamowienieAsync(
        ZamowienieFacade zam, CancellationToken ct = default)
    {
        var walidacja = _walidator.Waliduj(zam);
        if (!walidacja.CzyOk) return WynikZlozenia.NieUdane(walidacja.Komunikat!);

        foreach (var poz in zam.Pozycje)
            if (!_inwentarz.SprawdzDostepnosc(poz.ProduktId, poz.Ilosc))
                return WynikZlozenia.NieUdane($"Produkt #{poz.ProduktId} niedostępny");

        decimal suma   = _ceny.ObliczSume(zam.Pozycje);
        decimal rabat  = _ceny.ObliczRabat(zam.KlientId, suma);
        decimal kwota  = suma - rabat;

        string transId = await _platnosc.PobierzAsync(zam.KlientId, kwota, zam.Platnosc);

        foreach (var poz in zam.Pozycje)
            _inwentarz.ZarezerwujTowar(poz.ProduktId, poz.Ilosc);

        int zamId = new Random().Next(10000, 99999);
        _magazyn.ZlecWysylke(zamId, zam.KlientId, zam.Adres);
        await _email.WyslijPotwierdzenieAsync(zam.Email, zamId, kwota);
        _audit.Zapisz("ZamowienieZlozone", new { zamId, kwota, transId });

        return WynikZlozenia.Sukces(zamId, kwota, transId);
    }
}

// ============================================================
// 4. PROXY — pośrednik z kontrolą dostępu
// ============================================================
// "Ochroniarz" — kontroluje dostęp do prawdziwego obiektu

// Interfejs lokalny dla Proxy (niezależny od projektu 07)
public interface ICurrentUserProxy
{
    int UserId { get; }
    string Email { get; }
    bool IsAdmin { get; }
    string? TenantId { get; }
    IEnumerable<string> Roles { get; }
    bool HasClaim(string type, string value);
}

public interface ISerwisRaportow
{
    Task<RaportDto> GenerujRaportAsync(string typ, DateTime od, DateTime do_, CancellationToken ct = default);
    Task<byte[]> EksportujDoPdfAsync(RaportDto raport, CancellationToken ct = default);
}

public record RaportDto(string Typ, DateTime Od, DateTime Do_,
    int LiczbaRekordow, decimal SumaSprzedazy);

// Prawdziwy serwis — ciężki, wymaga uprawnień
public class SerwisRaportow : ISerwisRaportow
{
    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_, CancellationToken ct = default)
    {
        Console.WriteLine($"  [Raport] Generuję {typ} od {od:d} do {do_:d}...");
        await Task.Delay(50, ct);
        return new RaportDto(typ, od, do_, 1500, 250_000m);
    }

    public async Task<byte[]> EksportujDoPdfAsync(RaportDto raport, CancellationToken ct = default)
    {
        Console.WriteLine("  [Raport] Eksportuję do PDF...");
        await Task.Delay(20, ct);
        return System.Text.Encoding.UTF8.GetBytes($"PDF:{raport.Typ}");
    }
}

// Proxy 1 — Access Control
public class ProxyKontroliDostepu : ISerwisRaportow
{
    private readonly ISerwisRaportow _inner;
    private readonly ICurrentUserProxy _user;

    public ProxyKontroliDostepu(ISerwisRaportow inner, ICurrentUserProxy user)
    {
        _inner = inner; _user = user;
    }

    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_, CancellationToken ct = default)
    {
        SprawdzDostep(typ);
        if (!_user.IsAdmin && (do_ - od) > TimeSpan.FromDays(90))
            throw new UnauthorizedAccessException("Max 90 dni dla zwykłych użytkowników");
        return await _inner.GenerujRaportAsync(typ, od, do_, ct);
    }

    public async Task<byte[]> EksportujDoPdfAsync(RaportDto raport, CancellationToken ct = default)
    {
        if (!_user.HasClaim("permission", "export:pdf"))
            throw new UnauthorizedAccessException("Brak uprawnienia do eksportu PDF");
        return await _inner.EksportujDoPdfAsync(raport, ct);
    }

    private void SprawdzDostep(string typ)
    {
        bool ok = typ switch
        {
            "finansowy"   => _user.IsAdmin || _user.HasClaim("role", "Finanse"),
            "sprzedazowy" => _user.IsAdmin || _user.HasClaim("role", "Sprzedaz"),
            _             => _user.IsAdmin
        };
        if (!ok) throw new UnauthorizedAccessException($"Brak uprawnień do raportu: {typ}");
    }
}

// Proxy 2 — Cache
public class ProxyCache : ISerwisRaportow
{
    private readonly ISerwisRaportow _inner;
    private readonly Dictionary<string, (RaportDto R, DateTime Wygasa)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public ProxyCache(ISerwisRaportow inner) => _inner = inner;

    public async Task<RaportDto> GenerujRaportAsync(
        string typ, DateTime od, DateTime do_, CancellationToken ct = default)
    {
        string k = $"{typ}:{od:yyyyMMdd}:{do_:yyyyMMdd}";
        if (_cache.TryGetValue(k, out var c) && c.Wygasa > DateTime.UtcNow)
        {
            Console.WriteLine($"  [ProxyCache HIT] {k}");
            return c.R;
        }
        Console.WriteLine($"  [ProxyCache MISS] {k}");
        var r = await _inner.GenerujRaportAsync(typ, od, do_, ct);
        _cache[k] = (r, DateTime.UtcNow.Add(_ttl));
        return r;
    }

    public Task<byte[]> EksportujDoPdfAsync(RaportDto raport, CancellationToken ct = default)
        => _inner.EksportujDoPdfAsync(raport, ct);
}

// Proxy 3 — Lazy Loading
public class ProxyLazyLoading : ISerwisRaportow
{
    private ISerwisRaportow? _inner;
    private readonly Func<ISerwisRaportow> _fabryka;

    public ProxyLazyLoading(Func<ISerwisRaportow> fabryka) => _fabryka = fabryka;

    private ISerwisRaportow PobierzSerwis() => _inner ??= _fabryka();

    public Task<RaportDto> GenerujRaportAsync(string typ, DateTime od, DateTime do_, CancellationToken ct = default)
        => PobierzSerwis().GenerujRaportAsync(typ, od, do_, ct);
    public Task<byte[]> EksportujDoPdfAsync(RaportDto raport, CancellationToken ct = default)
        => PobierzSerwis().EksportujDoPdfAsync(raport, ct);
}

// Mock dla demonstracji
public class MockAdminUser : ICurrentUserProxy
{
    public int UserId => 1;
    public string Email => "admin@test.pl";
    public bool IsAdmin => true;
    public string? TenantId => null;
    public IEnumerable<string> Roles => new[] { "Admin" };
    public bool HasClaim(string type, string value) => true;
}

// ============================================================
// RUNNER
// ============================================================

public static class WzorceStrukturalneDemo
{
    public static async Task Uruchom()
    {
        // --- ADAPTER ---
        Console.WriteLine("  [Adapter] Adapter=tłumacz: zmienia interfejs bez modyfikacji oryginału");
        var platKarta = FabrykaAdapterowPlatnosci.Pobierz("karta");
        var wynik = await platKarta.PobierzPlatnosc(
            new DanePlatnosci("4111111111111111", "12/26|123", 299.99m));
        Console.WriteLine($"  [Adapter karta] Sukces={wynik.Sukces}, ID={wynik.TransakcjaId?[..8]}...");
        decimal saldo = await platKarta.SprawdzSaldoAsync("4111111111111111");
        Console.WriteLine($"  [Adapter karta] Saldo={saldo} PLN (string→decimal)");

        var platPP = FabrykaAdapterowPlatnosci.Pobierz("paypal");
        var wynikPP = await platPP.PobierzPlatnosc(
            new DanePlatnosci("jan@test.pl", null, 150m));
        Console.WriteLine($"  [Adapter PayPal] Sukces={wynikPP.Sukces}");

        // --- DECORATOR ---
        Console.WriteLine("  [Decorator] Decorator=ozdobnik: stackujemy Retry→Cache→Log→DB");
        IProduktRepo repo =
            new RetryProduktRepo(
                new CachedProduktRepo(
                    new LogowanyProduktRepo(
                        new ProduktRepoDb())));

        var prod = await repo.PobierzPoIdAsync(1);
        Console.WriteLine($"  [Decorator] Produkt: {prod?.Nazwa}");
        var prod2 = await repo.PobierzPoIdAsync(1); // cache hit
        Console.WriteLine($"  [Decorator] Ponowne pobranie (cache): {prod2?.Nazwa}");

        // --- FACADE ---
        Console.WriteLine("  [Facade] Facade=recepcja: 7 serwisów → 1 metoda ZlozZamowienieAsync");
        var facade = new SklepFacade(
            new WalidatorZamowieniaFacade(), new InwentarzSerwis(),
            new PrzelicznikCen(), new BramkaPlatnosci(),
            new MagazynSerwis(), new EmailNotyfikator(), new AuditLog());

        var wynikFacade = await facade.ZlozZamowienieAsync(new ZamowienieFacade
        {
            KlientId = 101,
            Email    = "jan@test.pl",
            Pozycje  = new() { new(1, 2, 3500m), new(2, 1, 150m) },
            Adres    = new AdresFacade("ul. Testowa 1", "Warszawa", "00-001"),
            Platnosc = "karta"
        });
        Console.WriteLine($"  [Facade] ZamId=#{wynikFacade.ZamId}, Kwota={wynikFacade.Kwota:C}");

        // Błąd — brak towaru
        var wynikBlad = await facade.ZlozZamowienieAsync(new ZamowienieFacade
        {
            KlientId = 101, Email = "x@x.pl",
            Pozycje  = new() { new(3, 5, 50m) }, // stan=0, potrzeba 5
            Adres    = new AdresFacade("ul. X", "Poznań", "61-001"), Platnosc = "karta"
        });
        Console.WriteLine($"  [Facade] Błąd (oczekiwany): {wynikBlad.Blad}");

        // --- PROXY ---
        Console.WriteLine("  [Proxy] Proxy=ochroniarz: Access Control → Cache → LazyLoad → Serwis");
        ISerwisRaportow serwis =
            new ProxyKontroliDostepu(
                new ProxyCache(
                    new ProxyLazyLoading(() => new SerwisRaportow())),
                new MockAdminUser());

        var raport = await serwis.GenerujRaportAsync(
            "sprzedazowy", DateTime.Today.AddMonths(-1), DateTime.Today);
        Console.WriteLine($"  [Proxy] Raport: {raport.LiczbaRekordow} rekordów, {raport.SumaSprzedazy:C}");

        var raport2 = await serwis.GenerujRaportAsync(
            "sprzedazowy", DateTime.Today.AddMonths(-1), DateTime.Today); // cache hit
        Console.WriteLine($"  [Proxy] Ponownie (cache): {raport2.SumaSprzedazy:C}");

        var pdf = await serwis.EksportujDoPdfAsync(raport);
        Console.WriteLine($"  [Proxy] PDF: {pdf.Length} bajtów");

        Console.WriteLine("  [Porównanie] Adapter=inny interfejs | Decorator=dodaj funkcję | Facade=uprość | Proxy=kontroluj dostęp");
    }
}
