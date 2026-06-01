using System.Threading.Channels;

namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public record PostepOperacjiZA(int Obecny, int Calkowity, string Komunikat)
{
    public double Procent => Calkowity > 0
        ? Math.Round((double)Obecny / Calkowity * 100, 1)
        : 0;
}

public record ElementZA(int Id, string Dane);
public record WynikZA(int Id, string Wynik, TimeSpan Czas);

// Własna IProgress — synchroniczna (bez marshallingu na UI thread)
public class SynchronicznyPostepZA<T> : IProgress<T>
{
    private readonly Action<T> _handler;
    public SynchronicznyPostepZA(Action<T> handler) => _handler = handler;
    public void Report(T value) => _handler(value);
}

// Kolektor postępów (do testów)
public class PostepKolektorZA<T> : IProgress<T>
{
    private readonly List<T> _raporty = new();
    public IReadOnlyList<T> Raporty => _raporty.AsReadOnly();
    public void Report(T value) => _raporty.Add(value);
}

// Stara klasa bez CancellationToken (demo WithCancellationAsync)
public static class StaryKlientZA
{
    public static Task<string> PobierzAsync() =>
        Task.Delay(200).ContinueWith(_ => "wynik stary klient");
}

// Kontekst żądania przez AsyncLocal
public static class KontekstZadaniaZA
{
    private static readonly AsyncLocal<string?> _traceId    = new();
    private static readonly AsyncLocal<string?> _uzytkownik = new();

    public static string? TraceId
    {
        get => _traceId.Value;
        set => _traceId.Value = value;
    }
    public static string? Uzytkownik
    {
        get => _uzytkownik.Value;
        set => _uzytkownik.Value = value;
    }
}

public static class ZaawansowaneAsync
{
    // ── 1. CancellationToken — głęboko ───────────────────────────────────────

    public static async Task CancellationTokenGłęboko()
    {
        Console.WriteLine("\n── CancellationTokenGłęboko ──");

        // Trzy sposoby anulowania
        using var cts1 = new CancellationTokenSource();
        cts1.Cancel();                                      // natychmiastowe
        Console.WriteLine($"cts1.IsCancellationRequested: {cts1.IsCancellationRequested}");

        using var cts2 = new CancellationTokenSource();
        cts2.CancelAfter(TimeSpan.FromSeconds(5));          // po czasie

        using var cts3 = new CancellationTokenSource();
        cts3.CancelAfter(5000);                             // ms — to samo

        // ThrowIfCancellationRequested vs IsCancellationRequested
        async Task DlugaOperacjaAsync(CancellationToken ct)
        {
            for (int i = 0; i < 10; i++)
            {
                // Sposób 1 — rzuca OperationCanceledException
                ct.ThrowIfCancellationRequested();

                // Sposób 2 — bool, nie rzuca, możesz sprzątać
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine("  IsCancellationRequested — sprzątam zasoby");
                    return;
                }

                await Task.Delay(30, ct);
            }
        }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(75);  // anuluj po ~75ms (2-3 iteracje)
        try { await DlugaOperacjaAsync(cts.Token); }
        catch (OperationCanceledException) { Console.WriteLine("  Operacja anulowana (ThrowIfCancellationRequested)"); }

        // Rejestracja callback przy anulowaniu
        using var ctsCallback = new CancellationTokenSource();
        using CancellationTokenRegistration rejestracja =
            ctsCallback.Token.Register(() => Console.WriteLine("  Callback: token anulowany!"));

        ctsCallback.Cancel();  // wywołuje callback
        Console.WriteLine("  Po Cancel()");

        // Linked tokens
        using var zewnetrzny = new CancellationTokenSource();
        using var lokalny    = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        using var linked     = CancellationTokenSource
            .CreateLinkedTokenSource(zewnetrzny.Token, lokalny.Token);

        try { await Task.Delay(500, linked.Token); }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  Linked: zewnetrzny={zewnetrzny.IsCancellationRequested}, " +
                              $"lokalny={lokalny.IsCancellationRequested}");
        }

        // WithCancellationAsync — polyfill dla starych bibliotek bez CT
        static async Task<T> WithCancellationAsync<T>(Task<T> task, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using CancellationTokenRegistration reg =
                ct.UnsafeRegister(s => ((TaskCompletionSource<T>)s!).TrySetCanceled(), tcs);
            try   { return await task.ConfigureAwait(false); }
            finally { tcs.TrySetResult(default!); }
        }

        using var ctsOld = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            string wynik = await WithCancellationAsync(StaryKlientZA.PobierzAsync(), ctsOld.Token);
            Console.WriteLine($"  StaryKlient (w czasie): {wynik}");
        }
        catch (OperationCanceledException) { Console.WriteLine("  StaryKlient: timeout przez WithCancellationAsync"); }
    }

    // ── 2. Progress Reporting ─────────────────────────────────────────────────

    public static async Task ProgressReporting()
    {
        Console.WriteLine("\n── ProgressReporting ──");

        // IProgress<T> — interfejs do raportowania postępu
        // Progress<T> — implementacja przechwytująca SynchronizationContext
        //   → wywołuje callback na UI thread automatycznie (w WPF/WinForms)
        // SynchronicznyPostepZA<T> — callback na bieżącym wątku (console, testy)

        async Task PrzetworzAsync(
            int ilosc,
            IProgress<PostepOperacjiZA>? postep = null,
            CancellationToken ct = default)
        {
            for (int i = 0; i < ilosc; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct).ConfigureAwait(false);
                postep?.Report(new PostepOperacjiZA(i + 1, ilosc, $"Przetwarzam {i + 1}/{ilosc}"));
            }
            postep?.Report(new PostepOperacjiZA(ilosc, ilosc, "Zakończono!"));
        }

        // Progress<T> — standardowy, bezpieczny dla UI
        var postepUI = new Progress<PostepOperacjiZA>(p =>
            Console.Write($"\r  Postęp: {p.Procent:F0}% ({p.Obecny}/{p.Calkowity})     "));

        await PrzetworzAsync(5, postepUI);
        Console.WriteLine();

        // SynchronicznyPostepZA — wywoływany bezpośrednio (console app, testy)
        int ostatniOdczyt = 0;
        var postepSync = new SynchronicznyPostepZA<PostepOperacjiZA>(p =>
        {
            ostatniOdczyt = p.Obecny;
        });

        await PrzetworzAsync(3, postepSync);
        Console.WriteLine($"  SynchronicznyPostep — ostatni odczyt: {ostatniOdczyt}");

        // PostepKolektor — zbieranie raportów do listy (np. do testów)
        var kolektor = new PostepKolektorZA<PostepOperacjiZA>();
        await PrzetworzAsync(4, kolektor);
        Console.WriteLine($"  Kolektor zebrał {kolektor.Raporty.Count} raportów");

        // Różnica Progress<T> vs Action<T>:
        Console.WriteLine("\n  Progress<T>: marshalling na UI thread — bezpieczny dla UI");
        Console.WriteLine("  Action<T>:  wywołany na bieżącym wątku — może być wątek puli → wyjątek w UI");
    }

    // ── 3. Task.WhenAll — szczegółowo ────────────────────────────────────────

    public static async Task WhenAllSzczegółowo()
    {
        Console.WriteLine("\n── WhenAllSzczegółowo ──");

        static async Task<int> ObliczAsync(int n, int msDelay)
        {
            await Task.Delay(msDelay);
            return n * n;
        }

        // Podstawowe WhenAll
        int[] wyniki = await Task.WhenAll(
            ObliczAsync(3, 50),
            ObliczAsync(4, 30),
            ObliczAsync(5, 70));
        Console.WriteLine($"WhenAll: [{string.Join(", ", wyniki)}]");

        // WhenAll z kolekcją — wszystkie uruchomione jednocześnie
        int[] ids = { 1, 2, 3, 4, 5 };
        var tasks = ids.Select(id => ObliczAsync(id, 20));
        int[] wszystkie = await Task.WhenAll(tasks);
        Console.WriteLine($"WhenAll z kolekcją: [{string.Join(", ", wszystkie)}]");

        // Obsługa WSZYSTKICH wyjątków (nie tylko pierwszego)
        Task<string>[] błędneTaski =
        {
            Task.FromException<string>(new InvalidOperationException("Błąd A")),
            Task.FromException<string>(new ArgumentException("Błąd B")),
            Task.FromResult("OK")
        };

        Task<string[]> wszystkieTaski = Task.WhenAll(błędneTaski);
        try { await wszystkieTaski; }
        catch
        {
            Console.WriteLine("WhenAll wszystkie błędy (przez .Exception):");
            foreach (var ex in wszystkieTaski.Exception!.InnerExceptions)
                Console.WriteLine($"  • {ex.GetType().Name}: {ex.Message}");
        }

        // WhenAll z SemaphoreSlim — limit równoległości
        async Task<string> PobierzAsync(int id, SemaphoreSlim semafor, CancellationToken ct)
        {
            await semafor.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await Task.Delay(20, ct).ConfigureAwait(false);
                return $"zasób_{id}";
            }
            finally { semafor.Release(); }
        }

        using var semafor3  = new SemaphoreSlim(3, 3);  // max 3 naraz
        using var ctsPobierz = new CancellationTokenSource();
        var zasoby = Enumerable.Range(1, 8)
            .Select(id => PobierzAsync(id, semafor3, ctsPobierz.Token));
        string[] wynikZasobow = await Task.WhenAll(zasoby);
        Console.WriteLine($"Z SemaphoreSlim (max 3): pobrano {wynikZasobow.Length} zasobów");

        // Batch processing — paczki równolegle, paczki sekwencyjnie
        static async Task<List<T>> PrzetworzPaczkamiAsync<T>(
            List<Task<T>> taskList, int rozmiarPaczki = 3)
        {
            var wynikiBatch = new List<T>();
            for (int i = 0; i < taskList.Count; i += rozmiarPaczki)
            {
                var paczka = taskList.Skip(i).Take(rozmiarPaczki);
                var wynikPaczki = await Task.WhenAll(paczka).ConfigureAwait(false);
                wynikiBatch.AddRange(wynikPaczki);
            }
            return wynikiBatch;
        }

        var batchTasks = Enumerable.Range(1, 7)
            .Select(i => ObliczAsync(i, 10)).ToList();
        var batchWyniki = await PrzetworzPaczkamiAsync(batchTasks, 3);
        Console.WriteLine($"Batch (paczki po 3): [{string.Join(", ", batchWyniki)}]");
    }

    // ── 4. Task.WhenAny — wzorce ──────────────────────────────────────────────

    public static async Task WhenAnyWzorce()
    {
        Console.WriteLine("\n── WhenAnyWzorce ──");

        static async Task<string> PobierzDaneAsync(string nazwa, int ms)
        {
            await Task.Delay(ms);
            return $"Dane z {nazwa}";
        }

        // WZORZEC 1 — Timeout
        async Task<T?> ZTimeoutemAsync<T>(Task<T> operacja, TimeSpan timeout) where T : class
        {
            using var cts = new CancellationTokenSource();
            Task timeoutTask = Task.Delay(timeout, cts.Token);
            Task pierwsza    = await Task.WhenAny(operacja, timeoutTask).ConfigureAwait(false);

            if (pierwsza == operacja)
            {
                cts.Cancel();  // anuluj niepotrzebny timeout
                return await operacja.ConfigureAwait(false);
            }
            return null;
        }

        Console.WriteLine("Timeout:");
        string? szybkie = await ZTimeoutemAsync(PobierzDaneAsync("szybkie", 50), TimeSpan.FromMilliseconds(200));
        string? wolne   = await ZTimeoutemAsync(PobierzDaneAsync("wolne", 500),  TimeSpan.FromMilliseconds(100));
        Console.WriteLine($"  Szybkie (50ms/limit 200ms): {szybkie ?? "Timeout"}");
        Console.WriteLine($"  Wolne   (500ms/limit 100ms): {wolne ?? "Timeout"}");

        // WZORZEC 2 — Wyścig (Race) — pierwszy poprawny wynik
        async Task<string> WyscigAsync(IEnumerable<Func<Task<string>>> zrodla)
        {
            var tasks     = zrodla.Select(z => z()).ToList();
            var pozostale = new List<Task<string>>(tasks);

            while (pozostale.Any())
            {
                Task<string> pierwszy = await Task.WhenAny(pozostale).ConfigureAwait(false);
                pozostale.Remove(pierwszy);

                if (!pierwszy.IsFaulted && !pierwszy.IsCanceled)
                    return await pierwszy.ConfigureAwait(false);
            }
            throw new InvalidOperationException("Wszystkie źródła zawiodły");
        }

        string wynikWyscigu = await WyscigAsync(new Func<Task<string>>[]
        {
            () => PobierzDaneAsync("CDN1 (300ms)", 300),
            () => PobierzDaneAsync("CDN2 (80ms)",   80),  // wygrywa
            () => PobierzDaneAsync("CDN3 (500ms)", 500),
        });
        Console.WriteLine($"Wyścig (Race): {wynikWyscigu}");

        // WZORZEC 3 — Przetwarzanie w kolejności ukończenia
        Console.WriteLine("Kolejność ukończenia:");
        var tasksKolejnosc = new List<Task<string>>
        {
            PobierzDaneAsync("C (150ms)", 150),
            PobierzDaneAsync("A (30ms)",   30),
            PobierzDaneAsync("B (90ms)",   90),
        };
        var pozostaleKolejnosc = new List<Task<string>>(tasksKolejnosc);
        while (pozostaleKolejnosc.Any())
        {
            Task<string> ukonczony = await Task.WhenAny(pozostaleKolejnosc).ConfigureAwait(false);
            pozostaleKolejnosc.Remove(ukonczony);
            Console.WriteLine($"  Ukończono: {await ukonczony}");
        }

        // WZORZEC 4 — Heartbeat — puls podczas długiej operacji
        async Task<T> ZHeartbeatAsync<T>(Task<T> operacja, TimeSpan interwal, Action puls)
        {
            while (true)
            {
                Task opoznienie = Task.Delay(interwal);
                Task pierwsza   = await Task.WhenAny(operacja, opoznienie).ConfigureAwait(false);
                if (pierwsza == operacja) return await operacja.ConfigureAwait(false);
                puls();
            }
        }

        int pulsow = 0;
        string wynikHeartbeat = await ZHeartbeatAsync(
            PobierzDaneAsync("serwer (300ms)", 300),
            TimeSpan.FromMilliseconds(80),
            () => { pulsow++; Console.Write($"  [puls {pulsow}]"); });
        Console.WriteLine($"\n  Heartbeat zakończony: {wynikHeartbeat}");
    }

    // ── 5. Deadlocks — przyczyny i rozwiązania ────────────────────────────────

    public static async Task DeadlockiIPrzyczyny()
    {
        Console.WriteLine("\n── DeadlockiIPrzyczyny ──");

        // MECHANIZM DEADLOCKU (w aplikacjach z SynchronizationContext = WPF/WinForms):
        // 1. Wątek UI wywołuje .Result lub .Wait()
        // 2. .Result blokuje wątek UI synchronicznie
        // 3. await próbuje wrócić na wątek UI (SynchronizationContext)
        // 4. Wątek UI zablokowany przez .Result → krąg → DEADLOCK
        //
        // W console app (.NET 8) i ASP.NET Core brak SynchronizationContext
        // więc .Result nie powoduje deadlocka — ale to ZŁY NAWYK!

        async Task<string> PobierzAsync()
        {
            await Task.Delay(10).ConfigureAwait(false);  // ConfigureAwait(false)!
            return "wynik";
        }

        // Rozwiązania (w kolejności preferencji):

        // ROZWIĄZANIE 1 — zawsze await (najlepsza opcja)
        string poprawne1 = await PobierzAsync();
        Console.WriteLine($"  Poprawne 1 (await):  {poprawne1}");

        // ROZWIĄZANIE 2 — ConfigureAwait(false) w async metodzie
        // (eliminuje próbę powrotu na UI thread)
        // PobierzAsync już ma ConfigureAwait(false) → .Result nie deadlockuje
        string poprawne2 = PobierzAsync().Result;  // bezpieczne gdy CA(false) wewnątrz
        Console.WriteLine($"  Poprawne 2 (CA false + .Result): {poprawne2}");

        // ROZWIĄZANIE 3 — Task.Run (wątek puli nie ma SynchronizationContext)
        string poprawne3 = Task.Run(async () => await PobierzAsync()).Result;
        Console.WriteLine($"  Poprawne 3 (Task.Run + .Result): {poprawne3}");

        // Diagnoza deadlocków — co szukać w kodzie
        Console.WriteLine("\nCo szukać:");
        Console.WriteLine("  .Result    — potencjalny deadlock w UI/ASP.NET Classic");
        Console.WriteLine("  .Wait()    — tak samo");
        Console.WriteLine("  .GetAwaiter().GetResult() — też może deadlockować");
        Console.WriteLine("  async void — wyjątki nie propagują przez awaiter callera");

        // WaitAsync — limit czasowy bez deadlocka
        async Task<string> DlugaOperacja()
        {
            await Task.Delay(500);
            return "wynik długiej";
        }

        try
        {
            string wynik = await DlugaOperacja().WaitAsync(TimeSpan.FromMilliseconds(100));
            Console.WriteLine($"\n  WaitAsync: {wynik}");
        }
        catch (TimeoutException)
        {
            Console.WriteLine("\n  WaitAsync: timeout po 100ms (bez deadlocka!)");
        }
    }

    // ── 6. Channel — producent/konsument ─────────────────────────────────────

    public static async Task ChannelProducentKonsument()
    {
        Console.WriteLine("\n── ChannelProducentKonsument ──");

        // Channel<T> — asynchroniczna thread-safe kolejka z backpressure i CT
        // Lepsza od ConcurrentQueue dla async pipeline — ReadAllAsync = IAsyncEnumerable

        // Ograniczony channel — backpressure gdy pełny
        var kanal = Channel.CreateBounded<int>(new BoundedChannelOptions(5)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        // Producent — pisze, wywołuje Complete gdy skończony
        async Task ProducentAsync(ChannelWriter<int> writer, int ilosc, CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < ilosc; i++)
                {
                    await writer.WriteAsync(i, ct).ConfigureAwait(false);
                    await Task.Delay(15, ct).ConfigureAwait(false);
                }
            }
            finally { writer.Complete(); }
        }

        // Konsument — ReadAllAsync kończy się gdy Complete()
        async Task KonsumentAsync(ChannelReader<int> reader, string nazwa, CancellationToken ct)
        {
            await foreach (int el in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await Task.Delay(30, ct).ConfigureAwait(false);  // wolniejszy niż producent
                Console.Write($"{nazwa}:{el} ");
            }
        }

        using var ctsChannel = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        Task producent   = ProducentAsync(kanal.Writer, 8, ctsChannel.Token);
        Task konsument1  = KonsumentAsync(kanal.Reader, "K1", ctsChannel.Token);
        Task konsument2  = KonsumentAsync(kanal.Reader, "K2", ctsChannel.Token);

        await producent.ConfigureAwait(false);
        await Task.WhenAll(konsument1, konsument2).ConfigureAwait(false);
        Console.WriteLine("\n  Channel pipeline zakończony");

        // 3-stopniowy pipeline
        Console.WriteLine("3-stopniowy pipeline:");
        var pipe1 = Channel.CreateBounded<string>(10);
        var pipe2 = Channel.CreateBounded<int>(10);

        using var ctsPipe = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        Task etap1 = Task.Run(async () =>
        {
            string[] dane = { "abc", "de", "fghij", "k", "lm" };
            foreach (string s in dane)
            {
                await pipe1.Writer.WriteAsync(s, ctsPipe.Token);
                await Task.Delay(10, ctsPipe.Token);
            }
            pipe1.Writer.Complete();
        }, ctsPipe.Token);

        Task etap2 = Task.Run(async () =>
        {
            await foreach (string s in pipe1.Reader.ReadAllAsync(ctsPipe.Token))
                await pipe2.Writer.WriteAsync(s.Length * 2, ctsPipe.Token);
            pipe2.Writer.Complete();
        }, ctsPipe.Token);

        Task etap3 = Task.Run(async () =>
        {
            var wyniki = new List<int>();
            await foreach (int n in pipe2.Reader.ReadAllAsync(ctsPipe.Token))
                wyniki.Add(n);
            Console.WriteLine($"  Wyniki pipeline: [{string.Join(", ", wyniki)}]");
        }, ctsPipe.Token);

        await Task.WhenAll(etap1, etap2, etap3).ConfigureAwait(false);
    }

    // ── 7. AsyncLocal — dane lokalne dla async flow ───────────────────────────

    public static async Task AsyncLocalDemo()
    {
        Console.WriteLine("\n── AsyncLocalDemo ──");

        // AsyncLocal<T> — dane które "podążają" za async flow
        // Jak ThreadLocal, ale dla async — każda kontynuacja DZIEDZICZY wartość
        // Zmiana w child-Task NIE propaguje do parent-Task

        KontekstZadaniaZA.TraceId    = "TRACE-MAIN";
        KontekstZadaniaZA.Uzytkownik = "anna";
        Console.WriteLine($"Main start: TraceId={KontekstZadaniaZA.TraceId}");

        // Task1 — dziedziczy TraceId, zmienia go tylko lokalnie
        Task task1 = Task.Run(async () =>
        {
            Console.WriteLine($"Task1 start: {KontekstZadaniaZA.TraceId}");  // TRACE-MAIN
            KontekstZadaniaZA.TraceId = "TRACE-CHILD1";  // zmiana tylko w tym flow
            await Task.Delay(50);
            Console.WriteLine($"Task1 kończy: {KontekstZadaniaZA.TraceId}"); // TRACE-CHILD1
        });

        // Task2 — niezależny flow, nie widzi zmiany Task1
        Task task2 = Task.Run(async () =>
        {
            Console.WriteLine($"Task2 start: {KontekstZadaniaZA.TraceId}");  // TRACE-MAIN (nie CHILD1!)
            await Task.Delay(30);
            Console.WriteLine($"Task2 kończy: {KontekstZadaniaZA.TraceId}"); // nadal TRACE-MAIN
        });

        await Task.WhenAll(task1, task2);
        Console.WriteLine($"Main po taskach: {KontekstZadaniaZA.TraceId}");  // TRACE-MAIN — niezmienione

        // Praktyczne zastosowanie — Request ID w middleware (ASP.NET Core)
        // AsyncLocal przechowuje ID requestu przez cały async call stack
        // Każda metoda może odczytać RequestId bez przekazywania przez parametry
        Console.WriteLine("\nPraktyczne zastosowanie AsyncLocal:");
        Console.WriteLine("  ASP.NET Core middleware: RequestId ← jeden raz set, wszędzie dostępny");
        Console.WriteLine("  Distributed tracing: TraceId propagowany przez cały async stos");
        Console.WriteLine("  Bez AsyncLocal: musisz przekazywać przez każdy parametr");

        // AsyncLocal vs ThreadLocal:
        Console.WriteLine("\nAsyncLocal vs ThreadLocal:");
        Console.WriteLine("  ThreadLocal: jeden flow per wątek — nie działa z async (wątek się zmienia)");
        Console.WriteLine("  AsyncLocal:  jeden flow per logical call context — działa z async");
    }
}
