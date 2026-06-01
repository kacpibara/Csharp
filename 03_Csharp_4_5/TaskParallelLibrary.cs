using System.Collections.Concurrent;
using System.Diagnostics;

namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public record WpisLoguTPL(
    DateTime Czas,
    string Poziom,
    string Serwis,
    string Komunikat,
    int? KodBledu = null);

public record StatystykaSerwisTPL(
    string Nazwa,
    int LacznaLiczba,
    int LiczbaErrorow,
    double SredniaRozmiaru);

public record RaportLogowTPL(
    int LacznaLiczbaBledow,
    List<StatystykaSerwisTPL> StatystykiSerwisow,
    Dictionary<int, int> TopKodyBledow,
    long CzasAnalizyMs);

public static class TaskParallelLibrary
{
    // ── 1. Parallel.For — równoległa pętla for ────────────────────────────────

    public static void ParallelForPodstawy()
    {
        Console.WriteLine("\n── ParallelForPodstawy ──");

        // Parallel.For — automatycznie dzieli zakres na wątki ThreadPool
        // Wyniki W LOSOWEJ KOLEJNOŚCI — wątki działają równolegle
        var watkiPoFor = new ConcurrentBag<int>();
        Parallel.For(0, 8, i => watkiPoFor.Add(Thread.CurrentThread.ManagedThreadId));
        Console.WriteLine($"Wątki użyte przez Parallel.For: [{string.Join(", ", watkiPoFor.Distinct())}]");

        // Wydajność — liczby pierwsze
        static bool IsPrime(int n)
        {
            if (n < 2) return false;
            for (int i = 2; i <= Math.Sqrt(n); i++)
                if (n % i == 0) return false;
            return true;
        }

        int[] dane = Enumerable.Range(2, 300_000).ToArray();
        var sw = Stopwatch.StartNew();

        int sekwCount = 0;
        for (int i = 0; i < dane.Length; i++)
            if (IsPrime(dane[i])) sekwCount++;
        long sekwMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int parCount = 0;
        Parallel.For(0, dane.Length, i =>
        {
            if (IsPrime(dane[i]))
                Interlocked.Increment(ref parCount);  // thread-safe!
        });
        long parMs = sw.ElapsedMilliseconds;

        Console.WriteLine($"Sekwencyjne:  {sekwMs,4}ms → {sekwCount} liczb pierwszych");
        Console.WriteLine($"Parallel.For: {parMs,4}ms → {parCount} (speedup: {(double)sekwMs/parMs:F1}×)");

        // ParallelOptions — kontrola zachowania
        var opcje = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken      = CancellationToken.None
        };

        Parallel.For(0, 5, opcje, i => Console.Write($"[{i}] "));
        Console.WriteLine();

        // ParallelLoopState — Break vs Stop
        Console.WriteLine("Break (przetwórz ≤ indeks progu, pomijaj nowe wyższe):");
        ParallelLoopResult wynik = Parallel.For(0, 20, (i, stan) =>
        {
            if (i == 10) stan.Break();  // nie zacznij nowych z i>10
            Console.Write($"{i} ");
        });
        Console.WriteLine($"\nIsCompleted={wynik.IsCompleted}, LowestBreak={wynik.LowestBreakIteration}");
        Console.WriteLine("Stop (zakończ jak najszybciej — porzuć niezaczęte):");
        Console.WriteLine("  ParallelLoopState.Stop() — gdy masz wynik i reszta zbędna");
    }

    // ── 2. Parallel.ForEach — równoległa iteracja kolekcji ───────────────────

    public static void ParallelForEach()
    {
        Console.WriteLine("\n── ParallelForEach ──");

        // Podstawowy ForEach — z kolekcją
        var dokumenty = Enumerable.Range(1, 12)
            .Select(i => (Id: i, Rozmiar: i * 100)).ToList();

        var przetworzone = new ConcurrentBag<int>();
        Parallel.ForEach(dokumenty,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            (doc, stan, indeks) =>
            {
                przetworzone.Add(doc.Id);
                if (doc.Rozmiar > 1000) stan.Break();  // zatrzymaj przy dużych
            });

        Console.WriteLine($"Przetworzone IDs: [{string.Join(", ", przetworzone.OrderBy(x => x))}]");

        // Thread-local state — akumulator per wątek, scal na końcu
        // Unika locków w każdej iteracji → znacznie szybsze!
        long sumaKwadratow = 0;

        Parallel.For(0, 1_000_000,
            () => 0L,                                    // localInit — własny akumulator
            (i, _, lokalnySum) => lokalnySum + (long)i * i,  // body — akumuluj lokalnie
            lokalny => Interlocked.Add(ref sumaKwadratow, lokalny)  // localFinally — scal
        );
        Console.WriteLine($"Thread-local sum (1M kwadratów): {sumaKwadratow:N0}");

        // To samo dla ForEach
        long calkowityRozmiar = 0;
        Parallel.ForEach(
            dokumenty,
            () => 0L,
            (doc, _, lokalny) => lokalny + doc.Rozmiar,
            lokalny => Interlocked.Add(ref calkowityRozmiar, lokalny));
        Console.WriteLine($"Thread-local ForEach: sumaRozmiarów={calkowityRozmiar}");

        // Partycjonowanie — kontrola podziału pracy
        int[] duzeDane = Enumerable.Range(0, 100_000).ToArray();
        var partycjoner = Partitioner.Create(0, duzeDane.Length, 10_000);  // paczki po 10k

        int sumaPartycji = 0;
        Parallel.ForEach(partycjoner, zakres =>
        {
            int lokalna = 0;
            for (int i = zakres.Item1; i < zakres.Item2; i++)
                lokalna += duzeDane[i] % 7 == 0 ? 1 : 0;
            Interlocked.Add(ref sumaPartycji, lokalna);
        });
        Console.WriteLine($"Partycjonowane (paczki 10k): podzielnych przez 7 = {sumaPartycji}");
    }

    // ── 3. Obsługa wyjątków w Parallel ───────────────────────────────────────

    public static void ObslugaWyjatkow()
    {
        Console.WriteLine("\n── ObslugaWyjatkow (TPL) ──");

        // Parallel zbiera WSZYSTKIE wyjątki → AggregateException
        try
        {
            Parallel.For(0, 10, i =>
            {
                if (i % 3 == 0)
                    throw new InvalidOperationException($"Błąd dla i={i}");
            });
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"AggregateException: {ae.InnerExceptions.Count} błędów");
            foreach (var ex in ae.InnerExceptions)
                Console.WriteLine($"  • {ex.Message}");
        }

        // Flatten — rozkłada zagnieżdżone AggregateException
        try
        {
            Parallel.ForEach(Enumerable.Range(1, 5), i =>
            {
                if (i == 2) throw new ArgumentException($"Zły argument: {i}");
                if (i == 4) throw new InvalidOperationException($"Błąd op: {i}");
            });
        }
        catch (AggregateException ae)
        {
            Console.WriteLine("\nFlatten:");
            foreach (var ex in ae.Flatten().InnerExceptions)
                Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
        }

        // Handle — obsłuż wybrane typy, rethrow reszty
        try
        {
            Parallel.For(0, 10, i =>
            {
                if (i == 3) throw new ArgumentNullException($"param_{i}");
                if (i == 7) throw new NotSupportedException($"op_{i}");
            });
        }
        catch (AggregateException ae)
        {
            try
            {
                ae.Handle(ex =>
                {
                    if (ex is ArgumentNullException ane)
                    {
                        Console.WriteLine($"\nObsłużono ArgumentNull: {ane.Message}");
                        return true;   // obsłużony
                    }
                    return false;      // nie obsłużony → rethrow
                });
            }
            catch (AggregateException nieObsluzony)
            {
                Console.WriteLine($"Nieobsłużone ({nieObsluzony.InnerExceptions.Count}): " +
                    string.Join(", ", nieObsluzony.InnerExceptions.Select(e => e.GetType().Name)));
            }
        }

        // Parallel z CancellationToken
        using var ctsPar = new CancellationTokenSource();
        ctsPar.CancelAfter(TimeSpan.FromMilliseconds(50));

        try
        {
            Parallel.For(0, 1000,
                new ParallelOptions { CancellationToken = ctsPar.Token, MaxDegreeOfParallelism = 4 },
                i =>
                {
                    ctsPar.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(5);
                });
        }
        catch (OperationCanceledException) { Console.WriteLine("\nParallel.For anulowany przez CT"); }
    }

    // ── 4. Parallel.ForEachAsync — I/O z limitem równoległości ───────────────

    public static async Task ForEachAsync()
    {
        Console.WriteLine("\n── ForEachAsync (.NET 6+) ──");

        // Parallel.ForEachAsync — async operacje z kontrolą równoległości
        // Lepsza alternatywa niż Task.WhenAll + SemaphoreSlim

        var endpointy = Enumerable.Range(1, 12).Select(i => $"/api/resource/{i}").ToList();
        var wyniki    = new ConcurrentBag<string>();

        var sw = Stopwatch.StartNew();
        await Parallel.ForEachAsync(
            endpointy,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = CancellationToken.None },
            async (endpoint, ct) =>
            {
                await Task.Delay(50, ct).ConfigureAwait(false);  // symulacja HTTP
                wyniki.Add($"OK:{endpoint}");
            });
        sw.Stop();

        Console.WriteLine($"ForEachAsync (12 zasobów, max 4, 50ms każdy): {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Pobrano: {wyniki.Count} zasobów");

        // Porównanie z Task.WhenAll + SemaphoreSlim
        Console.WriteLine("\nForEachAsync vs Task.WhenAll+SemaphoreSlim:");
        Console.WriteLine("  ForEachAsync:          czystszy kod, wbudowane zarządzanie");
        Console.WriteLine("  WhenAll+SemaphoreSlim: kompatybilny z .NET < 6, więcej kontroli");

        // Przykład z anulowaniem
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        int przetworzone = 0;

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(1, 20),
                new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = cts.Token },
                async (i, ct) =>
                {
                    await Task.Delay(30, ct).ConfigureAwait(false);
                    Interlocked.Increment(ref przetworzone);
                });
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"ForEachAsync anulowany po przetworzeniu ~{przetworzone} elementów");
        }
    }

    // ── 5. PLINQ — Parallel LINQ ─────────────────────────────────────────────

    public static void PLINQDemo()
    {
        Console.WriteLine("\n── PLINQDemo ──");

        int[] liczby = Enumerable.Range(1, 1_000_000).ToArray();
        static bool IsPrime(int n) { if (n < 2) return false; for (int i = 2; i <= Math.Sqrt(n); i++) if (n % i == 0) return false; return true; }

        // Sekwencyjny LINQ
        var sw = Stopwatch.StartNew();
        int seq = liczby.Where(IsPrime).Count();
        long seqMs = sw.ElapsedMilliseconds;

        // Równoległy PLINQ — ta sama składnia + .AsParallel()
        sw.Restart();
        int par = liczby.AsParallel().Where(IsPrime).Count();
        long parMs = sw.ElapsedMilliseconds;

        Console.WriteLine($"LINQ:  {seqMs,4}ms → {seq} liczb pierwszych");
        Console.WriteLine($"PLINQ: {parMs,4}ms → {par} (speedup: {(double)seqMs/parMs:F1}×)");

        // PLINQ z pełnymi opcjami
        var zaawansowany = liczby
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .WithCancellation(CancellationToken.None)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
            .Where(IsPrime)
            .Select(p => (long)p)
            .Take(10)
            .ToList();
        Console.WriteLine($"\nPLINQ z opcjami (pierwsze 10 pierwszych): [{string.Join(", ", zaawansowany)}]");

        // AsOrdered — zachowaj kolejność (kosztem wydajności)
        var posortowane = Enumerable.Range(1, 100)
            .AsParallel()
            .AsOrdered()         // wyniki w oryginalnej kolejności
            .Where(IsPrime)
            .Take(5)
            .ToList();
        Console.WriteLine($"AsOrdered (pierwsze 5): [{string.Join(", ", posortowane)}]");

        // AsSequential — powrót do sekwencyjnego po części równoległej
        var mieszane = Enumerable.Range(1, 1000)
            .AsParallel()
            .Where(IsPrime)      // równolegle
            .AsSequential()      // od tu sekwencyjnie
            .Take(5)
            .ToList();
        Console.WriteLine($"AsSequential po części parallel: [{string.Join(", ", mieszane)}]");

        // ForAll — konsumuj wyniki bez buforowania (najszybsze, bez zachowania kolejności)
        var wynikiBag = new ConcurrentBag<int>();
        Enumerable.Range(1, 1000)
            .AsParallel()
            .Where(IsPrime)
            .ForAll(p => wynikiBag.Add(p));
        Console.WriteLine($"ForAll: {wynikiBag.Count} liczb (nieuporządkowane)");

        // PLINQ z AggregateException
        try
        {
            Enumerable.Range(1, 100)
                .AsParallel()
                .Select(i =>
                {
                    if (i == 42) throw new Exception($"PLINQ błąd dla {i}");
                    return i * i;
                })
                .ToList();
        }
        catch (AggregateException ae)
        {
            Console.WriteLine($"PLINQ AggregateException: {ae.Flatten().InnerExceptions.Count} błąd(y)");
        }

        // Kiedy PLINQ pomaga / nie pomaga
        Console.WriteLine("\nKiedy PLINQ pomaga:");
        Console.WriteLine("  ✅ Duże kolekcje (>10k elementów)");
        Console.WriteLine("  ✅ Każdy element wymaga ciężkich obliczeń CPU");
        Console.WriteLine("  ✅ Operacje niezależne (bez shared state)");
        Console.WriteLine("  ❌ Małe kolekcje — narzut partycjonowania dominuje");
        Console.WriteLine("  ❌ Szybkie operacje (np. .Select(x => x * 2))");
        Console.WriteLine("  ❌ I/O operacje — użyj async/await");
    }

    // ── 6. ConcurrentCollections — thread-safe kolekcje ──────────────────────

    public static void ConcurrentCollections()
    {
        Console.WriteLine("\n── ConcurrentCollections ──");

        // ConcurrentBag<T> — nieuporządkowana, najszybsza dla dodawania
        // Każdy wątek ma własną listę lokalną — brak contention przy Add
        var bag = new ConcurrentBag<int>();
        Parallel.For(0, 100_000, i => bag.Add(i * i));
        Console.WriteLine($"ConcurrentBag: {bag.Count} elementów");

        // ConcurrentQueue<T> — FIFO, thread-safe
        var kolejka = new ConcurrentQueue<string>();
        Parallel.For(0, 100, i => kolejka.Enqueue($"Element_{i}"));
        if (kolejka.TryDequeue(out string? el))
            Console.WriteLine($"ConcurrentQueue.TryDequeue: {el}");

        // ConcurrentDictionary<K,V> — atomowe operacje
        var dict = new ConcurrentDictionary<string, int>();
        Parallel.ForEach(Enumerable.Range(1, 10_000), i =>
        {
            string klucz = $"Kat_{i % 5}";
            dict.AddOrUpdate(klucz,
                addValue: 1,
                updateValueFactory: (k, v) => v + 1);
        });
        Console.WriteLine("ConcurrentDictionary AddOrUpdate:");
        foreach (var (k, v) in dict.OrderBy(kv => kv.Key))
            Console.WriteLine($"  {k}: {v}");

        // GetOrAdd — pobierz lub dodaj atomowo (fabryka może być wywołana wielokrotnie!)
        int obliczen = 0;
        dict.GetOrAdd("NowyKlucz", klucz => { obliczen++; return klucz.Length; });
        dict.GetOrAdd("NowyKlucz", klucz => { obliczen++; return klucz.Length; });  // z cache
        Console.WriteLine($"GetOrAdd: {obliczen}× obliczona fabryka (ale 1 wartość wstawiona)");

        // Interlocked — atomowe operacje na liczbach (NAJSZYBSZE, bez locka)
        long licznik = 0;
        Parallel.For(0, 1_000_000, _ =>
        {
            Interlocked.Increment(ref licznik);  // ++
        });
        Console.WriteLine($"Interlocked.Increment (1M iteracji): {licznik}");

        // Interlocked operacje:
        Console.WriteLine("Inne operacje Interlocked:");
        long val = 10;
        Interlocked.Decrement(ref val);                         // --
        Interlocked.Add(ref val, 5);                            // += 5
        long stara = Interlocked.Exchange(ref val, 0);         // = 0, zwraca starą
        Interlocked.CompareExchange(ref val, 100, 0);          // if (val==0) val=100
        Console.WriteLine($"  Po operacjach: {val} (Exchange zwróciła: {stara})");

        // ConcurrentBag vs ConcurrentQueue vs lock — kiedy co
        Console.WriteLine("\nKiedy co:");
        Console.WriteLine("  ConcurrentBag:    wiele wątków DODAJE — lokalna lista per wątek");
        Console.WriteLine("  ConcurrentQueue:  FIFO, wiele wątków dodaje i pobiera");
        Console.WriteLine("  ConcurrentDict:   słownik z atomowym AddOrUpdate/GetOrAdd");
        Console.WriteLine("  Interlocked:      proste liczniki — atomowe bez locka");
    }

    // ── 7. Pułapki TPL ───────────────────────────────────────────────────────

    public static void PulapkiTPL()
    {
        Console.WriteLine("\n── PulapkiTPL ──");

        var sw = Stopwatch.StartNew();

        // PUŁAPKA 1 — zbyt mała praca — narzut TPL > zysk
        long suma1 = 0;
        for (int i = 0; i < 1000; i++) suma1 += i;
        long msSeq = sw.ElapsedMilliseconds;

        sw.Restart();
        long suma2 = 0;
        Parallel.For(0, 1000, i => Interlocked.Add(ref suma2, i));
        long msPar = sw.ElapsedMilliseconds;

        Console.WriteLine($"Pułapka 1 — mała praca (1k elem):");
        Console.WriteLine($"  Sekwencyjne: {msSeq}ms = {suma1}");
        Console.WriteLine($"  Parallel:    {msPar}ms = {suma2}  ← WOLNIEJSZE przez narzut!");

        // PUŁAPKA 2 — shared state bez synchronizacji — wyścig danych!
        int nieSynchronizowany = 0;
        Parallel.For(0, 100_000, _ => nieSynchronizowany++);  // DATA RACE!
        // Wynik losowy — mniejszy niż 100000

        int synchronizowany = 0;
        Parallel.For(0, 100_000, _ => Interlocked.Increment(ref synchronizowany));

        Console.WriteLine($"\nPułapka 2 — data race:");
        Console.WriteLine($"  Bez sync (data race): {nieSynchronizowany} ≠ 100000 (losowy wynik)");
        Console.WriteLine($"  Z Interlocked:        {synchronizowany} = 100000 (poprawny)");

        // PUŁAPKA 3 — lock w hot path — eliminuje równoległość
        object blokada = new();
        long sumaZLock = 0;
        sw.Restart();
        Parallel.For(0, 1_000_000, i =>
        {
            lock (blokada) sumaZLock += i;  // wąskie gardło!
        });
        long msLock = sw.ElapsedMilliseconds;

        long sumaBezLock = 0;
        sw.Restart();
        Parallel.For(0L, 1_000_000L,
            () => 0L,
            (i, _, lok) => lok + i,
            lok => Interlocked.Add(ref sumaBezLock, lok));
        long msBezLock = sw.ElapsedMilliseconds;

        Console.WriteLine($"\nPułapka 3 — lock w hot path:");
        Console.WriteLine($"  Z lock (serialized):     {msLock,4}ms");
        Console.WriteLine($"  Thread-local (poprawne): {msBezLock,4}ms ← {(double)msLock / Math.Max(1, msBezLock):F0}× szybciej");

        // PUŁAPKA 4 — I/O w Parallel.For — blokuje wątki ThreadPool
        Console.WriteLine($"\nPułapka 4 — I/O w Parallel.For:");
        Console.WriteLine("  Parallel.For(0, 100, i => Thread.Sleep(100))");
        Console.WriteLine("  → 100 wątków zablokowanych = ThreadPool Starvation!");
        Console.WriteLine("  Poprawnie: Parallel.ForEachAsync z async lambda");

        // PUŁAPKA 5 — ForAll PLINQ bez ToList — nie gwarantuje ukończenia
        Console.WriteLine("\nPułapka 5 — ForAll jest fire-and-forget jeśli nie materializujesz:");
        Console.WriteLine("  query.ForAll(p => bag.Add(p))  // może nie skończyć przed ToList()");
        Console.WriteLine("  Bezpiecznie: query.ToList() lub query.ToArray() zamiast ForAll");
    }

    // ── 8. Analiza logów — praktyczny przykład ────────────────────────────────

    public static async Task AnalizatorLogowDemo()
    {
        Console.WriteLine("\n── AnalizatorLogowDemo ──");

        // Generowanie danych testowych
        static List<WpisLoguTPL> GenerujLogi(int ilosc)
        {
            var rng     = new Random(42);
            string[] poziomy  = { "INFO", "WARN", "ERROR", "DEBUG" };
            string[] serwisy  = { "API", "DB", "Cache", "Auth", "Payment" };

            return Enumerable.Range(0, ilosc)
                .Select(i => new WpisLoguTPL(
                    Czas:      DateTime.Now.AddMinutes(-rng.Next(0, 10080)),
                    Poziom:    poziomy[rng.Next(poziomy.Length)],
                    Serwis:    serwisy[rng.Next(serwisy.Length)],
                    Komunikat: $"Komunikat_{i} " + new string('x', rng.Next(50, 200)),
                    KodBledu:  rng.Next(10) < 2 ? rng.Next(400, 600) : (int?)null))
                .ToList();
        }

        var logi = GenerujLogi(100_000);
        Console.WriteLine($"Wygenerowano {logi.Count:N0} wpisów logów");

        // KROK 1: PLINQ — CPU-bound analiza
        var sw = Stopwatch.StartNew();
        var bledy = logi.AsParallel().Where(l => l.Poziom == "ERROR").ToList();

        var statystykiSerwisow = logi
            .AsParallel()
            .GroupBy(l => l.Serwis)
            .Select(g => new StatystykaSerwisTPL(
                g.Key,
                g.Count(),
                g.Count(l => l.Poziom == "ERROR"),
                g.Average(l => l.Komunikat.Length)))
            .OrderByDescending(s => s.LiczbaErrorow)
            .ToList();

        var topKodyBledow = logi
            .AsParallel()
            .Where(l => l.KodBledu.HasValue)
            .GroupBy(l => l.KodBledu!.Value)
            .Select(g => (Kod: g.Key, Ilosc: g.Count()))
            .OrderByDescending(x => x.Ilosc)
            .Take(5)
            .ToDictionary(x => x.Kod, x => x.Ilosc);

        var raport = new RaportLogowTPL(
            bledy.Count,
            statystykiSerwisow,
            topKodyBledow,
            sw.ElapsedMilliseconds);

        Console.WriteLine($"PLINQ analiza: {raport.CzasAnalizyMs}ms");
        Console.WriteLine($"  Błędów łącznie: {raport.LacznaLiczbaBledow:N0}");
        Console.WriteLine("  Statystyki serwisów:");
        foreach (var s in raport.StatystykiSerwisow)
            Console.WriteLine($"    {s.Nazwa,-10} łącznie={s.LacznaLiczba,6:N0} błędy={s.LiczbaErrorow,5:N0}");

        Console.WriteLine("  Top kody błędów:");
        foreach (var (kod, ilosc) in raport.TopKodyBledow.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    HTTP {kod}: {ilosc:N0}×");

        // KROK 2: Parallel.ForEach — liczniki błędów per serwis
        var liczniki = new ConcurrentDictionary<string, int>();
        Parallel.ForEach(
            logi.Where(l => l.Poziom == "ERROR"),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            wpis => liczniki.AddOrUpdate(wpis.Serwis, 1, (_, v) => v + 1));

        Console.WriteLine("\nParallel.ForEach błędy per serwis:");
        foreach (var (serwis, ile) in liczniki.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"  {serwis}: {ile:N0}");

        // KROK 3: I/O-bound — zapis wyników (async)
        sw.Restart();
        string plikWyjsciowy = Path.Combine(Path.GetTempPath(), "raport_logi.csv");
        var podsumowanie = logi
            .AsParallel()
            .GroupBy(l => new { l.Serwis, l.Poziom })
            .Select(g => $"{g.Key.Serwis},{g.Key.Poziom},{g.Count()}")
            .OrderBy(s => s)
            .ToList();

        await File.WriteAllLinesAsync(
            plikWyjsciowy,
            new[] { "Serwis,Poziom,Ilosc" }.Concat(podsumowanie));

        Console.WriteLine($"\nZapis CSV: {sw.ElapsedMilliseconds}ms → {podsumowanie.Count} wierszy");
        Console.WriteLine($"Plik: {plikWyjsciowy}");

        // Czyszczenie
        if (File.Exists(plikWyjsciowy)) File.Delete(plikWyjsciowy);
    }
}
