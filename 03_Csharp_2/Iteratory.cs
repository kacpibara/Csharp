namespace _03_Csharp_2;

// ── Klasy pomocnicze dla Iteratory ───────────────────────────────────────────

// Ręczna implementacja IEnumerable<T> + IEnumerator<T> — to co robi yield za nas
public class ZakresLiczbIter : IEnumerable<int>
{
    private readonly int _start, _koniec, _krok;

    public ZakresLiczbIter(int start, int koniec, int krok = 1)
    {
        _start = start; _koniec = koniec; _krok = krok;
    }

    public IEnumerator<int> GetEnumerator() => new ZakresEnumerator(_start, _koniec, _krok);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    private class ZakresEnumerator : IEnumerator<int>
    {
        private readonly int _koniec, _krok;
        private int _aktualny;

        public ZakresEnumerator(int start, int koniec, int krok)
        {
            _aktualny = start - krok; _koniec = koniec; _krok = krok;
        }

        public int Current => _aktualny;
        object System.Collections.IEnumerator.Current => _aktualny;

        public bool MoveNext()
        {
            _aktualny += _krok;
            return _aktualny <= _koniec;
        }

        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}

// Węzeł drzewa dla demonstracji DFS
public class WęzełIter<T>
{
    public T Wartosc { get; }
    public List<WęzełIter<T>> Dzieci { get; } = new();

    public WęzełIter(T wartosc) => Wartosc = wartosc;

    public WęzełIter<T> DodajDziecko(T wartosc)
    {
        var dziecko = new WęzełIter<T>(wartosc);
        Dzieci.Add(dziecko);
        return dziecko;
    }
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class Iteratory
{
    public static void IEnumerableIEnumerator()
    {
        Console.WriteLine("\n── IEnumerableIEnumerator ──");

        // foreach jest lukrem składniowym (syntactic sugar):
        // compiler zamienia foreach na: GetEnumerator → while(MoveNext) → Current → Dispose
        var zakres = new ZakresLiczbIter(1, 10, 2);

        Console.Write("foreach (ZakresLiczbIter 1..10 krok 2): ");
        foreach (int n in zakres)
            Console.Write($"{n} ");
        Console.WriteLine();

        // foreach desugaring — to samo co powyżej
        Console.Write("Ręczne: ");
        using var enumerator = zakres.GetEnumerator();
        while (enumerator.MoveNext())
            Console.Write($"{enumerator.Current} ");
        Console.WriteLine();

        // Każde GetEnumerator() daje NOWY enumerator — niezależne iteracje
        Console.Write("Iteracja 1: ");
        foreach (int n in zakres) Console.Write($"{n} ");
        Console.WriteLine();
        Console.Write("Iteracja 2 (od nowa): ");
        foreach (int n in zakres) Console.Write($"{n} ");
        Console.WriteLine();

        // IEnumerator<T> — kluczowe różnice vs IEnumerable<T>
        Console.WriteLine("IEnumerable<T>: fabryka enumeratorów, można iterować wielokrotnie");
        Console.WriteLine("IEnumerator<T>: jednorazowy kursor (stan bieżącej pozycji)");
    }

    public static void YieldReturn()
    {
        Console.WriteLine("\n── YieldReturn ──");

        // yield return — kompilator generuje state machine, metoda staje się iteratorem
        // Wykonanie LENIWE — kod przed yield nie działa dopóki nie pobierzesz elementu

        Console.Write("PierwszeN(5): ");
        foreach (int n in PierwszeN(5))
            Console.Write($"{n} ");
        Console.WriteLine();

        // Lazy — kod w metodzie wykonuje się krok po kroku
        Console.WriteLine("Lazy demo:");
        foreach (int n in LazyDemoGenerator())
        {
            Console.Write($"odebrano:{n} ");
            if (n == 2) break;
        }
        Console.WriteLine("(zatrzymano)");

        // yield return z LINQ — leniwe transformacje
        var potega2 = PierwszeN(8).Select(n => (int)Math.Pow(2, n));
        Console.WriteLine($"Potęgi 2: [{string.Join(", ", potega2)}]");

        // Generator nieskończony (infinite sequence)
        Console.Write("Fibonacci (pierwsze 10): ");
        foreach (int f in Fibonacci().Take(10))
            Console.Write($"{f} ");
        Console.WriteLine();
    }

    static IEnumerable<int> PierwszeN(int n)
    {
        for (int i = 1; i <= n; i++)
            yield return i;
    }

    static IEnumerable<int> LazyDemoGenerator()
    {
        Console.Write("[gen:start] ");
        yield return 1;
        Console.Write("[gen:po1] ");
        yield return 2;
        Console.Write("[gen:po2] ");
        yield return 3;
        Console.Write("[gen:po3] ");
    }

    static IEnumerable<int> Fibonacci()
    {
        int a = 0, b = 1;
        while (true)
        {
            yield return a;
            (a, b) = (b, a + b);
        }
    }

    public static void YieldBreak()
    {
        Console.WriteLine("\n── YieldBreak ──");

        // yield break — natychmiastowe zakończenie iteratora
        Console.Write("TakeWhile parzyste z {2,4,6,7,8}: ");
        foreach (int n in TakeWhileParzysty(new[] { 2, 4, 6, 7, 8 }))
            Console.Write($"{n} ");
        Console.WriteLine("(zatrzymano na 7)");

        // Bezpieczne zwolnienie zasobów w iteratorze — finally zawsze wykonane
        Console.WriteLine("Iterator z zasobem:");
        foreach (int n in IteratorZZasobem(3))
            Console.Write($"{n} ");
        Console.WriteLine("(finally wykonane po break)");

        // Warunkowe zakończenie
        Console.Write("Liczby pierwszorzędne do 20: ");
        foreach (int p in LiczbyPierwszeDoN(20))
            Console.Write($"{p} ");
        Console.WriteLine();
    }

    static IEnumerable<int> TakeWhileParzysty(int[] dane)
    {
        foreach (int n in dane)
        {
            if (n % 2 != 0) yield break; // zatrzymaj gdy nieparzyste
            yield return n;
        }
    }

    static IEnumerable<int> IteratorZZasobem(int ile)
    {
        Console.Write("[otwieram zasób] ");
        try
        {
            for (int i = 1; i <= ile; i++)
                yield return i;
        }
        finally
        {
            Console.Write("[zamykam zasób] "); // zawsze wykonane, nawet przy break!
        }
    }

    static IEnumerable<int> LiczbyPierwszeDoN(int max)
    {
        for (int n = 2; n <= max; n++)
        {
            if (CzyPierwsza(n)) yield return n;
        }
    }

    static bool CzyPierwsza(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i <= Math.Sqrt(n); i++)
            if (n % i == 0) return false;
        return true;
    }

    public static void LazyVsEager()
    {
        Console.WriteLine("\n── LazyVsEager ──");

        // LAZY — elementy generowane na żądanie (pull model)
        // Zalety: brak alokacji pełnej kolekcji, możliwe nieskończone sekwencje
        Console.Write("Lazy (Fibonacci, Take 8): ");
        foreach (int f in Fibonacci().Take(8))
            Console.Write($"{f} ");
        Console.WriteLine();

        // Nieskończona sekwencja + filtrowanie + limit — bez materializacji całości
        Console.Write("Pierwsze 5 parzystych Fibonacci: ");
        var parzeysteFib = Fibonacci().Where(n => n % 2 == 0).Take(5);
        foreach (int f in parzeysteFib)
            Console.Write($"{f} ");
        Console.WriteLine();

        // EAGER — wszystko naraz (ToList, ToArray, Count, Sum itp.)
        // Materializacja wymagana gdy: wielokrotny dostęp, indeksy, sortowanie
        var zmaterializowane = Fibonacci().Take(10).ToList();
        Console.WriteLine($"Eager ToList: Count={zmaterializowane.Count}, Max={zmaterializowane.Max()}");

        // Różnica w efektywności
        Console.WriteLine("\nLazy LINQ chain (Range 1M, Where, Select, Take 3):");
        var lazyChain = Enumerable.Range(1, 1_000_000)
            .Where(n => n % 7 == 0)
            .Select(n => n * n)
            .Take(3);
        foreach (var v in lazyChain)
            Console.Write($"{v} ");
        Console.WriteLine("(przetworzone: ~21 elementów, nie milion)");

        // Pułapka: Count() na IEnumerable = pełne przejście
        // ToList() przed Count() gdy potrzebujesz obu
        var lista = Enumerable.Range(1, 100).Where(n => n % 3 == 0).ToList();
        Console.WriteLine($"ToList przed wielokrotnym użyciem: Count={lista.Count}, Sum={lista.Sum()}");
    }

    public static void PraktyczneWzorce()
    {
        Console.WriteLine("\n── PraktyczneWzorce ──");

        // Paginacja — yield return z indeksem
        var dane = Enumerable.Range(1, 23).ToList();
        Console.WriteLine("Paginacja (rozmiar=5):");
        int strona = 1;
        foreach (var paczka in Paginuj(dane, 5))
            Console.WriteLine($"  Strona {strona++}: [{string.Join(", ", paczka)}]");

        // Spłaszczanie zagnieżdżonych kolekcji
        var zagniezdzone = new List<List<int>>
        {
            new() { 1, 2, 3 },
            new() { 4, 5 },
            new() { 6, 7, 8, 9 }
        };
        Console.Write("Splaszcz: ");
        foreach (int n in Splaszcz(zagniezdzone))
            Console.Write($"{n} ");
        Console.WriteLine();

        // Batch processing
        Console.WriteLine("Batch (rozmiar=3):");
        foreach (var batch in Paginuj(Enumerable.Range(1, 10).ToList(), 3))
            Console.WriteLine($"  Batch: [{string.Join(", ", batch)}]");

        // Interleave — przeplatanie dwóch sekwencji
        var a = new[] { 1, 3, 5 };
        var b = new[] { 2, 4, 6 };
        Console.Write("Interleave: ");
        foreach (int n in Interleave(a, b))
            Console.Write($"{n} ");
        Console.WriteLine();

        // ExponentialBackoff — generator opóźnień retry
        Console.Write("ExponentialBackoff (max 5): ");
        foreach (var delay in ExponentialBackoff(5))
            Console.Write($"{delay.TotalMilliseconds:F0}ms ");
        Console.WriteLine();
    }

    static IEnumerable<List<T>> Paginuj<T>(IEnumerable<T> zrodlo, int rozmiar)
    {
        var paczka = new List<T>();
        foreach (T element in zrodlo)
        {
            paczka.Add(element);
            if (paczka.Count == rozmiar)
            {
                yield return paczka;
                paczka = new List<T>();
            }
        }
        if (paczka.Count > 0) yield return paczka;
    }

    static IEnumerable<T> Splaszcz<T>(IEnumerable<IEnumerable<T>> zagniezdzone)
    {
        foreach (var lista in zagniezdzone)
            foreach (T el in lista)
                yield return el;
    }

    static IEnumerable<T> Interleave<T>(IEnumerable<T> a, IEnumerable<T> b)
    {
        using var ea = a.GetEnumerator();
        using var eb = b.GetEnumerator();
        while (ea.MoveNext() && eb.MoveNext())
        {
            yield return ea.Current;
            yield return eb.Current;
        }
    }

    static IEnumerable<TimeSpan> ExponentialBackoff(int maxProb)
    {
        var delay = TimeSpan.FromMilliseconds(100);
        for (int i = 0; i < maxProb; i++)
        {
            yield return delay;
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
        }
    }

    public static void DrzewoDF()
    {
        Console.WriteLine("\n── DrzewoDF (DFS z yield) ──");

        // Budowanie drzewa
        var korzen = new WęzełIter<string>("Firma");
        var dział1 = korzen.DodajDziecko("IT");
        var dział2 = korzen.DodajDziecko("HR");
        var dział3 = korzen.DodajDziecko("Finanse");

        dział1.DodajDziecko("Backend");
        dział1.DodajDziecko("Frontend");
        dział1.DodajDziecko("DevOps");
        dział2.DodajDziecko("Rekrutacja");
        dział2.DodajDziecko("Szkolenia");
        dział3.DodajDziecko("Księgowość");

        Console.Write("DFS preorder: ");
        foreach (var wezel in DFS(korzen))
            Console.Write($"{wezel.Wartosc} ");
        Console.WriteLine();

        // Rekurencyjne yield — piękne, ale O(głębokość²) dla głębokich drzew
        Console.WriteLine("Wszystkie liście:");
        foreach (var lisc in DFS(korzen).Where(w => w.Dzieci.Count == 0))
            Console.WriteLine($"  {lisc.Wartosc}");
    }

    static IEnumerable<WęzełIter<T>> DFS<T>(WęzełIter<T> wezel)
    {
        yield return wezel; // preorder — najpierw węzeł
        foreach (var dziecko in wezel.Dzieci)
            foreach (var pod in DFS(dziecko)) // rekurencja z yield
                yield return pod;
    }

    public static void IAsyncEnumerableDemo()
    {
        Console.WriteLine("\n── IAsyncEnumerableDemo (C# 8+) ──");

        // IAsyncEnumerable<T> — asynchroniczny odpowiednik IEnumerable<T>
        // await foreach — asynchroniczne pobieranie elementów jeden po jednym
        // Idealne: streaming z bazy danych, API, pliku

        Task.Run(async () =>
        {
            Console.Write("await foreach (async streaming): ");
            await foreach (var item in GeneratorAsync(5))
                Console.Write($"{item} ");
            Console.WriteLine("(zakończono)");

            // Anulowanie z CancellationToken
            using var cts = new System.Threading.CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(250));

            Console.Write("Z CancellationToken: ");
            try
            {
                await foreach (var item in GeneratorZAnulowaniem(cts.Token))
                    Console.Write($"{item} ");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("(anulowano)");
            }

            // ConfigureAwait — sterowanie kontekstem synchronizacji
            Console.Write("ConfigureAwait(false): ");
            await foreach (var item in GeneratorAsync(3).ConfigureAwait(false))
                Console.Write($"{item} ");
            Console.WriteLine();

        }).GetAwaiter().GetResult();

        Console.WriteLine("IAsyncEnumerable: idealne dla streamingu — jeden element na raz, bez buforowania całości");
    }

    static async IAsyncEnumerable<int> GeneratorAsync(int ile)
    {
        for (int i = 1; i <= ile; i++)
        {
            await Task.Delay(10);
            yield return i;
        }
    }

    static async IAsyncEnumerable<int> GeneratorZAnulowaniem(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        System.Threading.CancellationToken ct = default)
    {
        for (int i = 1; ; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(100, ct);
            yield return i;
        }
    }

    public static void PulpakiIteratorow()
    {
        Console.WriteLine("\n── PulpakiIteratorow ──");

        // Pułapka 1: Wielokrotna enumeracja (re-enumeration)
        IEnumerable<int> gen = GenerujZLicznikiem();
        int sum = gen.Sum();  // pierwsze przejście — generator działa
        int max = gen.Max();  // drugie przejście — generator działa ponownie!
        Console.WriteLine($"Re-enumeration: Sum={sum}, Max={max} (generator użyty 2x)");
        Console.WriteLine("Naprawka: ToList() → List<int> gen = ....ToList()");

        // Pułapka 2: yield return w try/catch — nie można yield wewnątrz catch!
        // static IEnumerable<int> Niebezpieczny() {
        //     try { yield return 1; }
        //     catch { yield return -1; } // BŁĄD KOMPILACJI
        // }
        // Można: yield return w try (bez catch), yield w finally NIE
        Console.WriteLine("yield return: dozwolone w try, NIEDOZWOLONE w catch/finally");

        // Pułapka 3: Opóźniona walidacja argumentów
        // Metoda z yield NIE weryfikuje argumentów w momencie wywołania!
        var gen2 = GenerujZWalidacja(-1); // BRAK wyjątku tu!
        Console.Write("Walidacja dopiero przy pierwszym MoveNext(): ");
        try
        {
            foreach (var _ in gen2) ; // wyjątek tu
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine(ex.Message);
        }
        Console.WriteLine("Naprawka: wrapper — weryfikuj argument w metodzie niebędącej iteratorem");

        // Pułapka 4: Disposal — iterator musi poprawnie zwalniać zasoby
        Console.WriteLine("Pułapka disposal: 'using var e = ...' zapewnia Dispose() po break");
        using (var e = IteratorZZasobemDispose().GetEnumerator())
        {
            e.MoveNext(); Console.Write($"{e.Current} ");
            e.MoveNext(); Console.Write($"{e.Current} ");
            // e jest Disposed na końcu using — finally w iteratorze wykonane
        }
        Console.WriteLine("(finally wykonane)");
    }

    static IEnumerable<int> GenerujZLicznikiem()
    {
        Console.Write("[gen] ");
        for (int i = 1; i <= 5; i++)
            yield return i;
        Console.Write("[koniec gen] ");
    }

    static IEnumerable<int> GenerujZWalidacja(int n)
    {
        // Walidacja NIE wykona się przy wywołaniu GenerujZWalidacja(-1)!
        if (n < 0) throw new ArgumentException("n musi być >= 0");
        for (int i = 0; i < n; i++)
            yield return i;
    }

    static IEnumerable<int> IteratorZZasobemDispose()
    {
        Console.Write("[open] ");
        try
        {
            yield return 10;
            yield return 20;
            yield return 30;
        }
        finally
        {
            Console.Write("[close] ");
        }
    }
}
