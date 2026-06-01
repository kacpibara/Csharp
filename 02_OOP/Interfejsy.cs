namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// INTERFEJSY I KLASY ABSTRAKCYJNE
// Interfejs = czysty kontrakt CAN-DO (co potrafi obiekt)
// Klasa abstrakcyjna = IS-A + wspólna implementacja
// ─────────────────────────────────────────────────────────────────────────────

public static class Interfejsy
{
    // PODSTAWY — definicja i implementacja
    public static void PodstawyInterfejsu()
    {
        Console.WriteLine("\n=== PODSTAWY INTERFEJSU ===");

        // Interfejs jako typ — polimorfizm przez kontrakt
        IZwierzeInter kot = new KotInter("Mruczek", 5);
        IZwierzeInter pies = new PiesInter("Rex", 3);

        kot.Dzwiek();
        pies.Dzwiek();

        // Kolekcja dowolnych typów implementujących IZwierzeInter
        var zwierzeta = new List<IZwierzeInter> { kot, pies, new PiesInter("Azor", 2) };
        WyswietlZwierzeta(zwierzeta);

        // Wiele interfejsów — pies implementuje IZwierzeInter + IDomowy + ITresowalnyInter
        var rexPies = new PiesInter("Rex", 3);
        rexPies.Naucz("Siad");
        rexPies.Naucz("Leżeć");
        rexPies.Naucz("Siad"); // duplikat — ignorowany
        Console.WriteLine($"Rex: {rexPies.PoznaneKomendy.Count} komendy: {string.Join(", ", rexPies.PoznaneKomendy)}");
        rexPies.Glaskaj();
        Console.WriteLine($"CzyLubiLudzi: {rexPies.CzyLubiLudzi}");
    }

    // EXPLICIT INTERFACE IMPLEMENTATION — rozwiązanie konfliktów
    public static void ExplicitImplementacja()
    {
        Console.WriteLine("\n=== EXPLICIT INTERFACE IMPLEMENTATION ===");

        var doc = new DokumentInter("Raport Q1");
        doc.EdytujTresc("Treść raportu...");

        // doc.Serializuj() — BŁĄD! Niedostępne przez typ Dokument

        // Tylko przez referencję interfejsu
        ISerializowalnyInter ser = doc;
        Console.WriteLine($"ISerializowalny.Serializuj: {ser.Serializuj()}");

        IPersistableInter per = doc;
        Console.WriteLine($"IPersistable.Serializuj:    {per.Serializuj()}");

        // Cast też działa
        Console.WriteLine($"Cast:  {((ISerializowalnyInter)doc).Serializuj()}");

        // Praktyczne: IDisposable explicit — ukrywa Dispose() w API klasy
        Console.WriteLine("\nIDisposable explicit — dostęp tylko przez using/cast:");
        using var conn = new PolaczenieTrans(); // using wywołuje IDisposable.Dispose()
        conn.OtworzTransakcje();
        // conn.Dispose() — BŁĄD — explicit, niewidoczne przez typ Polaczenie
    }

    // KLUCZOWE INTERFEJSY .NET
    public static void InterfejsyNET()
    {
        Console.WriteLine("\n=== KLUCZOWE INTERFEJSY .NET ===");

        // IComparable<T> — naturalny porządek sortowania
        var studenci = new List<StudentComp>
        {
            new("Ania", 4.5), new("Bartek", 3.8), new("Celina", 4.9), new("Darek", 3.2)
        };
        studenci.Sort(); // używa IComparable<Student>
        Console.WriteLine("Posortowani po średniej:");
        studenci.ForEach(s => Console.WriteLine($"  {s}"));

        // IComparer<T> — zewnętrzny komparator (alternatywne sortowania)
        studenci.Sort(new StudentPoImieComparer());
        Console.WriteLine("Posortowani po imieniu:");
        studenci.ForEach(s => Console.WriteLine($"  {s}"));

        // Najprostsze — lambda
        studenci.Sort((a, b) => b.Srednia.CompareTo(a.Srednia)); // malejąco
        Console.WriteLine("Malejąco po średniej:");
        studenci.ForEach(s => Console.WriteLine($"  {s}"));

        // IEquatable<T> — wydajne równanie (bez boxing)
        var p1 = new PunktXY(1.0, 2.0);
        var p2 = new PunktXY(1.0, 2.0);
        var p3 = new PunktXY(3.0, 4.0);
        Console.WriteLine($"\nIEquatable: p1.Equals(p2)={p1.Equals(p2)}, p1==p2={p1 == p2}");
        var zbior = new HashSet<PunktXY> { p1, p2, p3 }; // p1 i p2 to "ten sam" punkt
        Console.WriteLine($"HashSet.Count={zbior.Count} (p1==p2 → tylko jeden)");

        // IEnumerable<T> — własna iterowalna kolekcja
        var zakres = new ZakresLiczbInter(1, 10, 2);
        Console.Write("ZakresLiczb(1,10,2): ");
        foreach (int n in zakres) Console.Write($"{n} "); // 1 3 5 7 9
        Console.WriteLine();
        Console.WriteLine($"Sum={zakres.Sum()}, Count>3={zakres.Count(n => n > 3)}  ← LINQ działa!");
    }

    // DOMYŚLNE IMPLEMENTACJE (C# 8+) i static members w interfejsie
    public static void DomyslneImplementacje()
    {
        Console.WriteLine("\n=== DEFAULT INTERFACE METHODS (C# 8+) ===");

        // KonsolaLoggerInter implementuje TYLKO Log() — reszta z domyślnych
        ILoggerInter logger = new KonsolaLoggerInter();
        logger.LogInfo("Aplikacja startuje");
        logger.LogWarning("Mała pamięć");
        logger.LogError("Krytyczny błąd!");

        // PUŁAPKA: domyślne metody dostępne tylko przez referencję INTERFEJSU
        var bezposrednio = new KonsolaLoggerInter();
        // bezposrednio.LogInfo("x"); — BŁĄD! KonsolaLoggerInter nie ma LogInfo jako własnej!
        ((ILoggerInter)bezposrednio).LogInfo("Przez cast — OK");

        Console.WriteLine("\nStatic member w interfejsie (C# 8+):");
        Console.WriteLine($"FormatujPoziom: {ILoggerInter.FormatujPoziom("INFO")}");

        // PlikLogger nadpisuje LogError z własną implementacją
        Console.WriteLine("\nPlikLogger (nadpisuje LogError):");
        ILoggerInter plikLogger = new PlikLoggerInter();
        plikLogger.LogInfo("Test pliku");
        plikLogger.LogError("Błąd pliku!");
    }

    // INTERFEJS vs KLASA ABSTRAKCYJNA — porównanie
    public static void InterfejsVsAbstrakcyjna()
    {
        Console.WriteLine("\n=== INTERFEJS vs KLASA ABSTRAKCYJNA ===");
        Console.WriteLine("┌─────────────────────────┬──────────────────────┬──────────────────────┐");
        Console.WriteLine("│ Cecha                   │ Interfejs            │ Klasa abstrakcyjna   │");
        Console.WriteLine("├─────────────────────────┼──────────────────────┼──────────────────────┤");
        Console.WriteLine("│ Pola instancyjne        │ ❌ Nie               │ ✅ Tak               │");
        Console.WriteLine("│ Konstruktory            │ ❌ Nie               │ ✅ Tak               │");
        Console.WriteLine("│ Dziedziczenie           │ Wiele interfejsów    │ Tylko jedna klasa    │");
        Console.WriteLine("│ Domyślna implementacja  │ ✅ C# 8+             │ ✅ Zawsze            │");
        Console.WriteLine("│ Modyfikatory dostępu    │ public only          │ Dowolne              │");
        Console.WriteLine("│ Semantyka               │ CAN-DO (zdolność)    │ IS-A (typ)           │");
        Console.WriteLine("└─────────────────────────┴──────────────────────┴──────────────────────┘");

        Console.WriteLine("\nUżyj INTERFEJSU gdy:");
        Console.WriteLine("  • definiujesz kontrakt (co), nie implementację (jak)");
        Console.WriteLine("  • klasy z różnych hierarchii mają tę samą zdolność");
        Console.WriteLine("  • chcesz wstrzykiwania zależności (DI)");
        Console.WriteLine("\nUżyj KLASY ABSTRAKCYJNEJ gdy:");
        Console.WriteLine("  • masz wspólny stan (pola) i częściową implementację");
        Console.WriteLine("  • stosujesz Template Method Pattern");
        Console.WriteLine("  • klasy pochodne mają naturalną hierarchię IS-A");
        Console.WriteLine("\n✅ ZŁOTY WZORZEC: interfejs (kontrakt dla DI) + abstract class (wspólna impl)");

        // Praktyczny przykład — system powiadomień
        var system = new SystemPowiadomienInter();
        system.DodajKanal(new KanalEmailInter("user@example.com"));
        system.DodajKanal(new KanalSMSInter("+48123456789"));
        system.Rozeslij(new PowiadomienieDane("Nowe zamówienie", "Zamówienie #1234 złożone"));
    }

    // STRATEGY PATTERN przez interfejsy + LINQ z IEnumerable
    public static void StrategiaWysylki()
    {
        Console.WriteLine("\n=== STRATEGY PATTERN (wysyłka) ===");

        var zamowienie = new ZamowienieWys { WagaKg = 2.5m, Kraj = "PL" };
        zamowienie.WyswietlOpcje(new IStrategiaWysylkiInter[] { new InPostInter(), new DHLInter() });

        Console.WriteLine("\nIEnumerable<T> jest LAZY — ewaluowany przy iteracji:");
        IEnumerable<int> liczby = new[] { 5, 2, 8, 1, 9, 3 };
        var wynik = liczby.Where(n => n > 3).OrderBy(n => n).Select(n => n * n);
        Console.Write("Wynik (lazy): ");
        foreach (int n in wynik) Console.Write($"{n} "); // 25 64 81
        Console.WriteLine();
    }

    // Helper
    private static void WyswietlZwierzeta(IEnumerable<IZwierzeInter> zwierzeta)
    {
        foreach (var z in zwierzeta)
        {
            Console.Write($"{z.Imie} ({z.Wiek}l): ");
            z.Dzwiek();
        }
    }
}

// ─── INTERFEJSY ────────────────────────────────────────────────────────────

public interface IZwierzeInter
{
    string Imie { get; }
    int Wiek { get; set; }
    void Dzwiek();
    void Jedz(int kalorii);
    void OpisGatunku(bool szczegolowy = false);
}

public interface IDomowy
{
    void Glaskaj();
    bool CzyLubiLudzi { get; }
}

public interface ITresowalnyInter
{
    bool Naucz(string komenda);
    IReadOnlyList<string> PoznaneKomendy { get; }
}

public interface ISerializowalnyInter { string Serializuj(); void Deserializuj(string dane); }
public interface IPersistableInter    { string Serializuj(); void Zapisz(string sciezka); }

public interface ILoggerInter
{
    void Log(string poziom, string wiadomosc);

    // Domyślne implementacje (C# 8+)
    void LogInfo(string msg) => Log("INFO", msg);
    void LogWarning(string msg) => Log("WARNING", msg);
    void LogError(string msg) => Log("ERROR", msg);

    // Static member w interfejsie (C# 8+)
    static string FormatujPoziom(string poziom) => $"[{poziom.ToUpper().PadRight(7)}]";
}

public interface IKanalPowiadomienInter
{
    string Nazwa { get; }
    bool CzyDostepny { get; }
    void Wyslij(IPowiadomienieDane powiadomienie);
}

public interface IPowiadomienieDane
{
    string Tytul { get; }
    string Tresc { get; }
}

public interface IStrategiaWysylkiInter
{
    decimal ObliczKoszt(decimal wagaKg, string kraj);
    string NazwaKuriera { get; }
    int DniDostawaPL { get; }
}

// ─── KLASY POMOCNICZE ────────────────────────────────────────────────────────

internal class KotInter : IZwierzeInter
{
    public string Imie { get; }
    public int Wiek { get; set; }

    public KotInter(string imie, int wiek) { Imie = imie; Wiek = wiek; }

    public void Dzwiek() => Console.WriteLine($"{Imie} mruczy: Mrrr...");
    public void Jedz(int kalorii) => Console.WriteLine($"{Imie} je elegancko. Kalorie: {kalorii}");
    public void OpisGatunku(bool szczegolowy = false) { Console.WriteLine("Felis catus."); }
}

internal class PiesInter : IZwierzeInter, IDomowy, ITresowalnyInter
{
    private readonly List<string> _komendy = new();

    public string Imie { get; }
    public int Wiek { get; set; }
    public bool CzyLubiLudzi { get; } = true;
    public IReadOnlyList<string> PoznaneKomendy => _komendy.AsReadOnly();

    public PiesInter(string imie, int wiek) { Imie = imie; Wiek = wiek; }

    public void Dzwiek() => Console.WriteLine($"{Imie}: Hau hau!");
    public void Jedz(int k) => Console.WriteLine($"{Imie} je łapczywie!");
    public void Glaskaj() => Console.WriteLine($"{Imie} macha ogonem!");
    public void OpisGatunku(bool szcz = false) => Console.WriteLine("Canis lupus familiaris.");

    public bool Naucz(string komenda)
    {
        if (_komendy.Contains(komenda)) return false;
        _komendy.Add(komenda);
        Console.WriteLine($"  {Imie} nauczył się: '{komenda}'!");
        return true;
    }
}

internal class DokumentInter : ISerializowalnyInter, IPersistableInter
{
    public string Tytul { get; }
    private string _tresc = "";

    public DokumentInter(string tytul) => Tytul = tytul;

    // Explicit — dostępne TYLKO przez referencję interfejsu
    string ISerializowalnyInter.Serializuj() => $"{{\"tytul\":\"{Tytul}\",\"tresc\":\"{_tresc}\"}}";
    string IPersistableInter.Serializuj() => $"<dokument><tytul>{Tytul}</tytul></dokument>";

    void ISerializowalnyInter.Deserializuj(string dane) => _tresc = dane.Contains("tresc") ? "odtworzono" : "";
    void IPersistableInter.Zapisz(string sciezka) => Console.WriteLine($"Zapisuję XML do {sciezka}");

    public void EdytujTresc(string t) => _tresc = t;
}

internal class PolaczenieTrans : IDisposable
{
    private bool _disposed;
    public void OtworzTransakcje() => Console.WriteLine("  Transakcja otwarta");
    void IDisposable.Dispose() // explicit — ukrywa Dispose() w API klasy
    {
        if (_disposed) return;
        Console.WriteLine("  Połączenie zamknięte (explicit Dispose)");
        _disposed = true;
    }
}

internal class StudentComp : IComparable<StudentComp>
{
    public string Imie { get; set; }
    public double Srednia { get; set; }

    public StudentComp(string imie, double srednia) { Imie = imie; Srednia = srednia; }

    // IComparable<T> — naturalny porządek (rosnąco po średniej)
    public int CompareTo(StudentComp? other)
    {
        if (other == null) return 1;
        return Srednia.CompareTo(other.Srednia);
    }

    public override string ToString() => $"{Imie} ({Srednia:F1})";
}

internal class StudentPoImieComparer : IComparer<StudentComp>
{
    public int Compare(StudentComp? x, StudentComp? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return string.Compare(x.Imie, y.Imie, StringComparison.Ordinal);
    }
}

internal class PunktXY : IEquatable<PunktXY>
{
    public double X { get; }
    public double Y { get; }

    public PunktXY(double x, double y) { X = x; Y = y; }

    // IEquatable<T> — typowane porównanie, bez boxing
    public bool Equals(PunktXY? other)
    {
        if (other is null) return false;
        return Math.Abs(X - other.X) < 1e-10 && Math.Abs(Y - other.Y) < 1e-10;
    }

    public override bool Equals(object? obj) => Equals(obj as PunktXY);
    public override int GetHashCode() => HashCode.Combine(Math.Round(X, 10), Math.Round(Y, 10));

    public static bool operator ==(PunktXY? a, PunktXY? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(PunktXY? a, PunktXY? b) => !(a == b);

    public override string ToString() => $"({X}, {Y})";
}

internal class ZakresLiczbInter : IEnumerable<int>
{
    private readonly int _od, _do, _krok;

    public ZakresLiczbInter(int od, int do_, int krok = 1) { _od = od; _do = do_; _krok = krok; }

    public IEnumerator<int> GetEnumerator()
    {
        for (int i = _od; i <= _do; i += _krok)
            yield return i; // lazy — generuje po jednej wartości
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal class KonsolaLoggerInter : ILoggerInter
{
    // Implementuje TYLKO Log() — reszta używa domyślnych implementacji
    public void Log(string poziom, string wiadomosc)
        => Console.WriteLine($"{ILoggerInter.FormatujPoziom(poziom)} {DateTime.Now:HH:mm:ss} {wiadomosc}");
}

internal class PlikLoggerInter : ILoggerInter
{
    public void Log(string poziom, string wiadomosc)
        => Console.WriteLine($"[PLIK] {DateTime.Now:HH:mm:ss} [{poziom}] {wiadomosc}");

    // Nadpisuje domyślną LogError z własną implementacją
    public void LogError(string msg)
    {
        Log("ERROR", msg);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ALERT] Błąd: {msg}");
        Console.ResetColor();
    }
}

// System powiadomień — interfejs + klasa abstrakcyjna razem
internal record PowiadomienieDane(string Tytul, string Tresc) : IPowiadomienieDane;

internal abstract class KanalPowiadomieniBase : IKanalPowiadomienInter
{
    public string Nazwa { get; }
    public bool CzyDostepny { get; protected set; } = true;
    private int _wyslanych = 0;

    protected KanalPowiadomieniBase(string nazwa) => Nazwa = nazwa;

    // Template method
    public void Wyslij(IPowiadomienieDane powiadomienie)
    {
        if (!CzyDostepny) { Console.WriteLine($"  [{Nazwa}] Kanał niedostępny"); return; }
        try
        {
            WyslijWewnetrznie(powiadomienie);
            _wyslanych++;
            Console.WriteLine($"  [{Nazwa}] ✓ Wysłano: {powiadomienie.Tytul}");
        }
        catch (Exception ex) { Console.WriteLine($"  [{Nazwa}] ✗ Błąd: {ex.Message}"); }
    }

    protected abstract void WyslijWewnetrznie(IPowiadomienieDane p);
    public string Statystyki => $"{Nazwa}: wysłanych={_wyslanych}";
}

internal class KanalEmailInter : KanalPowiadomieniBase
{
    private readonly string _email;
    public KanalEmailInter(string email) : base("Email") => _email = email;
    protected override void WyslijWewnetrznie(IPowiadomienieDane p)
        => Console.WriteLine($"    Email → {_email}: [{p.Tytul}] {p.Tresc}");
}

internal class KanalSMSInter : KanalPowiadomieniBase
{
    private readonly string _numer;
    public KanalSMSInter(string numer) : base("SMS") => _numer = numer;
    protected override void WyslijWewnetrznie(IPowiadomienieDane p)
    {
        if (p.Tresc.Length > 160) throw new InvalidOperationException("SMS za długi");
        Console.WriteLine($"    SMS → {_numer}: {p.Tresc[..Math.Min(30, p.Tresc.Length)]}");
    }
}

internal class SystemPowiadomienInter
{
    private readonly List<IKanalPowiadomienInter> _kanaly = new();

    public void DodajKanal(IKanalPowiadomienInter k) => _kanaly.Add(k);

    public void Rozeslij(IPowiadomienieDane p)
    {
        Console.WriteLine($"  Rozsyłam: {p.Tytul}");
        foreach (var k in _kanaly.Where(k => k.CzyDostepny))
            k.Wyslij(p);
    }
}

// Strategy Pattern
internal class InPostInter : IStrategiaWysylkiInter
{
    public string NazwaKuriera => "InPost";
    public int DniDostawaPL => 1;
    public decimal ObliczKoszt(decimal waga, string kraj)
        => kraj == "PL" ? waga * 2.5m + 5m : waga * 8m + 20m;
}

internal class DHLInter : IStrategiaWysylkiInter
{
    public string NazwaKuriera => "DHL";
    public int DniDostawaPL => 2;
    public decimal ObliczKoszt(decimal waga, string kraj)
        => kraj == "PL" ? waga * 3m + 8m : waga * 6m + 15m;
}

internal class ZamowienieWys
{
    public string Kraj { get; init; } = "PL";
    public decimal WagaKg { get; init; }

    public void WyswietlOpcje(IEnumerable<IStrategiaWysylkiInter> strategie)
    {
        Console.WriteLine($"  Opcje wysyłki dla {WagaKg}kg do {Kraj}:");
        foreach (var s in strategie.OrderBy(s => s.ObliczKoszt(WagaKg, Kraj)))
            Console.WriteLine($"    {s.NazwaKuriera,-10} {s.ObliczKoszt(WagaKg, Kraj),8:C}  ~{s.DniDostawaPL}d");
    }
}
