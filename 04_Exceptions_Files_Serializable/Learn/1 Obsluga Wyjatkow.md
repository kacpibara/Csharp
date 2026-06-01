### Obsługa wyjątków w C#

---

### 1. Podstawy — try/catch/finally

csharp

```csharp
// Podstawowa struktura
try
{
    // Kod który może rzucić wyjątek
    int wynik = 10 / 0;
}
catch (DivideByZeroException ex)
{
    // Obsługa konkretnego wyjątku
    Console.WriteLine($"Dzielenie przez zero: {ex.Message}");
}
catch (Exception ex)
{
    // Obsługa WSZYSTKICH pozostałych wyjątków
    // ZAWSZE na końcu — bardziej ogólny po bardziej szczegółowym!
    Console.WriteLine($"Nieoczekiwany błąd: {ex.Message}");
}
finally
{
    // Wykonuje się ZAWSZE — czy był wyjątek czy nie
    // Idealne do zwalniania zasobów
    Console.WriteLine("Finally — zawsze wykonane");
}

// Wiele catch — od najbardziej szczegółowego do ogólnego
try
{
    string? s = null;
    Console.WriteLine(s!.Length);  // NullReferenceException
}
catch (ArgumentNullException ex)
{
    Console.WriteLine($"Null argument: {ex.ParamName}");
}
catch (NullReferenceException ex)
{
    Console.WriteLine($"Null reference: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Nieprawidłowa operacja: {ex.Message}");
}
catch (Exception ex) when (ex.Message.Contains("specjalny"))  // exception filter
{
    Console.WriteLine($"Specjalny przypadek: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ogólny błąd: {ex.GetType().Name}: {ex.Message}");
}

// finally — gwarancja wykonania (ale nie przy Environment.FailFast lub StackOverflow)
StreamReader? reader = null;
try
{
    reader = new StreamReader("plik.txt");
    string tresc = reader.ReadToEnd();
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Plik nie znaleziony: {ex.FileName}");
}
catch (IOException ex)
{
    Console.WriteLine($"Błąd I/O: {ex.Message}");
}
finally
{
    reader?.Dispose();  // ZAWSZE zwalniaj zasoby w finally
    Console.WriteLine("Zasoby zwolnione");
}

// using — nowoczesna alternatywa dla try/finally z IDisposable
using (var reader2 = new StreamReader("plik.txt"))
{
    string tresc = reader2.ReadToEnd();
}  // Dispose() wywołane automatycznie

// Lub jeszcze krócej (C# 8+)
using var reader3 = new StreamReader("plik.txt");
string tresc3 = reader3.ReadToEnd();
// Dispose() na końcu scope'u metody
```

---

### 2. throw — rzucanie wyjątków

csharp

```csharp
// throw — rzuca wyjątek
// throw ex — RESETUJE stack trace! Traci informację o miejscu błędu
// throw — re-throw, ZACHOWUJE oryginalny stack trace

public void PrzykladThrow(int wartosc)
{
    if (wartosc < 0)
        throw new ArgumentOutOfRangeException(
            nameof(wartosc),
            wartosc,
            "Wartość musi być nieujemna");

    if (wartosc == 0)
        throw new ArgumentException("Wartość nie może być zerem", nameof(wartosc));
}

// Re-throw — zachowanie stack trace
public void Przetworz(string dane)
{
    try
    {
        WykonajOperacje(dane);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Logowanie błędu: {ex.Message}");

        throw;        // ✅ re-throw — zachowuje stack trace
        // throw ex;  // ❌ ŹLE — resetuje stack trace, traci miejsce błędu!
    }
}

// Throw expression — w wyrażeniach (C# 7+)
public class Konfiguracja
{
    private readonly string _polaczenie;

    public Konfiguracja(string? polaczenie) =>
        _polaczenie = polaczenie
            ?? throw new ArgumentNullException(nameof(polaczenie),
               "Connection string jest wymagany");

    public string Pobierz(string klucz) =>
        string.IsNullOrEmpty(klucz)
            ? throw new ArgumentException("Klucz nie może być pusty", nameof(klucz))
            : $"Wartość_{klucz}";
}

// Walidacja przez ThrowHelper — wzorzec z .NET runtime
// Zamiast powtarzać warunki — centralizujesz walidację
public static class Guard
{
    public static T NieNull<T>(T? wartosc, string nazwaParametru)
        where T : class =>
        wartosc ?? throw new ArgumentNullException(nazwaParametru);

    public static string NiePusta(string? wartosc, string nazwaParametru)
    {
        if (string.IsNullOrWhiteSpace(wartosc))
            throw new ArgumentException(
                "Wartość nie może być pusta lub składać się wyłącznie z białych znaków",
                nazwaParametru);
        return wartosc;
    }

    public static T WZakresie<T>(T wartosc, T min, T max, string nazwaParametru)
        where T : IComparable<T>
    {
        if (wartosc.CompareTo(min) < 0 || wartosc.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(
                nazwaParametru, wartosc,
                $"Wartość musi być między {min} a {max}");
        return wartosc;
    }
}

// Użycie Guard
public class ZamowienieSerwis
{
    public void Dodaj(string klientId, decimal kwota, int ilosc)
    {
        Guard.NiePusta(klientId, nameof(klientId));
        Guard.WZakresie(kwota, 0.01m, 100_000m, nameof(kwota));
        Guard.WZakresie(ilosc, 1, 999, nameof(ilosc));

        Console.WriteLine($"Zamówienie: {klientId}, {kwota:C}, {ilosc} szt.");
    }
}

void WykonajOperacje(string d) => Console.WriteLine(d);
```

---

### 3. Exception filters — when

csharp

```csharp
// when — filtr wyjątków — przechwytuj tylko gdy warunek jest spełniony
// NIE unwinds stack gdy warunek jest false — wyjątek leci dalej

public async Task<string> PobierzDaneAsync(string url)
{
    try
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        // Przechwytuj TYLKO 404
        return string.Empty;
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        throw new UnauthorizedAccessException("Brak uprawnień do zasobu", ex);
    }
    catch (HttpRequestException ex) when (IsTransientError(ex))
    {
        // Przejściowy błąd — można ponowić
        Console.WriteLine($"Błąd przejściowy: {ex.Message}");
        throw;
    }
    // HttpRequestException bez filtra — inne kody odpowiedzi
}

bool IsTransientError(HttpRequestException ex) =>
    ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                  or System.Net.HttpStatusCode.GatewayTimeout
                  or System.Net.HttpStatusCode.TooManyRequests;

// when dla logowania BEZ przechwytywania
// Sprytny trick — zawsze zwraca false, więc catch nie przechwytuje
// ale pozwala uruchomić kod (np. logowanie) przed unwindem!
public void MetodaZLogowaniem()
{
    try
    {
        RyzykownaOperacja();
    }
    catch (Exception ex) when (Loguj(ex))
    {
        // Ten catch NIGDY nie jest wykonany (Loguj zwraca false)
        // Ale Loguj() jest wywołany z pełnym stack trace!
    }
}

bool Loguj(Exception ex)
{
    Console.WriteLine($"[LOG przed unwind] {ex.GetType().Name}: {ex.Message}");
    return false;  // nie przechwytuj — wyjątek leci dalej
}

void RyzykownaOperacja() => throw new Exception("Test");
```

---

### 4. Własne wyjątki — hierarchia i projektowanie

csharp

```csharp
// Własny wyjątek — dziedziczy po Exception (lub bardziej szczegółowym)
public class AplikacjaException : Exception
{
    // Zawsze implementuj te 3 konstruktory!
    public AplikacjaException() { }

    public AplikacjaException(string message)
        : base(message) { }

    public AplikacjaException(string message, Exception innerException)
        : base(message, innerException) { }
}

// Własny wyjątek z dodatkowymi danymi
public class WalidacjaException : AplikacjaException
{
    public string NazwaPola { get; }
    public object? NieprawidlowaWartosc { get; }
    public IReadOnlyList<string> BledyWalidacji { get; }

    public WalidacjaException(
        string nazwaLola,
        object? nieprawidlowaWartosc,
        IEnumerable<string> bledy)
        : base($"Walidacja pola '{nazwaLola}' nieudana: " +
               string.Join("; ", bledy))
    {
        NazwaPola            = nazwaLola;
        NieprawidlowaWartosc = nieprawidlowaWartosc;
        BledyWalidacji       = bledy.ToList().AsReadOnly();
    }

    public WalidacjaException(string nazwaPola, string blad)
        : this(nazwaPola, null, new[] { blad }) { }
}

// Hierarchia wyjątków domeny
public class DomenaException : AplikacjaException
{
    public string KodBledu { get; }
    public DateTime CzasBledu { get; } = DateTime.UtcNow;

    public DomenaException(string kodBledu, string message)
        : base(message) => KodBledu = kodBledu;

    public DomenaException(string kodBledu, string message, Exception inner)
        : base(message, inner) => KodBledu = kodBledu;
}

public class NieznalezionyException : DomenaException
{
    public string TypZasobu { get; }
    public object Identyfikator { get; }

    public NieznalezionyException(string typZasobu, object id)
        : base("RESOURCE_NOT_FOUND",
               $"{typZasobu} o identyfikatorze '{id}' nie istnieje")
    {
        TypZasobu     = typZasobu;
        Identyfikator = id;
    }
}

public class KonflikException : DomenaException
{
    public KonflikException(string message)
        : base("CONFLICT", message) { }

    public KonflikException(string message, Exception inner)
        : base("CONFLICT", message, inner) { }
}

public class BrakUprawnieniException : DomenaException
{
    public string Operacja { get; }
    public string Uzytkownik { get; }

    public BrakUprawnieniException(string uzytkownik, string operacja)
        : base("FORBIDDEN",
               $"Użytkownik '{uzytkownik}' nie ma uprawnień do: {operacja}")
    {
        Uzytkownik = uzytkownik;
        Operacja   = operacja;
    }
}

// Użycie hierarchii wyjątków
public class KlientSerwis
{
    private readonly Dictionary<int, string> _klienci = new()
    {
        { 1, "Anna Kowalska" },
        { 2, "Bartek Nowak" }
    };

    public string PobierzKlienta(int id)
    {
        if (!_klienci.TryGetValue(id, out string? klient))
            throw new NieznalezionyException("Klient", id);
        return klient;
    }

    public void AktualizujEmail(int id, string email, string uzytkownik)
    {
        if (!_klienci.ContainsKey(id))
            throw new NieznalezionyException("Klient", id);

        if (uzytkownik != "admin")
            throw new BrakUprawnieniException(uzytkownik, "aktualizacja emaila klienta");

        if (!email.Contains('@'))
            throw new WalidacjaException("email", email,
                new[] { "Email musi zawierać znak @" });

        Console.WriteLine($"Email zaktualizowany dla klienta {id}");
    }
}
```

---

### 5. InnerException — łańcuch wyjątków

csharp

```csharp
// InnerException — oryginalny wyjątek schowany w nowym
// Pozwala dodać kontekst nie tracąc oryginalnej przyczyny

public class BazaDanychSerwis
{
    public List<string> PobierzDane(string zapytanie)
    {
        try
        {
            // Symulacja błędu bazy
            throw new Exception("Connection timeout after 30s");
        }
        catch (Exception ex)
        {
            // Owijamy w własny wyjątek — zachowując oryginał
            throw new DomenaException(
                "DB_ERROR",
                $"Błąd przy wykonaniu zapytania: {zapytanie}",
                ex);  // ← InnerException!
        }
    }
}

// Analiza łańcucha wyjątków
void AnalizujWyjatek(Exception ex, int poziom = 0)
{
    string wcięcie = new string(' ', poziom * 2);
    Console.WriteLine($"{wcięcie}[{poziom}] {ex.GetType().Name}: {ex.Message}");

    if (ex.InnerException != null)
        AnalizujWyjatek(ex.InnerException, poziom + 1);

    if (ex is AggregateException ae)
        foreach (var inner in ae.InnerExceptions)
            AnalizujWyjatek(inner, poziom + 1);
}

// Exception.GetBaseException() — najgłębszy InnerException
try
{
    new BazaDanychSerwis().PobierzDane("SELECT * FROM users");
}
catch (Exception ex)
{
    AnalizujWyjatek(ex);
    // [0] DomenaException: Błąd przy wykonaniu zapytania: SELECT * FROM users
    //   [1] Exception: Connection timeout after 30s

    Console.WriteLine($"\nPierwotna przyczyna: {ex.GetBaseException().Message}");
    // Connection timeout after 30s
}
```

---

### 6. Best practices — co robić i czego unikać

csharp

```csharp
// ❌ ŹLE — catch-all bez obsługi
public string ZlaPraktyka1(string url)
{
    try
    {
        return File.ReadAllText(url);
    }
    catch (Exception)  // połyka KAŻDY wyjątek w ciszy
    {
        return "";  // i skąd wiemy co poszło nie tak?
    }
}

// ✅ DOBRZE — przechwytuj konkretne, loguj, obsługuj
public string DobraPraktyka1(string sciezka)
{
    try
    {
        return File.ReadAllText(sciezka);
    }
    catch (FileNotFoundException)
    {
        Console.WriteLine($"Plik nie istnieje: {sciezka}");
        return string.Empty;
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine($"Brak dostępu do pliku: {sciezka}");
        throw;  // re-throw — nie wiesz jak obsłużyć, niech wyżej decyduje
    }
}

// ❌ ŹLE — używanie wyjątków do flow control
public int ZlaPraktyka2(string s)
{
    try
    {
        return int.Parse(s);
    }
    catch (FormatException)
    {
        return -1;  // wyjątki do sterowania przepływem = antypattern!
    }
}

// ✅ DOBRZE — TryParse zamiast wyjątku
public int DobraPraktyka2(string s) =>
    int.TryParse(s, out int wynik) ? wynik : -1;

// ❌ ŹLE — throw ex resetuje stack trace
public void ZlaPraktyka3()
{
    try { RyzykownaOperacja(); }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        throw ex;  // ŹLE! Traci oryginalny stack trace
    }
}

// ✅ DOBRZE — throw zachowuje stack trace
public void DobraPraktyka3()
{
    try { RyzykownaOperacja(); }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        throw;  // re-throw — zachowuje pełny stack trace
    }
}

// ❌ ŹLE — połykanie wyjątku w finally
public void ZlaPraktyka4()
{
    try
    {
        throw new Exception("Oryginalny błąd");
    }
    finally
    {
        throw new Exception("Błąd w finally");
        // Oryginalny wyjątek jest ZAGUBIONY!
    }
}

// ✅ DOBRZE — ostrożny finally
public void DobraPraktyka4()
{
    try
    {
        RyzykownaOperacja();
    }
    finally
    {
        try
        {
            // Operacja czyszcząca która może rzucić
            ZwolnijZasoby();
        }
        catch (Exception ex)
        {
            // Zaloguj ale nie rzucaj — nie zgub oryginalnego wyjątku
            Console.WriteLine($"Błąd podczas zwalniania zasobów: {ex.Message}");
        }
    }
}

void ZwolnijZasoby() => Console.WriteLine("Zasoby zwolnione");

// ❌ ŹLE — pusta właściwość Message w własnym wyjątku
public class ZlyWyjatek : Exception
{
    // Brak konstruktorów z message — użytkownik nie wie co się stało
}

// ✅ DOBRZE — znaczące komunikaty
public class DobryWyjatek : Exception
{
    public DobryWyjatek() : base("Operacja nieudana z powodu...") { }
    public DobryWyjatek(string message) : base(message) { }
    public DobryWyjatek(string message, Exception inner) : base(message, inner) { }
}

// Exception Data — dodatkowy kontekst bez własnej klasy
public void DodajKontekstDoWyjatku()
{
    try
    {
        throw new InvalidOperationException("Błąd przetwarzania");
    }
    catch (Exception ex)
    {
        ex.Data["UserId"]    = 42;
        ex.Data["Operation"] = "ProcessOrder";
        ex.Data["Timestamp"] = DateTime.UtcNow;
        throw;
    }
}
```

---

### 7. Obsługa wyjątków w async

csharp

```csharp
// Wyjątki w async są przechwytywane w Task i rzucane przy await

public async Task<string> AsyncWyjatek()
{
    await Task.Delay(100);
    throw new InvalidOperationException("Błąd async");
}

// Podstawowe przechwytywanie
try
{
    await AsyncWyjatek();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Async błąd: {ex.Message}");
}

// Task.WhenAll — AggregateException z wieloma błędami
async Task<int> MozeRzucic(int id)
{
    await Task.Delay(100);
    if (id % 2 == 0) throw new Exception($"Błąd dla {id}");
    return id;
}

var tasks = Enumerable.Range(1, 6).Select(MozeRzucic).ToList();

// Zbierz WSZYSTKIE wyniki i błędy
Task<int[]> wszystkie = Task.WhenAll(tasks);
try
{
    int[] wyniki2 = await wszystkie;
}
catch
{
    // wszystkie.Exception zawiera WSZYSTKIE błędy
    foreach (var ex in wszystkie.Exception!.InnerExceptions)
        Console.WriteLine($"  • {ex.Message}");

    // Wyniki udanych tasks
    var udane = tasks
        .Where(t => t.IsCompletedSuccessfully)
        .Select(t => t.Result);
    Console.WriteLine($"Udane: {string.Join(", ", udane)}");
}

// async void — wyjątki trudne do przechwycenia!
async void NiebezpiecznaAsyncVoid()
{
    try  // ZAWSZE w try-catch dla async void!
    {
        await Task.Delay(100);
        throw new Exception("Wyjątek z async void");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Obsłużono w async void: {ex.Message}");
        // Bez tego try-catch — wyjątek crashuje aplikację!
    }
}
```

---

### 8. Praktyczny przykład — kompletny system obsługi błędów

csharp

```csharp
// Result<T> — alternatywa dla wyjątków w przewidywalnych przypadkach
public class Result<T>
{
    public bool Sukces { get; }
    public T? Wartosc { get; }
    public string? BladKomunikat { get; }
    public Exception? Wyjatek { get; }

    private Result(T wartosc)
    {
        Sukces   = true;
        Wartosc  = wartosc;
    }

    private Result(string blad, Exception? ex = null)
    {
        Sukces         = false;
        BladKomunikat  = blad;
        Wyjatek        = ex;
    }

    public static Result<T> Ok(T wartosc) => new(wartosc);
    public static Result<T> Blad(string komunikat, Exception? ex = null) =>
        new(komunikat, ex);

    public Result<TNew> Map<TNew>(Func<T, TNew> transform) =>
        Sukces ? Result<TNew>.Ok(transform(Wartosc!)) :
                 Result<TNew>.Blad(BladKomunikat!, Wyjatek);

    public void Match(Action<T> onSukces, Action<string> onBlad)
    {
        if (Sukces) onSukces(Wartosc!);
        else        onBlad(BladKomunikat!);
    }

    public override string ToString() =>
        Sukces ? $"Ok({Wartosc})" : $"Błąd({BladKomunikat})";
}

// Globalny handler wyjątków — dla aplikacji
public class GlobalnyHandlerWyjatkow
{
    // ASP.NET Core middleware
    public async Task ObsluzAsync(Exception ex, HttpContext? context = null)
    {
        (int statusCode, string komunikat) = ex switch
        {
            NieznalezionyException e     => (404, $"Nie znaleziono: {e.TypZasobu} #{e.Identyfikator}"),
            WalidacjaException e         => (400, $"Błąd walidacji: {string.Join(", ", e.BledyWalidacji)}"),
            BrakUprawnieniException e    => (403, $"Brak uprawnień: {e.Operacja}"),
            KonflikException e           => (409, e.Message),
            DomenaException e            => (422, e.Message),
            ArgumentException e          => (400, $"Błędny argument: {e.Message}"),
            OperationCanceledException   => (499, "Żądanie anulowane"),
            _                            => (500, "Wewnętrzny błąd serwera")
        };

        Console.WriteLine($"[ERROR {statusCode}] {ex.GetType().Name}: {ex.Message}");

        if (context != null)
        {
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                blad = komunikat,
                typ  = ex.GetType().Name,
                czas = DateTime.UtcNow
            });
        }
    }
}

// Kompletny serwis z pełną obsługą błędów
public class ZamowieniaApiSerwis
{
    private readonly KlientSerwis _klientSerwis = new();
    private readonly GlobalnyHandlerWyjatkow _handler = new();

    public async Task<Result<string>> UtworzZamowienieAsync(
        int klientId,
        decimal kwota,
        string uzytkownik,
        CancellationToken ct = default)
    {
        try
        {
            // Walidacja wejścia
            if (kwota <= 0)
                throw new WalidacjaException("kwota", kwota,
                    new[] { "Kwota musi być większa od zera" });

            // Sprawdź klienta
            string klient = _klientSerwis.PobierzKlienta(klientId);

            // Sprawdź uprawnienia
            if (uzytkownik != "admin" && kwota > 10_000m)
                throw new BrakUprawnieniException(uzytkownik,
                    "tworzenie zamówień powyżej 10 000 PLN");

            // Symulacja zapisu
            await Task.Delay(100, ct);

            string potwierdzenie = $"ZAM-{DateTime.Now:yyyyMMdd}-{klientId:D4}";
            Console.WriteLine($"✓ Zamówienie {potwierdzenie} dla '{klient}': {kwota:C}");

            return Result<string>.Ok(potwierdzenie);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operacja anulowana przez użytkownika");
            throw;  // anulowanie propagujemy dalej
        }
        catch (DomenaException ex)
        {
            // Błędy domeny — przewidywalne, zwracamy jako Result
            Console.WriteLine($"Błąd domeny [{ex.KodBledu}]: {ex.Message}");
            return Result<string>.Blad(ex.Message, ex);
        }
        catch (Exception ex)
        {
            // Nieoczekiwany błąd — loguj szczegółowo, zwróć ogólny komunikat
            await _handler.ObsluzAsync(ex);
            return Result<string>.Blad("Nieoczekiwany błąd — spróbuj ponownie");
        }
    }
}

// Fake HttpContext dla przykładu
class HttpContext
{
    public HttpResponse Response { get; } = new();
}
class HttpResponse
{
    public int StatusCode { get; set; }
    public Task WriteAsJsonAsync(object obj)
    {
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(obj));
        return Task.CompletedTask;
    }
}

// Demonstracja
var serwis = new ZamowieniaApiSerwis();

// Przypadek 1 — sukces
var wynik1 = await serwis.UtworzZamowienieAsync(1, 500m, "admin");
wynik1.Match(
    nr => Console.WriteLine($"Zamówienie: {nr}"),
    err => Console.WriteLine($"Błąd: {err}"));

// Przypadek 2 — nie znaleziono klienta
var wynik2 = await serwis.UtworzZamowienieAsync(99, 100m, "admin");
wynik2.Match(
    nr => Console.WriteLine($"Zamówienie: {nr}"),
    err => Console.WriteLine($"Błąd: {err}"));

// Przypadek 3 — brak uprawnień
var wynik3 = await serwis.UtworzZamowienieAsync(1, 50_000m, "user");
wynik3.Match(
    nr => Console.WriteLine($"Zamówienie: {nr}"),
    err => Console.WriteLine($"Błąd: {err}"));

// Przypadek 4 — walidacja
var wynik4 = await serwis.UtworzZamowienieAsync(1, -100m, "admin");
wynik4.Match(
    nr => Console.WriteLine($"Zamówienie: {nr}"),
    err => Console.WriteLine($"Błąd: {err}"));
```

### Typowe pytania rekrutacyjne

**"Jaka różnica między `throw` a `throw ex`?"** `throw` (re-throw) zachowuje oryginalny stack trace — wiadomo dokładnie gdzie wyjątek powstał. `throw ex` resetuje stack trace — jako miejsce powstania wyjątku wskazywany jest catch block, traci się informację o prawdziwym źródle błędu. Zawsze używaj `throw` gdy re-throwujesz wyjątek. `throw ex` używaj tylko gdy świadomie chcesz "ukryć" wewnętrzną implementację — wtedy jednak lepiej owinąć w własny wyjątek z `innerException`.

**"Kiedy własny wyjątek zamiast standardowego?"** Własny wyjątek gdy: błąd jest specyficzny dla domeny biznesowej (zamówienie nie może być zrealizowane bo klient jest zablokowany), chcesz dodać dodatkowe dane do wyjątku (kod błędu, identyfikator zasobu), budujesz bibliotekę i chcesz odróżnić swoje błędy od systemowych. Standardowy wyjątek gdy: błąd jest technicznie generyczny (`ArgumentNullException`, `InvalidOperationException`, `NotSupportedException`). Nie twórz własnych wyjątków dla standardowych przypadków.

**"Co to exception filter (`when`) i jaką daje przewagę nad catch?"** `when` pozwala warunkowo przechwycić wyjątek bez unwindowania stosu gdy warunek jest false. Kluczowa zaleta: w debuggerze widzisz oryginalne miejsce wyjątku gdy filtr nie pasuje (stos nie jest zwinięty). Umożliwia też trik z logowaniem: `catch (Exception ex) when (Loguj(ex))` gdzie `Loguj` zawsze zwraca `false` — logujesz z pełnym stack trace bez przechwycenia wyjątku.

**"Czym jest Result<T> i kiedy używać zamiast wyjątków?"** `Result<T>` to wzorzec który reprezentuje albo sukces z wartością albo błąd z komunikatem — bez rzucania wyjątków. Wyjątki są drogie (unwind stosu) i semantycznie oznaczają "niespodziewany błąd". `Result<T>` używasz dla przewidywalnych niepowodzeń: nieznaleziony zasób, błąd walidacji, reguła biznesowa. Wyjątki zostawiasz dla prawdziwych wyjątków: baza niedostępna, brak pamięci, błąd sieci. W API webowym — błędy domeny jako `Result`, systemowe jako wyjątki złapane przez middleware.