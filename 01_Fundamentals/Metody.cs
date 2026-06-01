namespace _01_Fundamentals;

public static class Metody
{
    // ─────────────────────────────────────────────────────────────────────────
    // ANATOMIA METODY — co się dzieje pod spodem
    // Każde wywołanie metody = nowy stack frame (adres powrotu + parametry + zmienne lokalne)
    // Zbyt głęboka rekurencja = StackOverflowException (~1MB stosu)
    // ─────────────────────────────────────────────────────────────────────────

    public static void AnatomiaMetody()
    {
        Console.WriteLine("\n=== ANATOMIA METODY ===");

        int wynik = Dodaj(3, 5);
        Console.WriteLine($"Dodaj(3,5) = {wynik}");   // 8

        // Expression-bodied method (C# 6+) — skrót dla prostych, jednolinijkowych metod
        Console.WriteLine($"Kwadrat(7) = {Kwadrat(7)}");
        Console.WriteLine($"Powitaj('Ania') = {Powitaj("Ania")}");
    }

    private static int  Dodaj(int a, int b) { return a + b; }  // klasyczna forma
    private static int  Kwadrat(int x)    => x * x;            // expression-bodied
    private static string Powitaj(string imie) => $"Cześć, {imie}!";
    private static void Wypisz(string s)   => Console.WriteLine(s);

    // ─────────────────────────────────────────────────────────────────────────
    // TYPY ZWRACANE — void, wartość, tuple, nullable
    // ─────────────────────────────────────────────────────────────────────────

    public static void TypyZwracane()
    {
        Console.WriteLine("\n=== TYPY ZWRACANE ===");

        // Tuple — zwracanie wielu wartości (C# 7+, named tuple)
        var stat = Statystyki(new[] { 3, 7, 2, 9, 1 });
        Console.WriteLine($"Min={stat.Min}, Max={stat.Max}, Śr={stat.Srednia:F1}");

        // Dekonstrukcja tuple
        var (min, max, srednia) = Statystyki(new[] { 5, 10, 15 });
        Console.WriteLine($"Dekonstrukcja: min={min}, max={max}, śr={srednia}");

        // Nullable return — null zamiast magicznych wartości jak -1
        int? idx1 = SzukajNullable(new[] { 10, 20, 30 }, 20);
        int? idx2 = SzukajNullable(new[] { 10, 20, 30 }, 99);
        Console.WriteLine($"Szukaj 20: indeks={idx1 ?? -1}");   // 1
        Console.WriteLine($"Szukaj 99: {(idx2.HasValue ? $"{idx2}" : "nie znaleziono")}");
    }

    private static (int Min, int Max, double Srednia) Statystyki(int[] liczby) =>
        (liczby.Min(), liczby.Max(), liczby.Average());

    private static int? SzukajNullable(int[] tab, int cel)
    {
        for (int i = 0; i < tab.Length; i++)
            if (tab[i] == cel) return i;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PRZEKAZYWANIE PARAMETRÓW — wartość vs referencja
    // ─────────────────────────────────────────────────────────────────────────

    public static void PrzekazywaniePrzezWartosc()
    {
        Console.WriteLine("\n=== PRZEKAZYWANIE PRZEZ WARTOŚĆ ===");

        // Typy wartościowe — kopia trafia do metody
        int x = 5;
        PodwojWartosc(x);
        Console.WriteLine($"Po PodwojWartosc: x={x}");  // 5 — oryginał niezmieniony

        // Typy referencyjne — kopia referencji trafia do metody
        // Ale oba wskazują na TEN SAM obiekt!
        var listka = new List<int> { 10, 20 };
        DodajElement(listka);
        Console.WriteLine($"Po DodajElement: Count={listka.Count}");  // 3 — zmieniony!

        // Podmiana referencji w metodzie NIE zmienia oryginału
        PodmienListe(listka);
        Console.WriteLine($"Po PodmienListe: Count={listka.Count}");  // 3 — niezmieniony
    }

    private static void PodwojWartosc(int liczba) { liczba *= 2; }
    private static void DodajElement(List<int> lista) { lista.Add(99); }
    private static void PodmienListe(List<int> lista) { lista = new List<int> { 1, 2, 3 }; }

    // ─────────────────────────────────────────────────────────────────────────
    // ref — modyfikacja oryginału przez referencję
    // ─────────────────────────────────────────────────────────────────────────

    public static void ParametrRef()
    {
        Console.WriteLine("\n=== PARAMETR ref ===");

        int a = 5, b = 10;
        Console.WriteLine($"Przed Swap: a={a}, b={b}");
        Swap(ref a, ref b);
        Console.WriteLine($"Po Swap:    a={a}, b={b}");  // a=10, b=5

        // ref wymaga zainicjalizowanej zmiennej PRZED wywołaniem
        // int niezainicjalizowana; Podwoj(ref niezainicjalizowana); ← BŁĄD

        int x = 5;
        PodwojRef(ref x);
        Console.WriteLine($"PodwojRef: x={x}");  // 10 — oryginał zmieniony
    }

    private static void Swap(ref int a, ref int b) { int temp = a; a = b; b = temp; }
    private static void PodwojRef(ref int liczba)  { liczba *= 2; }

    // ─────────────────────────────────────────────────────────────────────────
    // out — metoda zwraca wartość przez parametr (nie wymaga inicjalizacji)
    // Wzorzec TryXxx — najczęstsze użycie out w .NET
    // ─────────────────────────────────────────────────────────────────────────

    public static void ParametrOut()
    {
        Console.WriteLine("\n=== PARAMETR out ===");

        // out var — inline deklaracja (C# 7+)
        if (int.TryParse("123", out int liczba))
            Console.WriteLine($"TryParse sukces: {liczba}");

        if (!int.TryParse("abc", out int zla))
            Console.WriteLine($"TryParse fail: wynik={zla}");  // 0 — domyślna

        // Własny wzorzec TryXxx
        if (TryPodziel(10, 3, out double wynik))
            Console.WriteLine($"10/3 = {wynik:F4}");

        if (!TryPodziel(10, 0, out double _))   // _ = discard — nie potrzebujemy wartości
            Console.WriteLine("Dzielenie przez zero!");

        // Wiele parametrów out
        PodzielLiczbe(47, out int czesc, out int reszta);
        Console.WriteLine($"47: część={czesc}, reszta={reszta}");
    }

    private static bool TryPodziel(int licznik, int mianownik, out double wynikD)
    {
        if (mianownik == 0) { wynikD = 0; return false; }
        wynikD = (double)licznik / mianownik;
        return true;
    }

    private static void PodzielLiczbe(int liczba, out int czesc, out int reszta)
    {
        czesc  = liczba / 10;
        reszta = liczba % 10;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // in — przekazanie przez referencję, ale tylko do odczytu (C# 7.2+)
    // Optymalizacja dla dużych struktur (brak kopiowania + gwarancja braku modyfikacji)
    // ─────────────────────────────────────────────────────────────────────────

    public static void ParametrIn()
    {
        Console.WriteLine("\n=== PARAMETR in ===");

        int x = 42;
        WypiszIn(in x);  // in opcjonalne w wywołaniu, ale zwiększa czytelność
        WypiszIn(x);     // też działa

        // in dla dużych struct — brak kopiowania przy każdym wywołaniu
        // (demonstrujemy koncepcyjnie — dla int nie ma sensu wydajnościowego)
        Console.WriteLine("in = przekaz przez ref, ale metoda NIE MOŻE modyfikować");
    }

    private static void WypiszIn(in int liczba)
    {
        Console.WriteLine($"WypiszIn: {liczba}");
        // liczba = 5; ← BŁĄD kompilacji — in jest readonly
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PARAMETRY DOMYŚLNE I NAZWANE
    // ─────────────────────────────────────────────────────────────────────────

    public static void DomyslneINazwane()
    {
        Console.WriteLine("\n=== PARAMETRY DOMYŚLNE I NAZWANE ===");

        // Parametry domyślne — MUSZĄ być po obowiązkowych
        LogujZdarzenie("Użytkownik zalogowany");                      // INFO, konsola
        LogujZdarzenie("Błąd krytyczny", "ERROR");                    // ERROR
        LogujZdarzenie("Audyt", doPliku: true);                       // pomijamy środkowe

        // Nazwane argumenty — kolejność nieważna
        UtworzUzytkownika(imie: "Jan", wiek: 30, aktywny: false);    // pomijamy 'rola'

        // PUŁAPKA: wartości domyślne wkompilowane w IL wywołującego
        // Jeśli zmienisz domyślną w DLL, stary kod nadal używa starej wartości!
    }

    private static void LogujZdarzenie(
        string komunikat,
        string poziom    = "INFO",
        bool   doPliku   = false)
    {
        Console.WriteLine($"[{poziom}] {komunikat}" + (doPliku ? " (→plik)" : ""));
    }

    private static void UtworzUzytkownika(
        string imie,
        string rola    = "User",
        bool   aktywny = true,
        int    wiek    = 0)
    {
        Console.WriteLine($"{imie} | {rola} | aktywny:{aktywny} | wiek:{wiek}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PARAMS — zmienna liczba argumentów
    // ─────────────────────────────────────────────────────────────────────────

    public static void ParametrParams()
    {
        Console.WriteLine("\n=== PARAMETR params ===");

        // params tworzy tablicę przy każdym wywołaniu — w hot path rozważ ReadOnlySpan<T>
        Console.WriteLine($"Suma():          {Suma()}");              // 0
        Console.WriteLine($"Suma(1,2,3):     {Suma(1, 2, 3)}");      // 6
        Console.WriteLine($"Suma(1,2,3,4,5): {Suma(1, 2, 3, 4, 5)}"); // 15

        // Przekazanie tablicy wprost
        int[] arr = { 10, 20, 30 };
        Console.WriteLine($"Suma(arr):        {Suma(arr)}");         // 60

        // params z innymi parametrami — musi być OSTATNI
        Console.WriteLine(Polacz(", ", "Ania", "Bartek", "Celina"));  // Ania, Bartek, Celina
        Console.WriteLine(Polacz(" | ", "A", "B", "C", "D"));         // A | B | C | D
    }

    private static int    Suma(params int[] liczby)   { int s = 0; foreach (int n in liczby) s += n; return s; }
    private static string Polacz(string sep, params string[] elementy) => string.Join(sep, elementy);

    // ─────────────────────────────────────────────────────────────────────────
    // PRZECIĄŻANIE METOD — ta sama nazwa, różne sygnatury
    // Kompilator wybiera wersję w czasie kompilacji (nie runtime!)
    // ─────────────────────────────────────────────────────────────────────────

    public static void PrzeciazanieMetod()
    {
        Console.WriteLine("\n=== PRZECIĄŻANIE METOD ===");

        Console.WriteLine($"Dodaj(1, 2):         {DodajO(1, 2)}");        // int version
        Console.WriteLine($"Dodaj(1.5, 2.5):     {DodajO(1.5, 2.5)}");    // double version
        Console.WriteLine($"Dodaj(1, 2, 3):      {DodajO(1, 2, 3)}");     // 3-param version
        Console.WriteLine($"Dodaj(\"A\", \"B\"): {DodajO("A", "B")}");    // string version

        // Resolucja przeciążeń przez kompilator:
        // 1. Dokładne dopasowanie typów
        // 2. Niejawna konwersja (implicit cast, np. int → long)
        // 3. Boxing (np. int → object)
        // Niejednoznaczne → BŁĄD kompilacji

        // NIE MOŻNA przeciążyć tylko typem zwracanym:
        // public double Dodaj(int a, int b) => (double)(a+b); ← BŁĄD — ta sama sygnatura
    }

    // Przeciążenia muszą różnić się liczbą parametrów, typami lub kolejnością typów
    private static int    DodajO(int a, int b)       => a + b;
    private static double DodajO(double a, double b) => a + b;
    private static int    DodajO(int a, int b, int c) => a + b + c;
    private static string DodajO(string a, string b) => a + b;

    // ─────────────────────────────────────────────────────────────────────────
    // METODY LOKALNE (Local Functions) — C# 7+
    // Widoczne tylko w metodzie, w której są zdefiniowane
    // ─────────────────────────────────────────────────────────────────────────

    public static void MetodyLokalne()
    {
        Console.WriteLine("\n=== METODY LOKALNE (C# 7+) ===");

        long wynik = Silnia(10);
        Console.WriteLine($"Silnia(10) = {wynik}");

        // Filtruj i transformuj — helper widoczny tylko tu
        int[] dane = { 1, 4, 7, 2, 8, 3, 6 };
        int[] przetworzone = FiltrujITransformuj(dane, prog: 3);
        Console.WriteLine($"Filtruj >3 i parzyste, podnieś do kwadratu: {string.Join(", ", przetworzone)}");

        // static local functions (C# 8+) — zakaz closures
        int prog2 = 5;
        int suma = dane.Where(x => x > prog2).Select(StatycznyTransformuj).Sum();
        Console.WriteLine($"Suma statyczna: {suma}");

        static int StatycznyTransformuj(int x) => x * 2;
        // static nie może używać prog2 — gwarancja braku niezamierzonego domknięcia
    }

    private static long Silnia(int n)
    {
        if (n < 0) throw new ArgumentException("n musi być >= 0");
        return ObliczSilnie(n);

        // Metoda lokalna — może być po wywołaniu!
        long ObliczSilnie(int x) => x <= 1 ? 1 : x * ObliczSilnie(x - 1);
    }

    private static int[] FiltrujITransformuj(int[] dane, int prog)
    {
        bool SpelniaWarunek(int x) => x > prog && x % 2 == 0;
        int Transformuj(int x) => x * x;

        return dane.Where(SpelniaWarunek).Select(Transformuj).ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // REKURENCJA + MEMOIZACJA
    // ─────────────────────────────────────────────────────────────────────────

    public static void Rekurencja()
    {
        Console.WriteLine("\n=== REKURENCJA I MEMOIZACJA ===");

        // Rekurencja — MUSI mieć warunek bazowy (base case), inaczej → StackOverflow
        Console.WriteLine($"Silnia(10) = {SilniaRek(10)}");   // 3628800

        // Fibonacci naiwny — O(2^n), dla n=40 już wolny
        Console.WriteLine($"Fib(10) naiwny = {FibNaiwny(10)}"); // 55

        // Fibonacci z memoizacją — O(n)
        Console.WriteLine($"Fib(50) z cache = {FibMemo(50)}");  // błyskawiczne

        // Wersja iteracyjna — preferowana w C# (C# NIE optymalizuje tail calls)
        Console.WriteLine($"Silnia(10) iteracyjna = {SilniaIteracyjna(10)}");
    }

    private static int SilniaRek(int n)
    {
        if (n <= 1) return 1;          // warunek bazowy
        return n * SilniaRek(n - 1);  // krok rekurencyjny
    }

    private static int FibNaiwny(int n)
    {
        if (n <= 1) return n;
        return FibNaiwny(n - 1) + FibNaiwny(n - 2);  // O(2^n) — bardzo wolne!
    }

    private static long FibMemo(int n, Dictionary<int, long>? cache = null)
    {
        cache ??= new Dictionary<int, long>();
        if (n <= 1) return n;
        if (cache.TryGetValue(n, out long cached)) return cached;
        long wynik = FibMemo(n - 1, cache) + FibMemo(n - 2, cache);
        cache[n] = wynik;
        return wynik;
    }

    private static long SilniaIteracyjna(int n)  // preferowana wersja w C#
    {
        long wynik = 1;
        for (int i = 2; i <= n; i++) wynik *= i;
        return wynik;
    }
}
