### IDisposable Pattern i Zarządzanie Zasobami w C#

---

### 1. Problem — dlaczego IDisposable istnieje

csharp

```csharp
// .NET Garbage Collector zarządza ZARZĄDZANĄ pamięcią automatycznie
// ALE nie wie jak zwolnić NIEZARZĄDZANE zasoby:
// - połączenia do bazy danych
// - uchwyty plików (FileHandle)
// - połączenia sieciowe (Socket)
// - zasoby GDI (grafika Windows)
// - niezarządzane bloki pamięci (IntPtr)

// BEZ IDisposable — wyciek zasobów!
public class ZlyKod
{
    public void OtworzIPorzuc()
    {
        var conn = new System.Data.SqlClient.SqlConnection("...");
        conn.Open();
        // Brak Close/Dispose — połączenie zajęte do GC
        // GC zbierze obiekt... KIEDYŚ... może za minutę, może za godzinę
        // W tym czasie: baza ma zajęte połączenie z puli!
    }
}

// Klasyczny problem — baza ma 100 połączeń w puli
// Wywołaj OtworzIPorzuc() 100 razy → "Connection pool exhausted"!

// IDisposable — kontrakt: "umiem zwolnić zasoby deterministycznie"
public interface IDisposable
{
    void Dispose();  // wywołaj gdy skończyłeś używać obiektu
}
```

---

### 2. using statement — gwarancja Dispose

csharp

```csharp
// using — syntactic sugar dla try/finally + Dispose()
// Gwarantuje wywołanie Dispose() nawet przy wyjątku!

// Klasyczny using block
using (var reader = new StreamReader("plik.txt"))
{
    string tresc = reader.ReadToEnd();
    Console.WriteLine(tresc);
}   // ← Dispose() wywołane ZAWSZE tutaj

// Co kompilator generuje:
{
    StreamReader reader = new StreamReader("plik.txt");
    try
    {
        string tresc = reader.ReadToEnd();
        Console.WriteLine(tresc);
    }
    finally
    {
        reader?.Dispose();   // Dispose() w finally — zawsze wykonane!
    }
}

// using declaration (C# 8+) — krótszy zapis
// Dispose() wywołane na końcu scope'u bloku (metody lub nawiasów klamrowych)
public void NowoczesnyUsing()
{
    using var reader = new StreamReader("plik.txt");   // brak nawiasów!
    using var writer = new StreamWriter("wynik.txt");

    string linia;
    while ((linia = reader.ReadLine()!) != null)
        writer.WriteLine(linia.ToUpper());

}   // ← writer.Dispose() POTEM reader.Dispose() — odwrotna kolejność!
    //   LIFO — Last In, First Out

// Zagnieżdżone using — właściwa kolejność Dispose
using (var outer = new StreamReader("outer.txt"))
using (var inner = new StreamReader("inner.txt"))  // można łączyć
{
    // inner zamknięty PRZED outer
}

// using z null check — gdy zasób może być null
StreamReader? mozliwieNull = null;
try
{
    mozliwieNull = new StreamReader("moze_brac.txt");
    Console.WriteLine(mozliwieNull.ReadToEnd());
}
finally
{
    mozliwieNull?.Dispose();  // bezpieczne gdy null
}

// Nowoczesna wersja:
if (File.Exists("moze_brac.txt"))
{
    using var r = new StreamReader("moze_brac.txt");
    Console.WriteLine(r.ReadToEnd());
}
```

---

### 3. Implementacja IDisposable — pełny wzorzec

csharp

```csharp
// Pełny wzorzec Dispose — dla klas z NIEZARZĄDZANYMI zasobami
public class ZarzadzaczZasobami : IDisposable
{
    // Niezarządzany zasób (np. uchwyt Win32)
    private IntPtr _uchwytNatywny = IntPtr.Zero;

    // Zarządzane zasoby (inne IDisposable)
    private StreamReader? _plik;
    private System.Net.Http.HttpClient? _client;

    // Flaga — czy już zwolniono zasoby
    private bool _zwolniony = false;

    public ZarzadzaczZasobami(string sciezka)
    {
        _uchwytNatywny = OtworzUchwytNatywny();  // symulacja
        _plik   = new StreamReader(sciezka);
        _client = new System.Net.Http.HttpClient();
        Console.WriteLine("Zasoby otwarte");
    }

    // Publiczne Dispose — wywoływane przez using/kod użytkownika
    public void Dispose()
    {
        Dispose(disposing: true);

        // Powiedz GC: "nie musisz wywoływać finalizatora"
        // Optymalizacja — obiekt nie trafi do kolejki finalizacji
        GC.SuppressFinalize(this);
    }

    // Protected virtual Dispose — serce wzorca
    protected virtual void Dispose(bool disposing)
    {
        if (_zwolniony) return;  // idempotentne — można wywołać wielokrotnie

        if (disposing)
        {
            // Zwalniaj ZARZĄDZANE zasoby tylko gdy wywołano wprost (nie z GC)
            // GC już może zbierać te obiekty z innych wątków — niebezpieczne!
            _plik?.Dispose();
            _plik = null;

            _client?.Dispose();
            _client = null;

            Console.WriteLine("Zarządzane zasoby zwolnione");
        }

        // Zwalniaj NIEZARZĄDZANE zasoby ZAWSZE (zarówno Dispose jak i GC)
        if (_uchwytNatywny != IntPtr.Zero)
        {
            ZwolnijUchwytNatywny(_uchwytNatywny);  // wywołanie Win32 API
            _uchwytNatywny = IntPtr.Zero;
            Console.WriteLine("Niezarządzany uchwyt zwolniony");
        }

        _zwolniony = true;
    }

    // Finalizator — ostatnia deska ratunku gdy ktoś zapomni Dispose()
    // TYLKO gdy masz niezarządzane zasoby!
    ~ZarzadzaczZasobami()
    {
        Console.WriteLine("Finalizator wywołany przez GC — Dispose zapomniane!");
        Dispose(disposing: false);  // false = nie ruszaj zarządzanych zasobów
    }

    // Metoda biznesowa z sprawdzeniem czy nie zwolniono
    public string CzytajDane()
    {
        ObjectDisposedException.ThrowIf(_zwolniony, this);
        return _plik?.ReadToEnd() ?? "";
    }

    // Symulacje Win32 API
    private IntPtr OtworzUchwytNatywny() => new IntPtr(12345);
    private void ZwolnijUchwytNatywny(IntPtr uchwyt) { /* CloseHandle(uchwyt) */ }
}

// Użycie
using var zasob = new ZarzadzaczZasobami("plik.txt");
string dane = zasob.CzytajDane();
// Koniec scope → Dispose(true) → GC.SuppressFinalize
// "Zarządzane zasoby zwolnione"
// "Niezarządzany uchwyt zwolniony"

// Zapomniany Dispose — GC w końcu wywoła finalizator
var zapomniany = new ZarzadzaczZasobami("plik2.txt");
// ... brak using/Dispose ...
// GC kiedyś: "Finalizator wywołany przez GC — Dispose zapomniane!"
//             "Niezarządzany uchwyt zwolniony"
// (zarządzane zasoby — NIE zwolnione przez finalizator)
```

---

### 4. Uproszczony wzorzec — tylko zarządzane zasoby

csharp

```csharp
// Gdy NIE masz niezarządzanych zasobów — znacznie prostszy wzorzec!
// Większość klas będzie właśnie taka

public class ProsteDisposable : IDisposable
{
    private readonly StreamReader _reader;
    private readonly System.Net.Http.HttpClient _client;
    private bool _zwolniony = false;

    public ProsteDisposable(string sciezka)
    {
        _reader = new StreamReader(sciezka);
        _client = new System.Net.Http.HttpClient();
    }

    public void Dispose()
    {
        if (_zwolniony) return;

        _reader.Dispose();
        _client.Dispose();
        _zwolniony = true;

        // Brak GC.SuppressFinalize — brak finalizatora
        // Brak protected virtual Dispose — brak dziedziczenia
    }

    public string Czytaj() =>
        _zwolniony
            ? throw new ObjectDisposedException(nameof(ProsteDisposable))
            : _reader.ReadToEnd();
}

// Wzorzec z dziedziczeniem — gdy klasa bazowa implementuje IDisposable
public abstract class BazowyZasob : IDisposable
{
    private bool _zwolniony = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_zwolniony) return;

        if (disposing)
            ZwolnijZarzadzane();   // hook dla klas pochodnych

        _zwolniony = true;
    }

    // Hook — klasy pochodne nadpisują
    protected virtual void ZwolnijZarzadzane() { }

    protected void SprawdzDisposed()
    {
        ObjectDisposedException.ThrowIf(_zwolniony, this);
    }
}

public class PochodnyZasob : BazowyZasob
{
    private readonly StreamWriter _writer;

    public PochodnyZasob(string sciezka) =>
        _writer = new StreamWriter(sciezka);

    public void Zapisz(string tekst)
    {
        SprawdzDisposed();
        _writer.WriteLine(tekst);
    }

    protected override void ZwolnijZarzadzane()
    {
        _writer.Dispose();
        base.ZwolnijZarzadzane();  // zawsze wywołaj bazę!
    }
}
```

---

### 5. IAsyncDisposable — async zwalnianie zasobów

csharp

```csharp
// IAsyncDisposable — gdy zwalnianie wymaga operacji async
// np. flush bufora do bazy, zamknięcie połączenia przez sieć

public class AsyncKolejkaZadan : IAsyncDisposable, IDisposable
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _zadania = new();
    private readonly System.Threading.SemaphoreSlim _semafor = new(1, 1);
    private bool _zwolniony = false;

    public void DodajZadanie(string zadanie)
    {
        ObjectDisposedException.ThrowIf(_zwolniony, this);
        _zadania.Enqueue(zadanie);
    }

    // ASYNC Dispose — flush wszystkich zadań przed zamknięciem
    public async ValueTask DisposeAsync()
    {
        if (_zwolniony) return;
        _zwolniony = true;

        // Flush - wyślij wszystkie zadania do "bazy"
        Console.Write("Flushing zadania: ");
        while (_zadania.TryDequeue(out string? zadanie))
        {
            await Task.Delay(10);  // symulacja async I/O
            Console.Write($"{zadanie} ");
        }
        Console.WriteLine("— gotowe");

        _semafor.Dispose();
        GC.SuppressFinalize(this);
    }

    // Synchroniczny Dispose — fallback (nie async flush)
    public void Dispose()
    {
        if (_zwolniony) return;
        _zwolniony = true;

        Console.WriteLine($"Synchroniczny Dispose — {_zadania.Count} zadań utraconych!");
        _semafor.Dispose();
        GC.SuppressFinalize(this);
    }
}

// await using — asynchroniczny odpowiednik using
await using var kolejka = new AsyncKolejkaZadan();
kolejka.DodajZadanie("T1");
kolejka.DodajZadanie("T2");
kolejka.DodajZadanie("T3");
// Koniec scope → await DisposeAsync() automatycznie
// "Flushing zadania: T1 T2 T3 — gotowe"

// Można też w bloku
await using (var kol2 = new AsyncKolejkaZadan())
{
    kol2.DodajZadanie("A");
    kol2.DodajZadanie("B");
}   // → DisposeAsync() wywołane
```

---

### 6. Finalizatory — szczegóły

csharp

```csharp
// Finalizator — wywoływany przez GC gdy nikt nie trzyma referencji
// NIGDY nie wiesz KIEDY — może sekundy, może minuty po ostatnim użyciu

public class PrzykladFinalizatora
{
    private readonly string _nazwa;

    public PrzykladFinalizatora(string nazwa)
    {
        _nazwa = nazwa;
        Console.WriteLine($"[{_nazwa}] Utworzono");
    }

    // Finalizator — składnia z ~
    ~PrzykladFinalizatora()
    {
        // OGRANICZENIA FINALIZATORA:
        // 1. Nie wiesz kolejności finalizacji między obiektami
        // 2. Inne obiekty mogły już być zebrane przez GC
        // 3. Nie możesz wywoływać metod na potencjalnie zebranych obiektach
        // 4. Finalizator wykonuje się na specjalnym wątku Finalizer Thread
        // 5. Wyjątek w finalizatorze = crash aplikacji!

        Console.WriteLine($"[{_nazwa}] Finalizator");

        // BEZPIECZNE w finalizatorze:
        // - operacje na wartościach (int, bool, struct)
        // - wywołania niezarządzanego kodu (P/Invoke)
        // - logowanie do pliku (jeśli FileStream nie był finalizowany)

        // NIEBEZPIECZNE w finalizatorze:
        // - wołanie metod na innych zarządzanych obiektach
        // - alokacje nowych obiektów (mogą nie działać)
    }
}

// Generacje GC i finalizatory
void DemoFinalizatorow()
{
    // Tworzenie obiektów
    var obj1 = new PrzykladFinalizatora("A");
    var obj2 = new PrzykladFinalizatora("B");

    // obj1 i obj2 osiągalne — nie zbierane

    obj1 = null!;  // A nie osiągalny — trafi do kolejki finalizacji

    GC.Collect();           // wymuś kolekcję (tylko do testów!)
    GC.WaitForPendingFinalizers();  // czekaj na finalizatory

    Console.WriteLine("Po GC.Collect");
    // [A] Finalizator  ← wywołany przez GC
}

// Resurrekcja — antypattern! obiekt "ożywa" w finalizatorze
public class RezurekcjaAntypattern
{
    public static RezurekcjaAntypattern? Instancja;

    ~RezurekcjaAntypattern()
    {
        // Przypisanie do statycznego pola = resurrekcja!
        Instancja = this;

        // GC musi ponownie śledzić ten obiekt
        // Finalizator nie zostanie wywołany drugi raz...
        // chyba że ponownie zarejestrujesz: GC.ReRegisterForFinalize(this);
    }
}
```

---

### 7. Typowe pułapki

csharp

```csharp
// PUŁAPKA 1 — using w pętli z tym samym zasobem
// ŹLE — reader jest Disposed po pierwszej iteracji!
public void ZlaPetla(string[] pliki)
{
    StreamReader? reader = null;
    foreach (string plik in pliki)
    {
        reader = new StreamReader(plik);
        using (reader)  // Dispose po każdej iteracji!
            Console.WriteLine(reader.ReadToEnd());
        // reader jest teraz Disposed — ale zmienna nadal istnieje
    }
}

// DOBRZE — using wewnątrz pętli
public void DobraPetla(string[] pliki)
{
    foreach (string plik in pliki)
    {
        using var reader = new StreamReader(plik);  // nowy reader dla każdego pliku
        Console.WriteLine(reader.ReadToEnd());
    }
}

// PUŁAPKA 2 — zwracanie Disposed zasobu
public StreamReader ZlyReturn()
{
    using var reader = new StreamReader("plik.txt");
    return reader;  // BŁĄD! reader jest Disposed zaraz po return!
}

// DOBRZE — przenieś odpowiedzialność na wywołującego
public StreamReader DobryReturn()
{
    return new StreamReader("plik.txt");  // wywołujący jest odpowiedzialny za Dispose
}

// Lub lepiej — przekaż treść, nie zasób
public string NajlepszyReturn() =>
    File.ReadAllText("plik.txt");

// PUŁAPKA 3 — brak Dispose w catch
public void ZlyCatch()
{
    var conn = new System.Data.SqlClient.SqlConnection("...");
    try
    {
        conn.Open();
        // ... błąd tu ...
    }
    catch (Exception)
    {
        // conn.Dispose() nie wywołane!
        throw;
    }
    conn.Dispose();  // nigdy nie osiągnięte przy wyjątku!
}

// DOBRZE — using gwarantuje Dispose przy wyjątku
public void DobryCatch()
{
    using var conn = new System.Data.SqlClient.SqlConnection("...");
    conn.Open();
    // wyjątek tu → conn.Dispose() i tak wywołane
}

// PUŁAPKA 4 — Dispose w konstruktorze gdy błąd
public class ZlKonstruktor : IDisposable
{
    private readonly StreamReader _r1;
    private readonly StreamReader _r2;  // może rzucić!
    private bool _disposed = false;

    public ZlKonstruktor()
    {
        _r1 = new StreamReader("plik1.txt");
        _r2 = new StreamReader("nieistniejacy.txt");  // FileNotFoundException!
        // _r1 nigdy nie Disposed jeśli tu rzuci!
    }

    public void Dispose()
    {
        if (_disposed) return;
        _r1.Dispose();
        _r2?.Dispose();
        _disposed = true;
    }
}

// DOBRZE — konstruktor który bezpiecznie zwalnia przy błędzie
public class DobryKonstruktor : IDisposable
{
    private readonly StreamReader _r1;
    private readonly StreamReader? _r2;
    private bool _disposed = false;

    public DobryKonstruktor()
    {
        _r1 = new StreamReader("plik1.txt");
        try
        {
            _r2 = new StreamReader("plik2.txt");
        }
        catch
        {
            _r1.Dispose();  // zwolnij to co już otwarto!
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _r1.Dispose();
        _r2?.Dispose();
        _disposed = true;
    }
}
```

---

### 8. Praktyczny przykład — connection pool z zasobami

csharp

```csharp
// Kompletny przykład: pula połączeń do bazy z IDisposable

// Symulacja połączenia do bazy
public class PolaczenieBazy : IDisposable
{
    private static int _licznikId = 0;
    private readonly int _id;
    private bool _disposed = false;
    private bool _wUzyciu = false;

    public int Id => _id;
    public bool Dostepne => !_wUzyciu && !_disposed;

    public PolaczenieBazy()
    {
        _id = Interlocked.Increment(ref _licznikId);
        Console.WriteLine($"  [Polaczenie #{_id}] Otwarto");
    }

    public void Zarezerwuj()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _wUzyciu = true;
    }

    public void Zwolnij() => _wUzyciu = false;

    public async Task<string> WykonajZapytanieAsync(string sql,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_wUzyciu) throw new InvalidOperationException("Połączenie nie jest zarezerwowane");

        await Task.Delay(50, ct);  // symulacja zapytania
        return $"Wynik[{_id}]: {sql}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wUzyciu  = false;
        Console.WriteLine($"  [Polaczenie #{_id}] Zamknięto");
    }
}

// Wrapper który zwraca połączenie do puli zamiast zamykać
public class WypozyczonePołączenie : IDisposable, IAsyncDisposable
{
    private readonly PolaczenieBazy _polaczenie;
    private readonly PulaPolaczen   _pula;
    private bool _zwrocone = false;

    public WypozyczonePołączenie(PolaczenieBazy polaczenie, PulaPolaczen pula)
    {
        _polaczenie = polaczenie;
        _pula       = pula;
    }

    public Task<string> WykonajAsync(string sql, CancellationToken ct = default) =>
        _polaczenie.WykonajZapytanieAsync(sql, ct);

    // Synchroniczny Dispose — zwróć do puli
    public void Dispose()
    {
        if (_zwrocone) return;
        _zwrocone = true;
        _pula.Zwroc(_polaczenie);
    }

    // Async Dispose — flush transakcji przed zwrotem
    public async ValueTask DisposeAsync()
    {
        if (_zwrocone) return;
        _zwrocone = true;

        // Flush oczekujących operacji
        await Task.Delay(5);  // symulacja flush

        _pula.Zwroc(_polaczenie);
    }
}

// Pula połączeń
public class PulaPolaczen : IDisposable
{
    private readonly List<PolaczenieBazy>     _wszystkie = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<PolaczenieBazy>
                                               _dostepne  = new();
    private readonly System.Threading.SemaphoreSlim
                                               _semafor;
    private readonly int _maxPolaczen;
    private bool _disposed = false;

    public PulaPolaczen(int maxPolaczen = 5)
    {
        _maxPolaczen = maxPolaczen;
        _semafor     = new System.Threading.SemaphoreSlim(maxPolaczen, maxPolaczen);

        // Inicjalizuj pule
        Console.WriteLine($"Inicjalizuję pulę {maxPolaczen} połączeń:");
        for (int i = 0; i < maxPolaczen; i++)
        {
            var pol = new PolaczenieBazy();
            _wszystkie.Add(pol);
            _dostepne.Enqueue(pol);
        }
    }

    // Pobierz połączenie z puli
    public async Task<WypozyczonePołączenie> PozyczAsync(
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Czekaj aż będzie dostępne połączenie
        await _semafor.WaitAsync(ct);

        if (!_dostepne.TryDequeue(out PolaczenieBazy? pol))
            throw new InvalidOperationException("Brak dostępnych połączeń");

        pol.Zarezerwuj();
        Console.WriteLine($"  Pożyczono połączenie #{pol.Id}");
        return new WypozyczonePołączenie(pol, this);
    }

    // Zwróć połączenie do puli (wywoływane przez WypozyczonePołączenie.Dispose)
    internal void Zwroc(PolaczenieBazy polaczenie)
    {
        if (_disposed)
        {
            polaczenie.Dispose();  // pula zamknięta — zamknij połączenie
            return;
        }

        polaczenie.Zwolnij();
        _dostepne.Enqueue(polaczenie);
        _semafor.Release();
        Console.WriteLine($"  Zwrócono połączenie #{polaczenie.Id}");
    }

    public int DostepnychPolaczen => _dostepne.Count;
    public int WszystkichPolaczen => _wszystkie.Count;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Console.WriteLine("Zamykam pulę połączeń:");
        foreach (var pol in _wszystkie)
            pol.Dispose();

        _semafor.Dispose();
        Console.WriteLine("Pula zamknięta");
    }
}

// Demonstracja
Console.WriteLine("=== Pula Połączeń ===\n");

using var pula = new PulaPolaczen(3);
Console.WriteLine($"Dostępnych: {pula.DostepnychPolaczen}/{pula.WszystkichPolaczen}\n");

// Użycie połączeń
Console.WriteLine("Wykonuję zapytania równolegle:");
var zadania = Enumerable.Range(1, 5).Select(async i =>
{
    await using var pol = await pula.PozyczAsync();
    string wynik = await pol.WykonajAsync($"SELECT * FROM Tabela{i}");
    Console.WriteLine($"  {wynik}");
    // await DisposeAsync() → połączenie wraca do puli
});

await Task.WhenAll(zadania);

Console.WriteLine($"\nDostępnych po wszystkich: {pula.DostepnychPolaczen}");
// using var pula → pula.Dispose() → wszystkie połączenia zamknięte
```

---

### Typowe pytania rekrutacyjne

**"Dlaczego `GC.SuppressFinalize(this)` w `Dispose()`?"** GC zbiera nieużywane obiekty w dwóch fazach: normalny garbage collection, a potem finalizatory w osobnej kolejce. Jeśli obiekt ma finalizator, GC musi najpierw go zebrać do kolejki finalizacji (generacja F), poczekać na finalizator, POTEM zwolnić pamięć. To dwa przejścia GC zamiast jednego — obiekt żyje dłużej. `GC.SuppressFinalize` mówi GC: "już wywołałem Dispose ręcznie, nie musisz wywoływać finalizatora" — obiekt jest zbierany w jednym przejściu.

**"Jaka różnica między `Dispose(bool disposing)` z `true` i `false`?"** `true` — wywołane przez kod użytkownika (z `using` lub jawne `Dispose()`). Bezpieczne do zwalniania zarządzanych zasobów (innych IDisposable). `false` — wywołane przez finalizator (GC). NIE wolno ruszać zarządzanych zasobów — GC może już je zbierać w nieznanej kolejności, mogą być w niespójnym stanie. Zawsze możesz zwalniać niezarządzane zasoby (IntPtr, uchwyty Win32) — te GC nie zbiera.

**"Kiedy `IAsyncDisposable` zamiast `IDisposable`?"** `IAsyncDisposable` gdy zwalnianie zasobu wymaga async operacji: flush danych do bazy przez sieć, wysłanie potwierdzenia zamknięcia połączenia, zapis bufora do streamu sieciowego. Implementuj oba interfejsy — `IDisposable` jako synchroniczny fallback (może zgubić część danych) i `IAsyncDisposable` jako preferowany. `await using` automatycznie używa `DisposeAsync()`.

**"Co się stanie gdy nie wywołasz Dispose i obiekt ma finalizator?"** GC w końcu wykryje że obiekt jest nieosiągalny i doda go do kolejki finalizatorów. Finalizator Thread wywoła destruktor (`~KlasaNazwa()`) — Dispose(false) — zwalniając niezarządzane zasoby. Ale: (1) czas jest niedeterministyczny — może to być za sekundy lub minuty, (2) zarządzane zasoby w tym czasie trzymają swoje zasoby, (3) finalizatory spowalniają GC — obiekty z finalizatorami przeżywają przynajmniej jedną dodatkową generację GC.