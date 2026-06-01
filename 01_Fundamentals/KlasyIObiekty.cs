namespace _01_Fundamentals;

// ─────────────────────────────────────────────────────────────────────────────
// KLASY I OBIEKTY W C#
// Klasa = szablon/blueprint. Obiekt = konkretna instancja zajmująca pamięć.
// Każda klasa dziedziczy niejawnie po System.Object.
// ─────────────────────────────────────────────────────────────────────────────

public static class KlasyIObiekty
{
    // ─────────────────────────────────────────────────────────────────────────
    // ANATOMIA KLASY — pola, właściwości, metody, konstruktor
    // ─────────────────────────────────────────────────────────────────────────

    public static void AnatomiaKlasy()
    {
        Console.WriteLine("\n=== ANATOMIA KLASY ===");

        var p1 = new Pracownik("Anna", "Kowalska", 5000m);
        var p2 = new Pracownik("Bartek", "Nowak", 6500m);

        Console.WriteLine(p1.PelneNazwisko);            // Anna Kowalska
        Console.WriteLine(p1.Przedstaw());              // Anna Kowalska, pensja: 5 000,00 zł
        Console.WriteLine(p1.ToString());               // override ToString
        Console.WriteLine($"Instancji: {Pracownik.LiczbaInstancji}"); // 2 — static

        p1.PodwyzkaPensji(10);
        Console.WriteLine($"Po podwyżce 10%: {p1.Pensja:C}");  // 5500
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KONSTRUKTORY — główny, przeciążony, domyślny, statyczny
    // ─────────────────────────────────────────────────────────────────────────

    public static void Konstruktory()
    {
        Console.WriteLine("\n=== KONSTRUKTORY ===");

        var k1 = new Konfiguracja("serwer", 5432, "baza", true);  // główny
        var k2 = new Konfiguracja("serwer", "baza");              // deleguje do głównego
        var k3 = new Konfiguracja();                              // domyślny

        Console.WriteLine($"k1: {k1.Host}:{k1.Port}/{k1.Baza} SSL={k1.SslWlaczony}");
        Console.WriteLine($"k2: {k2.Host}:{k2.Port}/{k2.Baza}");
        Console.WriteLine($"k3: {k3.Host}:{k3.Port}/{k3.Baza}");

        // Object initializer — inicjalizacja właściwości po konstruktorze
        var adres = new Adres
        {
            Ulica       = "Marszałkowska 1",
            Miasto      = "Warszawa",
            KodPocztowy = "00-001"
        };
        Console.WriteLine($"Adres: {adres.Ulica}, {adres.Miasto} {adres.KodPocztowy}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WŁAŚCIWOŚCI — wszystkie wzorce
    // Pole = surowa zmienna. Property = kontrolowany dostęp przez get/set.
    // Zawsze właściwości w publicznym API — pola to szczegół implementacyjny.
    // ─────────────────────────────────────────────────────────────────────────

    public static void WlasciwostiProperties()
    {
        Console.WriteLine("\n=== WŁAŚCIWOŚCI ===");

        var p = new Produkt
        {
            Id     = 1,        // init-only — tylko w object initializer lub konstruktorze
            Nazwa  = "Kawa",   // required — musi być podana
            Cena   = 15.99m
        };

        Console.WriteLine($"Opis: {p.Opis}");
        // p.Id = 2;  ← BŁĄD — init-only!

        p.Sprzedaj(3);
        Console.WriteLine($"Stan magazynowy po sprzedaży 3: {p.StanMagazynowy}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STATYCZNE vs INSTANCYJNE — kiedy co
    // ─────────────────────────────────────────────────────────────────────────

    public static void StatyczneVsInstancyjne()
    {
        Console.WriteLine("\n=== STATIC vs INSTANCE ===");

        // Instancyjna — wymaga obiektu (this)
        var helper = new MathHelper(2.0);
        Console.WriteLine($"2^10 (instancyjna) = {helper.Potega(10)}");   // 1024

        // Statyczna — przez nazwę klasy, brak dostępu do pól instancji
        Console.WriteLine($"Silnia(5) (statyczna) = {MathHelper.Silnia(5)}");   // 120
        Console.WriteLine($"CzyPierwsza(17) = {MathHelper.CzyPierwsza(17)}");   // True
        Console.WriteLine($"CzyPierwsza(15) = {MathHelper.CzyPierwsza(15)}");   // False

        // Kiedy static:
        // ✅ Narzędzia / utility (Math, Convert, Enumerable)
        // ✅ Factory methods
        // ✅ Brak stanu instancyjnego
        // ❌ Gdy potrzebujesz dziedziczenia lub polimorfizmu
        // ❌ Gdy stan zależy od instancji
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RECORD — nowoczesna alternatywa (C# 9+)
    // Kompilator generuje: konstruktor, właściwości, ToString, Equals,
    // GetHashCode, operator ==, with expression
    // ─────────────────────────────────────────────────────────────────────────

    public static void Rekordy()
    {
        Console.WriteLine("\n=== RECORD (C# 9+) ===");

        var o1 = new Osoba("Anna", "Kowalska", 30);
        var o2 = new Osoba("Anna", "Kowalska", 30);
        var o3 = new Osoba("Bartek", "Nowak", 25);

        // Porównanie VALUE-BASED (w klasie byłoby False — różne referencje)
        Console.WriteLine($"o1 == o2 (te same dane): {o1 == o2}");    // True!
        Console.WriteLine($"o1 == o3 (różne dane):   {o1 == o3}");    // False

        // Klasy — referencyjne:
        // new Pracownik("A","B",1m) == new Pracownik("A","B",1m) → False (różne obiekty)

        // ToString gotowe
        Console.WriteLine($"o1.ToString(): {o1}");  // Osoba { Imie = Anna, Nazwisko = Kowalska, Wiek = 30 }

        // with — utwórz kopię ze zmienionymi polami (non-destructive mutation)
        var o4 = o1 with { Wiek = 31 };
        Console.WriteLine($"o4 (with Wiek=31): {o4}");
        Console.WriteLine($"o1 niezmieniony:   {o1}");  // Wiek=30

        // Record struct (C# 10+) — value type record
        var p1 = new PunktR(1.0, 2.0);
        var p2 = new PunktR(1.0, 2.0);
        Console.WriteLine($"record struct p1==p2: {p1 == p2}");  // True

        // Kiedy record zamiast klasy:
        // ✅ Immutable data transfer objects (DTO)
        // ✅ Value objects (Domain-Driven Design)
        // ✅ Gdy potrzebujesz porównania po wartościach z automatu
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OBJECT — baza wszystkiego + override ToString/Equals/GetHashCode
    // ZASADA: jeśli nadpisujesz Equals, MUSISZ nadpisać GetHashCode!
    // ─────────────────────────────────────────────────────────────────────────

    public static void ObjectBaza()
    {
        Console.WriteLine("\n=== OBJECT — BAZA WSZYSTKIEGO ===");

        var s1 = new Samochod("Toyota", "Corolla", 2020);
        var s2 = new Samochod("Toyota", "Corolla", 2020);
        var s3 = s1;   // ta sama referencja!

        Console.WriteLine($"ToString: {s1}");               // Toyota Corolla (2020)
        Console.WriteLine($"s1.Equals(s2): {s1.Equals(s2)}");        // True — nadpisane
        Console.WriteLine($"s1 == s2: {s1 == s2}");                   // False — == dla klas = referencja
        Console.WriteLine($"s1 == s3: {s1 == s3}");                   // True — ta sama ref
        Console.WriteLine($"ReferenceEquals(s1,s2): {ReferenceEquals(s1, s2)}"); // False
        Console.WriteLine($"ReferenceEquals(s1,s3): {ReferenceEquals(s1, s3)}"); // True

        // GetType i typeof
        Console.WriteLine($"s1.GetType():     {s1.GetType()}");
        Console.WriteLine($"typeof(Samochod): {typeof(Samochod)}");
        Console.WriteLine($"Is Samochod:      {s1 is Samochod}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KOMPLETNA KLASA — konto bankowe łączące wszystkie koncepcje
    // ─────────────────────────────────────────────────────────────────────────

    public static void KontoBankoweDemo()
    {
        Console.WriteLine("\n=== KONTO BANKOWE (kompletna klasa) ===");

        var konto = new KontoBankowe("Anna Kowalska", 1000m);
        konto.Wplata(500m, "Premia");
        konto.Wyplata(200m, "Zakupy");

        Console.WriteLine(konto);  // override ToString

        foreach (var wpis in konto.PobierzHistorie())
            Console.WriteLine($"  {wpis}");

        // Walidacja w konstruktorze i metodach
        try
        {
            konto.Wyplata(5000m, "Za dużo");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// KLASY POMOCNICZE — poza klasą statyczną KlasyIObiekty
// ─────────────────────────────────────────────────────────────────────────────

internal class Pracownik
{
    private string  _imie;
    private decimal _pensja;
    private static int _liczbaInstancji = 0;

    public string Imie
    {
        get => _imie;
        set => _imie = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Imię nie może być puste")
            : value;
    }

    public string  Nazwisko          { get; set; }
    public string  PelneNazwisko     => $"{_imie} {Nazwisko}";
    public decimal Pensja
    {
        get => _pensja;
        private set => _pensja = value >= 0
            ? value
            : throw new ArgumentException("Pensja nie może być ujemna");
    }
    public static int LiczbaInstancji => _liczbaInstancji;

    public Pracownik(string imie, string nazwisko, decimal pensja)
    {
        Imie     = imie;
        Nazwisko = nazwisko;
        Pensja   = pensja;
        _liczbaInstancji++;
    }

    public void PodwyzkaPensji(decimal procent)    => Pensja *= (1 + procent / 100);
    public string Przedstaw()                      => $"{PelneNazwisko}, pensja: {Pensja:C}";
    public override string ToString()              => $"Pracownik({PelneNazwisko})";
}

internal class Konfiguracja
{
    public string Host        { get; }
    public int    Port        { get; }
    public string Baza        { get; }
    public bool   SslWlaczony { get; }

    public Konfiguracja(string host, int port, string baza, bool ssl = false)
    {
        Host = host; Port = port; Baza = baza; SslWlaczony = ssl;
    }

    public Konfiguracja(string host, string baza) : this(host, 1433, baza) { }
    public Konfiguracja()                          : this("localhost", 1433, "master") { }

    static Konfiguracja()
    {
        // Statyczny konstruktor — wywołany RAZ, przed pierwszym użyciem klasy
        // Brak modyfikatora dostępu, brak parametrów
    }
}

internal class Adres
{
    public string Ulica       { get; set; } = "";
    public string Miasto      { get; set; } = "";
    public string KodPocztowy { get; set; } = "";
}

internal class Produkt
{
    private decimal _cena;

    public decimal Cena
    {
        get => _cena;
        set => _cena = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Cena > 0");
    }

    public string   Kategoria      { get; set; } = "Ogólne";   // auto-property z domyślną
    public int      Id             { get; init; }               // init-only (C# 9+)
    public string   Opis           => $"{Nazwa} ({Kategoria}) - {Cena:C}"; // computed
    public int      StanMagazynowy { get; private set; } = 100; // tylko klasa może set

    public required string Nazwa { get; set; }   // required (C# 11+) — musi być ustawiona

    public void Sprzedaj(int ilosc)
    {
        if (ilosc > StanMagazynowy)
            throw new InvalidOperationException("Brak w magazynie");
        StanMagazynowy -= ilosc;
    }
}

internal class MathHelper
{
    private readonly double _podstawa;

    public MathHelper(double podstawa) { _podstawa = podstawa; }

    public double Potega(int wykladnik) => Math.Pow(_podstawa, wykladnik); // instancyjna

    public static double Silnia(int n)    => n <= 1 ? 1 : n * Silnia(n - 1);   // statyczna
    public static bool CzyPierwsza(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i <= Math.Sqrt(n); i++)
            if (n % i == 0) return false;
        return true;
    }
}

// Record — C# 9+ (positional record)
internal record Osoba(string Imie, string Nazwisko, int Wiek);

// Record struct (C# 10+) — value type record
internal record struct PunktR(double X, double Y);

internal class Samochod
{
    public string Marka { get; }
    public string Model { get; }
    public int    Rok   { get; }

    public Samochod(string marka, string model, int rok)
    {
        Marka = marka; Model = model; Rok = rok;
    }

    // Nadpisanie metod z object — ZALECANE dla własnych klas
    public override string ToString()    => $"{Marka} {Model} ({Rok})";
    public override bool   Equals(object? obj) =>
        obj is Samochod s && s.Marka == Marka && s.Model == Model && s.Rok == Rok;

    // ZASADA: jeśli nadpisujesz Equals, MUSISZ nadpisać GetHashCode!
    // Dictionary i HashSet używają GetHashCode() do wyznaczenia bucketu.
    public override int GetHashCode() => HashCode.Combine(Marka, Model, Rok);
}

internal class KontoBankowe
{
    private readonly List<string> _historia = new();
    private decimal _saldo;
    private static int _kolejnyNumer = 1000;

    public int     Numer      { get; }
    public string  Wlasciciel { get; }
    public decimal Saldo
    {
        get => _saldo;
        private set => _saldo = value;
    }

    public KontoBankowe(string wlasciciel, decimal saldoPoczatkowe = 0)
    {
        Wlasciciel = wlasciciel;
        Numer      = _kolejnyNumer++;
        if (saldoPoczatkowe > 0) Wplata(saldoPoczatkowe, "Saldo początkowe");
    }

    public void Wplata(decimal kwota, string opis = "Wpłata")
    {
        if (kwota <= 0) throw new ArgumentException("Kwota wpłaty musi być dodatnia");
        _saldo += kwota;
        _historia.Add($"+{kwota:C} | {opis} | Saldo: {_saldo:C}");
    }

    public void Wyplata(decimal kwota, string opis = "Wypłata")
    {
        if (kwota <= 0) throw new ArgumentException("Kwota wypłaty musi być dodatnia");
        if (kwota > _saldo) throw new InvalidOperationException("Niewystarczające środki");
        _saldo -= kwota;
        _historia.Add($"-{kwota:C} | {opis} | Saldo: {_saldo:C}");
    }

    public IReadOnlyList<string> PobierzHistorie() => _historia.AsReadOnly();

    public override string ToString() =>
        $"Konto {Numer} | {Wlasciciel} | Saldo: {Saldo:C}";
}
