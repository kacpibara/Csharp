### Wzorce Kreacyjne w C#

Wzorce kreacyjne rozwiązują **jak tworzyć obiekty** — oddzielają logikę tworzenia od logiki użytkowania.

---

### 1. Singleton — jedna instancja

csharp

```csharp
// Singleton — gwarantuje że klasa ma tylko jedną instancję
// i zapewnia globalny punkt dostępu

// === WERSJA 1 — klasyczna (thread-safe z lock) ===
public class KlasycznyKonfiguracja
{
    private static KlasycznyKonfiguracja? _instancja;
    private static readonly object _zamek = new();

    // Prywatny konstruktor — nikt spoza klasy nie tworzy instancji
    private KlasycznyKonfiguracja()
    {
        Console.WriteLine("Tworzę KlasycznyKonfiguracja");
    }

    public static KlasycznyKonfiguracja Instancja
    {
        get
        {
            if (_instancja is null)
            {
                lock (_zamek)  // thread-safe!
                {
                    _instancja ??= new KlasycznyKonfiguracja();
                }
            }
            return _instancja;
        }
    }

    public string PobierzUstawienie(string klucz) => $"wartosc_{klucz}";
}

// Użycie
var cfg1 = KlasycznyKonfiguracja.Instancja;
var cfg2 = KlasycznyKonfiguracja.Instancja;
Console.WriteLine(ReferenceEquals(cfg1, cfg2));  // True — ten sam obiekt!

// === WERSJA 2 — Lazy<T> — leniwa inicjalizacja (ZALECANE) ===
public class LazySingleton
{
    // Lazy<T> — thread-safe, inicjalizacja przy pierwszym użyciu
    private static readonly Lazy<LazySingleton> _lazy =
        new(() => new LazySingleton());

    private LazySingleton()
    {
        Console.WriteLine("Tworzę LazySingleton");
    }

    public static LazySingleton Instancja => _lazy.Value;

    public void ZrobCos() => Console.WriteLine("Robię coś...");
}

// === WERSJA 3 — static constructor — najprostsza ===
public class StaticSingleton
{
    // CLR gwarantuje że static constructor jest wywoływany raz
    // i jest thread-safe z natury
    public static readonly StaticSingleton Instancja = new();

    private StaticSingleton() { }

    public string Wersja => "1.0";
}

// === WERSJA 4 — przez DI (ASP.NET Core) — NAJLEPSZA dla aplikacji ===
// Nie implementuj Singleton ręcznie — użyj DI kontenera!
public class CacheManager
{
    private readonly Dictionary<string, object> _cache = new();

    public void Dodaj(string klucz, object wartosc)
        => _cache[klucz] = wartosc;

    public T? Pobierz<T>(string klucz)
        => _cache.TryGetValue(klucz, out var v) ? (T)v : default;
}

// Program.cs — rejestracja jako Singleton przez DI
// builder.Services.AddSingleton<CacheManager>();
// Jeden obiekt przez całe życie aplikacji — thread-safe przez DI

// === ANTI-PATTERN: Singleton jako global state ===
// Singleton utrudnia testowanie — nie można podmienić instancji!
// Problem z Singleton:
public class ZlySingleton
{
    public static ZlySingleton Instancja { get; } = new();
    private ZlySingleton() { }

    public string PobierzDaneZBazy() => "dane";  // jak przetestować bez bazy?
}

// Rozwiązanie — interfejs + DI zamiast bezpośredniego Singletona
public interface ICache { void Dodaj(string k, object v); T? Pobierz<T>(string k); }
public class InMemoryCache : ICache
{
    private readonly Dictionary<string, object> _d = new();
    public void Dodaj(string k, object v) => _d[k] = v;
    public T? Pobierz<T>(string k) => _d.TryGetValue(k, out var v) ? (T)v : default;
}
// builder.Services.AddSingleton<ICache, InMemoryCache>();
// W teście: podmień na MockCache!
```

---

### 2. Factory Method — fabryka przez dziedziczenie

csharp

```csharp
// Factory Method — definiuje interfejs tworzenia obiektów,
// ale pozwala podklasom decydować JAKĄ klasę instancjować

// Problem — tworzenie połączeń do różnych baz danych
public interface IPolaczenieBazy
{
    void Otworz();
    void Zamknij();
    IEnumerable<T> Wykonaj<T>(string sql);
}

public class SqlServerPolaczenie : IPolaczenieBazy
{
    private readonly string _connStr;
    public SqlServerPolaczenie(string connStr) => _connStr = connStr;
    public void Otworz()    => Console.WriteLine($"SQL Server: {_connStr}");
    public void Zamknij()   => Console.WriteLine("SQL Server zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"SQL Server wykonuje: {sql}");
        return Enumerable.Empty<T>();
    }
}

public class PostgresPolaczenie : IPolaczenieBazy
{
    private readonly string _connStr;
    public PostgresPolaczenie(string connStr) => _connStr = connStr;
    public void Otworz()    => Console.WriteLine($"PostgreSQL: {_connStr}");
    public void Zamknij()   => Console.WriteLine("PostgreSQL zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"PostgreSQL wykonuje: {sql}");
        return Enumerable.Empty<T>();
    }
}

public class SQLitePolaczenie : IPolaczenieBazy
{
    private readonly string _sciezka;
    public SQLitePolaczenie(string sciezka) => _sciezka = sciezka;
    public void Otworz()    => Console.WriteLine($"SQLite: {_sciezka}");
    public void Zamknij()   => Console.WriteLine("SQLite zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"SQLite wykonuje: {sql}");
        return Enumerable.Empty<T>();
    }
}

// Fabryka bazowa — definiuje metodę fabryczną
public abstract class BazaDanychFabryka
{
    // Factory Method — abstrakcyjna metoda tworzenia
    protected abstract IPolaczenieBazy UtworzPolaczenie(string connStr);

    // Logika biznesowa używa Factory Method — nie zna konkretnej klasy!
    public IPolaczenieBazy PobierzPolaczenie(string connStr)
    {
        var polaczenie = UtworzPolaczenie(connStr);  // factory method!
        Console.WriteLine($"Tworzę połączenie: {polaczenie.GetType().Name}");
        return polaczenie;
    }
}

// Konkretne fabryki — implementują Factory Method
public class SqlServerFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string connStr)
        => new SqlServerPolaczenie(connStr);
}

public class PostgresFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string connStr)
        => new PostgresPolaczenie(connStr);
}

public class SQLiteFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string sciezka)
        => new SQLitePolaczenie(sciezka);
}

// Użycie Factory Method
BazaDanychFabryka fabryka = Environment.GetEnvironmentVariable("DB_TYPE") switch
{
    "postgres" => new PostgresFabryka(),
    "sqlite"   => new SQLiteFabryka(),
    _          => new SqlServerFabryka()  // domyślnie SQL Server
};

var polaczenie = fabryka.PobierzPolaczenie("Server=localhost;...");
polaczenie.Otworz();
polaczenie.Wykonaj<object>("SELECT * FROM produkty");
polaczenie.Zamknij();

// === PROSTA FABRYKA — statyczna (nie wzorzec GoF, ale bardzo użyteczna) ===
public static class PolaczenieFabryka
{
    public static IPolaczenieBazy Utworz(string provider, string connStr)
        => provider.ToLower() switch
        {
            "sqlserver" => new SqlServerPolaczenie(connStr),
            "postgres"  => new PostgresPolaczenie(connStr),
            "sqlite"    => new SQLitePolaczenie(connStr),
            _ => throw new ArgumentException($"Nieznany provider: {provider}")
        };
}

var pol = PolaczenieFabryka.Utworz("postgres", "Host=localhost;Database=sklep");
pol.Otworz();
```

---

### 3. Abstract Factory — rodziny obiektów

csharp

```csharp
// Abstract Factory — tworzy RODZINY powiązanych obiektów
// bez wskazywania konkretnych klas

// Problem — różne style UI (Windows, Mac, Linux)
// Każdy styl ma swój Button i TextField — muszą być spójne!

// === PRODUKTY ===
// Interfejsy abstrakcyjne
public interface IPrzycisk
{
    void Renderuj();
    void Kliknij();
    string NazwaStyle { get; }
}

public interface IPoletekstowe
{
    void Renderuj();
    string PobierzTekst();
    void UstawTekst(string tekst);
    string NazwaStyle { get; }
}

public interface IPasekNarzedziowy
{
    void Renderuj();
    void DodajPrzycisk(IPrzycisk przycisk);
}

// Windows implementacje
public class WindowsPrzycisk : IPrzycisk
{
    private readonly string _etykieta;
    public WindowsPrzycisk(string etykieta) => _etykieta = etykieta;
    public string NazwaStyle  => "Windows";
    public void Renderuj()    => Console.WriteLine($"[Windows Btn: {_etykieta}]");
    public void Kliknij()     => Console.WriteLine($"Windows click: {_etykieta}");
}

public class WindowsPoleTekstowe : IPoleTextowe
{
    private string _tekst = "";
    public string NazwaStyle    => "Windows";
    public void Renderuj()      => Console.WriteLine($"[Windows Input: {_tekst}]");
    public string PobierzTekst()=> _tekst;
    public void UstawTekst(string t) { _tekst = t; Console.WriteLine($"Windows input: {t}"); }
}

public class WindowsPasekNarzedziowy : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _przyciski = new();
    public void Renderuj() => Console.WriteLine($"[Windows Toolbar: {_przyciski.Count} btns]");
    public void DodajPrzycisk(IPrzycisk p) => _przyciski.Add(p);
}

// Mac implementacje
public class MacPrzycisk : IPrzycisk
{
    private readonly string _etykieta;
    public MacPrzycisk(string etykieta) => _etykieta = etykieta;
    public string NazwaStyle  => "macOS";
    public void Renderuj()    => Console.WriteLine($"(macOS Btn: {_etykieta})");
    public void Kliknij()     => Console.WriteLine($"macOS click: {_etykieta}");
}

public class MacPoleTekstowe : IPoleTextowe
{
    private string _tekst = "";
    public string NazwaStyle    => "macOS";
    public void Renderuj()      => Console.WriteLine($"(macOS Field: {_tekst})");
    public string PobierzTekst()=> _tekst;
    public void UstawTekst(string t) { _tekst = t; Console.WriteLine($"macOS input: {t}"); }
}

public class MacPasekNarzedziowy : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _przyciski = new();
    public void Renderuj() => Console.WriteLine($"(macOS Toolbar: {_przyciski.Count} btns)");
    public void DodajPrzycisk(IPrzycisk p) => _przyciski.Add(p);
}

// Linux implementacje
public class LinuxPrzycisk : IPrzycisk
{
    private readonly string _etykieta;
    public LinuxPrzycisk(string etykieta) => _etykieta = etykieta;
    public string NazwaStyle  => "GTK";
    public void Renderuj()    => Console.WriteLine($"<GTK Btn: {_etykieta}>");
    public void Kliknij()     => Console.WriteLine($"GTK click: {_etykieta}");
}

public class LinuxPoleTekstowe : IPoleTextowe
{
    private string _tekst = "";
    public string NazwaStyle    => "GTK";
    public void Renderuj()      => Console.WriteLine($"<GTK Entry: {_tekst}>");
    public string PobierzTekst()=> _tekst;
    public void UstawTekst(string t) { _tekst = t; Console.WriteLine($"GTK entry: {t}"); }
}

public class LinuxPasekNarzedziowy : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _przyciski = new();
    public void Renderuj() => Console.WriteLine($"<GTK Toolbar: {_przyciski.Count} btns>");
    public void DodajPrzycisk(IPrzycisk p) => _przyciski.Add(p);
}

// === ABSTRACT FACTORY ===
public interface IUIFabryka
{
    IPrzycisk         UtworzPrzycisk(string etykieta);
    IPoleTextowe      UtworzPoleTekstowe();
    IPasekNarzedziowy UtworzPasekNarzedziowy();
}

// Konkretne fabryki
public class WindowsFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e)  => new WindowsPrzycisk(e);
    public IPoleTextowe      UtworzPoleTekstowe()      => new WindowsPoleTekstowe();
    public IPasekNarzedziowy UtworzPasekNarzedziowy()  => new WindowsPasekNarzedziowy();
}

public class MacFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e)  => new MacPrzycisk(e);
    public IPoleTextowe      UtworzPoleTekstowe()      => new MacPoleTekstowe();
    public IPasekNarzedziowy UtworzPasekNarzedziowy()  => new MacPasekNarzedziowy();
}

public class LinuxFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e)  => new LinuxPrzycisk(e);
    public IPoleTextowe      UtworzPoleTekstowe()      => new LinuxPoleTekstowe();
    public IPasekNarzedziowy UtworzPasekNarzedziowy()  => new LinuxPasekNarzedziowy();
}

// Aplikacja — nie wie o konkretnych klasach UI
public class OknoDialoguLogowania
{
    private readonly IPrzycisk         _btnZaloguj;
    private readonly IPrzycisk         _btnAnuluj;
    private readonly IPoleTextowe      _poleEmail;
    private readonly IPoleTextowe      _poleHaslo;
    private readonly IPasekNarzedziowy _pasek;

    // Wstrzyknij fabrykę — nie konkretne klasy!
    public OknoDialoguLogowania(IUIFabryka fabryka)
    {
        _btnZaloguj = fabryka.UtworzPrzycisk("Zaloguj");
        _btnAnuluj  = fabryka.UtworzPrzycisk("Anuluj");
        _poleEmail  = fabryka.UtworzPoleTekstowe();
        _poleHaslo  = fabryka.UtworzPoleTekstowe();
        _pasek      = fabryka.UtworzPasekNarzedziowy();

        _pasek.DodajPrzycisk(_btnZaloguj);
        _pasek.DodajPrzycisk(_btnAnuluj);

        Console.WriteLine($"Tworzę dialog dla: {_btnZaloguj.NazwaStyle}");
    }

    public void Pokaz()
    {
        Console.WriteLine("=== Okno logowania ===");
        _poleEmail.UstawTekst("user@example.com");
        _poleHaslo.UstawTekst("****");
        _poleEmail.Renderuj();
        _poleHaslo.Renderuj();
        _pasek.Renderuj();
    }

    public string Zaloguj()
    {
        _btnZaloguj.Kliknij();
        return _poleEmail.PobierzTekst();
    }
}

// Factory selector — wybierz fabrykę bazując na systemie
IUIFabryka fabrykaSys = System.Runtime.InteropServices.RuntimeInformation
    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
    ? new WindowsFabryka()
    : System.Runtime.InteropServices.RuntimeInformation
        .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
    ? new MacFabryka()
    : new LinuxFabryka();

var dialog = new OknoDialoguLogowania(fabrykaSys);
dialog.Pokaz();
dialog.Zaloguj();

// Poprawka brakujących interfejsów
public interface IPoleTextowe : IPoleTextowe { }
```

---

### 4. Builder — budowanie złożonych obiektów

csharp

```csharp
// Builder — budowanie złożonych obiektów krok po kroku
// Oddziela konstruowanie od reprezentacji

// Problem — zamówienie z wieloma opcjonalnymi polami
// Konstruktor z 15 parametrami → czytelność = 0

// === KLASYCZNY BUILDER ===
public class Zamowienie
{
    public int          Id             { get; private set; }
    public int          KlientId       { get; private set; }
    public List<Pozycja>Pozycje        { get; private set; } = new();
    public string       Status         { get; private set; } = "Nowe";
    public AdresDostawy Adres          { get; private set; } = null!;
    public string?      Komentarz      { get; private set; }
    public bool         CzyPilne       { get; private set; }
    public DateTime     DataDostawy    { get; private set; }
    public string       MetodaPlatnosci{ get; private set; } = "";
    public decimal      Rabat          { get; private set; }
    public string?      KodPromocyjny  { get; private set; }

    // Prywatny konstruktor — tylko Builder może tworzyć
    private Zamowienie() { }

    public decimal Suma => Pozycje.Sum(p => p.Cena * p.Ilosc)
                           * (1 - Rabat / 100);

    // Wewnętrzna klasa Builder
    public class Builder
    {
        private readonly Zamowienie _zamowienie = new();

        public Builder DlaKlienta(int klientId)
        {
            _zamowienie.KlientId = klientId;
            return this;  // fluent API — metody zwracają Builder
        }

        public Builder DodajPozycje(int produktId, int ilosc, decimal cena)
        {
            _zamowienie.Pozycje.Add(
                new Pozycja(produktId, ilosc, cena));
            return this;
        }

        public Builder DostarczyNa(AdresDostawy adres)
        {
            _zamowienie.Adres = adres;
            return this;
        }

        public Builder ZKomentarzem(string komentarz)
        {
            _zamowienie.Komentarz = komentarz;
            return this;
        }

        public Builder JakoPilne()
        {
            _zamowienie.CzyPilne   = true;
            _zamowienie.DataDostawy = DateTime.Today.AddDays(1);
            return this;
        }

        public Builder ZDataDostawy(DateTime data)
        {
            _zamowienie.DataDostawy = data;
            return this;
        }

        public Builder ZPlatnosciaKarta()
        {
            _zamowienie.MetodaPlatnosci = "Karta";
            return this;
        }

        public Builder ZPlatnoscia(string metoda)
        {
            _zamowienie.MetodaPlatnosci = metoda;
            return this;
        }

        public Builder ZRabatem(decimal procent)
        {
            if (procent < 0 || procent > 100)
                throw new ArgumentException("Rabat 0-100%");
            _zamowienie.Rabat = procent;
            return this;
        }

        public Builder ZKodemPromocyjnym(string kod)
        {
            _zamowienie.KodPromocyjny = kod;
            return this;
        }

        // Build — walidacja i zwrócenie gotowego obiektu
        public Zamowienie Buduj()
        {
            // Walidacja
            if (_zamowienie.KlientId <= 0)
                throw new InvalidOperationException("KlientId jest wymagany");

            if (!_zamowienie.Pozycje.Any())
                throw new InvalidOperationException("Zamówienie musi mieć pozycje");

            if (_zamowienie.Adres is null)
                throw new InvalidOperationException("Adres dostawy jest wymagany");

            if (string.IsNullOrEmpty(_zamowienie.MetodaPlatnosci))
                throw new InvalidOperationException("Metoda płatności jest wymagana");

            if (_zamowienie.DataDostawy == default)
                _zamowienie.DataDostawy = DateTime.Today.AddDays(3);

            _zamowienie.Id = new Random().Next(1000, 9999);  // symulacja ID

            return _zamowienie;
        }
    }

    // Statyczna fabryka Builder — wygodne wejście
    public static Builder Nowe() => new Builder();
}

public record Pozycja(int ProduktId, int Ilosc, decimal Cena);
public record AdresDostawy(string Ulica, string Miasto, string Kod);

// Użycie — czytelne i elastyczne!
var zamowienie = Zamowienie.Nowe()
    .DlaKlienta(42)
    .DodajPozycje(1, 2, 3500m)          // Laptop × 2
    .DodajPozycje(2, 1,  150m)          // Mysz × 1
    .DodajPozycje(3, 3,   49m)          // Kabel × 3
    .DostarczyNa(new AdresDostawy(
        "ul. Marszałkowska 1",
        "Warszawa",
        "00-001"))
    .JakoPilne()                         // jutro
    .ZPlatnosciaKarta()
    .ZRabatem(10m)
    .ZKomentarzem("Dzwoń przed dostawą")
    .Buduj();

Console.WriteLine($"Zamówienie #{zamowienie.Id}");
Console.WriteLine($"Pozycji: {zamowienie.Pozycje.Count}");
Console.WriteLine($"Suma: {zamowienie.Suma:C}");
Console.WriteLine($"Pilne: {zamowienie.CzyPilne}");
Console.WriteLine($"Dostawa: {zamowienie.DataDostawy:dd.MM.yyyy}");
```

---

### 5. Builder — zaawansowane wzorce

csharp

```csharp
// === FLUENT BUILDER z generics — reużywalny ===
public abstract class BaseBuilder<T, TBuilder>
    where T : class
    where TBuilder : BaseBuilder<T, TBuilder>
{
    protected T _obiekt;

    protected BaseBuilder() => _obiekt = UtworzNowy();

    protected abstract T UtworzNowy();

    // Zwróć this jako TBuilder — dla poprawnego fluent API w podklasach
    protected TBuilder This => (TBuilder)this;

    public abstract T Buduj();
}

// === BUILDER z Director — predefiniowane konfiguracje ===
public class EmailBuilder
{
    private string _od    = "";
    private string _do    = "";
    private string _temat = "";
    private string _tresc = "";
    private bool   _html  = false;
    private List<string> _cc  = new();
    private List<string> _bcc = new();
    private List<string> _zalaczniki = new();

    public EmailBuilder Od(string email)    { _od = email;    return this; }
    public EmailBuilder Do(string email)    { _do = email;    return this; }
    public EmailBuilder Temat(string t)     { _temat = t;     return this; }
    public EmailBuilder Tresc(string t)     { _tresc = t;     return this; }
    public EmailBuilder HtmlTresc(string t) { _tresc = t; _html = true; return this; }
    public EmailBuilder DodajCC(string e)   { _cc.Add(e);   return this; }
    public EmailBuilder DodajBCC(string e)  { _bcc.Add(e);  return this; }
    public EmailBuilder DodajZalacznik(string s) { _zalaczniki.Add(s); return this; }

    public EmailWiadomosc Buduj()
    {
        if (string.IsNullOrEmpty(_od))   throw new InvalidOperationException("Brak Od");
        if (string.IsNullOrEmpty(_do))   throw new InvalidOperationException("Brak Do");
        if (string.IsNullOrEmpty(_temat))throw new InvalidOperationException("Brak Tematu");

        return new EmailWiadomosc(_od, _do, _temat, _tresc, _html,
            _cc.AsReadOnly(), _bcc.AsReadOnly(), _zalaczniki.AsReadOnly());
    }
}

public record EmailWiadomosc(
    string Od, string Do, string Temat, string Tresc, bool Html,
    IReadOnlyList<string> CC, IReadOnlyList<string> BCC,
    IReadOnlyList<string> Zalaczniki);

// Director — wie jak budować typowe konfiguracje
public class EmailDirector
{
    private readonly string _adresNadawcy;

    public EmailDirector(string adresNadawcy)
        => _adresNadawcy = adresNadawcy;

    // Predefiniowana konfiguracja — email powitalny
    public EmailWiadomosc BudujEmailPowitalny(
        string doEmail, string imieUzytkownika)
    {
        return new EmailBuilder()
            .Od(_adresNadawcy)
            .Do(doEmail)
            .Temat($"Witamy, {imieUzytkownika}!")
            .HtmlTresc($"""
                <h1>Witaj {imieUzytkownika}!</h1>
                <p>Dziękujemy za rejestrację w naszym sklepie.</p>
                <a href="https://sklep.pl/aktywuj">Aktywuj konto</a>
                """)
            .Buduj();
    }

    // Predefiniowana konfiguracja — powiadomienie o zamówieniu
    public EmailWiadomosc BudujPowiadomienieZamowienia(
        string doEmail, int zamId, decimal suma)
    {
        return new EmailBuilder()
            .Od(_adresNadawcy)
            .Do(doEmail)
            .DodajBCC("zamowienia@sklep.pl")
            .Temat($"Potwierdzenie zamówienia #{zamId}")
            .HtmlTresc($"""
                <h2>Zamówienie #{zamId} przyjęte</h2>
                <p>Suma: {suma:C}</p>
                """)
            .Buduj();
    }

    // Predefiniowana konfiguracja — alert dla admina
    public EmailWiadomosc BudujAlertAdmin(
        string temat, string tresc, string[] odbiorcy)
    {
        var builder = new EmailBuilder()
            .Od(_adresNadawcy)
            .Do(odbiorcy[0])
            .Temat($"[ALERT] {temat}")
            .Tresc(tresc);

        // Dodaj pozostałych jako CC
        foreach (var o in odbiorcy.Skip(1))
            builder.DodajCC(o);

        return builder.Buduj();
    }
}

// Użycie Director
var director = new EmailDirector("noreply@sklep.pl");

var emailPowitalny = director.BudujEmailPowitalny(
    "jan@test.pl", "Jan");
Console.WriteLine($"Email: {emailPowitalny.Temat}");

var emailZam = director.BudujPowiadomienieZamowienia(
    "jan@test.pl", 1001, 3850m);
Console.WriteLine($"Email: {emailZam.Temat}");

// Lub własna konfiguracja — pomiń Director
var customEmail = new EmailBuilder()
    .Od("support@sklep.pl")
    .Do("jan@test.pl")
    .DodajCC("manager@sklep.pl")
    .Temat("Odpowiedź na Twoje zgłoszenie")
    .HtmlTresc("<p>Dziękujemy za kontakt...</p>")
    .DodajZalacznik("/files/regulamin.pdf")
    .Buduj();

Console.WriteLine($"Attachments: {customEmail.Zalaczniki.Count}");
```

---

### 6. Porównanie i kiedy co używać

csharp

```csharp
// === PODSUMOWANIE WZORCÓW KREACYJNYCH ===

// SINGLETON
// Kiedy: jeden obiekt przez całe życie aplikacji
// Przykłady: konfiguracja, cache, logger, connection pool
// W ASP.NET Core: builder.Services.AddSingleton<T>()
// ⚠️ Uważaj na global state i problemy z testowaniem

// FACTORY METHOD
// Kiedy: tworzenie obiektów przez dziedziczenie
//        podklasy decydują co tworzyć
// Przykłady: różne parsery, connektory do baz, serializers
// ✅ Łatwe dodanie nowego typu bez modyfikacji istniejącego kodu (OCP)

// ABSTRACT FACTORY
// Kiedy: rodziny powiązanych obiektów muszą być spójne
// Przykłady: UI themes, cloud providers (AWS/Azure/GCP), DB providers
// ✅ Gwarantuje spójność rodziny obiektów
// ⚠️ Trudne dodanie nowego produktu do rodziny

// BUILDER
// Kiedy: złożone obiekty z wieloma opcjonalnymi parametrami
//        tworzenie etapami, różne reprezentacje
// Przykłady: zapytania SQL, emaile, konfiguracje, dokumenty
// ✅ Czytelne API, walidacja przy Build()
// ✅ Ten sam builder dla różnych reprezentacji (przez Director)

// Przykładowe zastosowania w .NET:
// WebApplication.CreateBuilder(args) — Builder
// HttpClient — Builder pattern (HttpClientFactory)
// StringBuilder                     — Builder
// IServiceCollection (DI)           — Builder + Fluent API
// EF Core ModelBuilder              — Builder
// JsonSerializerOptions              — konfiguracja jak Builder

// Quick reference:
// Jeden obiekt globalny   → Singleton
// Twórz przez subklasy    → Factory Method
// Rodziny spójnych obiektów → Abstract Factory
// Złożone etapowe tworzenie → Builder
```

---

### Typowe pytania rekrutacyjne

**"Jaka różnica między Factory Method a Abstract Factory?"** Factory Method definiuje jedną metodę tworzenia produktu — podklasy decydują jaką konkretną klasę instancjować. Jeden typ produktu, rozszerzanie przez dziedziczenie. Abstract Factory tworzy RODZINY powiązanych produktów — jeden interfejs fabryki z wieloma metodami tworzenia. Gwarantuje że Button i TextField będą ze spójnego zestawu (np. oba Windows lub oba Mac). Factory Method = jedno dzieło, Abstract Factory = cała kolekcja spójnych dzieł.

**"Kiedy Builder zamiast konstruktora z wieloma parametrami?"** Gdy obiekt ma więcej niż 3-4 parametry, szczególnie opcjonalne. Konstruktor `new Zamowienie(1, null, null, true, false, "karta", 0, null)` — nie wiadomo co co oznacza. Builder `Zamowienie.Nowe().DlaKlienta(1).JakoPilne().ZPlatnosciaKarta().Buduj()` — samodokumentujące się. Dodatkowe zalety: walidacja w `Buduj()`, niemożliwe stworzenie niepoprawnego obiektu, możliwość ponownego użycia Builder dla różnych konfiguracji.

**"Co to problem z Singleton i jak go rozwiązać?"** Singleton jako global state utrudnia testowanie — nie można podmienić instancji na mock. Kod zależy od konkretnej klasy, nie interfejsu — tight coupling. Rozwiązanie: Singleton przez DI z interfejsem. `AddSingleton<ICache, MemoryCache>()` — jeden obiekt przez życie aplikacji, ale zależność przez interfejs, w testach podmień na `MockCache`. Zachowujesz korzyść jednej instancji, bez problemów z testowalnością.

**"Jaka różnica między Abstract Factory a Builder?"** Abstract Factory skupia się na CO tworzyć — zwraca gotowy obiekt jedną metodą, produkty są proste. Builder skupia się na JAK tworzyć — buduje jeden złożony obiekt etapami, kontrolujesz każdy aspekt konstrukcji. Abstract Factory: `fabryka.UtworzPrzycisk("OK")` — jeden krok. Builder: `builder.DlaKlienta(1).DodajPozycje(...).ZPlatnoscia("karta").Buduj()` — wiele kroków, jeden finalny obiekt.