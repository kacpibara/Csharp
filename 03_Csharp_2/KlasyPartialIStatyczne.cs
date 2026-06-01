namespace _03_Csharp_2;

// ── Partial class — część 1: podstawowe właściwości ─────────────────────────
// Kompilator scala wszystkie partial class Uzytkownikps w jedną klasę

public partial class UzytkownikPS
{
    public int Id { get; set; }
    public string Imie { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime DataRejestracji { get; set; } = DateTime.Now;

    public UzytkownikPS(int id, string imie, string email)
    {
        Id = id; Imie = imie; Email = email;
    }
}

// ── Partial class — część 2: walidacja (normalnie w osobnym pliku) ───────────
public partial class UzytkownikPS
{
    public bool CzyEmailPoprawny() => Email.Contains('@') && Email.Contains('.');
    public bool CzyImiePoprawne() => !string.IsNullOrWhiteSpace(Imie) && Imie.Length >= 2;

    public IReadOnlyList<string> Waliduj()
    {
        var bledy = new List<string>();
        if (!CzyEmailPoprawny()) bledy.Add("Nieprawidłowy email");
        if (!CzyImiePoprawne())  bledy.Add("Nieprawidłowe imię");
        return bledy;
    }
}

// ── Partial class — część 3: formatowanie ────────────────────────────────────
public partial class UzytkownikPS
{
    public string PelnyOpis() =>
        $"[{Id}] {Imie} <{Email}> — od {DataRejestracji:dd.MM.yyyy}";

    public override string ToString() => $"Uzytkownik({Id}: {Imie})";
}

// ── Partial class z partial methods ──────────────────────────────────────────

// Część generowana (symulacja kodu generatora np. EF Core)
public partial class FormularzPS
{
    private string _tytul = "";

    public string Tytul
    {
        get => _tytul;
        set
        {
            OnTytulZmieniaSie(_tytul, value); // hook — partial method
            _tytul = value;
            OnTytulZmieniony();               // hook — partial method
        }
    }

    public void Renderuj()
    {
        OnPrzedRenderowaniem(); // hook bez implementacji — kompilator usunie wywołanie
        Console.WriteLine($"  <form title='{Tytul}'>");
        OnPoRenderowaniu();     // hook bez implementacji — zero kosztu runtime
    }

    // Deklaracje partial methods — bez ciała, bez modyfikatora dostępu (prywatne)
    partial void OnTytulZmieniaSie(string stary, string nowy);
    partial void OnTytulZmieniony();
    partial void OnPrzedRenderowaniem();
    partial void OnPoRenderowaniu();
}

// Część pisana ręcznie — implementacja wybranych hooków
public partial class FormularzPS
{
    partial void OnTytulZmieniaSie(string stary, string nowy)
        => Console.WriteLine($"  Tytuł zmienia się: '{stary}' → '{nowy}'");

    partial void OnTytulZmieniony()
        => Console.WriteLine($"  Tytuł zmieniony na: '{_tytul}'");

    // OnPrzedRenderowaniem i OnPoRenderowaniu — brak implementacji
    // Kompilator usuwa ich wywołania — zero narzutu w runtime!
}

// ── Static class — klasa narzędziowa ─────────────────────────────────────────

public static class MathHelperPS
{
    public const double PI = Math.PI;
    public const double E  = Math.E;

    public static double Silnia(int n)
    {
        if (n < 0) throw new ArgumentException("n musi być >= 0");
        if (n <= 1) return 1;
        return n * Silnia(n - 1);
    }

    public static bool CzyPierwsza(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i <= Math.Sqrt(n); i++)
            if (n % i == 0) return false;
        return true;
    }

    public static int NWD(int a, int b) => b == 0 ? a : NWD(b, a % b);
    public static int NWW(int a, int b) => a / NWD(a, b) * b;

    public static double Zaokraglij(double v, int miejsca) =>
        Math.Round(v, miejsca, MidpointRounding.AwayFromZero);
}

// ── Extension methods — MUSZĄ być w static class ──────────────────────────────

public static class StringExtensionsPS
{
    public static bool CzyEmail(this string s) =>
        s.Contains('@') && s.Contains('.') && s.Length > 5;

    public static bool CzyPuste(this string? s) => string.IsNullOrWhiteSpace(s);

    public static string Skroc(this string s, int max, string suffix = "...")
    {
        if (s.Length <= max) return s;
        return s[..(max - suffix.Length)] + suffix;
    }

    public static string Powtorz(this string s, int razy) =>
        string.Concat(Enumerable.Repeat(s, razy));

    public static string UsunZnaki(this string s, params char[] znaki) =>
        new string(s.Where(c => !znaki.Contains(c)).ToArray());
}

public static class KolekcjaExtensionsPS
{
    public static IEnumerable<T> KazdeN<T>(this IEnumerable<T> src, int n)
    {
        int i = 0;
        foreach (T e in src)
        {
            if (i % n == 0) yield return e;
            i++;
        }
    }

    public static IEnumerable<List<T>> Podziel<T>(this IEnumerable<T> src, int rozmiar)
    {
        var paczka = new List<T>();
        foreach (T e in src)
        {
            paczka.Add(e);
            if (paczka.Count == rozmiar) { yield return paczka; paczka = new(); }
        }
        if (paczka.Count > 0) yield return paczka;
    }

    public static void ForEach<T>(this IEnumerable<T> src, Action<T> akcja)
    {
        foreach (T e in src) akcja(e);
    }

    public static TValue PobierzLubDodaj<TKey, TValue>(
        this Dictionary<TKey, TValue> dict, TKey klucz, Func<TValue> fabryka)
        where TKey : notnull
    {
        if (!dict.TryGetValue(klucz, out TValue? v))
        {
            v = fabryka();
            dict[klucz] = v;
        }
        return v;
    }
}

// ── Partial static class — połączenie obu ────────────────────────────────────

public static partial class KonwersjePS
{
    public static double CelsjuszNaFahrenheit(double c) => c * 9.0 / 5 + 32;
    public static double FahrenheitNaCelsjusz(double f) => (f - 32) * 5.0 / 9;
}

public static partial class KonwersjePS
{
    public static double KilogramyNaFunty(double kg) => kg * 2.20462;
    public static double FuntyNaKilogramy(double lbs) => lbs / 2.20462;
    public static double KilometryNaMile(double km) => km * 0.621371;
}

// ── Static class z globalnym stanem i static constructor ─────────────────────

public static class LoggerPS
{
    private static readonly object _blokada = new();
    private static LogPoziomPS _minPoziom = LogPoziomPS.Info;

    public enum LogPoziomPS { Debug, Info, Ostrzezenie, Blad }

    // Static constructor — wywołany raz przed pierwszym użyciem klasy
    static LoggerPS() => Console.WriteLine("  [Logger] Statyczny konstruktor — inicjalizacja raz");

    public static void UstawPoziom(LogPoziomPS poziom) => _minPoziom = poziom;

    public static void Info(string msg)    => Zapisz(LogPoziomPS.Info, msg);
    public static void Ostrzez(string msg) => Zapisz(LogPoziomPS.Ostrzezenie, msg);
    public static void Blad(string msg)    => Zapisz(LogPoziomPS.Blad, msg);

    private static void Zapisz(LogPoziomPS poziom, string msg)
    {
        if (poziom < _minPoziom) return;
        lock (_blokada)
            Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] [{poziom}] {msg}");
    }
}

// ── Walidator — partial class + static class extension methods ───────────────

public partial class ProduktPS
{
    public int Id { get; init; }
    public string Nazwa { get; set; } = "";
    public decimal Cena { get; set; }
    public int StanMagazynu { get; set; }
    public string Kategoria { get; set; } = "";
}

public partial class ProduktPS
{
    public WynikWalidacjiPS Waliduj() => WalidatorProduktPS.Waliduj(this);
    public bool CzyDostepny => StanMagazynu > 0;
}

public static class WalidatorProduktPS
{
    public static WynikWalidacjiPS Waliduj(ProduktPS p)
    {
        var bledy = new List<string>();
        if (!p.Nazwa.CzyPoprawna(3, 100)) bledy.Add("Nazwa musi mieć 3-100 znaków");
        if (!p.Cena.CzyWZakresie(0.01m, 999999m)) bledy.Add("Cena poza zakresem");
        if (p.StanMagazynu < 0) bledy.Add("Stan magazynu nie może być ujemny");
        if (!p.Kategoria.CzyWListie("Elektronika", "Odzież", "Dom", "Sport"))
            bledy.Add($"Nieznana kategoria: {p.Kategoria}");
        return new WynikWalidacjiPS(bledy);
    }
}

public static class RegulyWalidacjiPS
{
    public static bool CzyPoprawna(this string s, int min, int max) =>
        !string.IsNullOrWhiteSpace(s) && s.Length >= min && s.Length <= max;

    public static bool CzyWZakresie(this decimal v, decimal min, decimal max) =>
        v >= min && v <= max;

    public static bool CzyWListie(this string s, params string[] dozwolone) =>
        dozwolone.Contains(s);
}

public record WynikWalidacjiPS(IReadOnlyList<string> Bledy)
{
    public bool CzyPoprawny => Bledy.Count == 0;

    public void WypiszBledy()
    {
        if (CzyPoprawny) { Console.WriteLine("  ✓ Walidacja OK"); return; }
        Console.WriteLine($"  ✗ {Bledy.Count} błąd(ów):");
        foreach (var b in Bledy) Console.WriteLine($"    • {b}");
    }
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class KlasyPartialIStatyczne
{
    public static void KlasaPartial()
    {
        Console.WriteLine("\n── KlasaPartial ──");

        // UzytkownikPS złożony z 3 partial declarations (w tym samym pliku)
        // Dla kodu zewnętrznego — to JEDNA klasa, zero różnicy
        var u = new UzytkownikPS(1, "Anna", "anna@example.com");
        Console.WriteLine(u);                          // ToString() — z części 3
        Console.WriteLine(u.CzyEmailPoprawny());       // z części 2
        Console.WriteLine(u.PelnyOpis());             // z części 3

        var bledy = u.Waliduj();
        Console.WriteLine(bledy.Count == 0 ? "  Walidacja OK" : string.Join(", ", bledy));

        var niePoprawny = new UzytkownikPS(2, "X", "nie-email");
        Console.WriteLine("\nNiepoprawny:");
        foreach (var b in niePoprawny.Waliduj())
            Console.WriteLine($"  • {b}");

        Console.WriteLine("\nGłówne zastosowania partial class:");
        Console.WriteLine("  1. Kod generowany (EF Core, Swagger) + ręczny — generator nadpisuje swój plik");
        Console.WriteLine("  2. Duże klasy — podział tematyczny na pliki");
        Console.WriteLine("  3. Współpraca wielu devów — mniej konfliktów Git");
    }

    public static void MetodyPartial()
    {
        Console.WriteLine("\n── MetodyPartial ──");

        // partial method — deklaracja w jednej części, implementacja w drugiej
        // Bez implementacji: kompilator USUWA wywołania (zero narzutu)
        var f = new FormularzPS();
        f.Tytul = "Logowanie";         // wywołuje OnTytulZmieniaSie + OnTytulZmieniony
        f.Renderuj();                  // OnPrzedRenderowaniem i OnPoRenderowaniu usunięte

        Console.WriteLine("OnPrzedRenderowaniem/OnPoRenderowaniu: brak impl → kompilator usunął wywołania");
        Console.WriteLine("partial method bez impl: ZERO kosztu w runtime");
        Console.WriteLine("partial method z C# 9+: może mieć modyfikator dostępu → impl obowiązkowa");
    }

    public static void KlasaStatyczna()
    {
        Console.WriteLine("\n── KlasaStatyczna ──");

        // static class — nie można new(), wszystkie składowe muszą być static
        // new MathHelperPS(); // BŁĄD kompilacji

        Console.WriteLine($"Silnia(5) = {MathHelperPS.Silnia(5)}");
        Console.WriteLine($"Silnia(10) = {MathHelperPS.Silnia(10)}");
        Console.WriteLine($"CzyPierwsza(17) = {MathHelperPS.CzyPierwsza(17)}");
        Console.WriteLine($"CzyPierwsza(15) = {MathHelperPS.CzyPierwsza(15)}");
        Console.WriteLine($"NWD(12, 8) = {MathHelperPS.NWD(12, 8)}");
        Console.WriteLine($"NWW(4, 6) = {MathHelperPS.NWW(4, 6)}");
        Console.WriteLine($"Zaokraglij(3.145, 2) = {MathHelperPS.Zaokraglij(3.145, 2)}");

        Console.WriteLine("\nstatic class vs klasa ze statycznymi metodami:");
        Console.WriteLine("  static class: kompilator wymusza że WSZYSTKIE składowe są static");
        Console.WriteLine("  klasa ze stat. metodami: można tworzyć instancje, mix metod");
        Console.WriteLine("  static class = silniejszy kontrakt — 'to jest utility, nie obiekt'");
    }

    public static void MetodyRozszerzajace()
    {
        Console.WriteLine("\n── MetodyRozszerzajace ──");

        // Extension methods — static class, first param = this
        // Kompilator zamienia: "tekst".Skroc(10) → StringExtensionsPS.Skroc("tekst", 10)

        string email = "jan@example.com";
        Console.WriteLine($"CzyEmail: {email.CzyEmail()}");
        Console.WriteLine($"CzyEmail(\"\".): {""?.CzyEmail()}");

        string dlugi = "Bardzo długi tekst przykładowy";
        Console.WriteLine($"Skroc(15): \"{dlugi.Skroc(15)}\"");
        Console.WriteLine($"Powtorz(\"ab\", 4): {"ab".Powtorz(4)}");
        Console.WriteLine($"UsunZnaki(\"PL61 1090\", ' '): {"PL61 1090".UsunZnaki(' ')}");

        Console.WriteLine("\nExtension methods MUSZĄ być w static class");
        Console.WriteLine("LINQ (Where, Select, OrderBy) to extension methods na IEnumerable<T>");
    }

    public static void RozszerzeniaKolekcji()
    {
        Console.WriteLine("\n── RozszerzeniaKolekcji ──");

        var liczby = Enumerable.Range(1, 15).ToList();

        // KazdeN — co trzeci element
        Console.Write("KazdeN(3): ");
        liczby.KazdeN(3).ForEach(n => Console.Write($"{n} "));
        Console.WriteLine();

        // Podziel — na kawałki rozmiaru 4
        Console.WriteLine("Podziel(4):");
        foreach (var p in liczby.Podziel(4))
            Console.WriteLine($"  [{string.Join(", ", p)}]");

        // PobierzLubDodaj — lazy initialization w Dictionary
        var cache = new Dictionary<string, List<int>>();
        var lista = cache.PobierzLubDodaj("klucz", () => new List<int>());
        lista.Add(42);
        Console.WriteLine($"PobierzLubDodaj — cache count: {cache.Count}, wartość: [{string.Join(", ", lista)}]");

        // partial static class — metody z różnych części scalone
        Console.WriteLine($"\nKonwersjePS (partial static): 100°C = {KonwersjePS.CelsjuszNaFahrenheit(100)}°F");
        Console.WriteLine($"70kg = {KonwersjePS.KilogramyNaFunty(70):F1} funtów");
        Console.WriteLine($"100km = {KonwersjePS.KilometryNaMile(100):F2} mil");
    }

    public static void StanGlobalny()
    {
        Console.WriteLine("\n── StanGlobalny ──");

        // Static class z globalnym stanem — static constructor + thread safety
        // LoggerPS ma static constructor (wywołany raz przy pierwszym użyciu)
        LoggerPS.Info("Aplikacja uruchomiona");
        LoggerPS.Ostrzez("Niskie zasoby pamięci");
        LoggerPS.Blad("Błąd połączenia z bazą");

        LoggerPS.UstawPoziom(LoggerPS.LogPoziomPS.Ostrzezenie); // filtruje Info
        LoggerPS.Info("To się nie wyświetli (poniżej progu)");
        LoggerPS.Ostrzez("To się wyświetli");

        Console.WriteLine("\nStatic class z mutowalnym stanem — używaj OSTROŻNIE:");
        Console.WriteLine("  Zalety: prosty dostęp wszędzie");
        Console.WriteLine("  Wady: trudny do testowania, thread safety, ukryte zależności");
    }

    public static void PraktycznyWalidator()
    {
        Console.WriteLine("\n── PraktycznyWalidator ──");

        // Łączy: partial class (Produkt + Walidacja oddzielnie) +
        // static class (WalidatorPS ze statyczną metodą) +
        // extension methods (reguły walidacji jako reużywalne rozszerzenia)

        var poprawny = new ProduktPS
        {
            Id = 1, Nazwa = "Laptop Gaming",
            Cena = 3500m, StanMagazynu = 10, Kategoria = "Elektronika"
        };

        var niepoprawny = new ProduktPS
        {
            Id = 2, Nazwa = "AB",       // za krótka
            Cena = -100m,               // ujemna
            StanMagazynu = -5,          // ujemny
            Kategoria = "Zabawki"       // nieznana
        };

        Console.WriteLine("Poprawny produkt:");
        poprawny.Waliduj().WypiszBledy();

        Console.WriteLine("Niepoprawny produkt:");
        niepoprawny.Waliduj().WypiszBledy();

        Console.WriteLine($"\nCzyDostepny(poprawny)={poprawny.CzyDostepny}");
        Console.WriteLine($"CzyDostepny(niepoprawny)={niepoprawny.CzyDostepny}");
    }
}
