namespace _03_Csharp_4_5;

// ── Klasy pomocnicze ──────────────────────────────────────────────────────────

public interface ILoggerAA
{
    void LogInfo(string msg);
    void LogWarning(string msg);
}

public class ConsoleLoggerAA : ILoggerAA
{
    public void LogInfo(string msg)    => Console.WriteLine($"  [INFO] {msg}");
    public void LogWarning(string msg) => Console.WriteLine($"  [WARN] {msg}");
}

// Uproszczony serwis HTTP (symulacja — bez faktycznych wywołań sieciowych)
public class ApiSerwisDemoAA : IDisposable
{
    private readonly ILoggerAA _logger;

    public ApiSerwisDemoAA(ILoggerAA? logger = null)
        => _logger = logger ?? new ConsoleLoggerAA();

    public async Task<string> PobierzAsync(
        string endpoint,
        CancellationToken ct = default)
    {
        return await RetryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInfo($"GET {endpoint}");
            await Task.Delay(100, ct).ConfigureAwait(false);  // symulacja I/O
            return $"{{\"endpoint\":\"{endpoint}\",\"status\":200}}";
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<List<string>> PobierzWieleAsync(
        IEnumerable<string> endpointy,
        int maxRownolegle = 5,
        CancellationToken ct = default)
    {
        using var semafor = new SemaphoreSlim(maxRownolegle, maxRownolegle);

        var zadania = endpointy.Select(async ep =>
        {
            await semafor.WaitAsync(ct).ConfigureAwait(false);
            try   { return await PobierzAsync(ep, ct).ConfigureAwait(false); }
            finally { semafor.Release(); }
        });

        return (await Task.WhenAll(zadania).ConfigureAwait(false)).ToList();
    }

    private async Task<T> RetryAsync<T>(
        Func<Task<T>> operacja,
        int maxProb = 3,
        CancellationToken ct = default)
    {
        for (int proba = 1; proba <= maxProb; proba++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                return await operacja().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (proba < maxProb)
            {
                _logger.LogWarning($"Próba {proba}/{maxProb}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, proba)), ct)
                    .ConfigureAwait(false);
            }
        }
        return await operacja().ConfigureAwait(false);
    }

    public void Dispose() { }
}

// Cache demonstrujący ValueTask
public class CacheAA<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dane = new();

    public ValueTask<TValue?> PobierzAsync(TKey klucz)
    {
        // Hot path — synchroniczny, zero alokacji Task!
        if (_dane.TryGetValue(klucz, out TValue? wartosc))
            return ValueTask.FromResult<TValue?>(wartosc);

        // Cold path — faktyczna async operacja
        return new ValueTask<TValue?>(PobierzZZrodlaAsync(klucz));
    }

    private async Task<TValue?> PobierzZZrodlaAsync(TKey klucz)
    {
        await Task.Delay(50).ConfigureAwait(false);  // symulacja I/O
        return default;
    }

    public void Dodaj(TKey klucz, TValue wartosc) => _dane[klucz] = wartosc;
}

public static class AsyncAwait
{
    // ── 1. Problem — dlaczego async? + Task i Task<T> ────────────────────────

    public static async Task ProblematykaITask()
    {
        Console.WriteLine("\n── ProblematykaITask ──");

        // Problem synchroniczny — wątek blokuje się czekając na I/O
        // Analogia: dzwonisz na infolinię i trzymasz słuchawkę 20 minut
        // Asynchroniczny: zostawiasz numer, robisz inne rzeczy, oddzwaniają
        Console.WriteLine("I/O-bound:  async/await — wątek zwolniony podczas oczekiwania");
        Console.WriteLine("CPU-bound:  Task.Run   — obliczenia na osobnym wątku");

        // Task — reprezentacja operacji która MOŻE nie być skończona ("obietnica")
        Task taskBezWyniku = Task.Delay(10);  // odpowiednik void
        await taskBezWyniku;
        Console.WriteLine($"Task.Delay status: {taskBezWyniku.Status}");  // RanToCompletion

        // Task<T> — obietnica zwrócenia wartości
        Task<int>    gotowy  = Task.FromResult(42);        // natychmiastowy wynik
        Task         pusty   = Task.CompletedTask;         // gotowy void
        Task<string> opozniony = Task.Run(async () =>
        {
            await Task.Delay(5);
            return "wynik";
        });

        Console.WriteLine($"FromResult(42): {await gotowy}");
        Console.WriteLine($"CompletedTask.IsCompleted: {pusty.IsCompleted}");
        Console.WriteLine($"Task.Run wynik: {await opozniony}");

        // Właściwości Task
        var t = Task.Delay(50);
        Console.WriteLine($"Przed await: IsCompleted={t.IsCompleted}, IsFaulted={t.IsFaulted}");
        await t;
        Console.WriteLine($"Po await:    IsCompleted={t.IsCompleted}, IsCompletedSuccessfully={t.IsCompletedSuccessfully}");
    }

    // ── 2. Składnia async/await — state machine ───────────────────────────────

    public static async Task SkładniaAsyncAwait()
    {
        Console.WriteLine("\n── SkładniaAsyncAwait ──");

        // async — modyfikator: metoda może używać await
        // await — "poczekaj na Task, ale nie blokuj wątku"
        // Reguły:
        // 1. async bez await → ostrzeżenie, metoda synchroniczna
        // 2. await tylko w metodach async
        // 3. Typ zwracany: Task, Task<T>, ValueTask, ValueTask<T>
        // 4. async void — TYLKO dla event handlerów

        // Prosty przykład
        async Task ProstyAsync()
        {
            Console.WriteLine("  Przed await");
            await Task.Delay(5);
            Console.WriteLine("  Po await (może być inny wątek)");
        }
        await ProstyAsync();

        // State machine — kompilator generuje:
        // class ProstyAsync_StateMachine : IAsyncStateMachine { ... }
        // Pola: state(-1=start, -2=gotowe, >=0=punkt zawieszenia)
        // MoveNext() — wywoływany przy starcie i przy każdym wznowieniu

        // Wielokrotne await w jednej metodzie — wiele punktów zawieszenia
        async Task<string> WieloetapowyAsync()
        {
            await Task.Delay(5);               // punkt zawieszenia 1
            string krok1 = "dane1";
            await Task.Delay(5);               // punkt zawieszenia 2
            string krok2 = "dane2";
            return krok1 + "+" + krok2;
        }

        string wynik = await WieloetapowyAsync();
        Console.WriteLine($"  Wieloetapowy: {wynik}");

        // async z return — wygenerowany Task<T>
        async Task<int> ObliczAsync(int n)
        {
            await Task.Delay(1);
            return n * n;  // kompilator opakuje w Task<int>
        }
        Console.WriteLine($"  ObliczAsync(7) = {await ObliczAsync(7)}");

        // Jeśli Task jest już skończony — await kontynuuje synchronicznie!
        var juzGotowy = Task.FromResult(99);
        Console.WriteLine($"  await skończonego Task: {await juzGotowy} (synchronicznie)");
    }

    // ── 3. Sekwencyjne vs równoległe — kluczowa różnica wydajnościowa ─────────

    public static async Task SekwencyjneVsRownolegle()
    {
        Console.WriteLine("\n── SekwencyjneVsRownolegle ──");

        static async Task<string> PobierzDaneAsync(string nazwa, int msDelay)
        {
            await Task.Delay(msDelay);
            return $"Dane z {nazwa}";
        }

        // SEKWENCYJNE — jedno po drugim — łącznie ~300ms
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string w1 = await PobierzDaneAsync("API1", 100);
        string w2 = await PobierzDaneAsync("API2", 100);
        string w3 = await PobierzDaneAsync("API3", 100);
        sw.Stop();
        Console.WriteLine($"Sekwencyjne: {sw.ElapsedMilliseconds}ms → [{w1}, {w2}, {w3}]");

        // RÓWNOLEGŁE — wszystkie naraz — łącznie ~100ms (tyle co najdłuższy)
        sw.Restart();
        Task<string> t1 = PobierzDaneAsync("API1", 100);  // uruchamia od razu
        Task<string> t2 = PobierzDaneAsync("API2", 100);
        Task<string> t3 = PobierzDaneAsync("API3", 100);
        string[] wyniki = await Task.WhenAll(t1, t2, t3);  // czeka na wszystkie
        sw.Stop();
        Console.WriteLine($"Równoległe:  {sw.ElapsedMilliseconds}ms → [{string.Join(", ", wyniki)}]");
        Console.WriteLine("RÓŻNICA: sekwencyjne n×t, równoległe ≈ max(t)");

        // Task.WhenAny — timeout pattern
        async Task<string?> ZTimeoutemAsync(string nazwa, int opMs, int limitMs)
        {
            var zadanie    = PobierzDaneAsync(nazwa, opMs);
            var limitCzasu = Task.Delay(limitMs);
            Task pierwszy  = await Task.WhenAny(zadanie, limitCzasu);
            return pierwszy == zadanie ? await zadanie : null;
        }

        Console.WriteLine($"\nTask.WhenAny timeout:");
        Console.WriteLine($"  Szybkie (50ms/limit 200ms): {await ZTimeoutemAsync("szybkie", 50, 200) ?? "Timeout"}");
        Console.WriteLine($"  Wolne  (500ms/limit 100ms): {await ZTimeoutemAsync("wolne", 500, 100) ?? "Timeout"}");

        // Task.WhenAll z różnymi typami — await każdego po WhenAll
        var taskStr  = PobierzDaneAsync("S", 50);
        var taskNum  = Task.Run(async () => { await Task.Delay(30); return 42; });
        var taskBool = Task.Run(async () => { await Task.Delay(20); return true; });

        await Task.WhenAll(taskStr, taskNum, taskBool);
        Console.WriteLine($"\nWhenAll z różnymi typami: '{await taskStr}', {await taskNum}, {await taskBool}");
        Console.WriteLine("  Drugie await jest natychmiastowe — Task już skończony!");
    }

    // ── 4. ConfigureAwait — kontekst synchronizacji ───────────────────────────

    public static async Task ConfigureAwaitDemo()
    {
        Console.WriteLine("\n── ConfigureAwaitDemo ──");

        // SynchronizationContext — mechanizm decydujący gdzie wznowić kod po await
        // WPF/WinForms: wznowienie na wątku UI (potrzebne do aktualizacji UI)
        // ASP.NET Core: brak kontekstu (każdy wątek z puli jest OK)
        // Console app: brak kontekstu (jak ASP.NET Core)

        // ConfigureAwait(true)  — domyślne, powrót na oryginalny kontekst
        // ConfigureAwait(false) — powrót na dowolny wątek z puli (szybsze dla bibliotek)

        // W bibliotece — ZAWSZE ConfigureAwait(false):
        // 1. Zapobiega deadlockom gdy caller blokuje przez .Result
        // 2. Lepsza wydajność — brak marshalling do UI thread
        async Task<string> MetodaBiblioteczna()
        {
            await Task.Delay(10).ConfigureAwait(false);  // nie przechwytuj kontekstu
            return "wynik biblioteki";                    // kontynuacja na dowolnym wątku
        }

        Console.WriteLine($"Biblioteka ConfigureAwait(false): {await MetodaBiblioteczna()}");

        // W aplikacji UI — ConfigureAwait(true) lub brak gdy aktualizujesz UI:
        // private async void PrzyciskClick(object sender, EventArgs e)
        // {
        //     var dane = await PobierzDane();  // defaultowo ConfigureAwait(true)
        //     etykieta.Text = dane;            // MUSI być na wątku UI!
        // }

        // KLASYCZNY DEADLOCK (w aplikacjach z SynchronizationContext):
        // Wątek UI blokuje się przez .Result
        // await próbuje wrócić na wątek UI
        // Wątek UI zablokowany przez .Result → DEADLOCK!
        // string PowodujDeadlock() => MetodaBiblioteczna().Result;  // DEADLOCK bez ConfigureAwait(false)!

        Console.WriteLine("\nDeadlock mechanizm:");
        Console.WriteLine("  .Result blokuje wątek → await chce wrócić na zablokowany wątek → DEADLOCK");
        Console.WriteLine("  Rozwiązania:");
        Console.WriteLine("  1. Zawsze await (najlepsza opcja)");
        Console.WriteLine("  2. ConfigureAwait(false) w bibliotece — brak próby powrotu na UI");
        Console.WriteLine("  3. Task.Run(() => ...).Result — brak SynchronizationContext na wątku puli");

        // Demonstracja: await zawsze bezpieczne
        string wynik = await Task.Run(async () =>
        {
            await Task.Delay(5).ConfigureAwait(false);
            return "Task.Run — brak SynchronizationContext";
        });
        Console.WriteLine($"Task.Run result: {wynik}");
    }

    // ── 5. async void — niebezpieczne, tylko dla eventów ────────────────────

    public static async Task AsyncVoidIPulapki()
    {
        Console.WriteLine("\n── AsyncVoidIPulapki ──");

        // async void — UNIKAJ! Trzy powody:
        // 1. Wyjątki nie trafiają do callera → crashują aplikację
        // 2. Nie można await'ować async void → nie wiesz kiedy skończyła
        // 3. Niemożliwe testowanie jednostkowe

        // DOBRZE — async Task zamiast async void
        async Task DobraMetodaAsync()
        {
            await Task.Delay(5);
            // Wyjątek → trafia do awaitującego callera
        }

        try
        {
            await DobraMetodaAsync();
            Console.WriteLine("async Task: wyjątek możliwy do przechwycenia");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Przechwycony: {ex.Message}");
        }

        // Jedyny uzasadniony przypadek — event handler (framework wymaga void)
        // Zawsze owijaj w try-catch!
        //
        // private async void PrzyciskClick(object sender, EventArgs e)
        // {
        //     try {
        //         var wynik = await ZalogujAsync();
        //         Console.WriteLine(wynik);
        //     }
        //     catch (Exception ex) {
        //         Console.WriteLine($"Błąd: {ex.Message}");
        //         // BEZ try-catch: wyjątek crashuje aplikację!
        //     }
        // }

        // Wrapper pattern — event handler jako void, logika jako Task
        // private void PrzyciskClick(object s, EventArgs e) =>
        //     ObsluzKlkniecieAsync()
        //         .ContinueWith(t => {
        //             if (t.IsFaulted)
        //                 Console.WriteLine($"Błąd: {t.Exception!.GetBaseException().Message}");
        //         });

        Console.WriteLine("\nasync void — tylko event handlers, ZAWSZE z try-catch wewnątrz");
        Console.WriteLine("async Task — wszystko inne — wyjątki propagują do callera");

        // Fire-and-forget — gdy naprawdę nie zależy ci na wyniku
        async Task FireAndForget()
        {
            await Task.Delay(5);
            Console.WriteLine("  [fire-and-forget zakończony]");
        }

        _ = FireAndForget();  // _ = ignoruj Task, ale Task.Exception jest ignorowany!
        Console.WriteLine("Fire-and-forget uruchomiony (wynik ignorowany)");
        await Task.Delay(20);  // poczekaj żeby zobaczyć wynik
    }

    // ── 6. Obsługa wyjątków w async ───────────────────────────────────────────

    public static async Task ObslugaWyjatkow()
    {
        Console.WriteLine("\n── ObslugaWyjatkow ──");

        // Wyjątki w async są przechowywane w Task i rzucane przy await
        async Task<string> BezpiecznaOperacjaAsync(bool rzucWyjatek)
        {
            await Task.Delay(5);
            if (rzucWyjatek) throw new InvalidOperationException("Błąd operacji!");
            return "sukces";
        }

        // Standardowe try-catch działa normalnie
        try
        {
            string wynik = await BezpiecznaOperacjaAsync(true);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Przechwycony: {ex.Message}");
        }

        // AggregateException — Task.WhenAll z wieloma błędami
        var tasks = new[]
        {
            Task.FromException<string>(new InvalidOperationException("Błąd 1")),
            Task.FromException<string>(new ArgumentException("Błąd 2")),
            Task.FromResult("OK")
        };

        Task<string[]> wszystkie = Task.WhenAll(tasks);
        try
        {
            await wszystkie;
        }
        catch (Exception ex)
        {
            // await rzuca PIERWSZY wyjątek
            Console.WriteLine($"await WhenAll: pierwszy błąd = '{ex.Message}'");

            // Aby dostać WSZYSTKIE — sprawdź .Exception na Task
            var bledy = tasks
                .Where(t => t.IsFaulted)
                .SelectMany(t => t.Exception!.InnerExceptions);
            Console.WriteLine("  Wszystkie błędy:");
            foreach (var b in bledy)
                Console.WriteLine($"    • {b.GetType().Name}: {b.Message}");
        }

        // Retry pattern z exponential backoff
        async Task<T> RetryAsync<T>(Func<Task<T>> operacja, int maxProb = 3)
        {
            Exception? ostatni = null;
            for (int proba = 1; proba <= maxProb; proba++)
            {
                try { return await operacja(); }
                catch (Exception ex) when (proba < maxProb)
                {
                    ostatni = ex;
                    Console.WriteLine($"  Próba {proba}/{maxProb}: {ex.Message} — retry za {proba*100}ms");
                    await Task.Delay(proba * 100);  // exponential backoff
                }
            }
            throw new Exception($"Nieudane po {maxProb} próbach", ostatni);
        }

        int licznikProb = 0;
        try
        {
            string wynik = await RetryAsync<string>(async () =>
            {
                await Task.Delay(5);
                if (++licznikProb < 3) throw new Exception($"Próba {licznikProb}");
                return "sukces po 3 próbach";
            });
            Console.WriteLine($"\nRetry: {wynik}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Retry ostatecznie nieudane: {ex.Message}");
        }
    }

    // ── 7. CancellationToken ──────────────────────────────────────────────────

    public static async Task CancellationTokenDemo()
    {
        Console.WriteLine("\n── CancellationTokenDemo ──");

        // CancellationTokenSource — źródło anulowania
        // CancellationToken — readonly view przekazywany do operacji

        async Task<List<string>> PobierzWieleDanychAsync(
            string[] zasoby,
            CancellationToken ct = default)
        {
            var wyniki = new List<string>();
            foreach (string zasob in zasoby)
            {
                ct.ThrowIfCancellationRequested();  // kooperatywne sprawdzenie
                await Task.Delay(50, ct);           // Task.Delay respektuje token
                wyniki.Add($"Dane z {zasob}");
            }
            return wyniki;
        }

        // CancelAfter — automatyczne anulowanie po czasie
        using var cts1 = new CancellationTokenSource();
        cts1.CancelAfter(TimeSpan.FromMilliseconds(120));  // anuluj po 120ms

        try
        {
            var zasoby = new[] { "A", "B", "C", "D", "E" };
            var wyniki = await PobierzWieleDanychAsync(zasoby, cts1.Token);
            Console.WriteLine($"Pobrano: {string.Join(", ", wyniki)}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Anulowano po czasie (CancelAfter)");
        }

        // Ręczne anulowanie
        using var cts2 = new CancellationTokenSource();
        var zadanie = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                cts2.Token.ThrowIfCancellationRequested();
                await Task.Delay(20, cts2.Token);
            }
            return "gotowe";
        });

        await Task.Delay(80);
        cts2.Cancel();  // ręczne anulowanie

        try { await zadanie; }
        catch (OperationCanceledException) { Console.WriteLine("Ręczne anulowanie — OK"); }

        // Linked tokens — wiele źródeł anulowania
        using var zewnetrzny = new CancellationTokenSource();
        using var lokalny    = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        using var linked     = CancellationTokenSource
            .CreateLinkedTokenSource(zewnetrzny.Token, lokalny.Token);

        try
        {
            await Task.Delay(500, linked.Token);  // anulowany przez lokalny (150ms)
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Linked token: zewnetrzny={zewnetrzny.IsCancellationRequested}, " +
                              $"lokalny={lokalny.IsCancellationRequested}");
        }
    }

    // ── 8. ValueTask + praktyczny ApiSerwis ───────────────────────────────────

    public static async Task ValueTaskIApiSerwis()
    {
        Console.WriteLine("\n── ValueTaskIApiSerwis ──");

        // ValueTask<T> — struct zamiast class
        // Używaj gdy metoda CZĘSTO zwraca synchronicznie (np. z cache)
        // Zero alokacji gdy cache trafiony, pełna alokacja gdy cache pudło

        var cache = new CacheAA<string, string>();

        // Pierwsze pobranie — async (cache pudło)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? wynik1 = await cache.PobierzAsync("klucz1");  // I/O
        Console.WriteLine($"Cache miss: {sw.ElapsedMilliseconds}ms, wynik={wynik1 ?? "null"}");

        cache.Dodaj("klucz1", "wartość123");

        // Drugie pobranie — synchroniczne (cache trafiony), ZERO alokacji Task
        sw.Restart();
        string? wynik2 = await cache.PobierzAsync("klucz1");
        Console.WriteLine($"Cache hit:  {sw.ElapsedMilliseconds}ms, wynik={wynik2}");

        // Ograniczenia ValueTask:
        Console.WriteLine("\nValueTask ograniczenia:");
        Console.WriteLine("  - Nie awaituj dwa razy (undefined behavior)");
        Console.WriteLine("  - Nie Task.WhenAll bezpośrednio — użyj .AsTask()");
        Console.WriteLine("  - Używaj TYLKO gdy masz zmierzone, że cache-hit jest częsty");

        // Praktyczny ApiSerwis z retry, SemaphoreSlim, ConfigureAwait
        Console.WriteLine("\nPraktyczny ApiSerwis (symulacja):");
        using var serwis = new ApiSerwisDemoAA(new ConsoleLoggerAA());
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Jeden zasób
        string json = await serwis.PobierzAsync("/users/1", cts.Token);
        Console.WriteLine($"  Pobrano: {json}");

        // Wiele zasobów równolegle (max 3 naraz)
        var endpointy = Enumerable.Range(1, 5).Select(i => $"/posts/{i}");
        var posty = await serwis.PobierzWieleAsync(endpointy, maxRownolegle: 3, ct: cts.Token);
        Console.WriteLine($"  Pobrano {posty.Count} zasobów równolegle");

        // Serwis łączy: async/await + retry + SemaphoreSlim + ConfigureAwait(false) + CT
        Console.WriteLine("\nWzorzec ApiSerwis łączy:");
        Console.WriteLine("  await/async — nie blokuje wątków");
        Console.WriteLine("  SemaphoreSlim — limit równoległości");
        Console.WriteLine("  ConfigureAwait(false) — bez kontekstu UI");
        Console.WriteLine("  CancellationToken — przekazywany przez cały stos wywołań");
        Console.WriteLine("  Retry z exponential backoff — obsługa niestabilności sieci");
    }
}
