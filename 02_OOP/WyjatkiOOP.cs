namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// WYJĄTKI W C#
// try/catch/finally, throw vs throw ex, filtry, własne wyjątki,
// Result<T>, Circuit Breaker, using/IDisposable
// ─────────────────────────────────────────────────────────────────────────────

public static class WyjatkiOOP
{
    // TRY/CATCH/FINALLY — podstawy
    public static void TryCatchFinally()
    {
        Console.WriteLine("\n=== TRY/CATCH/FINALLY ===");

        // Kolejność catch: od najbardziej szczegółowego do ogólnego!
        Console.WriteLine("Dobra kolejność catch:");
        try
        {
            throw new ArgumentNullException("param");
        }
        catch (ArgumentNullException ex)     // najpierw najbardziej szczegółowy
        {
            Console.WriteLine($"  ArgumentNullException: {ex.ParamName}");
        }
        catch (ArgumentException ex)         // potem bardziej ogólny
        {
            Console.WriteLine($"  ArgumentException: {ex.Message}");
        }
        catch (Exception)                    // na końcu najbardziej ogólny
        {
            Console.WriteLine("  Exception ogólny");
            throw;  // re-throw — zawsze zachowuj oryginalny stack trace!
        }
        finally
        {
            // Wykonuje się ZAWSZE:
            // • Po normalnym zakończeniu try
            // • Po obsłużeniu wyjątku przez catch
            // • Gdy żaden catch nie pasuje (przed rozwinięciem stosu wyżej)
            // NIE wykonuje się gdy: Environment.FailFast(), StackOverflow,
            //   zabicie procesu, wyjątek w innym wątku bez obsługi
            Console.WriteLine("  finally: zawsze tu dotrę");
        }

        // finally a return: finally wykonuje się PRZED faktycznym return
        Console.WriteLine($"\n  MetodaZReturn(): {MetodaZReturn()}");

        // Pułapka: wyjątek rzucony w finally zastępuje wyjątek z catch!
        Console.WriteLine("\nfinally a wyjątek w catch:");
        try
        {
            try
            {
                throw new Exception("pierwszy");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  catch: {ex.Message}");
                throw new Exception("drugi");   // nowy wyjątek z catch!
            }
            finally
            {
                Console.WriteLine("  finally wewnętrzny");
                // Jeśli rzucisz tu TRZECI wyjątek — "drugi" jest UTRACONY!
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  zewnętrzny catch: {ex.Message}"); // "drugi"
        }

        Console.WriteLine("\nZła kolejność catch → kompilator: CS0160 (dead code):");
        Console.WriteLine("  catch (Exception ex) { }        ← za ogólny na początku");
        Console.WriteLine("  catch (ArgumentNullException ex) { }  ← MARTWY KOD!");
    }

    private static int MetodaZReturn()
    {
        try
        {
            return 1;   // return jest "zapamiętany"...
        }
        finally
        {
            Console.WriteLine("  finally przed return");  // ...ale finally przed nim
            // return 2; — możliwe, ale zły styl — ukrywa oryginalny return
        }
    }

    // THROW VS THROW EX — najczęstszy błąd juniorów
    public static void ThrowVsThrowEx()
    {
        Console.WriteLine("\n=== THROW VS THROW EX ===");

        Console.WriteLine("throw ex — ZŁO! Resetuje stack trace:");
        Console.WriteLine("  catch (Exception ex) { LogujBlad(ex); throw ex; }");
        Console.WriteLine("  → Stack trace zaczyna się od linii 'throw ex'");
        Console.WriteLine("  → Tracisz informację gdzie błąd oryginalnie powstał!\n");

        Console.WriteLine("throw — DOBRZE! Zachowuje oryginalny stack trace:");
        Console.WriteLine("  catch (Exception ex) { LogujBlad(ex); throw; }");
        Console.WriteLine("  → Pełny oryginalny stack trace zachowany\n");

        Console.WriteLine("Owijanie wyjątku w domenowy z InnerException:");
        try
        {
            try
            {
                WczytajKonfiguracje("brak.json");
            }
            catch (KonfiguracjaExc ex)
            {
                Console.WriteLine($"  KonfiguracjaExc: {ex.Message}");
                Console.WriteLine($"  InnerException: {ex.InnerException?.GetType().Name}");
            }
        }
        catch { /* demo — ignorujemy */ }

        Console.WriteLine("\nExceptionDispatchInfo — re-throw z innego miejsca:");
        Console.WriteLine("  (np. z innego wątku, po await, z Task)");

        System.Runtime.ExceptionServices.ExceptionDispatchInfo? przechwycony = null;
        try { throw new InvalidOperationException("test ExceptionDispatchInfo"); }
        catch (Exception ex)
        {
            // Przechwytuje wyjątek z pełnym stack trace
            przechwycony = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
            Console.WriteLine($"  Przechwycono: {ex.Message}");
        }

        try
        {
            // Rzuca z oryginalnym stack trace + adnotacją gdzie ponownie rzucony
            przechwycony?.Throw();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Ponownie rzucony z oryginalnym trace: {ex.Message}");
        }
    }

    private static void WczytajKonfiguracje(string plik)
    {
        try
        {
            // Symulacja błędu wczytywania pliku
            throw new FileNotFoundException($"Nie znaleziono: {plik}");
        }
        catch (FileNotFoundException ex)
        {
            // Owijasz w domenowy wyjątek — dodajesz kontekst biznesowy
            // InnerException zachowuje oryginalny wyjątek z pełnym stack trace
            throw new KonfiguracjaExc($"Brak pliku konfiguracji: {plik}", ex);
        }
    }

    // EXCEPTION FILTERS (C# 6+)
    // Filtr ewaluowany w FAZIE 1 (search), nie fazie 2 (unwind)
    // Jeśli filtr zwróci false — stack trace NIE jest rozwijany → lepsze diagnostyki!
    public static void ExceptionFilters()
    {
        Console.WriteLine("\n=== EXCEPTION FILTERS (when) ===");

        Console.WriteLine("Podstawowe użycie — różna obsługa w zależności od warunku:");

        // Symulacja różnych kodów HTTP
        for (int i = 0; i < 3; i++)
        {
            int kod = i switch { 0 => 404, 1 => 401, _ => 500 };
            try
            {
                throw new HttpRequestException($"HTTP {kod}", null,
                    (System.Net.HttpStatusCode)kod);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"  404: Zasób nie istnieje — obsługuję gracefully");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"  401: Brak autoryzacji — odśwież token");
            }
            catch (HttpRequestException ex) when (ex.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            {
                Console.WriteLine($"  5xx: Błąd serwera {(int)ex.StatusCode!} — retry za chwilę");
            }
        }

        Console.WriteLine("\nFilter z loggingiem — NIGDY nie łapie, tylko loguje:");
        Console.WriteLine("  catch (Exception ex) when (ZalogujIZwrocFalse(ex))");
        Console.WriteLine("  { /* ten blok NIGDY nie wykona się — filtr zawsze false */ }");
        Console.WriteLine("  Filtr wykonuje się w fazie 1 — stos NIEROZWINIĘTY = kompletny trace!");

        Console.WriteLine("\nFilter z retry logic (exponential backoff):");
        Console.WriteLine("  int proba = 0;");
        Console.WriteLine("  catch (HttpRequestException ex)");
        Console.WriteLine("      when (proba++ < maxProb && CzyMoznaRetry(ex))");
        Console.WriteLine("  {");
        Console.WriteLine("      var opoznienie = TimeSpan.FromSeconds(Math.Pow(2, proba));");
        Console.WriteLine("      await Task.Delay(opoznienie); // nie wróć do catch — wróć do while(true)");
        Console.WriteLine("  }");

        Console.WriteLine("\nFiltr gdy NIE jest filtrem — if w catch:");
        Console.WriteLine("  catch (Exception ex) when (warunek) { }   // faza 1 — stos zachowany");
        Console.WriteLine("  catch (Exception ex) { if (warunek) { } } // faza 2 — stos rozwinięty");
    }

    private static bool ZalogujIZwrocFalseDemo(Exception ex)
    {
        Console.WriteLine($"  [DIAGNOZA] {ex.GetType().Name}: {ex.Message}");
        return false;  // nie obsługujemy — wyjątek idzie dalej
    }

    // WŁASNE WYJĄTKI — trzy konstruktory to STANDARD
    public static void WlasneWyjatki()
    {
        Console.WriteLine("\n=== WŁASNE WYJĄTKI ===");

        Console.WriteLine("Trzy konstruktory to STANDARD .NET:");
        Console.WriteLine("  1. ZamowienieExc(string message)");
        Console.WriteLine("  2. ZamowienieExc(string message, Exception inner)");
        Console.WriteLine("  3. protected ZamowienieExc(SerializationInfo, StreamingContext) — legacy\n");

        Console.WriteLine("Wyjątek z kontekstem domenowym:");
        try
        {
            WykonajPrzelew("PL12345", saldo: 500m, kwota: 800m);
        }
        catch (NiewystarczajacySaldoExc ex)
        {
            Console.WriteLine($"  Konto: {ex.NumerKonta}");
            Console.WriteLine($"  Saldo: {ex.Saldo:C}, Kwota: {ex.Kwota:C}");
            Console.WriteLine($"  Brakuje: {ex.Brakujace:C}");
        }

        Console.WriteLine("\nHierarchia własnych wyjątków:");
        Console.WriteLine("  AplikacjaExc (abstract)");
        Console.WriteLine("    ├── BazaDanychExc (KodBledu=DB001, CzyKrytyczny=true)");
        Console.WriteLine("    ├── WalidacjaExc  (KodBledu=VAL001, lista błędów)");
        Console.WriteLine("    └── KonfiguracjaExc (KodBledu=CFG001)");

        try
        {
            throw new WalidacjaExc(["Imię jest wymagane", "Email nieprawidłowy"]);
        }
        catch (WalidacjaExc ex)
        {
            Console.WriteLine($"\n  WalidacjaExc [{ex.KodBledu}]:");
            foreach (var blad in ex.Bledy)
                Console.WriteLine($"    • {blad}");
        }

        try
        {
            throw new BazaDanychExc("Timeout połączenia", "SELECT * FROM Users");
        }
        catch (BazaDanychExc ex)
        {
            Console.WriteLine($"\n  BazaDanychExc [{ex.KodBledu}] krytyczny={ex.CzyKrytyczny}:");
            Console.WriteLine($"    {ex.Message}");
            Console.WriteLine($"    Zapytanie: {ex.Zapytanie}");
        }

        Console.WriteLine("\nPolimorfizm wyjątków — łapanie klasy bazowej:");
        try
        {
            throw new BazaDanychExc("Błąd połączenia");
        }
        catch (AplikacjaExc ex)  // łapie WSZYSTKIE AplikacjaExc!
        {
            Console.WriteLine($"  AplikacjaExc: [{ex.KodBledu}] {ex.Message}");
        }

        Console.WriteLine("\nKiedy NIE tworzyć własnych wyjątków:");
        Console.WriteLine("  Gdy standardowy wyjątek wystarczy: ArgumentException, InvalidOperationException itp.");
    }

    private static void WykonajPrzelew(string numerKonta, decimal saldo, decimal kwota)
    {
        if (kwota > saldo)
            throw new NiewystarczajacySaldoExc(numerKonta, saldo, kwota);
    }

    // RESULT<T> — alternatywa dla wyjątków dla oczekiwanych błędów
    public static void ResultPattern()
    {
        Console.WriteLine("\n=== RESULT<T> PATTERN ===");
        Console.WriteLine("Oczekiwane błędy (brak danych, walidacja) NIE powinny być wyjątkami.");
        Console.WriteLine("Result<T> enkapsuluje wynik LUB błąd — bez rzucania.\n");

        // Podstawowe użycie
        var ok  = WynikOOP<int>.Ok(42);
        var err = WynikOOP<int>.Fail("Coś poszło nie tak");

        Console.WriteLine($"  Ok(42)   → {ok}");
        Console.WriteLine($"  Fail(..) → {err}");

        // Łańcuchowanie przez Then/Map
        Console.WriteLine("\nKompozycja przez Then (monadic bind):");
        var rezultat = PodzielBezpieczne(10, 3)
            .Then(w => FormatujWynik(w));
        Console.WriteLine($"  10/3 → {rezultat}");

        var blad = PodzielBezpieczne(10, 0)
            .Then(w => FormatujWynik(w));
        Console.WriteLine($"  10/0 → {blad}");

        // Dekonstrukcja
        Console.WriteLine("\nDekonstrukcja:");
        var (sukces, wartosc, opisBledu) = PodzielBezpieczne(10, 0);
        Console.WriteLine(sukces
            ? $"  OK: {wartosc}"
            : $"  BŁĄD: {opisBledu}");

        Console.WriteLine("\nResult<T> vs wyjątki:");
        Console.WriteLine("  Wyjątki → dla NIEOCZEKIWANYCH błędów (bug, OutOfMemory, sieć)");
        Console.WriteLine("  Result<T> → dla OCZEKIWANYCH scenariuszy (brak rekordu, walidacja)");
    }

    private static WynikOOP<decimal> PodzielBezpieczne(decimal a, decimal b)
        => b == 0
            ? WynikOOP<decimal>.Fail("Dzielenie przez zero")
            : WynikOOP<decimal>.Ok(a / b);

    private static WynikOOP<string> FormatujWynik(decimal w)
        => w > 1000
            ? WynikOOP<string>.Fail("Wynik zbyt duży")
            : WynikOOP<string>.Ok($"Wynik: {w:F4}");

    // CIRCUIT BREAKER — wzorzec odporności
    public static void CircuitBreakerDemo()
    {
        Console.WriteLine("\n=== CIRCUIT BREAKER ===");
        Console.WriteLine("Chroni system przed kaskadowym awariom przy niestabilnych usługach.\n");
        Console.WriteLine("Stany: CLOSED (działa) → OPEN (odrzuca) → HALF-OPEN (próba)\n");

        var breaker = new CircuitBreakerOOP(progBledow: 3, sekundyRestartu: 5);

        Console.WriteLine("Symulacja 4 błędów → breaker otwiera się:");
        for (int i = 0; i < 4; i++)
        {
            try
            {
                breaker.Wykonaj(() => throw new Exception("usługa niedostępna"));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OTWARTY"))
            {
                Console.WriteLine($"  Próba {i + 1}: {ex.Message}");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Próba {i + 1}: błąd ({ex.Message})");
            }
        }

        Console.WriteLine("\nW produkcyjnym kodzie używaj biblioteki Polly:");
        Console.WriteLine("  Policy.Handle<HttpRequestException>()");
        Console.WriteLine("        .CircuitBreaker(3, TimeSpan.FromSeconds(30))");

        Console.WriteLine("\nGlobal exception handler — łap wszystko co nie zostało obsłużone:");
        Console.WriteLine("  AppDomain.CurrentDomain.UnhandledException += (s, e) => { ... }");
        Console.WriteLine("  TaskScheduler.UnobservedTaskException += (s, e) => { e.SetObserved(); }");
    }

    // USING I IDISPOSABLE
    public static void UsingDemo()
    {
        Console.WriteLine("\n=== USING I IDISPOSABLE ===");
        Console.WriteLine("'using' to cukier składniowy dla try/finally z Dispose().\n");

        Console.WriteLine("Kompilator zamienia using w:");
        Console.WriteLine("  var res = new Zasob();");
        Console.WriteLine("  try { ... } finally { ((IDisposable)res).Dispose(); }\n");

        Console.WriteLine("using statement (klasyczny):");
        Console.WriteLine("  using (var plik = new StreamReader(\"dane.txt\"))");
        Console.WriteLine("  using (var writer = new StreamWriter(\"wynik.txt\"))  // łączenie bez {}");
        Console.WriteLine("  { ... }  // Dispose() wywołany na writer, potem na plik — ODWROTNA kolejność!\n");

        Console.WriteLine("using declaration (C# 8+) — bez nawiasów klamrowych:");
        Console.WriteLine("  using var plik = new StreamReader(\"dane.txt\");");
        Console.WriteLine("  using var writer = new StreamWriter(\"wynik.txt\");");
        Console.WriteLine("  } ← Dispose() przy końcu zakresu bloku/metody\n");

        Console.WriteLine("IDisposable demo (ZasobOOP):");
        using (var zasob = new ZasobOOP("polaczenie-1"))
        {
            zasob.WykonajOperacje("SELECT 1");
            zasob.WykonajOperacje("SELECT 2");
        }   // ← tu Dispose()

        Console.WriteLine("\nusing declaration — scope do końca bloku:");
        {
            using var zasob2 = new ZasobOOP("polaczenie-2");
            zasob2.WykonajOperacje("UPDATE users SET active=1");
        }   // ← tu Dispose()

        Console.WriteLine("\nIAsyncDisposable (C# 8+) — dla async zasobów:");
        Console.WriteLine("  await using var polaczenie = new AsyncPolaczenie();");
        Console.WriteLine("  await polaczenie.WykonajAsync(\"SELECT 1\");");
        Console.WriteLine("  } ← await DisposeAsync() tutaj\n");

        // IAsyncDisposable demo (sync context)
        var asyncZasob = new AsyncZasobOOP("async-conn");
        asyncZasob.WykonajAsync("async query").GetAwaiter().GetResult();
        asyncZasob.DisposeAsync().GetAwaiter().GetResult();

        Console.WriteLine("\nusing z null — bezpieczne:");
        Console.WriteLine("  using var czytnik = MozeZwrocicNull();");
        Console.WriteLine("  // Jeśli null — Dispose() NIE jest wywoływany (kompilator obsługuje to)");

        Console.WriteLine("\nObjectDisposedException.ThrowIf(_disposed, nameof(Klasa));");
        Console.WriteLine("  — sprawdzaj przed każdą operacją na zasobie");
    }

    // NULLABLE REFERENCE TYPES — adnotacje NRT
    public static void NullableReferenceTypesDemo()
    {
        Console.WriteLine("\n=== NULLABLE REFERENCE TYPES (NRT) ===");
        Console.WriteLine("Włączone przez <Nullable>enable</Nullable> w .csproj\n");

        Console.WriteLine("Problem: string s = null; s.Length → NullReferenceException w runtime");
        Console.WriteLine("  = 'The Billion Dollar Mistake' (Tony Hoare)\n");

        Console.WriteLine("Dwa światy po włączeniu NRT:");
        Console.WriteLine("  string  nienullowalna = \"hello\";  // kompilator ZAKŁADA że nigdy null");
        Console.WriteLine("  string? nullowalna    = null;      // kompilator WIE że może być null\n");

        Console.WriteLine("Null state analysis — kompilator śledzi stan:");
        string? s = GetStringDemo();
        // Bezpośrednie użycie: CS8602 — możliwy null dereference
        if (s != null)
            Console.WriteLine($"  Po sprawdzeniu: s.Length = {s.Length}");  // OK

        int? len = s?.Length;  // null-conditional — też OK
        Console.WriteLine($"  s?.Length = {len?.ToString() ?? "null"}");

        Console.WriteLine("\nNull-forgiving operator ! — używaj RZADKO:");
        Console.WriteLine("  string naGwarancje = mozeNull!;  // 'zaufaj mi, nie jest null'");
        Console.WriteLine("  var bezNull = lista.Where(x => x != null).Select(x => x!);");

        Console.WriteLine("\nAdnotacje dla własnych metod:");
        Console.WriteLine("  [NotNullWhen(true)] out string? name");
        Console.WriteLine("  → Kompilator wie że name != null gdy metoda zwraca true\n");

        if (TryGetNameDemo(1, out string? n))
            Console.WriteLine($"  TryGetName(1): '{n}' — kompilator wie że n != null tu");

        Console.WriteLine("\n  [MemberNotNull(nameof(_pole))]");
        Console.WriteLine("  → Kompilator wie że pole != null po wywołaniu tej metody");

        Console.WriteLine("\nGenerics i NRT:");
        Console.WriteLine("  T? gdzie T : class  → T | null (reference type)");
        Console.WriteLine("  T? gdzie T : struct → Nullable<T> (value type)");
        Console.WriteLine("  T bez constraintu   → T? działa dla obu (C# 8+)");
    }

    private static string? GetStringDemo() => null;

    private static bool TryGetNameDemo(int id,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name)
    {
        name = id > 0 ? "Kacper" : null;
        return name != null;
    }
}

// ─── WŁASNE WYJĄTKI ───────────────────────────────────────────────────────────

// Minimalny poprawny wyjątek — trzy konstruktory to STANDARD .NET
[Serializable]
internal class ZamowienieExc : Exception
{
    public ZamowienieExc(string message) : base(message) { }
    public ZamowienieExc(string message, Exception inner) : base(message, inner) { }
#pragma warning disable SYSLIB0051
    protected ZamowienieExc(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
#pragma warning restore SYSLIB0051
}

// Wyjątek z kontekstem domenowym
internal class NiewystarczajacySaldoExc : Exception
{
    public string NumerKonta { get; }
    public decimal Saldo     { get; }
    public decimal Kwota     { get; }
    public decimal Brakujace => Kwota - Saldo;

    public NiewystarczajacySaldoExc(string numerKonta, decimal saldo, decimal kwota)
        : base($"Niewystarczające saldo na koncie {numerKonta}. " +
               $"Wymagane: {kwota:C}, dostępne: {saldo:C}, brakuje: {kwota - saldo:C}")
    {
        NumerKonta = numerKonta;
        Saldo = saldo;
        Kwota = kwota;
    }

    public NiewystarczajacySaldoExc(string numerKonta, decimal saldo, decimal kwota, Exception inner)
        : base($"Niewystarczające saldo: {kwota - saldo:C} brakuje", inner)
    {
        NumerKonta = numerKonta;
        Saldo = saldo;
        Kwota = kwota;
    }
}

// Hierarchia własnych wyjątków — bazowa klasa domeny
internal abstract class AplikacjaExc : Exception
{
    public string KodBledu  { get; }
    public bool CzyKrytyczny { get; }

    protected AplikacjaExc(string kodBledu, string message, bool krytyczny = false)
        : base(message) { KodBledu = kodBledu; CzyKrytyczny = krytyczny; }

    protected AplikacjaExc(string kodBledu, string message, Exception inner, bool krytyczny = false)
        : base(message, inner) { KodBledu = kodBledu; CzyKrytyczny = krytyczny; }
}

internal class BazaDanychExc : AplikacjaExc
{
    public string? Zapytanie { get; }

    public BazaDanychExc(string message, string? zapytanie = null)
        : base("DB001", message, krytyczny: true) => Zapytanie = zapytanie;

    public BazaDanychExc(string message, Exception inner, string? zapytanie = null)
        : base("DB001", message, inner, krytyczny: true) => Zapytanie = zapytanie;
}

internal class WalidacjaExc : AplikacjaExc
{
    public IReadOnlyList<string> Bledy { get; }

    public WalidacjaExc(IEnumerable<string> bledy)
        : base("VAL001", $"Walidacja nieudana: {string.Join(", ", bledy)}")
        => Bledy = bledy.ToList().AsReadOnly();
}

internal class KonfiguracjaExc : AplikacjaExc
{
    public KonfiguracjaExc(string message, Exception? inner = null)
        : base("CFG001", message, inner ?? new Exception(), krytyczny: true) { }
}

// ─── RESULT<T> ────────────────────────────────────────────────────────────────

internal readonly struct WynikOOP<T>
{
    private readonly T?      _value;
    private readonly string? _error;

    private WynikOOP(T value)      { _value = value; _error = null;  IsSuccess = true; }
    private WynikOOP(string error) { _value = default; _error = error; IsSuccess = false; }

    public bool IsSuccess  { get; }
    public bool IsFailure  => !IsSuccess;
    public T     Value => IsSuccess ? _value! : throw new InvalidOperationException(_error);
    public string Error => IsFailure ? _error! : throw new InvalidOperationException("Brak błędu");

    public static WynikOOP<T> Ok(T value)   => new(value);
    public static WynikOOP<T> Fail(string e) => new(e);

    // Monadic bind — komponowanie operacji Result
    public WynikOOP<TNext> Then<TNext>(Func<T, WynikOOP<TNext>> nastepna)
        => IsSuccess ? nastepna(_value!) : WynikOOP<TNext>.Fail(_error!);

    // Map — transformacja wartości sukcesu
    public WynikOOP<TNext> Map<TNext>(Func<T, TNext> transformacja)
        => IsSuccess ? WynikOOP<TNext>.Ok(transformacja(_value!)) : WynikOOP<TNext>.Fail(_error!);

    // Dekonstrukcja — wygodne unpacking
    public void Deconstruct(out bool sukces, out T? wartosc, out string? blad)
        => (sukces, wartosc, blad) = (IsSuccess, _value, _error);

    public override string ToString()
        => IsSuccess ? $"Ok({_value})" : $"Fail({_error})";
}

// ─── CIRCUIT BREAKER ─────────────────────────────────────────────────────────

internal class CircuitBreakerOOP
{
    private int _bledyZRzedu  = 0;
    private bool _otwarty     = false;
    private DateTime _czasOtwarcia;

    private readonly int _progBledow;
    private readonly TimeSpan _czasRestartu;

    public CircuitBreakerOOP(int progBledow = 3, int sekundyRestartu = 30)
    {
        _progBledow = progBledow;
        _czasRestartu = TimeSpan.FromSeconds(sekundyRestartu);
    }

    public void Wykonaj(Action operacja)
    {
        if (_otwarty)
        {
            if (DateTime.UtcNow - _czasOtwarcia < _czasRestartu)
                throw new InvalidOperationException("Circuit breaker OTWARTY — usługa niedostępna");

            // Half-open: spróbuj jedną operację
            _otwarty = false;
            Console.WriteLine("  Circuit breaker HALF-OPEN — próba");
        }

        try
        {
            operacja();
            _bledyZRzedu = 0;  // sukces — resetuj licznik
        }
        catch
        {
            _bledyZRzedu++;
            if (_bledyZRzedu >= _progBledow)
            {
                _otwarty = true;
                _czasOtwarcia = DateTime.UtcNow;
                Console.WriteLine($"  Circuit breaker OTWARTY po {_bledyZRzedu} błędach!");
            }
            throw;
        }
    }
}

// ─── USING / IDISPOSABLE ─────────────────────────────────────────────────────

// IDisposable — pełny wzorzec dla zasobu niezarządzanego
internal sealed class ZasobOOP : IDisposable
{
    private readonly string _nazwa;
    private bool _disposed;

    public ZasobOOP(string nazwa)
    {
        _nazwa = nazwa;
        Console.WriteLine($"  [{_nazwa}] otwarty");
    }

    public void WykonajOperacje(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(ZasobOOP));
        Console.WriteLine($"  [{_nazwa}] wykonuję: {sql}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Console.WriteLine($"  [{_nazwa}] zamknięty (Dispose)");
    }
}

// IAsyncDisposable — dla zasobów wymagających async cleanup (C# 8+)
internal sealed class AsyncZasobOOP : IAsyncDisposable
{
    private readonly string _nazwa;
    private bool _disposed;

    public AsyncZasobOOP(string nazwa)
    {
        _nazwa = nazwa;
        Console.WriteLine($"  [{_nazwa}] otwarty (async)");
    }

    public async Task WykonajAsync(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(AsyncZasobOOP));
        await Task.Delay(1);  // symulacja async I/O
        Console.WriteLine($"  [{_nazwa}] async: {sql}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await Task.Delay(1);  // async cleanup
        _disposed = true;
        Console.WriteLine($"  [{_nazwa}] zamknięty (DisposeAsync)");
    }
}
