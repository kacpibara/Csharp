using System.Collections.Concurrent;

namespace _04_Exceptions_Files_Serializable;

// ===== PELNY WZORZEC DISPOSE (managed + unmanaged resources) =====

public class ZarzadzaczZasobamiID : IDisposable
{
    private FileStream? _plik;           // zasob managed
    private IntPtr _uchwyt;             // zasob unmanaged (symulowany)
    private bool _zwolniony;            // flaga idempotentnosci

    public ZarzadzaczZasobamiID(string sciezkaPliku)
    {
        _plik = new FileStream(sciezkaPliku, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        _uchwyt = new IntPtr(42); // symulacja niezarzadzanego uchwytu
        Console.WriteLine($"  [{GetType().Name}] Utworzony: plik={_plik.Name}");
    }

    public void ZapiszDane(string dane)
    {
        ObjectDisposedException.ThrowIf(_zwolniony, this); // NET 7+
        var bajty = System.Text.Encoding.UTF8.GetBytes(dane);
        _plik!.Write(bajty, 0, bajty.Length);
    }

    // Publiczne Dispose - wywolywane przez uzytkownika lub using
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this); // zapobiega podwojnemu przejsciu przez GC
    }

    // Chroniony Dispose(bool) - separuje managed od unmanaged
    protected virtual void Dispose(bool disposing)
    {
        if (_zwolniony) return; // idempotentne

        if (disposing)
        {
            // Managed resources - zwalniaj TYLKO gdy disposing=true
            // (gdy disposing=false GC mogl juz zebrac te obiekty!)
            _plik?.Dispose();
            _plik = null;
            Console.WriteLine($"  [{GetType().Name}] Managed zwolniony");
        }

        // Unmanaged resources - zwalniaj zawsze (rowniez z finalizatora)
        if (_uchwyt != IntPtr.Zero)
        {
            // Tutaj: CloseHandle(_uchwyt) lub inna operacja zwalniajaca
            _uchwyt = IntPtr.Zero;
            Console.WriteLine($"  [{GetType().Name}] Unmanaged zwolniony");
        }

        _zwolniony = true;
    }

    // Finalizator - ostatnia deska ratunku gdy uzytkownik zapomnial Dispose()
    // NIGDY: nie uzywaj obiektow managed! GC mogl je juz zebrac.
    // NIGDY: nie rzucaj wyjatkow - spowodowaloby crash procesu
    ~ZarzadzaczZasobamiID()
    {
        Dispose(disposing: false); // tylko unmanaged resources
    }
}

// ===== UPROSZCZONY WZORZEC (tylko managed resources - bez finalizatora) =====

public class ProsteDisposableID : IDisposable
{
    private MemoryStream? _strumien;
    private bool _zwolniony;

    public ProsteDisposableID()
    {
        _strumien = new MemoryStream();
        Console.WriteLine($"  [{GetType().Name}] Utworzony");
    }

    public void Dispose()
    {
        if (_zwolniony) return;
        _strumien?.Dispose();
        _strumien = null;
        _zwolniony = true;
        Console.WriteLine($"  [{GetType().Name}] Zwolniony");
        // Brak GC.SuppressFinalize - nie ma finalizatora
    }
}

// ===== WZORZEC DZIEDZICZENIA =====

public class BazowyZasobID : IDisposable
{
    private bool _zwolniony;
    private readonly MemoryStream _bazaStrumien = new();

    public virtual void Dispose()
    {
        if (_zwolniony) return;
        ZwolnijZarzadzane();
        _bazaStrumien.Dispose();
        _zwolniony = true;
        Console.WriteLine($"  [{GetType().Name}] BazowyZasobID.Dispose");
    }

    protected virtual void ZwolnijZarzadzane()
    {
        Console.WriteLine($"  [{GetType().Name}] ZwolnijZarzadzane (baza)");
    }
}

public class PochodnyZasobID : BazowyZasobID
{
    private readonly MemoryStream _pochodnyStrumien = new();

    protected override void ZwolnijZarzadzane()
    {
        _pochodnyStrumien.Dispose();
        Console.WriteLine($"  [{GetType().Name}] ZwolnijZarzadzane (pochodny)");
        base.ZwolnijZarzadzane();
    }
}

// ===== IAsyncDisposable =====

public class AsyncKolejkaZadanID : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _semafor = new(3, 3);
    private readonly CancellationTokenSource _cts = new();
    private bool _zwolniony;

    public async Task DodajZadanieAsync(string nazwa)
    {
        await _semafor.WaitAsync(_cts.Token);
        try
        {
            Console.WriteLine($"    Zadanie '{nazwa}' wykonywane...");
            await Task.Delay(10, _cts.Token);
        }
        finally { _semafor.Release(); }
    }

    // IAsyncDisposable - preferowany sposob gdy zasoby wymagaja async cleanup
    public async ValueTask DisposeAsync()
    {
        if (_zwolniony) return;
        _zwolniony = true;
        _cts.Cancel();
        await Task.Delay(10); // symulacja async cleanup (np. flushowanie bufora)
        _semafor.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
        Console.WriteLine($"  [{GetType().Name}] DisposeAsync (await using)");
    }

    // Sync fallback - implementuj oba gdy uzywasz IAsyncDisposable
    public void Dispose()
    {
        if (_zwolniony) return;
        _zwolniony = true;
        _cts.Cancel();
        _semafor.Dispose();
        _cts.Dispose();
        Console.WriteLine($"  [{GetType().Name}] Dispose (sync fallback)");
    }
}

// ===== FINALIZATOR - DEMONSTRACJA =====

public class PrzykladFinalizatoraID
{
    private static int _licznikFinal = 0;
    public int Nr { get; }

    public PrzykladFinalizatoraID(int nr) { Nr = nr; }

    // Finalizator: ~ClassName() - wywolywany przez GC na oddzielnym watku
    // Ograniczenia: nie mozna zaufac kolejnosci, nie mozna wywolac managed Dispose
    // Wyjatek w finalizatorze = crash calego procesu!
    ~PrzykladFinalizatoraID()
    {
        _licznikFinal++;
        // Console.WriteLine tutaj moze spowodowac problemy w prawdziwym kodzie
        // (Console jest managed - tu tylko dla celów demo)
    }

    public static int LicznikFinalizacji => _licznikFinal;
}

// ===== ANTYPATTERN: RESURREKCJA =====

public class RezurekcjaAntypatternID
{
    public static RezurekcjaAntypatternID? Instancja;
    private bool _zrezurektowana;

    ~RezurekcjaAntypatternID()
    {
        if (!_zrezurektowana)
        {
            // ANTYPATTERN: przypisanie this do statycznego pola w finalizatorze
            // Obiekt "zmartwychwstaje" - nie zostaje zebrany przez GC!
            // Wymaga ponownej rejestracji finalizatora: GC.ReRegisterForFinalize(this)
            // Stosowac TYLKO w specjalnych przypadkach (np. pula obiektow)
            Instancja = this;
            _zrezurektowana = true;
            GC.ReRegisterForFinalize(this); // zarejestuj finalizator na nastepne GC
        }
        // Drugi raz: Instancja = null, obiekt faktycznie zebrany
    }
}

// ===== PULA POLACZEN - PELNY PRZYKLAD =====

public class PolaczenieBazyID : IDisposable
{
    public int Id { get; }
    public bool CzyWolne { get; set; } = true;
    private bool _zwolniony;

    public PolaczenieBazyID(int id)
    {
        Id = id;
        Console.WriteLine($"    [Polaczenie #{id}] Otwarto polaczenie z baza");
    }

    public string WykonajZapytanie(string sql)
    {
        ObjectDisposedException.ThrowIf(_zwolniony, this);
        return $"Wynik '{sql}' z polaczenia #{Id}";
    }

    public void Dispose()
    {
        if (_zwolniony) return;
        _zwolniony = true;
        Console.WriteLine($"    [Polaczenie #{Id}] Polaczenie zamkniete");
    }
}

public class WypozyczonePolaczenieID : IDisposable, IAsyncDisposable
{
    private readonly PolaczenieBazyID _polaczenie;
    private readonly PulaPolaczenID _pula;
    private bool _zwrocone;

    internal WypozyczonePolaczenieID(PolaczenieBazyID polaczenie, PulaPolaczenID pula)
    {
        _polaczenie = polaczenie;
        _pula = pula;
    }

    public string WykonajZapytanie(string sql) => _polaczenie.WykonajZapytanie(sql);

    // Dispose zwraca polaczenie do puli (nie zamyka!)
    public void Dispose()
    {
        if (_zwrocone) return;
        _zwrocone = true;
        _pula.Zwroc(_polaczenie);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

public class PulaPolaczenID : IDisposable
{
    private readonly ConcurrentQueue<PolaczenieBazyID> _wolne = new();
    private readonly SemaphoreSlim _semafor;
    private readonly List<PolaczenieBazyID> _wszystkie = new();
    private bool _zwolniony;

    public PulaPolaczenID(int rozmiar)
    {
        _semafor = new SemaphoreSlim(rozmiar, rozmiar);
        for (int i = 1; i <= rozmiar; i++)
        {
            var pol = new PolaczenieBazyID(i);
            _wszystkie.Add(pol);
            _wolne.Enqueue(pol);
        }
    }

    // Asynchroniczne pozyczanie - czeka na wolne polaczenie
    public async Task<WypozyczonePolaczenieID> PozyczAsync(CancellationToken token = default)
    {
        await _semafor.WaitAsync(token); // czeka az polaczenie bedzie dostepne
        _wolne.TryDequeue(out var polaczenie);
        return new WypozyczonePolaczenieID(polaczenie!, this);
    }

    // Zwrot do puli (wywolywany przez WypozyczonePolaczenieID.Dispose)
    internal void Zwroc(PolaczenieBazyID polaczenie)
    {
        _wolne.Enqueue(polaczenie);
        _semafor.Release(); // odblokuj oczekujace pozyczenie
        Console.WriteLine($"    [Polaczenie #{polaczenie.Id}] Zwrocone do puli");
    }

    public void Dispose()
    {
        if (_zwolniony) return;
        _zwolniony = true;
        foreach (var pol in _wszystkie) pol.Dispose();
        _semafor.Dispose();
    }
}

// ===== GLOWNA KLASA DEMO =====

public static class IDisposableUsing
{
    // 1. using statement i using declaration (C# 8+)
    public static void DemoUsingStatement()
    {
        Console.WriteLine("\n--- using statement i using declaration ---");

        // Dlaczego IDisposable: GC zwalnia pamiec managed, ale NIE:
        // - uchwyty plikow, polaczenia DB, gniazda sieciowe, GDI, IntPtr
        // Bez Dispose() = wyciek zasobow mimo dzialajacego GC

        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_using.txt");
        File.WriteAllText(tmpFile, "dane testowe");

        try
        {
            // using statement (klasyczny) - syntactic sugar dla try/finally + Dispose()
            // Gwarantuje Dispose() nawet gdy wyjatek!
            using (var sr = new StreamReader(tmpFile))
            {
                string linia = sr.ReadLine()!;
                Console.WriteLine($"  using statement: '{linia}'");
            } // sr.Dispose() wywolane tutaj (nawet przy wyjatku)

            // using declaration (C# 8+) - Dispose() na koncu zakresu bloku {}
            using var sr2 = new StreamReader(tmpFile);
            string linia2 = sr2.ReadLine()!;
            Console.WriteLine($"  using declaration: '{linia2}'");
            // sr2.Dispose() zostanie wywolane na koncu metody (end of scope)

            Console.WriteLine("  using declaration: Dispose na koncu bloku scope");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // 2. LIFO - kolejnosc zwalniania przy using declaration
    public static void DemoLIFO()
    {
        Console.WriteLine("\n--- LIFO (Last In, First Out) - kolejnosc Dispose ---");

        // using declarations sa zwalniane w odwrotnej kolejnosci deklaracji (LIFO)
        // Ostatni zadeklarowany = pierwszy zwolniony
        {
            using var a = new ProsteDisposableID(); // A
            using var b = new ProsteDisposableID(); // B - ostatni zadeklarowany
            Console.WriteLine("  Oba zasoby uzywane");
            // Koniec bloku: najpierw b.Dispose(), potem a.Dispose() (LIFO!)
        }
        Console.WriteLine("  (B bylo zwolnione przed A - LIFO)");
    }

    // 3. Pelny wzorzec Dispose
    public static void DemoFullDisposable()
    {
        Console.WriteLine("\n--- Pelny wzorzec Dispose (managed + unmanaged + finalizer) ---");

        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_full_dispose.tmp");
        File.WriteAllText(tmpFile, "");

        try
        {
            // Normalny przeplyp: Dispose() → Dispose(true) → GC.SuppressFinalize
            using (var zasob = new ZarzadzaczZasobamiID(tmpFile))
            {
                zasob.ZapiszDane("dane");
                Console.WriteLine("  Zasob uzyty");
            } // Dispose() → Dispose(true) → finalizator POMINIETY dzieki SuppressFinalize

            Console.WriteLine("  using: Dispose(true) + GC.SuppressFinalize - finalizator nie wywolany");

            // ObjectDisposedException.ThrowIf
            var zasob2 = new ZarzadzaczZasobamiID(tmpFile);
            zasob2.Dispose();
            try
            {
                zasob2.ZapiszDane("po Dispose"); // rzuci ObjectDisposedException
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine($"  ObjectDisposedException.ThrowIf: {ex.Message[..30]}...");
            }
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }
    }

    // 4. Uproszczony wzorzec + dziedziczenie
    public static void DemoUproszczonyDisposable()
    {
        Console.WriteLine("\n--- Uproszczony wzorzec + dziedziczenie ---");

        // ProsteDisposableID - tylko managed, bez finalizatora
        Console.WriteLine("  ProsteDisposableID:");
        using (new ProsteDisposableID()) { }

        // Dziedziczenie: Dispose() wywoluje ZwolnijZarzadzane() (virtual hook)
        Console.WriteLine("  PochodnyZasobID (dziedziczenie):");
        using (var pochodny = new PochodnyZasobID())
        {
            // pochodny.Dispose() → ZwolnijZarzadzane() (override) → base.ZwolnijZarzadzane()
        }
    }

    // 5. IAsyncDisposable + await using
    public static async Task DemoIAsyncDisposable()
    {
        Console.WriteLine("\n--- IAsyncDisposable + await using ---");

        // await using - wywoluje DisposeAsync() zamiast Dispose()
        await using (var kolejka = new AsyncKolejkaZadanID())
        {
            await kolejka.DodajZadanieAsync("Zadanie A");
            await kolejka.DodajZadanieAsync("Zadanie B");
            Console.WriteLine("  Kolejka uzyta z await using");
        } // DisposeAsync() wywolane tutaj

        // await using declaration (C# 8+)
        await using var kolejka2 = new AsyncKolejkaZadanID();
        await kolejka2.DodajZadanieAsync("Zadanie C");
        Console.WriteLine("  await using declaration: DisposeAsync na koncu scope");
        // kolejka2.DisposeAsync() wywolane na koncu metody
    }

    // 6. Finalizatory - GC.Collect, WaitForPendingFinalizers
    public static void DemoFinalizatory()
    {
        Console.WriteLine("\n--- Finalizatory i generacje GC ---");
        Console.WriteLine("  Finalizatory sa wywolywane przez GC na oddzielnym watku");
        Console.WriteLine("  Ograniczenia: nie mozna uzyc managed objects, wyjatek = crash");

        {
            var obj1 = new PrzykladFinalizatoraID(1);
            var obj2 = new PrzykladFinalizatoraID(2);
            // obj1, obj2 wychodza ze scope - staja sie eligible for GC
        }

        // GC.Collect / WaitForPendingFinalizers - TYLKO DO TESTOW, nigdy w produkcji!
        int przed = PrzykladFinalizatoraID.LicznikFinalizacji;
        GC.Collect();                    // wymusza zbieranie smieci
        GC.WaitForPendingFinalizers();   // czeka az finalizatory sie wykonaja
        GC.Collect();                    // drugi przebieg - obiekty po finalizacji
        int po = PrzykladFinalizatoraID.LicznikFinalizacji;

        Console.WriteLine($"  Przed GC.Collect: {przed} finalizacji");
        Console.WriteLine($"  Po GC.Collect + WaitForPendingFinalizers: {po} finalizacji");
        Console.WriteLine("  GC.Collect/WaitForPendingFinalizers - TYLKO w testach!");

        // Generacje GC
        Console.WriteLine($"  MaxGeneration: {GC.MaxGeneration}"); // zwykle 2
        Console.WriteLine("  Gen0: nowe obiekty | Gen1: przetrwaly 1 GC | Gen2: dlugo zyja");
        Console.WriteLine("  Obiekt w finalizatorze przezywa 1 generacje wiecej (2-pass GC)");
        Console.WriteLine("  GC.SuppressFinalize zapobiega temu - dlatego w Dispose()!");
    }

    // 7. Antypattern: Resurrekcja
    public static void DemoRezurekcja()
    {
        Console.WriteLine("\n--- ANTYPATTERN: Resurrekcja w finalizatorze ---");
        Console.WriteLine("  Resurrekcja: przypisanie 'this' do statycznego pola w finalizatorze");
        Console.WriteLine("  Obiekt 'zmartwychwstaje' - GC nie moze go zebrac!");
        Console.WriteLine("  Po resurrekcji finalizator NIE zostanie wywolany ponownie");
        Console.WriteLine("  chyba ze GC.ReRegisterForFinalize(this) zostanie wywolane");
        Console.WriteLine("  Zastosowania: pule obiektow (ostroznos - bardzo zaawansowane)");
        Console.WriteLine("  W praktyce: UNIKAJ. Implementuj prawidlowy wzorzec Dispose zamiast tego.");
    }

    // 8. Pulapki - typowe bledy z IDisposable
    public static void DemoPulapki()
    {
        Console.WriteLine("\n--- Pulapki IDisposable ---");

        // PULAPKA 1: using wewnatrz petli - Dispose po kazdej iteracji
        Console.WriteLine("  [1] using w petli - Dispose po kazdej iteracji:");
        var tmpFile = Path.Combine(Path.GetTempPath(), "demo_pulapka.txt");
        File.WriteAllText(tmpFile, "abc");
        try
        {
            for (int i = 0; i < 3; i++)
            {
                using var sr = new StreamReader(tmpFile); // nowy StreamReader = nowy Dispose per iteracja
                string? linia = sr.ReadLine();
                Console.WriteLine($"    Iteracja {i}: '{linia}'");
            }
            Console.WriteLine("    Poprawne: nowy using per iteracja jesli zasoby nie sa wspoldzielone");
        }
        finally { if (File.Exists(tmpFile)) File.Delete(tmpFile); }

        // PULAPKA 2: Zwracanie zasobu z bloku using (obiekt bedzie Disposed!)
        Console.WriteLine("  [2] Nie zwracaj zasobu stworzonym wewnatrz using:");
        Console.WriteLine("      ZLY KOD: using(var sr = new StreamReader(f)) { return sr; }");
        Console.WriteLine("      sr jest Disposed po powrocie - uzycie = ObjectDisposedException");
        Console.WriteLine("      POPRAWNE: zwroc dane, nie sam zasob");

        // PULAPKA 3: Brak Dispose w bloku catch
        Console.WriteLine("  [3] Brak Dispose w catch - uzyj using zamiast try/catch/Dispose:");
        Console.WriteLine("      ZLY KOD:");
        Console.WriteLine("        var conn = new Polaczenie();");
        Console.WriteLine("        try { ... } catch { conn.Dispose(); }  // Dispose tylko w catch!");
        Console.WriteLine("      POPRAWNE: using (var conn = new Polaczenie()) { ... }");
        Console.WriteLine("        using gwarantuje Dispose w KAZDEJ sciezce (sukces I wyjatek)");

        // PULAPKA 4: Wyjatek w konstruktorze = wyciek czesciowo zainicjowanych zasobow
        Console.WriteLine("  [4] Wyjatek w konstruktorze - wyciek zasobow:");
        Console.WriteLine("      PROBLEM: jesli ctor rzuci wyjatek po stworzeniu zasobu1");
        Console.WriteLine("               ale przed zasobu2 - using nigdy nie wywola Dispose!");
        Console.WriteLine("      POPRAWNE: uzyj try/catch w konstruktorze, albo fabryki:");
        Console.WriteLine("        try { _zasob1 = new X(); _zasob2 = new Y(); }");
        Console.WriteLine("        catch { _zasob1?.Dispose(); throw; }");
    }

    // 9. Pula polaczen - pelny przyklad z IDisposable
    public static async Task DemoPulaPolaczen()
    {
        Console.WriteLine("\n--- Pula polaczen (PolaczenieBazyID + WypozyczonePolaczenieID + PulaPolaczenID) ---");

        // Pula 2 polaczen
        using var pula = new PulaPolaczenID(rozmiar: 2);
        Console.WriteLine("  Pula 2 polaczen stworzonych");

        // Pozyczanie i automatyczny zwrot przez using/await using
        await using (var pol1 = await pula.PozyczAsync())
        {
            Console.WriteLine($"  [{pol1.WykonajZapytanie("SELECT 1")}]");

            await using (var pol2 = await pula.PozyczAsync())
            {
                Console.WriteLine($"  [{pol2.WykonajZapytanie("SELECT 2")}]");
                Console.WriteLine("  Oba polaczenia w uzyciu");
            } // pol2.DisposeAsync() → Zwroc → _semafor.Release()

            Console.WriteLine("  pol2 zwrocone do puli");

            // pol2 juz zwrocone - mozna pozyczac ponownie
            await using var pol3 = await pula.PozyczAsync();
            Console.WriteLine($"  [{pol3.WykonajZapytanie("SELECT 3")}]");
        } // pol1 i pol3 zwrocone

        Console.WriteLine("  Koniec: using(pula) zamknie wszystkie polaczenia");
    }
}
