namespace _08_DesignPatterns_Architecture;

// ============================================================
// WZORCE KREACYJNE — Singleton, Factory Method, Abstract Factory, Builder
// ============================================================

// ============================================================
// 1. SINGLETON — jedna instancja
// ============================================================

// Wersja 1 — klasyczna (thread-safe z double-check lock)
public class KlasycznyKonfiguracja
{
    private static KlasycznyKonfiguracja? _instancja;
    private static readonly object _zamek = new();
    private KlasycznyKonfiguracja() { }

    public static KlasycznyKonfiguracja Instancja
    {
        get
        {
            if (_instancja is null)
                lock (_zamek)
                    _instancja ??= new KlasycznyKonfiguracja();
            return _instancja;
        }
    }

    public string PobierzUstawienie(string klucz) => $"wartosc_{klucz}";
}

// Wersja 2 — Lazy<T> — leniwa inicjalizacja (ZALECANE)
public class LazySingleton
{
    private static readonly Lazy<LazySingleton> _lazy = new(() => new LazySingleton());
    private LazySingleton() { }
    public static LazySingleton Instancja => _lazy.Value;
    public void ZrobCos() => Console.WriteLine("  [LazySingleton] Robię coś...");
}

// Wersja 3 — static constructor — CLR gwarantuje thread-safety
public class StaticSingleton
{
    public static readonly StaticSingleton Instancja = new();
    private StaticSingleton() { }
    public string Wersja => "1.0";
}

// Wersja 4 — interfejs + DI (NAJLEPSZA dla aplikacji)
// W teście podmień InMemoryCache na MockCache!
public interface ICache
{
    void Dodaj(string k, object v);
    T? Pobierz<T>(string k);
}
public class InMemoryCache : ICache
{
    private readonly Dictionary<string, object> _d = new();
    public void Dodaj(string k, object v) => _d[k] = v;
    public T? Pobierz<T>(string k) => _d.TryGetValue(k, out var v) ? (T)v : default;
}
// builder.Services.AddSingleton<ICache, InMemoryCache>();

// ============================================================
// 2. FACTORY METHOD — fabryka przez dziedziczenie
// ============================================================

public interface IPolaczenieBazy
{
    void Otworz();
    void Zamknij();
    IEnumerable<T> Wykonaj<T>(string sql);
}

public class SqlServerPolaczenie : IPolaczenieBazy
{
    private readonly string _cs;
    public SqlServerPolaczenie(string cs) => _cs = cs;
    public void Otworz()  => Console.WriteLine($"  [SQL Server] {_cs}");
    public void Zamknij() => Console.WriteLine("  [SQL Server] zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"  [SQL Server] {sql}");
        return Enumerable.Empty<T>();
    }
}

public class PostgresPolaczenie : IPolaczenieBazy
{
    private readonly string _cs;
    public PostgresPolaczenie(string cs) => _cs = cs;
    public void Otworz()  => Console.WriteLine($"  [PostgreSQL] {_cs}");
    public void Zamknij() => Console.WriteLine("  [PostgreSQL] zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"  [PostgreSQL] {sql}");
        return Enumerable.Empty<T>();
    }
}

public class SQLitePolaczenie : IPolaczenieBazy
{
    private readonly string _sc;
    public SQLitePolaczenie(string sc) => _sc = sc;
    public void Otworz()  => Console.WriteLine($"  [SQLite] {_sc}");
    public void Zamknij() => Console.WriteLine("  [SQLite] zamknięty");
    public IEnumerable<T> Wykonaj<T>(string sql)
    {
        Console.WriteLine($"  [SQLite] {sql}");
        return Enumerable.Empty<T>();
    }
}

// Fabryka bazowa — definiuje Factory Method
public abstract class BazaDanychFabryka
{
    protected abstract IPolaczenieBazy UtworzPolaczenie(string connStr);
    public IPolaczenieBazy PobierzPolaczenie(string connStr)
    {
        var p = UtworzPolaczenie(connStr);
        Console.WriteLine($"  [FactoryMethod] Tworzę {p.GetType().Name}");
        return p;
    }
}

public class SqlServerFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string cs) => new SqlServerPolaczenie(cs);
}
public class PostgresFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string cs) => new PostgresPolaczenie(cs);
}
public class SQLiteFabryka : BazaDanychFabryka
{
    protected override IPolaczenieBazy UtworzPolaczenie(string s) => new SQLitePolaczenie(s);
}

// Prosta fabryka statyczna (nie wzorzec GoF, ale bardzo użyteczna)
public static class PolaczenieFabryka
{
    public static IPolaczenieBazy Utworz(string provider, string connStr) =>
        provider.ToLower() switch
        {
            "sqlserver" => new SqlServerPolaczenie(connStr),
            "postgres"  => new PostgresPolaczenie(connStr),
            "sqlite"    => new SQLitePolaczenie(connStr),
            _ => throw new ArgumentException($"Nieznany provider: {provider}")
        };
}

// ============================================================
// 3. ABSTRACT FACTORY — rodziny obiektów
// ============================================================

public interface IPrzycisk
{
    void Renderuj();
    void Kliknij();
    string NazwaStyle { get; }
}

public interface IPoleTekstowe
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

// Windows rodzina
public class WindowsPrzycisk : IPrzycisk
{
    private readonly string _e;
    public WindowsPrzycisk(string e) => _e = e;
    public string NazwaStyle => "Windows";
    public void Renderuj() => Console.WriteLine($"  [Windows Btn: {_e}]");
    public void Kliknij()  => Console.WriteLine($"  [Windows click: {_e}]");
}
public class WindowsPoleTekstowe : IPoleTekstowe
{
    private string _t = "";
    public string NazwaStyle    => "Windows";
    public void Renderuj()      => Console.WriteLine($"  [Windows Input: {_t}]");
    public string PobierzTekst()=> _t;
    public void UstawTekst(string t) { _t = t; Console.WriteLine($"  [Windows input←] {t}"); }
}
public class WindowsPasek : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _p = new();
    public void Renderuj()                 => Console.WriteLine($"  [Windows Toolbar: {_p.Count} btns]");
    public void DodajPrzycisk(IPrzycisk p) => _p.Add(p);
}

// Mac rodzina
public class MacPrzycisk : IPrzycisk
{
    private readonly string _e;
    public MacPrzycisk(string e) => _e = e;
    public string NazwaStyle => "macOS";
    public void Renderuj() => Console.WriteLine($"  (macOS Btn: {_e})");
    public void Kliknij()  => Console.WriteLine($"  (macOS click: {_e})");
}
public class MacPoleTekstowe : IPoleTekstowe
{
    private string _t = "";
    public string NazwaStyle    => "macOS";
    public void Renderuj()      => Console.WriteLine($"  (macOS Field: {_t})");
    public string PobierzTekst()=> _t;
    public void UstawTekst(string t) { _t = t; Console.WriteLine($"  (macOS input←) {t}"); }
}
public class MacPasek : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _p = new();
    public void Renderuj()                 => Console.WriteLine($"  (macOS Toolbar: {_p.Count} btns)");
    public void DodajPrzycisk(IPrzycisk p) => _p.Add(p);
}

// Linux (GTK) rodzina
public class LinuxPrzycisk : IPrzycisk
{
    private readonly string _e;
    public LinuxPrzycisk(string e) => _e = e;
    public string NazwaStyle => "GTK";
    public void Renderuj() => Console.WriteLine($"  <GTK Btn: {_e}>");
    public void Kliknij()  => Console.WriteLine($"  <GTK click: {_e}>");
}
public class LinuxPoleTekstowe : IPoleTekstowe
{
    private string _t = "";
    public string NazwaStyle    => "GTK";
    public void Renderuj()      => Console.WriteLine($"  <GTK Entry: {_t}>");
    public string PobierzTekst()=> _t;
    public void UstawTekst(string t) { _t = t; Console.WriteLine($"  <GTK entry←> {t}"); }
}
public class LinuxPasek : IPasekNarzedziowy
{
    private readonly List<IPrzycisk> _p = new();
    public void Renderuj()                 => Console.WriteLine($"  <GTK Toolbar: {_p.Count} btns>");
    public void DodajPrzycisk(IPrzycisk p) => _p.Add(p);
}

// Abstract Factory — interfejs
public interface IUIFabryka
{
    IPrzycisk         UtworzPrzycisk(string etykieta);
    IPoleTekstowe     UtworzPoleTekstowe();
    IPasekNarzedziowy UtworzPasek();
}

public class WindowsFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e) => new WindowsPrzycisk(e);
    public IPoleTekstowe     UtworzPoleTekstowe()     => new WindowsPoleTekstowe();
    public IPasekNarzedziowy UtworzPasek()            => new WindowsPasek();
}
public class MacFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e) => new MacPrzycisk(e);
    public IPoleTekstowe     UtworzPoleTekstowe()     => new MacPoleTekstowe();
    public IPasekNarzedziowy UtworzPasek()            => new MacPasek();
}
public class LinuxFabryka : IUIFabryka
{
    public IPrzycisk         UtworzPrzycisk(string e) => new LinuxPrzycisk(e);
    public IPoleTekstowe     UtworzPoleTekstowe()     => new LinuxPoleTekstowe();
    public IPasekNarzedziowy UtworzPasek()            => new LinuxPasek();
}

// Aplikacja — zależy tylko od IUIFabryka
public class OknoDialoguLogowania
{
    private readonly IPrzycisk         _btnZaloguj;
    private readonly IPrzycisk         _btnAnuluj;
    private readonly IPoleTekstowe     _poleEmail;
    private readonly IPoleTekstowe     _poleHaslo;
    private readonly IPasekNarzedziowy _pasek;

    public OknoDialoguLogowania(IUIFabryka fab)
    {
        _btnZaloguj = fab.UtworzPrzycisk("Zaloguj");
        _btnAnuluj  = fab.UtworzPrzycisk("Anuluj");
        _poleEmail  = fab.UtworzPoleTekstowe();
        _poleHaslo  = fab.UtworzPoleTekstowe();
        _pasek      = fab.UtworzPasek();
        _pasek.DodajPrzycisk(_btnZaloguj);
        _pasek.DodajPrzycisk(_btnAnuluj);
        Console.WriteLine($"  [AbstractFactory] Dialog styl: {_btnZaloguj.NazwaStyle}");
    }

    public void Pokaz()
    {
        _poleEmail.UstawTekst("user@example.com");
        _poleHaslo.UstawTekst("****");
        _poleEmail.Renderuj();
        _poleHaslo.Renderuj();
        _pasek.Renderuj();
    }
}

// ============================================================
// 4. BUILDER — budowanie złożonych obiektów krok po kroku
// ============================================================

public record PozycjaZlecenia(int ProduktId, int Ilosc, decimal Cena);
public record AdresDostawy(string Ulica, string Miasto, string Kod);

public class ZamowienieZlecenie
{
    public int                   Id              { get; private set; }
    public int                   KlientId        { get; private set; }
    public List<PozycjaZlecenia> Pozycje         { get; private set; } = new();
    public AdresDostawy          Adres           { get; private set; } = null!;
    public string?               Komentarz       { get; private set; }
    public bool                  CzyPilne        { get; private set; }
    public DateTime              DataDostawy     { get; private set; }
    public string                MetodaPlatnosci { get; private set; } = "";
    public decimal               Rabat           { get; private set; }
    public string?               KodPromocyjny   { get; private set; }

    private ZamowienieZlecenie() { }

    public decimal Suma => Pozycje.Sum(p => p.Cena * p.Ilosc) * (1 - Rabat / 100);

    public class Builder
    {
        private readonly ZamowienieZlecenie _z = new();

        public Builder DlaKlienta(int id)               { _z.KlientId = id; return this; }
        public Builder DodajPozycje(int pId, int il, decimal c)
        {
            _z.Pozycje.Add(new(pId, il, c)); return this;
        }
        public Builder DostarczyNa(AdresDostawy a)      { _z.Adres = a; return this; }
        public Builder ZKomentarzem(string k)           { _z.Komentarz = k; return this; }
        public Builder JakoPilne()                      { _z.CzyPilne = true; _z.DataDostawy = DateTime.Today.AddDays(1); return this; }
        public Builder ZDataDostawy(DateTime d)         { _z.DataDostawy = d; return this; }
        public Builder ZPlatnoscia(string m)            { _z.MetodaPlatnosci = m; return this; }
        public Builder ZRabatem(decimal p)
        {
            if (p < 0 || p > 100) throw new ArgumentException("Rabat 0-100%");
            _z.Rabat = p; return this;
        }
        public Builder ZKodemPromocyjnym(string k)      { _z.KodPromocyjny = k; return this; }

        public ZamowienieZlecenie Buduj()
        {
            if (_z.KlientId <= 0)
                throw new InvalidOperationException("KlientId wymagany");
            if (!_z.Pozycje.Any())
                throw new InvalidOperationException("Brak pozycji");
            if (_z.Adres is null)
                throw new InvalidOperationException("Adres wymagany");
            if (string.IsNullOrEmpty(_z.MetodaPlatnosci))
                throw new InvalidOperationException("Metoda płatności wymagana");
            if (_z.DataDostawy == default)
                _z.DataDostawy = DateTime.Today.AddDays(3);
            _z.Id = new Random().Next(1000, 9999);
            return _z;
        }
    }

    public static Builder Nowe() => new();
}

// Email Builder z Director — predefiniowane konfiguracje
public class EmailBuilder
{
    private string _od = "", _do = "", _temat = "", _tresc = "";
    private bool _html = false;
    private readonly List<string> _cc = new(), _bcc = new(), _zalaczniki = new();

    public EmailBuilder Od(string e)          { _od    = e;    return this; }
    public EmailBuilder Do(string e)          { _do    = e;    return this; }
    public EmailBuilder Temat(string t)       { _temat = t;    return this; }
    public EmailBuilder Tresc(string t)       { _tresc = t;    return this; }
    public EmailBuilder HtmlTresc(string t)   { _tresc = t; _html = true; return this; }
    public EmailBuilder DodajCC(string e)     { _cc.Add(e);   return this; }
    public EmailBuilder DodajBCC(string e)    { _bcc.Add(e);  return this; }
    public EmailBuilder DodajZalacznik(string s){ _zalaczniki.Add(s); return this; }

    public EmailWiadomosc Buduj()
    {
        if (string.IsNullOrEmpty(_od))    throw new InvalidOperationException("Brak Od");
        if (string.IsNullOrEmpty(_do))    throw new InvalidOperationException("Brak Do");
        if (string.IsNullOrEmpty(_temat)) throw new InvalidOperationException("Brak Tematu");
        return new EmailWiadomosc(_od, _do, _temat, _tresc, _html,
            _cc.AsReadOnly(), _bcc.AsReadOnly(), _zalaczniki.AsReadOnly());
    }
}

public record EmailWiadomosc(
    string Od, string Do, string Temat, string Tresc, bool Html,
    IReadOnlyList<string> CC, IReadOnlyList<string> BCC,
    IReadOnlyList<string> Zalaczniki);

// Director — predefiniowane konfiguracje emaili
public class EmailDirector
{
    private readonly string _nadawca;
    public EmailDirector(string nadawca) => _nadawca = nadawca;

    public EmailWiadomosc BudujEmailPowitalny(string doEmail, string imie) =>
        new EmailBuilder()
            .Od(_nadawca).Do(doEmail)
            .Temat($"Witamy, {imie}!")
            .HtmlTresc($"<h1>Witaj {imie}!</h1><a href='https://sklep.pl/aktywuj'>Aktywuj konto</a>")
            .Buduj();

    public EmailWiadomosc BudujPowiadomienieZamowienia(string doEmail, int zamId, decimal suma) =>
        new EmailBuilder()
            .Od(_nadawca).Do(doEmail)
            .DodajBCC("zamowienia@sklep.pl")
            .Temat($"Potwierdzenie zamówienia #{zamId}")
            .HtmlTresc($"<h2>Zamówienie #{zamId} przyjęte</h2><p>Suma: {suma:C}</p>")
            .Buduj();

    public EmailWiadomosc BudujAlertAdmin(string temat, string tresc, string[] odbiorcy)
    {
        var b = new EmailBuilder()
            .Od(_nadawca).Do(odbiorcy[0])
            .Temat($"[ALERT] {temat}").Tresc(tresc);
        foreach (var o in odbiorcy.Skip(1)) b.DodajCC(o);
        return b.Buduj();
    }
}

// ============================================================
// RUNNER
// ============================================================

public static class WzorceKreacyjneDemo
{
    public static void Uruchom()
    {
        // --- SINGLETON ---
        var s1 = KlasycznyKonfiguracja.Instancja;
        var s2 = KlasycznyKonfiguracja.Instancja;
        Console.WriteLine($"  [Singleton klasyczny] ten sam obiekt: {ReferenceEquals(s1, s2)}");
        Console.WriteLine($"  [Singleton Lazy] Instancja: {LazySingleton.Instancja.GetType().Name}");
        LazySingleton.Instancja.ZrobCos();
        Console.WriteLine($"  [Singleton static] Wersja: {StaticSingleton.Instancja.Wersja}");
        ICache cache = new InMemoryCache();
        cache.Dodaj("klucz", 42);
        Console.WriteLine($"  [ICache+DI] Pobierz int: {cache.Pobierz<int>("klucz")}");
        Console.WriteLine("  [Anti-pattern] AddSingleton przez DI z interfejsem = testowalne!");

        // --- FACTORY METHOD ---
        BazaDanychFabryka fab = new SqlServerFabryka();
        var pol = fab.PobierzPolaczenie("Server=localhost;Database=sklep");
        pol.Otworz();
        pol.Wykonaj<object>("SELECT * FROM produkty");
        pol.Zamknij();
        var polPG = PolaczenieFabryka.Utworz("postgres", "Host=localhost;Database=sklep");
        polPG.Otworz();

        // --- ABSTRACT FACTORY ---
        IUIFabryka uiFab = new WindowsFabryka();
        var dialog = new OknoDialoguLogowania(uiFab);
        dialog.Pokaz();
        IUIFabryka macFab = new MacFabryka();
        new OknoDialoguLogowania(macFab).Pokaz();
        Console.WriteLine("  [AbstractFactory] Spójność rodziny — Button+TextField+Toolbar z tego samego stylu");

        // --- BUILDER (Zamowienie) ---
        var zam = ZamowienieZlecenie.Nowe()
            .DlaKlienta(42)
            .DodajPozycje(1, 2, 3500m)
            .DodajPozycje(2, 1, 150m)
            .DodajPozycje(3, 3, 49m)
            .DostarczyNa(new AdresDostawy("ul. Marszałkowska 1", "Warszawa", "00-001"))
            .JakoPilne()
            .ZPlatnoscia("Karta")
            .ZRabatem(10m)
            .ZKomentarzem("Dzwoń przed dostawą")
            .Buduj();

        Console.WriteLine($"  [Builder] Zamówienie #{zam.Id}, pozycji: {zam.Pozycje.Count}, suma: {zam.Suma:C}");
        Console.WriteLine($"  [Builder] Pilne: {zam.CzyPilne}, dostawa: {zam.DataDostawy:dd.MM.yyyy}");

        // --- BUILDER z Director (Email) ---
        var director = new EmailDirector("noreply@sklep.pl");
        var ePow = director.BudujEmailPowitalny("jan@test.pl", "Jan");
        Console.WriteLine($"  [EmailDirector] {ePow.Temat}, HTML: {ePow.Html}");
        var eZam = director.BudujPowiadomienieZamowienia("jan@test.pl", 1001, 3850m);
        Console.WriteLine($"  [EmailDirector] {eZam.Temat}, BCC: {eZam.BCC.Count}");
        var custom = new EmailBuilder()
            .Od("support@sklep.pl").Do("jan@test.pl")
            .DodajCC("manager@sklep.pl")
            .Temat("Odpowiedź na zgłoszenie")
            .HtmlTresc("<p>Dziękujemy za kontakt...</p>")
            .DodajZalacznik("/files/regulamin.pdf")
            .Buduj();
        Console.WriteLine($"  [EmailBuilder] Załączniki: {custom.Zalaczniki.Count}, CC: {custom.CC.Count}");

        Console.WriteLine("  [Porównanie] Singleton=1instancja | FactoryMethod=dziedziczenie | AbstractFactory=rodziny | Builder=etapowo");
    }
}
