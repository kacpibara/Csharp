namespace _03_Csharp_3;

// ── Klasy demonstracyjne ──────────────────────────────────────────────────────

// Ewolucja właściwości auto-property
public class EvolucjaWlasciwosci
{
    // C# 2: pełna właściwość
    private string _imie = "";
    public string Imie { get { return _imie; } set { _imie = value; } }

    // C# 3: auto-property get;set;
    public int Wiek { get; set; }

    // C# 6: auto-property tylko do odczytu (init przez konstruktor)
    public Guid Id { get; } = Guid.NewGuid();

    // C# 9: init-only — ustawienie tylko w inicjalizatorze / konstruktorze
    public string Miasto { get; init; } = "";

    // C# 11: required — musi być podane w inicjalizatorze (kompilator wymaga)
    public required string Nazwa { get; set; }
}

public class AdresOI
{
    public string Ulica { get; set; } = "";
    public string Miasto { get; set; } = "";
    public string KodPocztowy { get; set; } = "";
}

public class PracownikOI
{
    public required string Imie { get; init; }
    public required string Nazwisko { get; init; }
    public int Wiek { get; set; }
    public string? Email { get; set; }
    public AdresOI Adres { get; set; } = new();
    public List<string> Umiejetnosci { get; set; } = new();
}

public class KonfigOI
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool UseTls { get; set; } = false;
    public int Timeout { get; set; } = 30;
    public Dictionary<string, string> Headers { get; set; } = new();
}

public record OsobaOI(string Imie, int Wiek, string Miasto = "Warszawa");

public static class ObjectInitializers
{
    // ── 1. Ewolucja inicjalizacji ─────────────────────────────────────────────

    public static void EwolucjaInicjalizacji()
    {
        Console.WriteLine("\n── EwolucjaInicjalizacji ──");

        // C# 1: tylko konstruktor
        // var p = new PracownikOI("Jan", "Kowalski"); — wymagało konstruktora z parametrami

        // C# 3: object initializer — przypisanie właściwości po wywołaniu konstruktora
        var pracownik = new PracownikOI
        {
            Imie = "Jan",
            Nazwisko = "Kowalski",
            Wiek = 30,
            Email = "jan@example.com"
        };
        Console.WriteLine($"Object initializer: {pracownik.Imie} {pracownik.Nazwisko}, {pracownik.Wiek} lat");

        // C# 9: init-only properties — bezpieczniejszy immutable object initializer
        // po zakończeniu {} już nie można zmienić init properties
        // pracownik.Imie = "Piotr"; // CS8852 — Imie jest init-only!
        Console.WriteLine("Imie jest init-only — nie można zmodyfikować po inicjalizacji");

        // C# 11: required — kompilator zmusza do podania wartości
        var ew = new EvolucjaWlasciwosci
        {
            Nazwa = "Test", // required — musi być
            Miasto = "Gdańsk", // init-only
            Wiek = 25
            // Imie = "" // opcjonalne — ma wartość domyślną
        };
        Console.WriteLine($"Required Nazwa: {ew.Nazwa}, init Miasto: {ew.Miasto}");

        // record: skrócona składnia z primary constructor
        var osoba = new OsobaOI("Anna", 28);
        Console.WriteLine($"Record: {osoba}");

        // record with expression — kopiowanie z modyfikacją
        var starsza = osoba with { Wiek = 29 };
        Console.WriteLine($"With expression: {starsza}");
    }

    // ── 2. Inicjalizatory kolekcji ────────────────────────────────────────────

    public static void InicjalizatoryKolekcji()
    {
        Console.WriteLine("\n── InicjalizatoryKolekcji ──");

        // C# 3: collection initializer — { } po new List<T>
        var lista = new List<int> { 1, 2, 3, 4, 5 };
        Console.WriteLine($"List<int>: {string.Join(", ", lista)}");

        // Dictionary initializer — klucz:wartość
        var dict1 = new Dictionary<string, int>
        {
            { "jeden", 1 },
            { "dwa",   2 },
            { "trzy",  3 }
        };

        // C# 6: index initializer — czytelniejszy zapis
        var dict2 = new Dictionary<string, int>
        {
            ["jeden"] = 1,
            ["dwa"]   = 2,
            ["trzy"]  = 3
        };
        Console.WriteLine($"Dictionary: {string.Join(", ", dict2.Select(kv => $"{kv.Key}={kv.Value}"))}");

        // HashSet initializer
        var zbior = new HashSet<string> { "A", "B", "C", "A" }; // duplikat A ignorowany
        Console.WriteLine($"HashSet: {string.Join(", ", zbior)}");

        // Zagnieżdżone inicjalizatory
        var pracownicy = new List<PracownikOI>
        {
            new() { Imie="Anna", Nazwisko="Kowalska", Umiejetnosci={ "C#", "SQL" } },
            new() { Imie="Jan",  Nazwisko="Nowak",    Umiejetnosci={ "Java", "Docker" } },
        };
        Console.WriteLine("Zagnieżdżone inicjalizatory:");
        foreach (var p in pracownicy)
            Console.WriteLine($"  {p.Imie}: {string.Join(", ", p.Umiejetnosci)}");

        // AddRange w inicjalizatorze — niemożliwe bezpośrednio, ale można:
        var baza = new List<string> { "X", "Y" };
        var rozszerzona = new List<string>(baza) { "Z", "W" }; // kopiuj + dodaj
        Console.WriteLine($"List z bazą: {string.Join(", ", rozszerzona)}");
    }

    // ── 3. C# 12 — wyrażenia kolekcji ────────────────────────────────────────

    public static void WyrazzeniaKolekcjiCSharp12()
    {
        Console.WriteLine("\n── WyrazzeniaKolekcjiCSharp12 (C# 12) ──");

        // C# 12: collection expressions — ujednolicona składnia []
        // Działa dla: T[], List<T>, Span<T>, ImmutableArray<T>, i in.
        int[] tablica = [1, 2, 3, 4, 5];
        List<string> lista = ["raz", "dwa", "trzy"];
        Console.WriteLine($"Array: [{string.Join(", ", tablica)}]");
        Console.WriteLine($"List:  [{string.Join(", ", lista)}]");

        // Spread element (..) — "rozwinięcie" kolekcji do innej
        int[] a = [1, 2, 3];
        int[] b = [4, 5, 6];
        int[] polaczone = [..a, ..b];        // spread obu
        int[] polaczoneZDodatkiem = [0, ..a, ..b, 7]; // spread + wartości
        Console.WriteLine($"Spread [..a,..b]: [{string.Join(", ", polaczone)}]");
        Console.WriteLine($"Spread z dod.:    [{string.Join(", ", polaczoneZDodatkiem)}]");

        // Działa również z List<T> i innymi kolekcjami
        List<int> listaA = [1, 2, 3];
        List<int> listaB = [4, 5, 6];
        List<int> polaczonaLista = [..listaA, ..listaB, 7];
        Console.WriteLine($"List spread: [{string.Join(", ", polaczonaLista)}]");

        // Span<T> — alokacja na stosie
        Span<int> span = [10, 20, 30];
        Console.WriteLine($"Span: [{string.Join(", ", span.ToArray())}]");

        // Pusta kolekcja
        int[] pusta = [];
        List<string> pustaLista = [];
        Console.WriteLine($"Pusta tablica: [{string.Join(", ", pusta)}]");
        Console.WriteLine($"Pusta lista: [{string.Join(", ", pustaLista)}]");

        // Porównanie ze starą składnią:
        Console.WriteLine("\nEwolucja składni kolekcji:");
        Console.WriteLine("  C# 1: new int[] { 1, 2, 3 }");
        Console.WriteLine("  C# 3: new[] { 1, 2, 3 }         (type inference)");
        Console.WriteLine("  C# 3: new List<int> { 1, 2, 3 }");
        Console.WriteLine("  C# 12: [1, 2, 3]                 (działa dla wielu typów)");
        Console.WriteLine("  C# 12: [..a, ..b]                (spread)");
    }

    // ── 4. Wzorce inicjalizacji ───────────────────────────────────────────────

    public static void WzorceInicjalizacji()
    {
        Console.WriteLine("\n── WzorceInicjalizacji ──");

        // 1. Builder pattern przez Action<T> configure
        var config = Skonfiguruj(cfg =>
        {
            cfg.Host = "api.example.com";
            cfg.Port = 443;
            cfg.UseTls = true;
            cfg.Timeout = 60;
            cfg.Headers["Authorization"] = "Bearer token123";
            cfg.Headers["Accept"] = "application/json";
        });
        Console.WriteLine($"Konfiguracja: {config.Host}:{config.Port}, TLS={config.UseTls}");
        Console.WriteLine($"Headers: {string.Join(", ", config.Headers.Select(kv => $"{kv.Key}:{kv.Value[..Math.Min(5,kv.Value.Length)]}..."))}");

        // 2. Default object + override — wartości domyślne w klasie
        var domyslna = new KonfigOI();
        var nadpisana = new KonfigOI { Host = "prod.server.com", Port = 443, UseTls = true };
        Console.WriteLine($"\nDomyślna: {domyslna.Host}:{domyslna.Port}");
        Console.WriteLine($"Nadpisana: {nadpisana.Host}:{nadpisana.Port}");

        // 3. Record z wartościami domyślnymi
        var osoba1 = new OsobaOI("Anna", 28);           // Miasto="Warszawa" (default)
        var osoba2 = new OsobaOI("Jan", 35, "Kraków");  // Miasto="Kraków"
        Console.WriteLine($"\nRecord z default: {osoba1}");
        Console.WriteLine($"Record custom:    {osoba2}");

        // 4. Defensive copy z with expression
        var oryginał = new OsobaOI("Anna", 28, "Warszawa");
        var kopia = oryginał with { Wiek = 29 };
        Console.WriteLine($"\nOryginał: {oryginał}");
        Console.WriteLine($"Kopia: {kopia}");
        Console.WriteLine($"ReferenceEquals: {ReferenceEquals(oryginał, kopia)}"); // false

        // 5. Nested object initializer — inicjalizacja właściwości obiektowej
        var pracownik = new PracownikOI
        {
            Imie = "Zofia",
            Nazwisko = "Adamczyk",
            Adres = new AdresOI
            {
                Ulica = "Marszałkowska 1",
                Miasto = "Warszawa",
                KodPocztowy = "00-001"
            },
            Umiejetnosci = { "C#", "Azure", "SQL" }
        };
        Console.WriteLine($"\nZagnieżdżony: {pracownik.Imie}, {pracownik.Adres.Ulica}, {pracownik.Adres.Miasto}");
        Console.WriteLine($"Skills: {string.Join(", ", pracownik.Umiejetnosci)}");
    }

    static KonfigOI Skonfiguruj(Action<KonfigOI> configure)
    {
        var cfg = new KonfigOI();
        configure(cfg);
        return cfg;
    }

    // ── 5. Pułapki inicjalizatorów ────────────────────────────────────────────

    public static void PulapkiInicjalizatorow()
    {
        Console.WriteLine("\n── PulapkiInicjalizatorow ──");

        // Pułapka 1: kolekcja w klasie — współdzielona referencja przy shallow copy
        var p1 = new PracownikOI { Imie = "Anna", Nazwisko = "K", Umiejetnosci = { "C#" } };
        // with expression kopiuje REFERENCJE do mutowalnych kolekcji (nie zawartość!)
        // Dla record: with kopiuje shallow — List<string> to ta sama referencja!
        Console.WriteLine("Pułapka 1: shallow copy kolekcji przy record with expression");
        Console.WriteLine("  Rozwiązanie: record z ImmutableList<T> lub głęboka kopia w konstruktorze");

        // Pułapka 2: kolejność inicjalizacji a wyjątek konstruktora
        // { Prop = wartość } przypisuje po new(), więc konstruktor musi się powieść
        try
        {
            var dobry = new KonfigOI { Host = "test", Port = -1 }; // -1 nie jest walidowane
            Console.WriteLine($"Port -1 jest OK jeśli klasa nie waliduje: {dobry.Port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wyjątek: {ex.Message}");
        }

        // Pułapka 3: init-only a reflection (TestCase: obejście przez reflection)
        Console.WriteLine("\nPułapka 3: init-only jest wymuszane przez kompilator (nie runtime)");
        Console.WriteLine("  Reflection może ominąć init — nie polegaj na init jako invariant runtime");

        // Pułapka 4: required + dziedziczenie
        Console.WriteLine("\nPułapka 4: required działa od C# 11 / .NET 7+ — starsze projekty nie skompilują");
        Console.WriteLine("  SetsRequiredMembers na konstruktorze pozwala pominąć required przy new(...) z args");

        // Pułapka 5: inicjalizator kolekcji działa przez Add()
        // Jeśli klasa ma publiczną metodę Add(T), można użyć {} — to jest kontrakt!
        Console.WriteLine("\nPułapka 5: collection initializer = kompilator wywołuje .Add() dla każdego elementu");
        Console.WriteLine("  Własna klasa z Add<T>(T) może korzystać z {} collection initializer");

        Console.WriteLine("\nKiedy co wybrać:");
        Console.WriteLine("  get;set;  — mutowalny obiekt, konfiguracja przez configure action");
        Console.WriteLine("  get;init; — semi-immutable, można ustawić w inicjalizatorze");
        Console.WriteLine("  get;      — pełne immutable (tylko przez konstruktor lub = new...)");
        Console.WriteLine("  required  — wymuszenie przez kompilator że pole musi być podane");
        Console.WriteLine("  record    — immutable DTO + Equals/GetHashCode/ToString za darmo");
    }
}
