namespace _01_Fundamentals;

public static class Stringi
{
    // String to klasa (reference type), ale zachowuje się jak value type — jest IMMUTABLE.
    // Każda modyfikacja tworzy NOWY obiekt. GC sprząta stare.

    // ─────────────────────────────────────────────────────────────────────────
    // TWORZENIE STRINGÓW — konkatenacja, interpolacja, verbatim, raw
    // ─────────────────────────────────────────────────────────────────────────

    public static void TworzeniaStringow()
    {
        Console.WriteLine("\n=== TWORZENIE STRINGÓW ===");

        string imie     = "Kacper";
        string nazwisko = "Kowalski";

        // Konkatenacja — tworzy NOWY string przy każdym +, nieefektywne w pętli
        string pelne = "Cześć, " + imie + " " + nazwisko + "!";

        // Interpolacja $ — ZALECANA, czytelna, wyrażenia w { }
        string powitanie = $"Cześć, {imie} {nazwisko}!";
        decimal kwota = 1234.5M;
        Console.WriteLine($"Kwota: {kwota:C}");     // waluta
        Console.WriteLine($"PI: {Math.PI:F2}");      // 2 miejsca po przecinku
        Console.WriteLine($"Warunek: {(imie.Length > 3 ? "długie" : "krótkie")}");

        // Verbatim string @ — ignoruje escape sequences (przydatne do ścieżek)
        string sciezka   = @"C:\Users\Kacper\Documents";  // bez @: C:\\Users\\...
        string wielolinia = @"Linia 1
Linia 2
Linia 3";
        Console.WriteLine(sciezka);

        // Raw string literal (C# 11+) — trzy cudzysłowy, idealne do JSON/SQL/HTML
        string json = """
            {
                "imie": "Kacper",
                "wiek": 25
            }
            """;
        Console.WriteLine(json);

        Console.WriteLine($"pelne={pelne}");
        Console.WriteLine($"powitanie={powitanie}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WAŻNE METODY STRING
    // ─────────────────────────────────────────────────────────────────────────

    public static void MetodyString()
    {
        Console.WriteLine("\n=== METODY STRING ===");

        string tekst = "  Witaj, Świecie C#!  ";

        // Informacje
        Console.WriteLine($"Length: {tekst.Length}");
        Console.WriteLine($"IndexOf('C'): {tekst.IndexOf('C')}");
        Console.WriteLine($"Contains('C#'): {tekst.Contains("C#")}");

        // Transformacje — każda zwraca NOWY string
        Console.WriteLine($"Trim:    '{tekst.Trim()}'");
        Console.WriteLine($"ToUpper: '{tekst.ToUpper()}'");
        Console.WriteLine($"ToLower: '{tekst.ToLower()}'");
        Console.WriteLine($"Replace: '{tekst.Trim().Replace("Świecie", "Polsko")}'");

        // Wycinanie — Substring vs range (C# 8+)
        string s = "Hello, World!";
        Console.WriteLine($"Substring(7):   '{s.Substring(7)}'");
        Console.WriteLine($"s[7..]:         '{s[7..]}'");   // nowoczesny zapis
        Console.WriteLine($"s[..5]:         '{s[..5]}'");
        Console.WriteLine($"s[7..12]:       '{s[7..12]}'");

        // Sprawdzanie
        Console.WriteLine($"StartsWith: {s.StartsWith("Hello")}");
        Console.WriteLine($"EndsWith:   {s.EndsWith("!")}");
        Console.WriteLine($"IsNullOrEmpty(''):       {string.IsNullOrEmpty("")}");
        Console.WriteLine($"IsNullOrWhiteSpace('  '): {string.IsNullOrWhiteSpace("   ")}");

        // Padding
        string kod = "42";
        Console.WriteLine($"PadLeft(5,'0'): '{kod.PadLeft(5, '0')}'");   // "00042"
        Console.WriteLine($"PadRight(5,'-'): '{kod.PadRight(5, '-')}'"); // "42---"

        // Split i Join
        string csv = "Kacper,25,Warszawa,Developer";
        string[] czesci = csv.Split(',');
        Console.WriteLine($"Split: {string.Join(" | ", czesci)}");

        // Split z opcjami — usuwa puste wpisy
        string brud = "a,,b,,,c,";
        string[] czyste = brud.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"Split RemoveEmpty: {string.Join(",", czyste)}");

        string[] owoce = { "jabłko", "banan", "gruszka" };
        Console.WriteLine($"Join: {string.Join(", ", owoce)}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PORÓWNYWANIE STRINGÓW — PUŁAPKI
    // ─────────────────────────────────────────────────────────────────────────

    public static void PorownywanieStringow()
    {
        Console.WriteLine("\n=== PORÓWNYWANIE STRINGÓW ===");

        string s1 = "hello";
        string s2 = "HELLO";

        Console.WriteLine($"s1 == s2:                {s1 == s2}");         // False
        Console.WriteLine($"s1.Equals(s2):           {s1.Equals(s2)}");    // False
        // OrdinalIgnoreCase — poprawne porównanie bez względu na wielkość liter
        Console.WriteLine($"OrdinalIgnoreCase:        {s1.Equals(s2, StringComparison.OrdinalIgnoreCase)}"); // True
        Console.WriteLine($"string.Equals statyczne: {string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)}");

        // String interning — literały → ten sam obiekt w pamięci
        string a = "hello";
        string b = "hello";
        Console.WriteLine($"ReferenceEquals literały: {object.ReferenceEquals(a, b)}"); // True — intern pool

        string c = new string('h', 1) + "ello"; // tworzony w runtime — nie internowany
        Console.WriteLine($"ReferenceEquals runtime:  {object.ReferenceEquals(a, c)}"); // False
        Console.WriteLine($"a == c (wartość):         {a == c}");                       // True
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IMMUTABILITY — DLACZEGO STRING + W PĘTLI JEST ZŁE
    // ─────────────────────────────────────────────────────────────────────────

    public static void ImmutabilityIStringBuilder()
    {
        Console.WriteLine("\n=== IMMUTABILITY I STRINGBUILDER ===");

        // Każde + tworzy nowy obiekt — O(n²) alokacji dla n iteracji!
        string zly = "";
        for (int i = 0; i < 5; i++)
            zly += i;   // 5 alokacji pośrednich — dla 10000 iteracji = setki MB

        Console.WriteLine($"Zła konkatenacja: {zly}");

        // StringBuilder — mutowalny bufor, O(n) alokacji, podwaja rozmiar gdy pełny
        var sb = new System.Text.StringBuilder(capacity: 64); // initial capacity

        sb.Append("Cześć");
        sb.Append(", ");
        sb.Append("Kacper");
        sb.AppendLine("!");
        sb.AppendFormat("Mam {0} lat", 25);
        sb.Insert(0, ">>> ");
        sb.Replace("Kacper", "Ania");

        string wynik = sb.ToString();   // jeden string na końcu
        Console.WriteLine(wynik);

        // Fluent chaining — StringBuilder zwraca this
        string html = new System.Text.StringBuilder()
            .Append("<html>")
            .AppendLine("<body>")
            .AppendFormat("<h1>{0}</h1>", "Tytuł")
            .AppendLine()
            .Append("</body></html>")
            .ToString();
        Console.WriteLine($"HTML (fragment): {html[..30]}...");

        // Kiedy StringBuilder, kiedy nie:
        // ✅ Budowanie w pętli (>3 iteracji)
        // ✅ Złożona logika (warunki, wiele kroków)
        // ❌ 2-3 proste konkatenacje — + jest OK
        // ❌ Gdy możesz użyć string.Join
        string[] dane = { "A", "B", "C" };
        Console.WriteLine($"string.Join zamiast SB: {string.Join("-", dane)}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FORMATOWANIE STRINGÓW — wszystkie specyfikatory
    // ─────────────────────────────────────────────────────────────────────────

    public static void FormatowanieStringow()
    {
        Console.WriteLine("\n=== FORMATOWANIE STRINGÓW ===");

        double liczba    = 1234567.891234;
        decimal pieniadze = 9876.54M;
        DateTime data    = new DateTime(2025, 3, 15, 14, 30, 0);

        // Liczby
        Console.WriteLine($"F2 (Fixed 2):   {liczba:F2}");     // 1234567,89
        Console.WriteLine($"N2 (Number):    {liczba:N2}");     // 1 234 567,89
        Console.WriteLine($"E2 (Scientific):{liczba:E2}");     // 1,23E+006
        Console.WriteLine($"C  (Currency):  {pieniadze:C}");   // 9 876,54 zł
        Console.WriteLine($"C0 (No cents):  {pieniadze:C0}");  // 9 877 zł
        Console.WriteLine($"P  (Percent):   {0.7531:P}");      // 75,31%
        Console.WriteLine($"P1:             {0.7531:P1}");     // 75,3%

        // Całkowite
        int n = 255;
        Console.WriteLine($"D5 (Decimal):   {n:D5}");   // 00255
        Console.WriteLine($"X  (Hex):       {n:X}");    // FF
        Console.WriteLine($"x  (hex):       {n:x}");    // ff
        Console.WriteLine($"B  (Binary):    {n:B}");    // 11111111

        // Daty
        Console.WriteLine($"d (short date): {data:d}");
        Console.WriteLine($"T (long time):  {data:T}");
        Console.WriteLine($"custom:         {data:yyyy-MM-dd HH:mm:ss}");

        // Wyrównanie — {wartość,szerokość} (minus = do lewej)
        Console.WriteLine($"{"Imię",-15} {"Wiek",5} {"Saldo",12}");
        Console.WriteLine($"{"Kacper",-15} {25,5} {1234.56M,12:C}");
        Console.WriteLine($"{"Anna",-15} {30,5} {5678.90M,12:C}");

        // string.Format — stary styl, nadal przydatny
        string stary = string.Format("Imię: {0}, Wiek: {1}, Saldo: {2:C}", "Kacper", 25, 1234.56M);
        Console.WriteLine(stary);
    }
}
