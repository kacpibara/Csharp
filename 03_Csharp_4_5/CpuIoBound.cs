using System.Collections.Concurrent;
using System.Diagnostics;

namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public record DanySuroweCIB(int Id, double Wartosc);
public record DanePrzetworzoneCIB(int Id, double Wartosc);
public record RaportWynikowCIB(int LiczbaPrzetworzonych, double Suma);

public static class CpuIoBound
{
    // ── 1. Fundamentalna różnica CPU-bound vs I/O-bound ───────────────────────

    public static void FundamentalnaRoznica()
    {
        Console.WriteLine("\n── FundamentalnaRoznica ──");

        // I/O-BOUND — wątek czeka na zewnętrzny zasób, CPU bezczynny
        // Przykłady: sieć, baza danych, dysk, Redis, zewnętrzne API
        // Rozwiązanie: async/await — zwolnij wątek podczas oczekiwania
        Console.WriteLine("I/O-bound: sieć, DB, dysk → async/await");
        Console.WriteLine("  Jeden wątek obsługuje tysiące jednoczesnych operacji I/O");

        // CPU-BOUND — CPU aktywnie pracuje przez cały czas
        // Przykłady: kryptografia, kompresja, sortowanie, renderowanie
        // Rozwiązanie: Task.Run — przenieś na osobny wątek z puli
        Console.WriteLine("CPU-bound: obliczenia, krypto, kompresja → Task.Run / Parallel");
        Console.WriteLine("  Task.Run zwalnia wątek wywołujący (UI/request thread)");

        // MIESZANY — I/O potem CPU
        static byte[] Skompresuj(byte[] dane)
        {
            using var ms = new System.IO.MemoryStream();
            using var gz = new System.IO.Compression.GZipStream(
                ms, System.IO.Compression.CompressionMode.Compress);
            gz.Write(dane, 0, dane.Length);
            gz.Close();
            return ms.ToArray();
        }

        byte[] testDane = System.Text.Encoding.UTF8.GetBytes(new string('X', 1000));
        byte[] skompresowane = Skompresuj(testDane);
        Console.WriteLine($"\nMieszany przykład: oryginał={testDane.Length}B → " +
                          $"skompresowany={skompresowane.Length}B");

        // TABELA DECYZYJNA:
        Console.WriteLine("\nTABELA DECYZYJNA:");
        Console.WriteLine("  I/O-bound              → async/await");
        Console.WriteLine("  CPU-bound (UI thread)  → Task.Run + await");
        Console.WriteLine("  CPU-bound (serwer/batch)→ Parallel/PLINQ");
        Console.WriteLine("  Wiele I/O naraz        → Task.WhenAll");
        Console.WriteLine("  CPU + I/O              → async (I/O) + Task.Run (CPU)");
    }

    // ── 2. Jak wątki działają pod maską ──────────────────────────────────────

    public static async Task WatkiPodMaska()
    {
        Console.WriteLine("\n── WatkiPodMaska ──");

        // ThreadPool — zarządzana pula wątków .NET
        ThreadPool.GetAvailableThreads(out int dostepne, out int ioWatki);
        ThreadPool.GetMaxThreads(out int maxWatki, out int maxIO);
        Console.WriteLine($"ThreadPool: {dostepne}/{maxWatki} roboczych, {ioWatki}/{maxIO} I/O");

        // I/O-bound z async — wątek ZWOLNIONY podczas oczekiwania
        Console.WriteLine("\nI/O-bound async — zmiana wątku:");
        Console.WriteLine($"  Przed await: wątek {Thread.CurrentThread.ManagedThreadId}");
        await Task.Delay(10);  // wątek zwolniony do puli
        Console.WriteLine($"  Po await:    wątek {Thread.CurrentThread.ManagedThreadId} (może być inny)");

        // CPU-bound — wątek ZAJĘTY przez cały czas
        Console.WriteLine("\nCPU-bound Task.Run:");
        int wynik = await Task.Run(() =>
        {
            Console.WriteLine($"  Obliczenia na wątku: {Thread.CurrentThread.ManagedThreadId}");
            long suma = 0;
            for (int i = 0; i < 1_000_000; i++) suma += i;
            return (int)(suma % 1000);
        });
        Console.WriteLine($"  Wynik: {wynik}, powrót na wątek: {Thread.CurrentThread.ManagedThreadId}");

        // Skalowanie — demo
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(1, 50).Select(_ => Task.Delay(100)));
        sw.Stop();
        Console.WriteLine($"\n50 równoczesnych I/O operacji (100ms każda): {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("  ← wszystkie naraz, prawie bez wątków!");
    }

    // ── 3. async/await dla I/O — skalowalność ────────────────────────────────

    public static async Task AsyncAwaitDlaIO()
    {
        Console.WriteLine("\n── AsyncAwaitDlaIO ──");

        // Bez async — n requestów = n zablokowanych wątków
        // Z async   — n requestów = ~kilka aktywnych wątków
        Console.WriteLine("Skalowanie:");
        Console.WriteLine("  Synchroniczne: 1000 requestów = 1000 zablokowanych wątków");
        Console.WriteLine("  async/await:   1000 requestów = kilka aktywnych wątków");

        // Demonstracja skalowalności
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(1, 100).Select(_ => Task.Delay(100)));
        sw.Stop();
        Console.WriteLine($"\n100 operacji I/O (100ms) jednocześnie: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("  Sekwencyjnie zajęłoby ~10s, tu ~100ms");

        // Typowe I/O operacje — zawsze async
        Console.WriteLine("\nTypowe async I/O:");
        Console.WriteLine("  EF Core: await context.Users.ToListAsync(ct)");
        Console.WriteLine("  Plik:    await File.ReadAllTextAsync(path, ct)");
        Console.WriteLine("  HTTP:    await httpClient.GetStringAsync(url, ct)");
        Console.WriteLine("  Redis:   await cache.GetAsync(key, ct)");

        // Symulacja bazy danych
        static async Task<List<string>> PobierzUzytkownikowAsync(CancellationToken ct = default)
        {
            await Task.Delay(50, ct);  // symulacja EF Core query
            return new List<string> { "anna@test.pl", "bartek@test.pl", "celina@test.pl" };
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var users = await PobierzUzytkownikowAsync(cts.Token);
        Console.WriteLine($"\nBaza (symulacja): {string.Join(", ", users)}");
    }

    // ── 4. Task.Run dla CPU-bound ─────────────────────────────────────────────

    public static async Task TaskRunDlaCPU()
    {
        Console.WriteLine("\n── TaskRunDlaCPU ──");

        // KIEDY Task.Run:
        // ✅ Długie obliczenia (>50ms) na wątku UI — żeby UI nie zamarzał
        // ✅ Długie obliczenia w ASP.NET Core — żeby nie blokować request thread
        // ❌ Krótkie operacje (<1ms) — narzut Task.Run > zysk
        // ❌ I/O operacje — użyj await bezpośrednio

        // Przykład 1 — przetwarzanie obrazu (CPU intensive)
        static async Task<byte[]> PrzetworzObrazAsync(byte[] dane)
        {
            return await Task.Run(() =>
            {
                // CPU-bound: filtr negatywu — zajmuje wątek aktywnie
                var wynik = new byte[dane.Length];
                for (int i = 0; i < dane.Length; i++)
                    wynik[i] = (byte)(255 - dane[i]);
                return wynik;
            });
        }

        byte[] obraz = Enumerable.Range(0, 1000).Select(i => (byte)(i % 256)).ToArray();
        byte[] negatyw = await PrzetworzObrazAsync(obraz);
        Console.WriteLine($"Przetwarzanie obrazu (Task.Run): {obraz.Length}B → wynik[0]={negatyw[0]}, wynik[1]={negatyw[1]}");

        // Przykład 2 — haszowanie hasła (celowo wolne CPU)
        static async Task<string> HashujHasloAsync(string haslo)
        {
            return await Task.Run(() =>
            {
                // SHA256 — CPU-bound
                byte[] hash = System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(haslo));
                return Convert.ToHexString(hash)[..16] + "...";
            });
        }

        string hash = await HashujHasloAsync("MojeHaslo123!");
        Console.WriteLine($"Hash hasła: {hash}");

        // Przykład 3 — Task.Run z CancellationToken
        static async Task<int> SortujAsync(int[] dane, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                // Sprawdzaj cancellation co N iteracji
                for (int i = 0; i < dane.Length; i++)
                {
                    if (i % 10000 == 0) ct.ThrowIfCancellationRequested();
                    // uproszczone "sortowanie"
                    dane[i] = dane.Length - i;
                }
                return dane.Sum();
            }, ct);
        }

        using var ctsCpu = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int sumaDanych = await SortujAsync(new int[50_000], ctsCpu.Token);
        Console.WriteLine($"CPU Task.Run z CT: wynik={sumaDanych}");

        // Ostrzeżenie: Task.Run w ASP.NET Core dla I/O to ANTYPATTERN
        Console.WriteLine("\nANTYPATTERN w ASP.NET Core:");
        Console.WriteLine("  await Task.Run(async () => await IoOperacjaAsync())");
        Console.WriteLine("  → przenosi wątek z puli na wątek puli — zero korzyści dla I/O");
        Console.WriteLine("  Prawidłowo: await IoOperacjaAsync() bezpośrednio");
    }

    // ── 5. Parallel dla CPU-bound ─────────────────────────────────────────────

    public static void ParallelDlaCPU()
    {
        Console.WriteLine("\n── ParallelDlaCPU ──");

        static bool CzyPierwsza(int n)
        {
            if (n < 2) return false;
            for (int i = 2; i <= Math.Sqrt(n); i++)
                if (n % i == 0) return false;
            return true;
        }

        int[] duzeDane = Enumerable.Range(2, 500_000).ToArray();

        // Sekwencyjny
        var sw = Stopwatch.StartNew();
        int sekwIlosc = duzeDane.Count(CzyPierwsza);
        long sekwMs = sw.ElapsedMilliseconds;

        // Parallel.For z Interlocked
        sw.Restart();
        int parIlosc = 0;
        Parallel.For(0, duzeDane.Length, i =>
        {
            if (CzyPierwsza(duzeDane[i]))
                Interlocked.Increment(ref parIlosc);
        });
        long parMs = sw.ElapsedMilliseconds;

        // PLINQ — czystszy zapis
        sw.Restart();
        int plinqIlosc = duzeDane.AsParallel().Count(CzyPierwsza);
        long plinqMs = sw.ElapsedMilliseconds;

        Console.WriteLine($"Liczby pierwsze (500k):");
        Console.WriteLine($"  Sekwencyjny LINQ: {sekwMs,4}ms → {sekwIlosc}");
        Console.WriteLine($"  Parallel.For:     {parMs,4}ms → {parIlosc}");
        Console.WriteLine($"  PLINQ:            {plinqMs,4}ms → {plinqIlosc}");

        // Parallel.ForEach — kolekcje
        var produkty = Enumerable.Range(1, 100)
            .Select(i => (Id: i, Cena: i * 10.5m)).ToList();

        var wyniki = new ConcurrentBag<(int Id, decimal Cena)>();
        Parallel.ForEach(produkty,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            produkt => { if (produkt.Cena > 500) wyniki.Add(produkt); });

        Console.WriteLine($"\nParallel.ForEach — produkty >500: {wyniki.Count}");

        // Parallel.ForEachAsync — I/O z limitem (.NET 6+)
        Console.WriteLine("Parallel.ForEachAsync (I/O + limit równoległości): dostępne .NET 6+");
        Console.WriteLine("  await Parallel.ForEachAsync(urls,");
        Console.WriteLine("      new ParallelOptions { MaxDegreeOfParallelism = 10 },");
        Console.WriteLine("      async (url, ct) => await httpClient.GetAsync(url, ct))");
    }

    // ── 6. Benchmark i decyzja ────────────────────────────────────────────────

    public static async Task BenchmarkIDecyzja()
    {
        Console.WriteLine("\n── BenchmarkIDecyzja ──");

        static (long Ms, T Wynik) Mierz<T>(string nazwa, Func<T> fn)
        {
            var sw = Stopwatch.StartNew();
            T wynik = fn();
            sw.Stop();
            Console.WriteLine($"  {nazwa,-30}: {sw.ElapsedMilliseconds,4}ms");
            return (sw.ElapsedMilliseconds, wynik);
        }

        static async Task<(long Ms, T Wynik)> MierzAsync<T>(string nazwa, Func<Task<T>> fn)
        {
            var sw = Stopwatch.StartNew();
            T wynik = await fn();
            sw.Stop();
            Console.WriteLine($"  {nazwa,-30}: {sw.ElapsedMilliseconds,4}ms");
            return (sw.ElapsedMilliseconds, wynik);
        }

        // CPU-bound benchmark
        int[] dane = Enumerable.Range(1, 200_000).ToArray();
        Console.WriteLine("CPU-bound (liczby pierwsze z 200k):");
        static bool IsPrime(int n) { if (n < 2) return false; for (int i = 2; i <= Math.Sqrt(n); i++) if (n % i == 0) return false; return true; }
        Mierz("Sekwencyjny LINQ",   () => dane.Count(IsPrime));
        Mierz("PLINQ AsParallel",   () => dane.AsParallel().Count(IsPrime));

        // I/O-bound benchmark
        Console.WriteLine("I/O-bound (symulacja 20 requestów × 50ms):");
        await MierzAsync("Sekwencyjny async", async () =>
        {
            int c = 0;
            foreach (int _ in Enumerable.Range(1, 20))
            {
                await Task.Delay(50);
                c++;
            }
            return c;
        });
        await MierzAsync("WhenAll async (równolegle)", async () =>
        {
            await Task.WhenAll(Enumerable.Range(1, 20).Select(_ => Task.Delay(50)));
            return 20;
        });

        // Kompletny przykład — pipeline I/O + CPU
        Console.WriteLine("\nKompletny pipeline I/O + CPU:");
        var sw2 = Stopwatch.StartNew();

        // Krok 1: I/O — pobierz dane równolegle
        int[] ids = Enumerable.Range(1, 10).ToArray();
        var pobrane = await Task.WhenAll(ids.Select(async id =>
        {
            await Task.Delay(30);  // symulacja HTTP
            return new DanySuroweCIB(id, new Random(id).Next(1, 1000));
        }));

        // Krok 2: CPU — przetwórz równolegle na wielu rdzeniach
        var przetworzoneBag = new ConcurrentBag<DanePrzetworzoneCIB>();
        Parallel.ForEach(pobrane,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            item =>
            {
                double w = Math.Sqrt(item.Wartosc) * Math.Log(item.Wartosc + 1);
                przetworzoneBag.Add(new DanePrzetworzoneCIB(item.Id, w));
            });

        var przetworzone = przetworzoneBag.OrderBy(x => x.Id).ToList();
        sw2.Stop();

        Console.WriteLine($"  I/O (WhenAll) + CPU (Parallel): {sw2.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Wyniki: {przetworzone.Count} elementów, suma={przetworzone.Sum(x => x.Wartosc):F1}");

        Console.WriteLine("\nThREADPOOL STARVATION — co to i jak zapobiegać:");
        Console.WriteLine("  Przyczyna: .Result/.Wait() na async metodach blokuje wątki puli");
        Console.WriteLine("  Objaw: aplikacja przestaje odpowiadać pod obciążeniem");
        Console.WriteLine("  Nowe wątki dodawane wolno (1 co 500ms)");
        Console.WriteLine("  Rozwiązanie: await wszędzie, nigdy .Result w async kontekście");
    }
}
