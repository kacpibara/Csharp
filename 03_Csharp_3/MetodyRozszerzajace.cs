namespace _03_Csharp_3;

// ── Extension methods na różnych typach ──────────────────────────────────────

public static class StringExtMR
{
    public static bool CzyEmail(this string s) =>
        !string.IsNullOrEmpty(s) && s.Contains('@') && s.Contains('.') && s.Length > 5;

    public static bool CzyPesel(this string s) =>
        s.Length == 11 && s.All(char.IsDigit);

    public static string Skroc(this string s, int max, string suffix = "...")
    {
        if (s.Length <= max) return s;
        return s[..(max - suffix.Length)] + suffix;
    }

    public static string Powtorz(this string s, int razy) =>
        string.Concat(Enumerable.Repeat(s, razy));

    public static bool CzyPuste(this string? s) => string.IsNullOrWhiteSpace(s);

    public static int ToIntOrDefault(this string s, int domyslna = 0) =>
        int.TryParse(s, out int v) ? v : domyslna;
}

public static class IntExtMR
{
    public static bool CzyParzysta(this int n) => n % 2 == 0;

    public static bool CzyPierwsza(this int n)
    {
        if (n < 2) return false;
        for (int i = 2; i <= Math.Sqrt(n); i++)
            if (n % i == 0) return false;
        return true;
    }

    public static IEnumerable<int> Do(this int start, int koniec)
    {
        for (int i = start; i <= koniec; i++) yield return i;
    }
}

public static class DateTimeExtMR
{
    public static bool CzyWeekend(this DateTime d) =>
        d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static string DoPolskiego(this DateTime d) =>
        d.ToString("dd.MM.yyyy HH:mm");

    public static int WiekWLatach(this DateTime dataUrodzenia)
    {
        var dzisiaj = DateTime.Today;
        int wiek = dzisiaj.Year - dataUrodzenia.Year;
        if (dataUrodzenia.Date > dzisiaj.AddYears(-wiek)) wiek--;
        return wiek;
    }
}

// ── Extension methods na interfejsach i kolekcjach ───────────────────────────

public static class IEnumerableExtMR
{
    public static IEnumerable<T> KazdeN<T>(this IEnumerable<T> src, int n)
    {
        int i = 0;
        foreach (T e in src) { if (i++ % n == 0) yield return e; }
    }

    public static IEnumerable<IEnumerable<T>> Porcje<T>(this IEnumerable<T> src, int rozmiar)
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

    public static bool NiePusta<T>(this IEnumerable<T> src) => src.Any();
}

public static class DictionaryExtMR
{
    public static TValue PobierzLubDodaj<TKey, TValue>(
        this Dictionary<TKey, TValue> dict, TKey klucz, Func<TValue> fabryka)
        where TKey : notnull
    {
        if (!dict.TryGetValue(klucz, out TValue? v))
            dict[klucz] = v = fabryka();
        return v;
    }
}

// ── Fluent walidacja z extension methods ─────────────────────────────────────

public class WalidatorEmailMR
{
    private readonly string _email;
    private readonly List<string> _bledy = new();

    public WalidatorEmailMR(string email) => _email = email;

    public WalidatorEmailMR NieMozeBycPusty()
    {
        if (string.IsNullOrWhiteSpace(_email)) _bledy.Add("Email nie może być pusty");
        return this;
    }

    public WalidatorEmailMR MusiZawierac(string znak)
    {
        if (!_email.Contains(znak)) _bledy.Add($"Email musi zawierać '{znak}'");
        return this;
    }

    public WalidatorEmailMR MinimalnieDlugi(int min)
    {
        if (_email.Length < min) _bledy.Add($"Email musi mieć min {min} znaków");
        return this;
    }

    public (bool OK, IReadOnlyList<string> Bledy) Waliduj()
        => (_bledy.Count == 0, _bledy.AsReadOnly());
}

public static class WalidatorExtMR
{
    public static WalidatorEmailMR ZaczniWalidowac(this string email) => new(email);
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class MetodyRozszerzajace
{
    public static void PodstawyEM()
    {
        Console.WriteLine("\n── PodstawyEM ──");

        // Składnia: static method w static class, first param = this TargetType
        // Compiler: "tekst".CzyEmail() → StringExtMR.CzyEmail("tekst")
        Console.WriteLine($"\"jan@test.com\".CzyEmail() = {"jan@test.com".CzyEmail()}");
        Console.WriteLine($"\"nie-email\".CzyEmail() = {"nie-email".CzyEmail()}");
        Console.WriteLine($"\"12345678901\".CzyPesel() = {"12345678901".CzyPesel()}");
        Console.WriteLine($"\"bardzo długi tekst\".Skroc(12) = \"{"bardzo długi tekst".Skroc(12)}\"");
        Console.WriteLine($"\"ab\".Powtorz(4) = {"ab".Powtorz(4)}");
        Console.WriteLine($"\"42\".ToIntOrDefault() = {"42".ToIntOrDefault()}");
        Console.WriteLine($"\"abc\".ToIntOrDefault(99) = {"abc".ToIntOrDefault(99)}");

        Console.WriteLine($"17.CzyPierwsza() = {17.CzyPierwsza()}");
        Console.WriteLine($"7.CzyParzysta() = {7.CzyParzysta()}");

        Console.Write("1.Do(5): ");
        1.Do(5).ForEach(n => Console.Write($"{n} "));
        Console.WriteLine();

        var data = new DateTime(1990, 5, 15);
        Console.WriteLine($"data.WiekWLatach() = {data.WiekWLatach()}");
        Console.WriteLine($"DateTime.Now.CzyWeekend() = {DateTime.Now.CzyWeekend()}");
    }

    public static void RozszerzenieInterfejsow()
    {
        Console.WriteLine("\n── RozszerzenieInterfejsow ──");

        var liczby = Enumerable.Range(1, 15).ToList();

        Console.Write("KazdeN(3): ");
        liczby.KazdeN(3).ForEach(n => Console.Write($"{n} "));
        Console.WriteLine();

        Console.WriteLine("Porcje(4):");
        foreach (var p in liczby.Porcje(4))
            Console.WriteLine($"  [{string.Join(", ", p)}]");

        var cache = new Dictionary<string, int>();
        int v1 = cache.PobierzLubDodaj("klucz", () => { Console.Write("[oblicz] "); return 42; });
        int v2 = cache.PobierzLubDodaj("klucz", () => 0); // z cache, fabryka nie wywołana
        Console.WriteLine($"\nPobierzLubDodaj: v1={v1}, v2={v2} (z cache)");
    }

    public static void NullSafetyEM()
    {
        Console.WriteLine("\n── NullSafetyEM ──");

        // Extension methods MOŻNA wywoływać na null receiver (jeśli metoda to obsługuje)
        string? nullStr = null;
        Console.WriteLine($"null.CzyPuste() = {nullStr.CzyPuste()}"); // OK — nie rzuca NPE

        // Priorytet: metody instancji > extension methods
        // Jeśli List<T> ma metodę ForEach — to ona jest wywoływana, nie nasza extension
        Console.WriteLine("Priorytet: metody instancji > extension methods (dla List.ForEach)");

        // Brak polimorfizmu — rozwiązana statycznie na podstawie TYPU ZMIENNEJ
        Console.WriteLine("Brak polimorfizmu: typ zmiennej decyduje, nie typ obiektu");
        Console.WriteLine("IEnumerable<T> zmienna → extension dla IEnumerable<T> (nie List<T>)");
    }

    public static void FluentAPI()
    {
        Console.WriteLine("\n── FluentAPI ──");

        // Fluent walidacja przez extension methods
        var (ok1, _) = "jan@test.com"
            .ZaczniWalidowac()
            .NieMozeBycPusty()
            .MusiZawierac("@")
            .MinimalnieDlugi(5)
            .Waliduj();
        Console.WriteLine($"\"jan@test.com\": OK={ok1}");

        var (ok2, bledy2) = ""
            .ZaczniWalidowac()
            .NieMozeBycPusty()
            .MusiZawierac("@")
            .MinimalnieDlugi(5)
            .Waliduj();
        Console.WriteLine($"\"\": OK={ok2}, błędy: [{string.Join("; ", bledy2)}]");

        // LINQ jako fluent API przez extension methods
        var wynik = Enumerable.Range(1, 20)
            .Where(n => n % 2 == 0)
            .Select(n => n * n)
            .Where(n => n > 50)
            .Take(3)
            .ToList();
        Console.WriteLine($"LINQ fluent: [{string.Join(", ", wynik)}]");
    }

    public static void PulapkiEM()
    {
        Console.WriteLine("\n── PulapkiEM ──");

        Console.WriteLine("Pułapki extension methods:");
        Console.WriteLine("1. Brak polimorfizmu — rozwiązywanie statyczne (compile-time), nie dynamic");
        Console.WriteLine("2. Konflikty nazw — dwa using z tą samą ext → CS0121 niejednoznaczność");
        Console.WriteLine("3. Brak dostępu do private składowych — jest zewnętrzną metodą statyczną");
        Console.WriteLine("4. Null receiver — dozwolony, ale musisz jawnie obsłużyć null");

        Console.WriteLine("\nKiedy NIE używać:");
        Console.WriteLine("  - Gdy możesz dodać metodę bezpośrednio do klasy (modyfikujesz jej kod)");
        Console.WriteLine("  - Gdy modyfikujesz mutowalny stan (zamiast tego: metoda instancji)");
        Console.WriteLine("  - Zbyt wiele extensions na prostych typach (string, int) obniża czytelność");
        Console.WriteLine("  - Gdy potrzebujesz virtual/override — extension tego nie zapewnia");

        Console.WriteLine("\nKiedy WARTO używać:");
        Console.WriteLine("  - Rozszerzasz zewnętrzną klasę/interfejs (np. string, IEnumerable<T>)");
        Console.WriteLine("  - Fluent API (walidatory, query builtery, konfiguracja)");
        Console.WriteLine("  - Adapter między interfejsem a konkretnym typem");
    }

    public static void LinqJakoEM()
    {
        Console.WriteLine("\n── LinqJakoEM ──");

        // LINQ = extension methods na IEnumerable<T> (System.Linq.Enumerable)
        var liczby = new[] { 5, 3, 8, 1, 9, 2, 7 };

        // Query syntax i method syntax to to samo — kompilator tłumaczy query na metody
        var z_metodami = liczby.Where(n => n > 4).OrderBy(n => n).Select(n => n * n);
        var z_query    = from n in liczby where n > 4 orderby n select n * n;

        Console.WriteLine($"Method syntax: [{string.Join(", ", z_metodami)}]");
        Console.WriteLine($"Query syntax:  [{string.Join(", ", z_query)}]");

        // Reimplementacja Where jako extension — to dokładnie jak działa LINQ
        Console.Write("MojeWhere(n>4): ");
        foreach (var n in MojeWhere(liczby, n => n > 4))
            Console.Write($"{n} ");
        Console.WriteLine();

        // Łańcuchowanie możliwe bo każda metoda przyjmuje i zwraca IEnumerable<T>
        Console.WriteLine("Każda LINQ metoda: IEnumerable<T> wejście → IEnumerable<T> wyjście");
        Console.WriteLine("Lazy evaluation: elementy generowane przy iteracji (ToList/foreach)");
    }

    static IEnumerable<T> MojeWhere<T>(IEnumerable<T> src, Func<T, bool> pred)
    {
        foreach (var e in src) if (pred(e)) yield return e;
    }
}
