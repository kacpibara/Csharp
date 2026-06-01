namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// HERMETYZACJA (ENKAPSULACJA) — ukrywanie stanu i implementacji
// Eksponuj KONTRAKT (co), ukryj IMPLEMENTACJĘ (jak)
// ─────────────────────────────────────────────────────────────────────────────

public static class Hermetyzacja
{
    // MODYFIKATORY DOSTĘPU — 6 poziomów
    public static void ModyfikatoryDostepu()
    {
        Console.WriteLine("\n=== MODYFIKATORY DOSTĘPU ===");

        var obj = new DemoModyfikatory();
        Console.WriteLine(obj.PublicznyNazwa);      // public — wszędzie
        Console.WriteLine(obj.WewnetrznyNazwa);     // internal — to samo assembly
        // obj._prywatny — BŁĄD: private tylko w tej klasie
        // obj._chroniony — BŁĄD: protected tylko w tej klasie i pochodnych

        Console.WriteLine("\nModyfikatory — tabela:");
        Console.WriteLine("  public            — wszędzie (wewnątrz + inne assembly)");
        Console.WriteLine("  private           — TYLKO ta klasa (domyślny dla pól/metod)");
        Console.WriteLine("  protected         — ta klasa + klasy POCHODNE");
        Console.WriteLine("  internal          — to assembly (.dll/.exe)");
        Console.WriteLine("  protected internal — protected LUB internal");
        Console.WriteLine("  private protected  — pochodne TYLKO w tym assembly (C# 7.2+)");

        Console.WriteLine("\nDomyślne: klasa na poziomie namespace = internal, pola/metody = private");

        // Pułapka: protected nie znaczy dostępny przez inną instancję bazowej
        Console.WriteLine("\nPułapka protected: pochodna MA dostęp do protected SWOJEJ instancji,");
        Console.WriteLine("ale NIE do protected innej instancji klasy Bazowej przez referencję Bazowej");
    }

    // HERMETYZACJA — ZleKonto vs DobreKonto
    public static void PrzykladHermetyzacji()
    {
        Console.WriteLine("\n=== PRZYKŁAD HERMETYZACJI ===");

        // ŹLE — brak hermetyzacji
        var zly = new ZlyPracownikH();
        zly.Pensja = -50000;           // dozwolone! brak walidacji
        zly.Uprawnienia.Add("Admin");  // każdy może nadawać uprawnienia!
        Console.WriteLine($"ZlyPracownik: pensja={zly.Pensja}, uprawn={string.Join(",", zly.Uprawnienia)}");

        // DOBRZE — pełna hermetyzacja
        var dobry = new DobryPracownikH("Kacper", 5000m);
        dobry.PodwyzkaPensji(10);         // +10% przez metodę z walidacją
        dobry.NadajUprawnienie("Editor");
        dobry.NadajUprawnienie("Editor"); // duplikat ignorowany
        Console.WriteLine($"DobryPracownik: {dobry.Imie}, pensja={dobry.Pensja:C}");
        Console.WriteLine($"Uprawnienia: {string.Join(", ", dobry.Uprawnienia)}");

        // IReadOnlyList — zewnętrzny kod nie może modyfikować!
        IReadOnlyList<string> uprawnieniaRO = dobry.Uprawnienia;
        // uprawnieniaRO.Add("Hack"); // BŁĄD kompilacji — IReadOnlyList nie ma Add
        Console.WriteLine($"IReadOnlyList — brak Add: {uprawnieniaRO.Count} pozycji");

        try { dobry.PodwyzkaPensji(-5); }
        catch (ArgumentOutOfRangeException ex) { Console.WriteLine($"Walidacja: {ex.ParamName}"); }
    }

    // WŁAŚCIWOŚCI — wszystkie wzorce
    public static void WlasciwosciIPolaDemo()
    {
        Console.WriteLine("\n=== WŁAŚCIWOŚCI I POLA ===");

        // const vs static readonly
        Console.WriteLine($"const MAX_POLACZEN = {DemoPola.MAX_POLACZEN}  (wkompilowana w IL)");
        Console.WriteLine($"static readonly DataUruchomienia = {DemoPola.DataUruchomienia:HH:mm:ss}  (runtime)");

        // Auto-property, computed, lazy, INotifyPropertyChanged
        var p = new PracownikWlasciwosci("Anna", "Kowalska", DateTime.Now.AddYears(-5));
        Console.WriteLine($"PelneNazwisko (computed): {p.PelneNazwisko}");
        Console.WriteLine($"LataStazu (computed): {p.LataStazu}");
        p.Pensja = 6000m;
        Console.WriteLine($"Pensja (z walidacją): {p.Pensja:C}");
        Console.WriteLine($"Raport (lazy, 1. dostęp): {p.Raport[..45]}...");
        Console.WriteLine($"Raport (lazy, 2. dostęp): {p.Raport[..45]}... (cached, brak ponownego generowania)");

        // init-only (C# 9+) i required (C# 11+)
        var produkt = new DemoProdukt { Id = 1, Nazwa = "Laptop", Cena = 3499m };
        // produkt.Id = 2; // BŁĄD — init-only po inicjalizacji
        Console.WriteLine($"Produkt (init+required): Id={produkt.Id}, {produkt.Nazwa}, {produkt.Cena:C}");
    }

    // KONSTRUKTORY — delegowanie, factory methods, required, primary constructor
    public static void KonstruktoryDemo()
    {
        Console.WriteLine("\n=== KONSTRUKTORY ===");

        // Delegowanie: this() wywołuje inny konstruktor PRZED ciałem
        var p1 = new ProduktKonstr("Kawa", 12.99m, "PLN");  // główny
        var p2 = new ProduktKonstr("Herbata", 8.49m);        // → deleguje do 3-param
        var p3 = new ProduktKonstr("Darmowy");               // → 0m → "PLN"
        Console.WriteLine($"p1: {p1.Nazwa} {p1.Cena:C} {p1.Waluta}");
        Console.WriteLine($"p2: {p2.Nazwa} {p2.Cena:C} {p2.Waluta}");
        Console.WriteLine($"p3: {p3.Nazwa} {p3.Cena:C} {p3.Waluta}");

        // Static factory method — może mieć NAZWĘ i być async (konstruktor nie może!)
        var euro = ProduktKonstr.UtworzWEuro("Espresso", 2.5m);
        Console.WriteLine($"Factory UtworzWEuro: {euro.Nazwa} {euro.Cena} {euro.Waluta}");

        // Object initializer — gdy wiele opcjonalnych właściwości
        var adres = new AdresObj { Ulica = "Marszałkowska 1", Miasto = "Warszawa" };
        Console.WriteLine($"Object initializer: {adres.Ulica}, {adres.Miasto}, {adres.Kraj}");

        // required (C# 11+) — kompilator wymusza ustawienie
        var zam = new ZamowienieReq { NumerZamowienia = "ZAM-001", Kwota = 299.99m };
        // new ZamowienieReq() — BŁĄD kompilacji — brak required properties
        Console.WriteLine($"required: {zam.NumerZamowienia}, {zam.Kwota:C}");

        // Primary constructor (C# 12) — parametry jako pola klasy
        var pkt = new PunktPrim(3.0, 4.0);
        Console.WriteLine($"Primary ctor: {pkt}, odległość={pkt.Odleglosc:F2}");
    }

    // SINGLETON z Lazy<T> + Extension methods
    public static void SingletonDemo()
    {
        Console.WriteLine("\n=== SINGLETON (Lazy<T>) ===");

        var k1 = KonfigSingleton.Instancja;
        var k2 = KonfigSingleton.Instancja;
        Console.WriteLine($"Ta sama instancja: {ReferenceEquals(k1, k2)}"); // True
        Console.WriteLine($"ConnectionString: {k1.ConnectionString}");
        Console.WriteLine($"MaxPolaczen: {k1.MaxPolaczen}");

        // Extension methods na klasie statycznej
        string email = "user@example.com";
        string dlugi = "Bardzo długi tekst na test skracania";
        Console.WriteLine($"CzyEmail '{email}': {email.CzyEmailExt()}");
        Console.WriteLine($"SkrocDo 15 '{dlugi}': {dlugi.SkrocDoExt(15)}");
        Console.WriteLine($"Odwróć 'Hello': {"Hello".OdwrocExt()}");
    }

    // IDisposable i using
    public static void IDisposableDemo()
    {
        Console.WriteLine("\n=== IDisposable I USING ===");

        // using statement — gwarantuje Dispose() nawet przy wyjątku
        Console.WriteLine("Klasyczny using statement:");
        using (var zasob = new ZasobDemo("polaczenie-1"))
        {
            zasob.Wykonaj("SELECT * FROM Users");
        } // Dispose() automatycznie nawet przy wyjątku

        // using declaration (C# 8+) — bez nawiasów klamrowych
        Console.WriteLine("\nNowoczesny using declaration (C# 8+):");
        using var zasob2 = new ZasobDemo("polaczenie-2");
        zasob2.Wykonaj("SELECT * FROM Products");
        // Dispose() przy końcu zakresu (metody lub bloku {...})

        // Pełny wzorzec Dispose: virtual Dispose(bool) + GC.SuppressFinalize
        Console.WriteLine("\nDispatch pattern: Dispose(true) + GC.SuppressFinalize");
        Console.WriteLine("  • true = managed resources (inne IDisposable)");
        Console.WriteLine("  • false = unmanaged (IntPtr, uchwyty Win32)");
        Console.WriteLine("  • finalizer jako backup gdy zapomnisz using");

        // ObjectDisposedException po Dispose
        var zasob3 = new ZasobDemo("polaczenie-3");
        zasob3.Dispose();
        try { zasob3.Wykonaj("Za późno!"); }
        catch (ObjectDisposedException) { Console.WriteLine("ObjectDisposedException po Dispose — poprawne!"); }
    }

    // BUILDER PATTERN
    public static void BuilderPattern()
    {
        Console.WriteLine("\n=== BUILDER PATTERN ===");

        // Prywatny konstruktor — obiekt tylko przez Builder
        // Fluent interface — każda metoda zwraca this
        var email = new EmailH.Builder()
            .Od("kacper@firma.pl")
            .Do("szef@firma.pl")
            .Temat("Raport miesięczny")
            .Tresc("<h1>Raport</h1>", html: true)
            .DodajDW("kolega@firma.pl")
            .ZPriorytetem(1)
            .Zbuduj();

        Console.WriteLine($"Od: {email.Od} → Do: {email.Do}");
        Console.WriteLine($"Temat: {email.Temat}, Priorytet: {email.Priorytet}");
        Console.WriteLine($"DW: {string.Join(", ", email.DW)}");
        Console.WriteLine($"HTML: {email.CzyHTML}");

        // Walidacja w Zbuduj()
        try { new EmailH.Builder().Do("szef@firma.pl").Zbuduj(); }
        catch (InvalidOperationException ex) { Console.WriteLine($"Walidacja: {ex.Message}"); }
    }
}

// ─── KLASY POMOCNICZE ────────────────────────────────────────────────────────

internal class DemoModyfikatory
{
    public string PublicznyNazwa = "public";
    private string _prywatny = "private";
    protected string _chroniony = "protected";
    internal string WewnetrznyNazwa = "internal";
    protected internal string ChronionaWewnetrzna = "protected internal";
    private protected string PrywatnaChroniona = "private protected";

    public void PokazWszystkie()
    {
        Console.WriteLine(_prywatny);       // OK — własna klasa
        Console.WriteLine(_chroniony);
        Console.WriteLine(PrywatnaChroniona);
    }
}

internal class ZlyPracownikH
{
    public string Imie = "";
    public decimal Pensja = 0;
    public List<string> Uprawnienia = new();  // zewnętrzny kod może modyfikować!
}

internal class DobryPracownikH
{
    private decimal _pensja;
    private readonly List<string> _uprawnienia = new();

    public string Imie { get; private set; }

    public decimal Pensja
    {
        get => _pensja;
        private set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _pensja = value;
        }
    }

    public IReadOnlyList<string> Uprawnienia => _uprawnienia.AsReadOnly();

    public DobryPracownikH(string imie, decimal pensja) { Imie = imie; Pensja = pensja; }

    public void PodwyzkaPensji(decimal procent)
    {
        if (procent <= 0 || procent > 100) throw new ArgumentOutOfRangeException(nameof(procent));
        Pensja *= (1 + procent / 100);
    }

    public void NadajUprawnienie(string up) { if (!_uprawnienia.Contains(up)) _uprawnienia.Add(up); }
    public void OdbierzUprawnienie(string up) => _uprawnienia.Remove(up);
}

internal static class DemoPola
{
    // const — wartość znana w kompilacji, wkompilowana bezpośrednio w IL
    public const int MAX_POLACZEN = 100;
    public const string DOMYSLNY_HOST = "localhost";

    // static readonly — wartość ustalana w runtime raz (może być dowolnym typem)
    public static readonly DateTime DataUruchomienia = DateTime.Now;
    public static readonly TimeSpan DomyslnyTimeout = TimeSpan.FromSeconds(30);
}

internal class PracownikWlasciwosci
{
    private decimal _pensja;
    private string? _raport;  // backing field dla lazy property

    public string Imie { get; set; }
    public string Nazwisko { get; set; }
    public DateTime DataZatrudnienia { get; }

    // Computed (expression-bodied) — nie ma backing field
    public string PelneNazwisko => $"{Imie} {Nazwisko}";
    public int LataStazu => (DateTime.Now - DataZatrudnienia).Days / 365;

    // Full property z walidacją
    public decimal Pensja
    {
        get => _pensja;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Pensja ujemna");
            if (value > 1_000_000) throw new ArgumentOutOfRangeException(nameof(value), "Pensja za wysoka");
            _pensja = value;
        }
    }

    // Lazy — oblicza i cache'uje przy pierwszym dostępie
    public string Raport => _raport ??= GenerujRaport();

    private string GenerujRaport() =>
        $"Raport dla {PelneNazwisko}, staż {LataStazu} lat, pensja {_pensja:C}";

    public PracownikWlasciwosci(string imie, string nazwisko, DateTime data)
    {
        Imie = imie; Nazwisko = nazwisko; DataZatrudnienia = data;
    }
}

internal class DemoProdukt
{
    public int Id { get; init; }               // init-only (C# 9+)
    public required string Nazwa { get; set; } // required (C# 11+)
    public decimal Cena { get; set; }
}

internal class ProduktKonstr
{
    public string Nazwa { get; }
    public decimal Cena { get; }
    public string Waluta { get; }

    // Główny konstruktor — waliduje w środku
    public ProduktKonstr(string nazwa, decimal cena, string waluta = "PLN")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nazwa);
        ArgumentOutOfRangeException.ThrowIfNegative(cena);
        Nazwa = nazwa; Cena = cena; Waluta = waluta;
    }

    // Delegowanie — this() wywołuje inny konstruktor PRZED ciałem tego
    public ProduktKonstr(string nazwa, decimal cena) : this(nazwa, cena, "PLN") { }
    public ProduktKonstr(string nazwa) : this(nazwa, 0m) { }

    // Static factory method — ma NAZWĘ (czytelność), może być async
    public static ProduktKonstr UtworzWEuro(string nazwa, decimal cena) => new(nazwa, cena, "EUR");

    // Async factory — gdy inicjalizacja wymaga async (konstruktor NIE może być async!)
    public static async Task<ProduktKonstr> UtworzZBazyAsync(int id)
    {
        await Task.Delay(10); // symulacja async IO
        return new ProduktKonstr($"Produkt_{id}", 99m);
    }
}

internal class AdresObj
{
    public string Ulica { get; set; } = "";
    public string Miasto { get; set; } = "";
    public string Kraj { get; set; } = "Polska"; // wartość domyślna
}

internal class ZamowienieReq
{
    public required string NumerZamowienia { get; init; }
    public required decimal Kwota { get; init; }
    public string? Uwagi { get; init; }
}

// Primary constructor (C# 12) — parametry widoczne w całej klasie
internal class PunktPrim(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Odleglosc => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:F2}, {Y:F2})";
}

// Singleton z Lazy<T> — thread-safe, lazy loading
internal sealed class KonfigSingleton  // sealed — brak dziedziczenia
{
    private static readonly Lazy<KonfigSingleton> _instancja
        = new(() => new KonfigSingleton());

    private KonfigSingleton()  // private — nikt nie tworzy bezpośrednio
    {
        ConnectionString = Environment.GetEnvironmentVariable("DB_CONN") ?? "Server=localhost;Database=Dev";
    }

    public static KonfigSingleton Instancja => _instancja.Value;
    public string ConnectionString { get; }
    public int MaxPolaczen { get; } = 10;
}

// Extension methods — metody statyczne, first param = this
internal static class StringExts
{
    public static bool CzyEmailExt(this string s) => s.Contains('@') && s.Contains('.');
    public static string OdwrocExt(this string s) => new string(s.Reverse().ToArray());
    public static string SkrocDoExt(this string s, int max)
        => s.Length <= max ? s : s[..max] + "...";
}

// IDisposable — pełny wzorzec Dispose
internal class ZasobDemo : IDisposable
{
    private bool _disposed = false;
    private readonly string _nazwa;

    public ZasobDemo(string nazwa)
    {
        _nazwa = nazwa;
        Console.WriteLine($"  [{_nazwa}] Zasób otwarty");
    }

    public void Wykonaj(string operacja)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Console.WriteLine($"  [{_nazwa}] {operacja}");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
            Console.WriteLine($"  [{_nazwa}] Zasób zamknięty");
        _disposed = true;
    }

    ~ZasobDemo() => Dispose(disposing: false); // backup gdy zapomnisz using
}

// Builder Pattern — prywatny konstruktor, zagnieżdżony Builder
internal class EmailH
{
    private EmailH() { }

    public string Od { get; private set; } = "";
    public string Do { get; private set; } = "";
    public string Temat { get; private set; } = "";
    public string Tresc { get; private set; } = "";
    public List<string> DW { get; private set; } = new();
    public bool CzyHTML { get; private set; }
    public int Priorytet { get; private set; } = 3;

    // Builder zagnieżdżony ma dostęp do prywatnych składowych Email
    public class Builder
    {
        private readonly EmailH _email = new();

        public Builder Od(string a) { _email.Od = a; return this; }
        public Builder Do(string a) { _email.Do = a; return this; }
        public Builder Temat(string t) { _email.Temat = t; return this; }
        public Builder Tresc(string t, bool html = false) { _email.Tresc = t; _email.CzyHTML = html; return this; }
        public Builder DodajDW(string a) { _email.DW.Add(a); return this; }
        public Builder ZPriorytetem(int p) { _email.Priorytet = p; return this; }

        public EmailH Zbuduj()
        {
            if (string.IsNullOrEmpty(_email.Od)) throw new InvalidOperationException("Brak nadawcy");
            if (string.IsNullOrEmpty(_email.Do)) throw new InvalidOperationException("Brak odbiorcy");
            return _email;
        }
    }
}
