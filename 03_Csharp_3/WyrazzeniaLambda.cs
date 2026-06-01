namespace _03_Csharp_3;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public class ZamowienieLP
{
    public int Id { get; set; }
    public string Klient { get; set; } = "";
    public decimal Kwota { get; set; }
    public bool Oplacone { get; set; }
}

public static class WyrazzeniaLambda
{
    // ── 1. Składnia lambdy ────────────────────────────────────────────────────

    public static void SkładniaLambdy()
    {
        Console.WriteLine("\n── SkładniaLambdy ──");

        // Lambda wyrażeniowa — pojedyncze wyrażenie (bez klamr, bez return)
        Func<int, int> kwadrat = x => x * x;
        Func<int, int, int> suma = (a, b) => a + b;
        Action<string> wypisz = s => Console.WriteLine($"  {s}");

        // Lambda blokowa — blok kodu z return
        Func<int, string> opis = n =>
        {
            if (n < 0) return "ujemna";
            if (n == 0) return "zero";
            return "dodatnia";
        };

        Console.WriteLine($"kwadrat(5) = {kwadrat(5)}");
        Console.WriteLine($"suma(3,4) = {suma(3, 4)}");
        wypisz("Lambda wyrażeniowa");
        Console.WriteLine($"opis(-3) = {opis(-3)}, opis(0) = {opis(0)}, opis(7) = {opis(7)}");

        // Lambda bez parametrów
        Func<DateTime> teraz = () => DateTime.Now;
        Console.WriteLine($"teraz() = {teraz():HH:mm:ss}");

        // Lambda z typem zwracanym (jawny, C# 10+)
        var parseIntOrDefault = (string s) =>
        {
            return int.TryParse(s, out int v) ? v : 0;
        };
        Console.WriteLine($"parseIntOrDefault(\"42\") = {parseIntOrDefault("42")}");
        Console.WriteLine($"parseIntOrDefault(\"abc\") = {parseIntOrDefault("abc")}");

        // Async lambda
        Func<int, Task<int>> asyncKwadrat = async x =>
        {
            await Task.Delay(1);
            return x * x;
        };
        int wynikAsync = asyncKwadrat(7).GetAwaiter().GetResult();
        Console.WriteLine($"asyncKwadrat(7) = {wynikAsync}");

        // Przekazywanie istniejącej metody zamiast lambdy (method group)
        Func<double, double> sqrt = Math.Sqrt;
        Console.WriteLine($"sqrt(16) = {sqrt(16)}");

        // Lambda jako argument — najczęstszy przypadek użycia
        var liczby = new[] { 5, 2, 8, 1, 9, 3 };
        Array.Sort(liczby, (a, b) => b - a); // malejąco
        Console.WriteLine($"Posortowane: [{string.Join(", ", liczby)}]");
    }

    // ── 2. Domknięcia (closures) ──────────────────────────────────────────────

    public static void Domkniecia()
    {
        Console.WriteLine("\n── Domkniecia ──");

        // Domknięcie przechwytuje REFERENCJĘ do zmiennej (nie wartość)
        int mnoznik = 3;
        Func<int, int> mnoz = x => x * mnoznik; // przechwytuje mnoznik

        Console.WriteLine($"mnoz(5) gdzie mnoznik=3: {mnoz(5)}");
        mnoznik = 10; // zmiana zmiennej zewnętrznej!
        Console.WriteLine($"mnoz(5) gdzie mnoznik=10: {mnoz(5)}"); // 50 — domknięcie!

        // Compiler generuje klasę domknięcia:
        // class <>c__DisplayClass { public int mnoznik; }
        // i przenosi zmienne do tej klasy

        // Shared state przez domknięcia — zamierzony efekt
        int licznik = 0;
        Action inkrementuj = () => licznik++;
        Action[] tabs = { inkrementuj, inkrementuj, inkrementuj };
        foreach (var t in tabs) t();
        Console.WriteLine($"Shared state: licznik={licznik}"); // 3

        // Pułapka pętli — przechwytuje zmienną (nie wartość) iteracji
        var akcjeZle = new List<Action>();
        for (int i = 0; i < 5; i++)
            akcjeZle.Add(() => Console.Write($"{i} ")); // przechwytuje ref do i!

        Console.Write("Błędne (loop trap): ");
        foreach (var a in akcjeZle) a();
        Console.WriteLine("← wszystkie wypisują 5 (i=5 po pętli)");

        // Naprawka — kopia zmiennej w każdej iteracji
        var akcjeOk = new List<Action>();
        for (int i = 0; i < 5; i++)
        {
            int kopia = i; // nowa zmienna dla każdej iteracji
            akcjeOk.Add(() => Console.Write($"{kopia} "));
        }
        Console.Write("Poprawne (kopia): ");
        foreach (var a in akcjeOk) a();
        Console.WriteLine();

        // foreach NIE ma tej pułapki w C# 5+ (każda iteracja ma własną zmienną)
        var elementy = new[] { "A", "B", "C" };
        var akcjeForEach = new List<Action>();
        foreach (var e in elementy)
            akcjeForEach.Add(() => Console.Write($"{e} ")); // OK
        Console.Write("foreach OK: ");
        foreach (var a in akcjeForEach) a();
        Console.WriteLine();

        // Zagnieżdżone domknięcia — każde ma swój zakres
        Func<int, Func<int, int>> adder = x => y => x + y;
        var add5 = adder(5);
        Console.WriteLine($"adder(5)(3) = {add5(3)}");
    }

    // ── 3. Static lambda (C# 9+) ─────────────────────────────────────────────

    public static void StaticLambda()
    {
        Console.WriteLine("\n── StaticLambda (C# 9+) ──");

        // static lambda — nie może przechwytywać żadnych zmiennych z zewnątrz
        // Zalety: brak alokacji obiektu domknięcia, czytelny kontrakt
        // int mnoznik = 5;
        Func<int, int> razy2 = static x => x * 2; // OK — nie przechwytuje niczego
        // Func<int, int> zla = static x => x * mnoznik; // BŁĄD kompilacji CS8820

        Console.WriteLine($"static lambda razy2(7) = {razy2(7)}");

        // Lambda caching — kompilator może zoptymalizować lambdę bez domknięcia
        // jako cached singleton (zamiast tworzyć nową instancję przy każdym wywołaniu)
        var lista = Enumerable.Range(1, 10).ToList();

        // Ta sama lambda — kompilator może ją zakeszować
        var parzyste = lista.Where(static n => n % 2 == 0).ToList();
        Console.WriteLine($"static lambda w LINQ: [{string.Join(", ", parzyste)}]");

        // Porównanie alokacji:
        // static lambda:           0 alokacji (zakeszowana)
        // lambda bez domknięcia:   0 alokacji (kompilator zazwyczaj optymalizuje)
        // lambda z domknięciem:    1 alokacja na każde wywołanie otaczającej metody
        Console.WriteLine("Alokacje: static=0, bez domknięcia=0, z domknięciem=1 per wywołanie");
    }

    // ── 4. Memoizacja ─────────────────────────────────────────────────────────

    public static void Memoizacja()
    {
        Console.WriteLine("\n── Memoizacja ──");

        // Memoizacja — zapamiętuje wynik dla danego argumentu (cache wyników)
        // Użyteczna dla kosztownych obliczeń z tymi samymi argumentami
        var memoFib = Memoize<int, long>(n =>
        {
            Console.Write($"[calc:{n}] ");
            return n <= 1 ? n : FibRekurencja(n);
        });

        Console.Write("Pierwsze wywołanie: ");
        Console.WriteLine(memoFib(10));
        Console.Write("Drugie wywołanie (z cache): ");
        Console.WriteLine(memoFib(10)); // brak "[calc]" — z cache!

        // Memoizacja Fibonacci z własną rekurencją z cache
        int obliczen = 0;
        var cache = new Dictionary<int, long>();
        Func<int, long> fibMemo = null!;
        fibMemo = n =>
        {
            if (cache.TryGetValue(n, out long v)) return v;
            obliczen++;
            long wynik = n <= 1 ? n : fibMemo(n - 1) + fibMemo(n - 2);
            cache[n] = wynik;
            return wynik;
        };

        long fib30 = fibMemo(30);
        Console.WriteLine($"fib(30) = {fib30}, obliczeń: {obliczen} (zamiast ~1M bez memo)");
    }

    static Func<TIn, TOut> Memoize<TIn, TOut>(Func<TIn, TOut> f) where TIn : notnull
    {
        var cache = new Dictionary<TIn, TOut>();
        return x =>
        {
            if (!cache.TryGetValue(x, out TOut? v))
                cache[x] = v = f(x);
            return v;
        };
    }

    static long FibRekurencja(int n) => n <= 1 ? n : FibRekurencja(n - 1) + FibRekurencja(n - 2);

    // ── 5. Currying i partial application ────────────────────────────────────

    public static void CurryingIPartialApplication()
    {
        Console.WriteLine("\n── CurryingIPartialApplication ──");

        // Currying — przekształcenie f(a,b) w f(a)(b)
        // Każde wywołanie zwraca funkcję oczekującą kolejnego argumentu
        Func<int, Func<int, int>> dodaj = a => b => a + b;
        var dodaj5 = dodaj(5);    // Func<int,int> — częściowo zastosowana
        var dodaj10 = dodaj(10);

        Console.WriteLine($"dodaj(5)(3) = {dodaj5(3)}");
        Console.WriteLine($"dodaj(10)(7) = {dodaj10(7)}");

        // Partial application — utrwalanie niektórych argumentów
        Func<int, int, int> mnoz = (a, b) => a * b;
        Func<int, int> podwoj  = PartialApply(mnoz, 2);
        Func<int, int> potraj  = PartialApply(mnoz, 3);

        Console.WriteLine($"podwoj(7) = {podwoj(7)}");
        Console.WriteLine($"potraj(7) = {potraj(7)}");

        // Praktyczny przykład: logger z utrwalonym prefixem
        Action<string, string> log = (prefix, msg) => Console.WriteLine($"  [{prefix}] {msg}");
        Action<string> infoLog  = msg => log("INFO", msg);
        Action<string> errorLog = msg => log("ERROR", msg);

        infoLog("Aplikacja uruchomiona");
        errorLog("Coś poszło nie tak");

        // Kompozycja funkcji
        Func<int, int> razy2   = x => x * 2;
        Func<int, int> plus10  = x => x + 10;
        Func<int, int> razy2Plus10 = Compose(razy2, plus10);

        Console.WriteLine($"Compose(razy2, plus10)(5) = {razy2Plus10(5)}"); // (5*2)+10 = 20

        // Potok transformacji
        var pipeline = new[] { razy2, plus10, (Func<int, int>)(x => x * x) }
            .Aggregate((f, g) => x => g(f(x)));
        Console.WriteLine($"Pipeline razy2→plus10→kwadrat(3): {pipeline(3)}"); // ((3*2)+10)^2 = 400
    }

    static Func<T2, TResult> PartialApply<T1, T2, TResult>(Func<T1, T2, TResult> f, T1 arg)
        => x => f(arg, x);

    static Func<T, TResult> Compose<T, TMiddle, TResult>(
        Func<T, TMiddle> f, Func<TMiddle, TResult> g) => x => g(f(x));

    // ── 6. Wzorce funkcyjne ───────────────────────────────────────────────────

    public static void WzorceFunkcyjne()
    {
        Console.WriteLine("\n── WzorceFunkcyjne ──");

        // Retry — lambda jako strategia ponawiania
        int prob = 0;
        string wynik = Retry(() =>
        {
            prob++;
            if (prob < 3) throw new InvalidOperationException($"Próba {prob} nieudana");
            return "sukces";
        }, maxProb: 5);
        Console.WriteLine($"Retry — wynik: '{wynik}' po {prob} próbach");

        // Fluent builder przez lambdy
        var zamowienie = BudujZamowienie(o =>
        {
            o.Id = 1;
            o.Klient = "Jan Kowalski";
            o.Kwota = 1500m;
            o.Oplacone = false;
        });
        Console.WriteLine($"Builder: {zamowienie.Klient}, {zamowienie.Kwota:C}");

        // Pipeline — sekwencyjne przetwarzanie przez funkcje
        var rezultat = PipelineTransform(
            "  hello world  ",
            s => s.Trim(),
            s => s.ToUpper(),
            s => s.Replace(" ", "_"),
            s => $"[{s}]"
        );
        Console.WriteLine($"Pipeline: '{rezultat}'");

        // Lazy evaluation z Lazy<T>
        var lazyOblic = new Lazy<int>(() =>
        {
            Console.Write("  [kosztowna operacja] ");
            return 42 * 42;
        });
        Console.WriteLine("Lazy<T> przed pierwszym dostępem — brak obliczeń");
        Console.WriteLine($"Lazy<T>.Value = {lazyOblic.Value}"); // teraz oblicza
        Console.WriteLine($"Lazy<T>.Value ponownie = {lazyOblic.Value}"); // z cache
    }

    static T Retry<T>(Func<T> operacja, int maxProb)
    {
        for (int i = 1; i <= maxProb; i++)
        {
            try { return operacja(); }
            catch when (i < maxProb) { }
        }
        return operacja(); // ostatnia próba — wyjątek propaguje
    }

    static ZamowienieLP BudujZamowienie(Action<ZamowienieLP> configure)
    {
        var z = new ZamowienieLP();
        configure(z);
        return z;
    }

    static string PipelineTransform(string input, params Func<string, string>[] kroki)
        => kroki.Aggregate(input, (curr, krok) => krok(curr));
}
