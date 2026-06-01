namespace _01_Fundamentals;

public static class TypyDanychIZmienne
{
    // ─────────────────────────────────────────────────────────────────────────
    // TYPY WARTOŚCIOWE vs REFERENCYJNE
    // ─────────────────────────────────────────────────────────────────────────

    public static void WartoscioweVsReferencyjne()
    {
        Console.WriteLine("\n=== TYPY WARTOŚCIOWE vs REFERENCYJNE ===");

        // VALUE TYPES — przechowują wartość bezpośrednio (kopia przy przypisaniu)
        int a = 5;
        int b = a;   // kopia wartości
        b = 99;
        Console.WriteLine($"a={a}, b={b}");    // a=5, b=99 — a niezmienione!

        // REFERENCE TYPES — przechowują referencję (adres) do obiektu na stercie
        int[] tab1 = { 1, 2, 3 };
        int[] tab2 = tab1;    // kopia REFERENCJI — oba wskazują na TEN SAM obiekt
        tab2[0] = 99;
        Console.WriteLine($"tab1[0]={tab1[0]}");  // 99 — tab1 też się zmieniło!

        // Mity vs fakty o stosie i stercie:
        // MIT: Typy wartościowe zawsze żyją na stosie.
        // FAKT: Lokalne zmienne i parametry → stos. Pola klasy → sterta (razem z obiektem).
        //       Typy referencyjne (obiekty klas) → zawsze sterta.
        //       GC sprząta obiekty ze sterty gdy nie ma żadnej referencji do nich.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WBUDOWANE TYPY LICZBOWE CAŁKOWITE
    // ─────────────────────────────────────────────────────────────────────────

    public static void TypyLiczboweCalkowite()
    {
        Console.WriteLine("\n=== TYPY CAŁKOWITE ===");

        byte b    = 255;                        // 8-bit,  0 do 255
        short s   = 32_767;                     // 16-bit, -32768 do 32767
        int i     = 2_147_483_647;              // 32-bit, ±2.1 mld — najczęściej używany
        long l    = 9_223_372_036_854_775_807L; // 64-bit, ±9.2 trln, sufiks L

        uint zawszeDodatni = 4_294_967_295U;    // 32-bit unsigned, sufiks U

        // Podkreślniki jako separator cyfr (czytelność, bez wpływu na wartość)
        int milion = 1_000_000;

        Console.WriteLine($"byte:  {byte.MinValue} do {byte.MaxValue}");
        Console.WriteLine($"int:   {int.MinValue} do {int.MaxValue}");
        Console.WriteLine($"long:  {long.MinValue} do {long.MaxValue}");
        Console.WriteLine($"milion ze spacją: {milion:N0}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TYPY ZMIENNOPRZECINKOWE — double, float, decimal
    // ─────────────────────────────────────────────────────────────────────────

    public static void TypyZmiennoprzecinkowe()
    {
        Console.WriteLine("\n=== TYPY ZMIENNOPRZECINKOWE ===");

        double pi = 3.14159265358979;    // 64-bit, ~15 cyfr — domyślny dla ułamków
        float  f  = 3.14f;              // 32-bit, ~7 cyfr, sufiks f — grafika/gry
        decimal cena = 19.99M;          // 128-bit, ~28 cyfr, sufiks M — pieniądze/finanse

        Console.WriteLine($"double pi: {pi}");
        Console.WriteLine($"float:     {f}");
        Console.WriteLine($"decimal:   {cena}");

        // PUŁAPKA: błąd zaokrąglenia w double (IEEE 754)
        double wynikDouble  = 0.1 + 0.2;
        decimal wynikDecimal = 0.1M + 0.2M;

        Console.WriteLine($"0.1 + 0.2 (double):  {wynikDouble}");    // 0.30000000000000004!
        Console.WriteLine($"0.1 + 0.2 (decimal): {wynikDecimal}");   // 0.3 dokładnie

        Console.WriteLine($"wynikDouble == 0.3: {wynikDouble == 0.3}");   // False!
        // Poprawne porównanie float/double:
        Console.WriteLine($"Epsilon test: {Math.Abs(wynikDouble - 0.3) < 1e-10}"); // True

        // Zasada: pieniądze → decimal | obliczenia naukowe → double | grafika → float
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BOOL I CHAR
    // ─────────────────────────────────────────────────────────────────────────

    public static void BoolIChar()
    {
        Console.WriteLine("\n=== BOOL I CHAR ===");

        bool czyZalogowany = true;
        int wiek = 20;
        bool mozePic = wiek >= 18;    // true — wynik porównania

        // W C# NIE można wrzucić liczby tam gdzie bool (w odróżnieniu od C/C++)
        // if (1) { } ← BŁĄD kompilacji

        char litera = 'A';             // Apostrofy, NIE cudzysłów!
        char cyfra  = '5';             // To ZNAK '5', nie liczba 5
        char emoji  = '★';            // Unicode działa

        int kodUnicode = (int)'A';     // 65 — char to pod spodem liczba
        char zKodu    = (char)65;      // 'A'

        Console.WriteLine($"czyZalogowany={czyZalogowany}, mozePic={mozePic}");
        Console.WriteLine($"litera={litera}, cyfra={cyfra}, kod ASCII 'A'={kodUnicode}");
        Console.WriteLine($"(char)65 = {zKodu}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VAR — WNIOSKOWANIE TYPÓW
    // ─────────────────────────────────────────────────────────────────────────

    public static void VarWnioskowanieTypow()
    {
        Console.WriteLine("\n=== VAR — WNIOSKOWANIE TYPÓW ===");

        // var = kompilator sam wywnioskuje typ z prawej strony przypisania
        var wiek   = 25;              // int
        var imie   = "Piotr";         // string
        var pi     = 3.14;            // double
        var lista  = new List<int>(); // List<int>

        // var to NIE jest dynamic — typ znany w CZASIE KOMPILACJI
        // wiek = "tekst"; ← BŁĄD — wiek jest int na zawsze

        // var NIE może być niezainicjalizowany: var x; ← BŁĄD

        // DOBRE użycie var — typ oczywisty z kontekstu:
        var user      = new System.Text.StringBuilder();
        var slownik   = new Dictionary<string, List<int>>();   // bez var = masakra

        // ZŁE użycie var — typ nieoczywisty:
        // var x = ObliczCos();  ← co zwraca? Trzeba zajrzeć w definicję
        // int x = ObliczCos();  ← od razu wiadomo

        // var vs object:
        var    typowany = 5;   // int — intellisense działa, błędy przy kompilacji
        object luzy     = 5;   // object — kompilator nie wie że to int

        Console.WriteLine($"var wiek={wiek} (int), var imie={imie} (string)");
        Console.WriteLine($"var pi={pi} (double)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONST I READONLY
    // ─────────────────────────────────────────────────────────────────────────

    public static void ConstIReadonly()
    {
        Console.WriteLine("\n=== CONST I READONLY ===");

        // const — wartość znana w CZASIE KOMPILACJI, wbudowana w IL, zawsze static
        // Tylko typy proste (int, string, bool, double...)
        const double PI            = 3.14159265358979;
        const int    MAX_USERS     = 1000;
        const string WERSJA_API    = "v1";

        // PI = 3.0; ← BŁĄD kompilacji — const jest niezmienny

        // readonly — wartość ustalana w konstruktorze lub przy deklaracji, potem niezmienna
        // Może być instancyjna, może być dowolnym typem
        // (demonstracja przez lokalny readonly — readonly pola są w klasach)

        Console.WriteLine($"PI={PI}, MAX_USERS={MAX_USERS}, API={WERSJA_API}");
        Console.WriteLine("const → wartość znana w kompilacji, wkompilowana w IL");
        Console.WriteLine("readonly → wartość ustalana w konstruktorze (runtime)");
        // Różnica: const szybsze ale tylko typy proste; readonly dla runtime values i obiektów
    }

    // ─────────────────────────────────────────────────────────────────────────
    // KONWERSJE TYPÓW
    // ─────────────────────────────────────────────────────────────────────────

    public static void KonwersjeTypow()
    {
        Console.WriteLine("\n=== KONWERSJE TYPÓW ===");

        // IMPLICIT (niejawna) — bezpieczna, bez utraty danych
        int  i = 100;
        long l = i;      // int → long — zawsze bezpieczne
        double d = i;    // int → double — OK
        Console.WriteLine($"implicit: int {i} → long {l} → double {d}");

        // EXPLICIT CAST (jawna) — może stracić dane, świadome rzutowanie
        double duzy = 9.99;
        int maly    = (int)duzy;   // NIE zaokrągla — OBCINA część ułamkową!
        Console.WriteLine($"explicit: (int)9.99 = {maly}");   // 9, nie 10!

        long duzyLong      = 3_000_000_000L;
        int  przepelnienie = (int)duzyLong;  // OVERFLOW — wynik nieprzewidywalny
        Console.WriteLine($"overflow: (int)3_000_000_000L = {przepelnienie}");

        // Convert.* — bezpieczna konwersja, null → 0 (nie rzuca wyjątku dla null)
        string tekst = "42";
        int zTekstu  = Convert.ToInt32(tekst);   // 42
        string? n    = null;
        int zNull    = Convert.ToInt32(n);       // 0 — dla null zwraca 0
        Console.WriteLine($"Convert: '{tekst}'→{zTekstu}, null→{zNull}");

        // int.Parse — rzuca wyjątek jeśli string nie jest liczbą
        int sparsowany = int.Parse("123");
        Console.WriteLine($"Parse: {sparsowany}");

        // TryParse — ZALECANE, nie rzuca wyjątku
        if (int.TryParse("456abc", out int wynik))
            Console.WriteLine($"TryParse sukces: {wynik}");
        else
            Console.WriteLine("TryParse: nie udało się sparsować '456abc'");

        bool ok = double.TryParse("3.14", out double parsedPi);
        Console.WriteLine($"TryParse double '3.14': ok={ok}, wartość={parsedPi}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NULLABLE REFERENCE TYPES (C# 8+)
    // ─────────────────────────────────────────────────────────────────────────

    public static void NullableReferenceTypes()
    {
        Console.WriteLine("\n=== NULLABLE REFERENCE TYPES (C# 8+) ===");

        // Bez nullable — klasyczny problem: NullReferenceException w runtime
        // string imie = null;
        // Console.WriteLine(imie.Length); ← NullReferenceException!

        // Z nullable (włączone przez <Nullable>enable</Nullable> w .csproj):
        string  nienullowalne = "Ania"; // NIE może być null — kompilator ostrzeże
        string? nullowalne    = null;   // może być null — ? = świadoma decyzja

        // Operator ?. — null-conditional (nie rzuca wyjątku dla null)
        string? s       = null;
        int?   dlugosc  = s?.Length;    // null, nie wyjątek
        Console.WriteLine($"null?.Length = {dlugosc?.ToString() ?? "null"}");

        // Operator ?? — null coalescing (użyj wartości domyślnej gdy null)
        string wynik = s ?? "domyślna wartość";
        Console.WriteLine($"null ?? \"domyślna wartość\" = {wynik}");

        // Operator ??= — null coalescing assignment
        string? imie = null;
        imie ??= "Anonimowy";           // przypisz tylko jeśli null
        Console.WriteLine($"??= : {imie}");

        // Łańcuch ?. przez zagnieżdżone obiekty
        string? miasto = nullowalne?.ToUpper()?.Trim() ?? "Nieznane";
        Console.WriteLine($"łańcuch ?.: {miasto}");

        // Nullable value types — int? double? itp.
        int? liczbaLubNull = null;
        Console.WriteLine($"int? = {liczbaLubNull.HasValue}, domyślna: {liczbaLubNull ?? 0}");
        liczbaLubNull = 42;
        Console.WriteLine($"int? = {liczbaLubNull.HasValue}, wartość: {liczbaLubNull.Value}");
    }
}
