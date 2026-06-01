namespace _01_Fundamentals;

public static class InstrukcjeWarunkowe
{
    // ─────────────────────────────────────────────────────────────────────────
    // IF / ELSE IF / ELSE
    // ─────────────────────────────────────────────────────────────────────────

    public static void IfElse()
    {
        Console.WriteLine("\n=== IF / ELSE ===");

        int wiek = 20;

        if (wiek < 0)
            Console.WriteLine("Nieprawidłowy wiek");
        else if (wiek < 18)
            Console.WriteLine("Niepełnoletni");
        else if (wiek < 65)
            Console.WriteLine("Dorosły");
        else
            Console.WriteLine("Senior");

        // Operator trójargumentowy (ternary) — warunek ? true_val : false_val
        string status = wiek >= 18 ? "dorosły" : "niepełnoletni";
        Console.WriteLine($"Status: {status}");

        // Zagnieżdżony ternary — czytelny dla prostych przypadków
        string kategoria = wiek < 13  ? "dziecko"
                         : wiek < 18  ? "nastolatek"
                         : wiek < 65  ? "dorosły"
                         : "senior";
        Console.WriteLine($"Kategoria: {kategoria}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATTERN MATCHING Z IS (C# 7+)
    // ─────────────────────────────────────────────────────────────────────────

    public static void PatternMatchingIsOperator()
    {
        Console.WriteLine("\n=== PATTERN MATCHING Z 'is' ===");

        object obj = "Cześć, świecie!";

        // is — sprawdza typ I od razu rzutuje do zmiennej
        if (obj is string tekst)
            Console.WriteLine($"String o długości {tekst.Length}: '{tekst}'");

        // is z warunkiem (guard)
        if (obj is string t && t.Length > 5)
            Console.WriteLine("Długi string (>5 znaków)");

        // is not (C# 9+) — negacja
        if (obj is not null)
            Console.WriteLine("Obiekt nie jest null");

        // Różne typy
        object[] obiekty = { 42, -7, "tekst", 3.14, true, null! };
        foreach (object o in obiekty)
        {
            string opis = o switch
            {
                int n when n > 0    => $"Dodatni int: {n}",
                int n when n < 0    => $"Ujemny int: {n}",
                int                 => "Int zero",
                string s            => $"String: '{s}'",
                double d            => $"Double: {d}",
                bool bl             => $"Bool: {bl}",
                null                => "Null",
                _                   => $"Nieznany: {o}"
            };
            Console.WriteLine($"  {opis}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SWITCH KLASYCZNY
    // ─────────────────────────────────────────────────────────────────────────

    public static void SwitchKlasyczny()
    {
        Console.WriteLine("\n=== SWITCH KLASYCZNY ===");

        int dzien = 3;

        switch (dzien)
        {
            case 1:
                Console.WriteLine("Poniedziałek"); break;
            case 2:
                Console.WriteLine("Wtorek"); break;
            case 3:
                Console.WriteLine("Środa"); break;
            case 4:
                Console.WriteLine("Czwartek"); break;
            case 5:
                Console.WriteLine("Piątek"); break;
            case 6:
            case 7:                              // fall-through dozwolony tylko dla pustych case'ów
                Console.WriteLine("Weekend"); break;
            default:
                Console.WriteLine("Inny dzień"); break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SWITCH EXPRESSION (C# 8+) — zwraca wartość, zwięźlejszy, exhaustive
    // ─────────────────────────────────────────────────────────────────────────

    public static void SwitchExpression()
    {
        Console.WriteLine("\n=== SWITCH EXPRESSION (C# 8+) ===");

        for (int dzien = 1; dzien <= 7; dzien++)
        {
            // Wyrażenie, nie instrukcja — można przypisać do zmiennej
            string nazwaDnia = dzien switch
            {
                1 => "Poniedziałek",
                2 => "Wtorek",
                3 => "Środa",
                4 => "Czwartek",
                5 => "Piątek",
                6 or 7 => "Weekend",        // operator 'or' w pattern matching
                _ => "Nieznany"             // _ = wildcard (odpowiednik default)
            };
            Console.Write($"{nazwaDnia} ");
        }
        Console.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROPERTY PATTERN (C# 8+) — sprawdzanie właściwości obiektu
    // ─────────────────────────────────────────────────────────────────────────

    public static void PropertyPattern()
    {
        Console.WriteLine("\n=== PROPERTY PATTERN (C# 8+) ===");

        // Inline record dla demonstracji
        var osoby = new[]
        {
            new { Imie = "Ania",  Wiek = 17 },
            new { Imie = "Admin", Wiek = 30 },
            new { Imie = "Jan",   Wiek = 25 },
        };

        foreach (var osoba in osoby)
        {
            string komunikat = osoba switch
            {
                { Wiek: < 18 }                         => $"{osoba.Imie}: Niepełnoletni",
                { Wiek: >= 18, Imie: "Admin" }         => $"{osoba.Imie}: Dorosły administrator",
                { Wiek: >= 18 }                        => $"{osoba.Imie}: Dorosły",
                _                                      => "Nieznany"
            };
            Console.WriteLine(komunikat);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FIZZBUZZ — klasyczne zadanie rekrutacyjne łączące switch expression
    // ─────────────────────────────────────────────────────────────────────────

    public static void FizzBuzz()
    {
        Console.WriteLine("\n=== FIZZBUZZ (switch expression + tuple pattern) ===");

        for (int i = 1; i <= 20; i++)
        {
            string wynik = (i % 3, i % 5) switch
            {
                (0, 0) => "FizzBuzz",
                (0, _) => "Fizz",
                (_, 0) => "Buzz",
                _      => i.ToString()
            };
            Console.Write($"{wynik} ");
        }
        Console.WriteLine();
    }
}
