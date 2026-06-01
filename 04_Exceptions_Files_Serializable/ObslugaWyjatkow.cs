namespace _04_Exceptions_Files_Serializable;

// ── Hierarchia wyjątków ───────────────────────────────────────────────────────

public class AplikacjaException : Exception
{
    public string? KodBledu { get; }
    public AplikacjaException(string message, string? kodBledu = null)
        : base(message) { KodBledu = kodBledu; }
    public AplikacjaException(string message, Exception inner, string? kodBledu = null)
        : base(message, inner) { KodBledu = kodBledu; }
}

public class DomenaException : AplikacjaException
{
    public DomenaException(string message) : base(message, "DOMENA") { }
    public DomenaException(string message, Exception inner) : base(message, inner, "DOMENA") { }
}

public class NieznalezionyException : DomenaException
{
    public string NazwaEncji  { get; }
    public object Identyfikator { get; }
    public NieznalezionyException(string nazwaEncji, object id)
        : base($"{nazwaEncji} o id={id} nie znaleziony")
    { NazwaEncji = nazwaEncji; Identyfikator = id; }
}

public class KonflikException : DomenaException
{
    public KonflikException(string message) : base(message) { }
}

public class BrakUprawnieniException : DomenaException
{
    public string Operacja { get; }
    public BrakUprawnieniException(string operacja)
        : base($"Brak uprawnień do: {operacja}") { Operacja = operacja; }
}

public class WalidacjaException : AplikacjaException
{
    public string         NazwaPola           { get; }
    public object?        NieprawidlowaWartosc { get; }
    public List<string>   BledyWalidacji       { get; }

    public WalidacjaException(string nazwa, object? wartosc, params string[] bledy)
        : base($"Błąd walidacji pola '{nazwa}'", "WALIDACJA")
    { NazwaPola = nazwa; NieprawidlowaWartosc = wartosc; BledyWalidacji = bledy.ToList(); }
}

// ── Guard pattern ─────────────────────────────────────────────────────────────

public static class GuardOW
{
    public static T NieNull<T>(T? wartosc, string nazwaParam) where T : class =>
        wartosc ?? throw new ArgumentNullException(nazwaParam);

    public static string NiePusta(string? wartosc, string nazwaParam) =>
        string.IsNullOrWhiteSpace(wartosc)
            ? throw new ArgumentException("Wartość nie może być pusta", nazwaParam)
            : wartosc;

    public static T WZakresie<T>(T wartosc, T min, T max, string nazwaParam)
        where T : IComparable<T>
    {
        if (wartosc.CompareTo(min) < 0 || wartosc.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(nazwaParam, wartosc,
                $"Wartość musi być między {min} a {max}");
        return wartosc;
    }
}

// ── Result<T> ─────────────────────────────────────────────────────────────────

public class Result<T>
{
    public bool    CzyOk         { get; }
    public T?      Wartosc       { get; }
    public string? BladKomunikat { get; }
    public string? KodBledu      { get; }

    private Result(T wartosc)                         { CzyOk = true;  Wartosc = wartosc; }
    private Result(string blad, string? kod = null)   { CzyOk = false; BladKomunikat = blad; KodBledu = kod; }

    public static Result<T> Ok(T wartosc)                           => new(wartosc);
    public static Result<T> Blad(string komunikat, string? kod = null) => new(komunikat, kod);

    public Result<TNew> Map<TNew>(Func<T, TNew> transform) =>
        CzyOk ? Result<TNew>.Ok(transform(Wartosc!))
               : Result<TNew>.Blad(BladKomunikat!, KodBledu);

    public TResult Match<TResult>(Func<T, TResult> sukces, Func<string, TResult> blad) =>
        CzyOk ? sukces(Wartosc!) : blad(BladKomunikat!);
}

// ── Globalny handler wyjątków ─────────────────────────────────────────────────

public static class GlobalnyHandlerWyjatkow
{
    public static (int StatusCode, string Message) Obsłuż(Exception ex) =>
        ex switch
        {
            NieznalezionyException e    => (404, $"Nie znaleziono: {e.Message}"),
            WalidacjaException e        => (400, $"Walidacja: {string.Join(", ", e.BledyWalidacji)}"),
            BrakUprawnieniException e   => (403, $"Brak uprawnień: {e.Operacja}"),
            KonflikException e          => (409, $"Konflikt: {e.Message}"),
            AplikacjaException e        => (500, $"Aplikacja [{e.KodBledu}]: {e.Message}"),
            OperationCanceledException  => (408, "Żądanie anulowane"),
            _                           => (500, $"Nieoczekiwany: {ex.Message}")
        };
}

// ── Klasa główna ──────────────────────────────────────────────────────────────

public static class ObslugaWyjatkow
{
    // ── 1. try / catch / finally ──────────────────────────────────────────────

    public static void TryCatchFinally()
    {
        Console.WriteLine("\n── TryCatchFinally ──");

        // Wiele bloków catch — od NAJBARDZIEJ SPECYFICZNEGO do ogólnego
        // Kolejność jest kluczowa — kompilator blokuje nieosiągalne catch
        static void Przetwarzaj(string? wejście)
        {
            try
            {
                GuardOW.NieNull(wejście, nameof(wejście));   // null → ArgumentNullException
                int liczba = int.Parse(wejście!);             // "abc" → FormatException
                int wynik  = 100 / liczba;                   // 0 → DivideByZeroException
                Console.WriteLine($"  Wynik: {wynik}");
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"  Null: {ex.ParamName}");
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"  Format: {ex.Message}");
            }
            catch (DivideByZeroException)
            {
                Console.WriteLine("  Dzielenie przez zero!");
            }
            catch (Exception ex)                // ogólny — ZAWSZE NA KOŃCU
            {
                Console.WriteLine($"  Ogólny: {ex.GetType().Name}");
                throw;                          // re-throw — nie połykaj wyjątku!
            }
            finally
            {
                // ZAWSZE wykonywane — przy powodzeniu, wyjątku i return!
                Console.WriteLine("  finally: sprzątanie");
            }
        }

        Przetwarzaj(null);
        Przetwarzaj("abc");
        Przetwarzaj("0");
        Przetwarzaj("5");
    }

    // ── 2. throw vs throw ex ──────────────────────────────────────────────────

    public static void ThrowVsThrowEx()
    {
        Console.WriteLine("\n── ThrowVsThrowEx ──");

        // throw — zachowuje oryginalny stack trace
        // throw ex — RESETUJE stack trace do bieżącego miejsca (NIGDY nie rób!)
        Console.WriteLine("  throw:    zachowuje stack trace (DOBRZE)");
        Console.WriteLine("  throw ex: RESETUJE stack trace do catch (ŹLE)");

        // Throw expressions — C# 7+ — w ??, ternary, expression-bodied
        static string WalidujEmail(string? email) =>
            email ?? throw new ArgumentNullException(nameof(email));

        static int WalidujWiek(int wiek) =>
            wiek is >= 0 and <= 150
                ? wiek
                : throw new ArgumentOutOfRangeException(nameof(wiek), "Wiek 0-150");

        try { _ = WalidujEmail(null); }
        catch (ArgumentNullException ex) { Console.WriteLine($"  ?? throw: {ex.ParamName}"); }

        try { _ = WalidujWiek(200); }
        catch (ArgumentOutOfRangeException ex) { Console.WriteLine($"  Ternary throw: {ex.ParamName}"); }

        // Owijanie wyjątku z kontekstem — zachowaj InnerException!
        static void OwijAjWyjatek()
        {
            try { throw new IOException("Błąd pliku"); }
            catch (IOException ex)
            {
                // DOBRZE: nowy wyjątek z InnerException
                throw new DomenaException("Nie można załadować konfiguracji", ex);
            }
        }

        try { OwijAjWyjatek(); }
        catch (DomenaException ex)
            { Console.WriteLine($"  Wrapped: {ex.Message} | Inner: {ex.InnerException?.Message}"); }
    }

    // ── 3. Filtry wyjątków when ───────────────────────────────────────────────

    public static void FilterryWyjatkow()
    {
        Console.WriteLine("\n── FilterryWyjatkow ──");

        // when — warunkowo łapie wyjątek BEZ unwindingu stosu gdy false
        // Pozwala na catch po właściwości wyjątku, bez try-catch wewnątrz catch
        static void ObsluzHttp(int kod)
        {
            var ex = new HttpRequestException($"HTTP {kod}");
            try { throw ex; }
            catch (HttpRequestException e) when (kod == 404)
                { Console.WriteLine($"  404 Not Found: {e.Message}"); }
            catch (HttpRequestException e) when (kod >= 500)
                { Console.WriteLine($"  Błąd serwera {kod}: {e.Message}"); }
            catch (HttpRequestException e)
                { Console.WriteLine($"  Inny HTTP {kod}: {e.Message}"); }
        }

        ObsluzHttp(404);
        ObsluzHttp(503);
        ObsluzHttp(401);

        // Sztuczka logowania — Loguj() zwraca false, więc catch NIE łapie wyjątku
        // ale efekt uboczny (logowanie) jest wykonany ZANIM stos się unwind
        static bool Loguj(Exception ex)
        {
            Console.WriteLine($"  [LOG] {ex.GetType().Name}: {ex.Message}");
            return false;  // ← false = catch nie wchodzi, wyjątek propaguje dalej
        }

        try
        {
            try { throw new InvalidOperationException("Testowy błąd"); }
            catch (Exception ex) when (Loguj(ex)) { /* nigdy tu nie wchodzimy */ }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Złapany po zalogowaniu: {ex.Message}");
        }
    }

    // ── 4. Guard pattern ──────────────────────────────────────────────────────

    public static void WzorzecGuard()
    {
        Console.WriteLine("\n── WzorzecGuard ──");

        // Guard centralizuje walidację argumentów — DRY, czytelne
        try { GuardOW.NieNull<string>(null, "parametr"); }
        catch (ArgumentNullException ex)
            { Console.WriteLine($"  NieNull: {ex.ParamName}"); }

        try { GuardOW.NiePusta("   ", "email"); }
        catch (ArgumentException ex)
            { Console.WriteLine($"  NiePusta: {ex.ParamName}"); }

        try { GuardOW.WZakresie(150, 0, 100, "procent"); }
        catch (ArgumentOutOfRangeException ex)
            { Console.WriteLine($"  WZakresie: {ex.ParamName}={ex.ActualValue}"); }

        // Zastosowanie w metodzie biznesowej
        static decimal ObliczRabat(string? kodKlienta, decimal cena, int procent)
        {
            GuardOW.NiePusta(kodKlienta, nameof(kodKlienta));
            GuardOW.WZakresie(cena, 0m, 1_000_000m, nameof(cena));
            GuardOW.WZakresie(procent, 0, 100, nameof(procent));
            return cena * (1 - procent / 100m);
        }

        decimal wynik = ObliczRabat("K001", 1000m, 15);
        Console.WriteLine($"  ObliczRabat: {wynik:C}");
    }

    // ── 5. Hierarchia wyjątków domenowych ────────────────────────────────────

    public static void HierarchiaWyjatkow()
    {
        Console.WriteLine("\n── HierarchiaWyjatkow ──");

        static void WyszukajKlienta(int id)
        {
            if (id <= 0)  throw new WalidacjaException("id", id, "ID musi być > 0");
            if (id == 99) throw new NieznalezionyException("Klient", id);
            if (id == 77) throw new BrakUprawnieniException("WyszukajKlienta");
            Console.WriteLine($"  Klient #{id} znaleziony");
        }

        foreach (int id in new[] { -1, 99, 77, 1 })
        {
            try { WyszukajKlienta(id); }
            catch (WalidacjaException ex)
                { Console.WriteLine($"  Walidacja [{ex.NazwaPola}={ex.NieprawidlowaWartosc}]: {string.Join(", ", ex.BledyWalidacji)}"); }
            catch (NieznalezionyException ex)
                { Console.WriteLine($"  NotFound: {ex.NazwaEncji} #{ex.Identyfikator}"); }
            catch (BrakUprawnieniException ex)
                { Console.WriteLine($"  Forbidden: {ex.Operacja}"); }
        }

        // Catch przez typ bazowy — łapie całą gałąź hierarchii
        try { throw new KonflikException("Duplikat email"); }
        catch (DomenaException ex)
            { Console.WriteLine($"  DomenaException ({ex.GetType().Name}): {ex.Message}"); }
    }

    // ── 6. InnerException i łańcuch ───────────────────────────────────────────

    public static void InnerExceptionChaining()
    {
        Console.WriteLine("\n── InnerExceptionChaining ──");

        static void AnalizujWyjatek(Exception ex, int poziom = 0)
        {
            string prefix = new string(' ', poziom * 2);
            Console.WriteLine($"{prefix}[{ex.GetType().Name}] {ex.Message}");
            if (ex.InnerException != null)
                AnalizujWyjatek(ex.InnerException, poziom + 1);
        }

        // Buduj łańcuch — zachowuj kontekst na każdej warstwie
        Exception lańcuch;
        try
        {
            try
            {
                try
                {
                    throw new IOException("Błąd odczytu pliku konfiguracji");
                }
                catch (IOException ex)
                {
                    throw new DomenaException("Nie można załadować konfiguracji", ex);
                }
            }
            catch (DomenaException ex)
            {
                throw new AplikacjaException("Inicjalizacja aplikacji nieudana", ex, "INIT_ERR");
            }
        }
        catch (AplikacjaException ex) { lańcuch = ex; }

        AnalizujWyjatek(lańcuch!);

        // GetBaseException() — najgłębsza przyczyna
        Console.WriteLine($"  GetBaseException: [{lańcuch!.GetBaseException().GetType().Name}] {lańcuch.GetBaseException().Message}");

        // exception.Data — słownik z metadanymi kontekstowymi
        var ex2 = new AplikacjaException("Błąd przetwarzania");
        ex2.Data["UserId"]    = 42;
        ex2.Data["Action"]    = "SaveOrder";
        ex2.Data["Timestamp"] = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"  Data: UserId={ex2.Data["UserId"]}, Action={ex2.Data["Action"]}");
    }

    // ── 7. Najlepsze praktyki ─────────────────────────────────────────────────

    public static void NajlepszyePraktyki()
    {
        Console.WriteLine("\n── NajlepszyePraktyki ──");

        // PRAKTYKA 1 — Nie połykaj wyjątków
        // ŹLE:  catch { }             // błąd znika bez śladu
        // DOBRZE: catch (Ex ex) { log; throw; }
        Console.WriteLine("  1. Nie połykaj: loguj i re-throw lub obsłuż świadomie");

        // PRAKTYKA 2 — TryParse zamiast catch dla oczekiwanych błędów
        // ŹLE:  try { int.Parse(s); } catch { return false; }
        string input = "abc";
        bool sukces = int.TryParse(input, out int _);
        Console.WriteLine($"  2. TryParse zamiast try-catch dla Parse: {sukces}");

        // PRAKTYKA 3 — re-throw z bare throw (nie throw ex)
        Console.WriteLine("  3. Re-throw: 'throw' zachowuje stack; 'throw ex' resetuje");

        // PRAKTYKA 4 — finally tylko do sprzątania, nie logiki biznesowej
        Console.WriteLine("  4. finally: zwalnianie zasobów, NIE logika warunkowa");

        // PRAKTYKA 5 — exception.Data dla kontekstu diagnozy
        static void PrzetwarzajZamowienie(int orderId)
        {
            try { throw new InvalidOperationException("Błąd"); }
            catch (Exception ex) { ex.Data["OrderId"] = orderId; ex.Data["Czas"] = DateTime.Now; throw; }
        }
        try { PrzetwarzajZamowienie(12345); }
        catch (Exception ex)
            { Console.WriteLine($"  5. Data: OrderId={ex.Data["OrderId"]}"); }

        // PRAKTYKA 6 — własne wyjątki dla domeny, wbudowane dla IO/sieci/parsowania
        Console.WriteLine("  6. Własne wyjątki dla domeny; ArgumentException/IOException dla infrastruktury");
    }

    // ── 8. Async wyjątki ──────────────────────────────────────────────────────

    public static async Task AsyncExceptions()
    {
        Console.WriteLine("\n── AsyncExceptions ──");

        // Task przechowuje wyjątek — await go rethrows
        static async Task<int> DzielAsync(int a, int b)
        {
            await Task.Delay(1);
            return b == 0 ? throw new DivideByZeroException() : a / b;
        }

        try { _ = await DzielAsync(10, 0); }
        catch (DivideByZeroException)
            { Console.WriteLine("  await rethrows wyjątek z Task"); }

        // Task.WhenAll — WSZYSTKIE wyjątki w AggregateException
        // ale await wyrzuca tylko pierwszy
        Task[] zadania = new[]
        {
            Task.Run(() => throw new InvalidOperationException("Błąd 1")),
            Task.Run(() => throw new ArgumentException("Błąd 2")),
        };
        Task wszystkie = Task.WhenAll(zadania);

        try { await wszystkie; } catch { }

        if (wszystkie.Exception != null)
        {
            Console.WriteLine($"  WhenAll.Exception.InnerExceptions ({wszystkie.Exception.InnerExceptions.Count}):");
            foreach (var e in wszystkie.Exception.InnerExceptions)
                Console.WriteLine($"    [{e.GetType().Name}] {e.Message}");
        }

        // Retry z exponential backoff
        static async Task<T> ZRetryAsync<T>(Func<Task<T>> operacja, int maxProby = 3)
        {
            for (int proba = 1; proba <= maxProby; proba++)
            {
                try { return await operacja(); }
                catch when (proba < maxProby)
                    { await Task.Delay(TimeSpan.FromMilliseconds(50 * proba)); }
            }
            return await operacja();  // ostatnia — niech rzuci
        }

        int licznik = 0;
        int wynik = await ZRetryAsync(async () =>
        {
            await Task.Delay(1);
            if (++licznik < 3) throw new IOException("Błąd sieciowy");
            return licznik;
        });
        Console.WriteLine($"  Retry: sukces w próbie #{wynik}");
    }

    // ── 9. Result<T> pattern ──────────────────────────────────────────────────

    public static void ResultPattern()
    {
        Console.WriteLine("\n── ResultPattern ──");

        // Result<T> — dla przewidywalnych błędów domenowych bez wyjątków
        static Result<decimal> ObliczCene(int ilosc, decimal cenaJednostkowa)
        {
            if (ilosc <= 0)         return Result<decimal>.Blad("Ilość musi być > 0", "INVALID_QTY");
            if (cenaJednostkowa < 0) return Result<decimal>.Blad("Cena ujemna",       "INVALID_PRICE");
            return Result<decimal>.Ok(ilosc * cenaJednostkowa);
        }

        // Map — transformuj w przypadku sukcesu
        Result<string> sformatowana = ObliczCene(5, 29.99m)
            .Map(cena => $"{cena:C2}");

        // Match — obsłuż oba przypadki
        Console.WriteLine("  " + sformatowana.Match(
            sukces => $"OK: {sukces}",
            blad   => $"Blad: {blad}"));

        // Błąd
        Console.WriteLine("  " + ObliczCene(-1, 10m).Match(
            sukces => $"OK: {sukces}",
            blad   => $"Blad [{ObliczCene(-1, 10m).KodBledu}]: {blad}"));

        // Łańcuchowanie Map
        static Result<string> FormatujFakture(int ilosc, decimal cena, string? klient) =>
            string.IsNullOrWhiteSpace(klient)
                ? Result<string>.Blad("Brak klienta")
                : ObliczCene(ilosc, cena).Map(total => $"Faktura dla {klient}: {total:C}");

        Console.WriteLine("  " + FormatujFakture(3, 50m, "Kowalski").Match(s => s, e => $"Blad: {e}"));
        Console.WriteLine("  " + FormatujFakture(3, 50m, null).Match(s => s, e => $"Blad: {e}"));
    }

    // ── 10. Globalny handler ──────────────────────────────────────────────────

    public static void GlobalnyHandler()
    {
        Console.WriteLine("\n── GlobalnyHandler ──");

        // Switch expression na typie wyjątku → HTTP status code
        // Centralny punkt obsługi wyjątków domenowych w API/middleware
        Exception[] testy =
        {
            new NieznalezionyException("Produkt", 42),
            new WalidacjaException("email", "niepoprawny", "Format email nieprawidłowy"),
            new BrakUprawnieniException("DeleteUser"),
            new KonflikException("Email już istnieje"),
            new OperationCanceledException(),
            new InvalidOperationException("Niespodziewany błąd"),
        };

        foreach (var ex in testy)
        {
            var (status, msg) = GlobalnyHandlerWyjatkow.Obsłuż(ex);
            Console.WriteLine($"  HTTP {status}: {msg}");
        }
    }
}
