namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// TYPY WYLICZENIOWE (ENUM) I STRUKTURY (STRUCT)
// Enum = zbiór nazwanych stałych całkowitoliczbowych
// Struct = value type — kopia przy przypisaniu, żyje na stosie (lub inline)
// ─────────────────────────────────────────────────────────────────────────────

public static class EnumyIStruktury
{
    // PODSTAWY ENUM
    public static void PodstawyEnum()
    {
        Console.WriteLine("\n=== PODSTAWY ENUM ===");

        DzienTygodnia dzien = DzienTygodnia.Sroda;
        Console.WriteLine($"dzien = {dzien}");           // Sroda
        Console.WriteLine($"(int)dzien = {(int)dzien}"); // 2

        bool czyWeekend = dzien is DzienTygodnia.Sobota or DzienTygodnia.Niedziela;
        Console.WriteLine($"CzyWeekend: {czyWeekend}"); // False

        // Switch expression — czytelne
        string typ = dzien switch
        {
            DzienTygodnia.Sobota or DzienTygodnia.Niedziela => "Weekend",
            DzienTygodnia.Piatek                             => "Prawie weekend",
            _                                                => "Dzień roboczy"
        };
        Console.WriteLine($"Typ dnia: {typ}");

        // KodHTTP — własne wartości
        KodHTTP kod = KodHTTP.NotFound;
        Console.WriteLine($"kod = {kod}, int = {(int)kod}"); // NotFound, 404

        KodHTTP z404 = (KodHTTP)404;
        Console.WriteLine($"(KodHTTP)404 = {z404}"); // NotFound

        // WAŻNE: cast ZAWSZE się powiedzie, nawet dla nieznanych wartości!
        KodHTTP zly = (KodHTTP)999;
        Console.WriteLine($"(KodHTTP)999 = {zly}"); // 999 — nie rzuca wyjątku!
        Console.WriteLine($"IsDefined(999): {Enum.IsDefined(typeof(KodHTTP), 999)}"); // False

        // Bezpieczna konwersja string → enum
        if (Enum.TryParse<KodHTTP>("NotFound", out KodHTTP wynik))
            Console.WriteLine($"TryParse 'NotFound': {wynik}");
        if (Enum.TryParse<KodHTTP>("200", out KodHTTP wynik2))
            Console.WriteLine($"TryParse '200': {wynik2}");
    }

    // FLAGS — flagi bitowe
    public static void FlagiEnum()
    {
        Console.WriteLine("\n=== FLAGS ENUM ===");

        Uprawnienia uprAnii = Uprawnienia.Odczyt | Uprawnienia.Zapis;
        Console.WriteLine($"uprAnii = {uprAnii}"); // Odczyt, Zapis (dzięki [Flags] ładny ToString)

        // Sprawdzanie — HasFlag (czytelne)
        Console.WriteLine($"HasFlag(Zapis): {uprAnii.HasFlag(Uprawnienia.Zapis)}");      // True
        Console.WriteLine($"HasFlag(Usuwanie): {uprAnii.HasFlag(Uprawnienia.Usuwanie)}"); // False

        // Dodaj uprawnienie
        uprAnii |= Uprawnienia.Usuwanie;
        Console.WriteLine($"Po dodaniu Usuwanie: {uprAnii}");

        // Usuń uprawnienie — AND z negacją
        uprAnii &= ~Uprawnienia.Usuwanie;
        Console.WriteLine($"Po usunięciu Usuwanie: {uprAnii}");

        // Toggle (przełącz)
        uprAnii ^= Uprawnienia.Odczyt;
        Console.WriteLine($"Po toggle Odczyt: {uprAnii}"); // Zapis

        // Sprawdzenie wielu flag naraz
        var admin = Uprawnienia.Admin;
        bool maOdczytIZapis = admin.HasFlag(Uprawnienia.Odczyt | Uprawnienia.Zapis);
        Console.WriteLine($"Admin ma Odczyt|Zapis: {maOdczytIZapis}"); // True

        Console.WriteLine("Wartości muszą być potęgami 2: 1,2,4,8,... — każda zajmuje jeden bit");
    }

    // ENUM API — GetValues, GetNames, Parse
    public static void MetodyEnum()
    {
        Console.WriteLine("\n=== ENUM API ===");

        Kolor[] kolory = Enum.GetValues<Kolor>();
        Console.WriteLine($"GetValues: {string.Join(", ", kolory)}");

        string[] nazwy = Enum.GetNames<Kolor>();
        Console.WriteLine($"GetNames: {string.Join(", ", nazwy)}");

        // Parse — rzuca wyjątek gdy nieprawidłowe
        Kolor z = Enum.Parse<Kolor>("Zielony");
        Console.WriteLine($"Parse 'Zielony': {z}");

        // TryParse — bezpieczne
        bool ok = Enum.TryParse<Kolor>("Niebieski", out Kolor niebieski);
        Console.WriteLine($"TryParse 'Niebieski': ok={ok}, wartość={niebieski}");

        bool notOk = Enum.TryParse<Kolor>("Różowy", out Kolor _);
        Console.WriteLine($"TryParse 'Różowy': ok={notOk}");

        Console.WriteLine($"Liczba wartości: {Enum.GetValues<Kolor>().Length}");

        // GetName — nazwa wartości
        string? nazwa = Enum.GetName(typeof(Kolor), 2);
        Console.WriteLine($"GetName(2): {nazwa}"); // Niebieski
    }

    // EXTENSION METHODS DLA ENUM
    public static void RozszerzeniaEnum()
    {
        Console.WriteLine("\n=== EXTENSION METHODS DLA ENUM ===");

        var status = StatusZamowienia.WRealizacji;
        Console.WriteLine($"Status: {status.Opis()}");
        Console.WriteLine($"CzyAktywne: {status.CzyAktywne()}");
        Console.WriteLine($"MoznaAnulowac: {status.MoznaAnulowac()}");
        Console.WriteLine($"KolorUI: {status.KolorUI()}");

        Console.Write("Następne statusy: ");
        foreach (var s in status.NastepneStatusy())
            Console.Write($"{s.Opis()} | ");
        Console.WriteLine();

        // State machine — enum modeluje stany
        Console.WriteLine("\n--- State machine: sygnalizacja świetlna ---");
        var sygnalizacja = new SygnalizacjaSwietlna();
        Console.WriteLine($"Stan: {sygnalizacja.AktualnyStan} → {sygnalizacja.Instrukcja()}");
        sygnalizacja.Przelacz();
        Console.WriteLine($"Stan: {sygnalizacja.AktualnyStan} → {sygnalizacja.Instrukcja()}");
        sygnalizacja.Przelacz();
        Console.WriteLine($"Stan: {sygnalizacja.AktualnyStan} → {sygnalizacja.Instrukcja()}");
    }

    // STRUCT vs CLASS
    public static void StructVsClass()
    {
        Console.WriteLine("\n=== STRUCT vs CLASS ===");

        // STRUCT — value type, kopia przy przypisaniu
        WalutaStruct pln = new(100m, "PLN");
        WalutaStruct pln2 = pln; // KOPIA wartości — niezależna!
        // pln2 = niezależna kopia, modyfikacja nie wpływa na pln

        WalutaStruct suma = pln + new WalutaStruct(50m, "PLN");
        Console.WriteLine($"pln: {pln}");
        Console.WriteLine($"suma: {suma}");
        Console.WriteLine($"pln * 1.5: {pln * 1.5m}");
        Console.WriteLine($"pln == suma: {pln == suma}"); // False

        // CLASS — reference type, kopia referencji
        Console.WriteLine("\nStruct: kopia wartości (niezależna)");
        Console.WriteLine("Class:  kopia referencji (ten sam obiekt)");

        Console.WriteLine("\nWytyczne użycia STRUCT:");
        Console.WriteLine("  ✅ Mały rozmiar (≤ 16-24 bajtów)");
        Console.WriteLine("  ✅ Immutable (readonly struct)");
        Console.WriteLine("  ✅ Value semantics (kopia = niezależna wartość)");
        Console.WriteLine("  ✅ Krótki czas życia, dużo instancji w tablicy");
        Console.WriteLine("  ❌ > 24 bajtów, mutowalny, polimorfizm, dziedziczenie");
    }

    // READONLY STRUCT + in + Span<T>
    public static void ReadonlyStructISpan()
    {
        Console.WriteLine("\n=== READONLY STRUCT I Span<T> ===");

        var p = new Punkt3DStruct(1, 2, 3);
        Console.WriteLine($"Punkt: {p}, długość: {p.Dlugosc:F3}");
        Console.WriteLine($"Znormalizowany: {p.Normalizuj()}");

        // in — przekazanie przez referencję bez kopii (readonly struct)
        WypiszPunkt(p); // zero kopii dzięki readonly struct

        // Span<T> — zero-copy okno na pamięć
        Console.WriteLine("\nSpan<T> — zero-copy:");
        int[] tablica = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Span<int> srodek = tablica.AsSpan(2, 5); // elementy 2..6 bez kopii!
        Console.Write("Span(2, 5): ");
        foreach (int x in srodek) Console.Write($"{x} "); // 3 4 5 6 7
        Console.WriteLine();

        srodek[0] = 99; // modyfikuje ORYGINAŁ!
        Console.WriteLine($"tablica[2] po modyfikacji Span: {tablica[2]}"); // 99

        // ReadOnlySpan<char> — zero-copy substring
        string tekst = "Hello, World!";
        ReadOnlySpan<char> fragment = tekst.AsSpan(7, 5); // "World" — ZERO KOPII
        Console.WriteLine($"AsSpan(7,5): {fragment.ToString()}");
        // vs tekst.Substring(7,5) — alokuje nowy string!

        Console.WriteLine("\nStackalloc — alokacja na stosie:");
        Span<int> bufor = stackalloc int[8]; // 0 alokacji na heap!
        for (int i = 0; i < bufor.Length; i++) bufor[i] = i * i;
        Console.Write("stackalloc: ");
        foreach (int n in bufor) Console.Write($"{n} ");
        Console.WriteLine();
    }

    // BOXING I UNBOXING
    public static void BoxingIUnboxing()
    {
        Console.WriteLine("\n=== BOXING I UNBOXING ===");

        // Boxing: value type → heap
        int liczba = 42;
        object boxed = liczba;    // BOXING — nowy obiekt na heap!
        int unboxed = (int)boxed; // UNBOXING — kopiowanie z powrotem
        Console.WriteLine($"boxed.GetType(): {boxed.GetType().Name}"); // Int32, nie Nullable<int>

        Console.WriteLine("\nKiedy boxing następuje (często nieświadomie):");
        Console.WriteLine("  1. Przypisanie do object: object o = 42;");
        Console.WriteLine("  2. Interfejs bez generic constraint: IComparable c = 42;");
        Console.WriteLine("  3. ArrayList (pre-generic): lista.Add(42);");

        // Benchmark konceptualny
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var al = new System.Collections.ArrayList();
        for (int n = 0; n < 500_000; n++) al.Add(n); // 500K boxing!
        sw.Stop();
        long msArrayList = sw.ElapsedMilliseconds;

        sw.Restart();
        var gl = new List<int>();
        for (int n = 0; n < 500_000; n++) gl.Add(n); // 0 boxing
        sw.Stop();
        Console.WriteLine($"\nArrayList (boxing):  {msArrayList}ms");
        Console.WriteLine($"List<int> (no boxing): {sw.ElapsedMilliseconds}ms");

        // Nullable<T> boxing — specjalna reguła CLR
        int? hasValue = 42;
        int? noValue = null;
        object boxedHas = hasValue; // boxing int? z wartością → boxed INT (nie Nullable!)
        object? boxedNo = noValue;  // boxing int? bez wartości → null
        Console.WriteLine($"\nNullable boxing: boxedHas.GetType()={boxedHas?.GetType().Name}"); // Int32!
        Console.WriteLine($"Nullable boxing: boxedNo == null: {boxedNo == null}");              // True

        Console.WriteLine("\nJak unikać boxing:");
        Console.WriteLine("  ✅ List<int> zamiast ArrayList");
        Console.WriteLine("  ✅ Generyki z 'where T : struct'");
        Console.WriteLine("  ✅ Interfejsy generyczne (List<int> zamiast IList z int)");
    }

    // RECORD STRUCT (C# 10+)
    public static void RecordStruct()
    {
        Console.WriteLine("\n=== RECORD STRUCT (C# 10+) ===");

        var w1 = new WektorRS(1, 2, 3);
        var w2 = new WektorRS(1, 2, 3);
        var w3 = new WektorRS(4, 5, 6);

        // Wygenerowane automatycznie: Equals (VALUE!), GetHashCode, ToString, with
        Console.WriteLine($"w1 = {w1}");
        Console.WriteLine($"w1 == w2 (te same dane): {w1 == w2}");  // True — value equality
        Console.WriteLine($"w1 == w3 (różne dane): {w1 == w3}");    // False

        // with — niedestrukcyjna kopia z modyfikacją
        var w4 = w1 with { Z = 10 };
        Console.WriteLine($"w4 = w1 with Z=10: {w4}");
        Console.WriteLine($"w1 niezmieniony: {w1}");
        Console.WriteLine($"Długość w1: {w1.Dlugosc:F3}");

        // readonly record struct — immutable + value type + generated code
        var czerwony = KolorRGB.Czerwony;
        Console.WriteLine($"\nKolorRGB.Czerwony: {czerwony}");
        Console.WriteLine($"HexKod: {czerwony.HexKod}");
        Console.WriteLine($"Przyciemniony: {czerwony.Przyciemnij(0.5)}");

        Console.WriteLine("\nStruct z record: zwięzła składnia, value equality, with expression");
        Console.WriteLine("readonly record struct = immutable value type — najlepsza forma dla małych danych");
    }

    // Helper
    private static void WypiszPunkt(in Punkt3DStruct punkt)
    {
        // in = readonly ref, zero kopii dla readonly struct
        Console.WriteLine($"  in Punkt3D: {punkt}");
        // punkt.X = 5; — BŁĄD: in parameter jest readonly
    }
}

// ─── ENUM DEFINICJE ──────────────────────────────────────────────────────────

public enum DzienTygodnia { Poniedzialek, Wtorek, Sroda, Czwartek, Piatek, Sobota, Niedziela }

public enum KodHTTP : int
{
    OK = 200, Created = 201, NoContent = 204,
    BadRequest = 400, Unauthorized = 401, Forbidden = 403, NotFound = 404,
    InternalServerError = 500, ServiceUnavailable = 503
}

[Flags]
public enum Uprawnienia
{
    Brak      = 0,
    Odczyt    = 1,     // 0001
    Zapis     = 2,     // 0010
    Usuwanie  = 4,     // 0100
    Administracja = 8, // 1000
    UzytkownikStandard = Odczyt | Zapis,
    Moderator          = Odczyt | Zapis | Usuwanie,
    Admin              = Odczyt | Zapis | Usuwanie | Administracja
}

public enum Kolor { Czerwony, Zielony, Niebieski, Zolty, Fioletowy }

public enum StatusZamowienia
{
    Nowe = 1, Potwierdzone = 2, WRealizacji = 3,
    Wyslane = 4, Dostarczone = 5, Anulowane = 6, Zwrocone = 7
}

// Extension methods dla enum
public static class StatusZamowieniaExtensions
{
    public static string Opis(this StatusZamowienia s) => s switch
    {
        StatusZamowienia.Nowe         => "Nowe zamówienie",
        StatusZamowienia.Potwierdzone => "Zamówienie potwierdzone",
        StatusZamowienia.WRealizacji  => "W trakcie realizacji",
        StatusZamowienia.Wyslane      => "Wysłane do klienta",
        StatusZamowienia.Dostarczone  => "Dostarczone",
        StatusZamowienia.Anulowane    => "Anulowane",
        StatusZamowienia.Zwrocone     => "Zwrócone",
        _                             => "Nieznany"
    };

    public static bool CzyAktywne(this StatusZamowienia s) =>
        s is StatusZamowienia.Nowe or StatusZamowienia.Potwierdzone
          or StatusZamowienia.WRealizacji or StatusZamowienia.Wyslane;

    public static bool MoznaAnulowac(this StatusZamowienia s) =>
        s is StatusZamowienia.Nowe or StatusZamowienia.Potwierdzone;

    public static string KolorUI(this StatusZamowienia s) => s switch
    {
        StatusZamowienia.Nowe         => "blue",
        StatusZamowienia.Potwierdzone => "cyan",
        StatusZamowienia.WRealizacji  => "orange",
        StatusZamowienia.Wyslane      => "purple",
        StatusZamowienia.Dostarczone  => "green",
        StatusZamowienia.Anulowane    => "red",
        StatusZamowienia.Zwrocone     => "gray",
        _                             => "black"
    };

    public static IReadOnlyList<StatusZamowienia> NastepneStatusy(this StatusZamowienia s) => s switch
    {
        StatusZamowienia.Nowe         => new[] { StatusZamowienia.Potwierdzone, StatusZamowienia.Anulowane },
        StatusZamowienia.Potwierdzone => new[] { StatusZamowienia.WRealizacji, StatusZamowienia.Anulowane },
        StatusZamowienia.WRealizacji  => new[] { StatusZamowienia.Wyslane },
        StatusZamowienia.Wyslane      => new[] { StatusZamowienia.Dostarczone, StatusZamowienia.Zwrocone },
        _                             => Array.Empty<StatusZamowienia>()
    };
}

// State machine
public class SygnalizacjaSwietlna
{
    public enum StanSwiatla { Czerwone, Zolte, Zielone }

    private static readonly Dictionary<StanSwiatla, (StanSwiatla nastepny, int sekundy)>
        _przejscia = new()
        {
            [StanSwiatla.Czerwone] = (StanSwiatla.Zielone, 30),
            [StanSwiatla.Zielone]  = (StanSwiatla.Zolte, 25),
            [StanSwiatla.Zolte]    = (StanSwiatla.Czerwone, 5)
        };

    public StanSwiatla AktualnyStan { get; private set; } = StanSwiatla.Czerwone;

    public void Przelacz()
    {
        var (nastepny, _) = _przejscia[AktualnyStan];
        Console.WriteLine($"  Zmiana: {AktualnyStan} → {nastepny}");
        AktualnyStan = nastepny;
    }

    public int CzasDoZmiany() => _przejscia[AktualnyStan].sekundy;
    public bool CzyMoznaPrzejsc() => AktualnyStan == StanSwiatla.Zielone;

    public string Instrukcja() => AktualnyStan switch
    {
        StanSwiatla.Czerwone => "STÓJ",
        StanSwiatla.Zolte    => "UWAGA",
        StanSwiatla.Zielone  => "JEDŹ",
        _                    => throw new InvalidOperationException()
    };
}

// ─── STRUCT DEFINICJE ────────────────────────────────────────────────────────

internal struct WalutaStruct
{
    private readonly decimal _kwota;
    private readonly string _kod;

    public WalutaStruct(decimal kwota, string kod)
    {
        if (kwota < 0) throw new ArgumentException("Kwota ujemna");
        if (string.IsNullOrWhiteSpace(kod)) throw new ArgumentException("Kod wymagany");
        _kwota = kwota;
        _kod = kod.ToUpper();
    }

    public decimal Kwota => _kwota;
    public string Kod => _kod;

    public WalutaStruct Dodaj(WalutaStruct inna)
    {
        if (_kod != inna._kod) throw new InvalidOperationException($"Nie można dodać {_kod} i {inna._kod}");
        return new WalutaStruct(_kwota + inna._kwota, _kod);
    }

    // Operatory — naturalne dla struct
    public static WalutaStruct operator +(WalutaStruct a, WalutaStruct b) => a.Dodaj(b);
    public static WalutaStruct operator *(WalutaStruct w, decimal m) => new(w._kwota * m, w._kod);
    public static bool operator ==(WalutaStruct a, WalutaStruct b) => a._kwota == b._kwota && a._kod == b._kod;
    public static bool operator !=(WalutaStruct a, WalutaStruct b) => !(a == b);

    // Dla struct ZAWSZE nadpisuj Equals i GetHashCode
    public override bool Equals(object? obj) => obj is WalutaStruct w && w == this;
    public override int GetHashCode() => HashCode.Combine(_kwota, _kod);
    public override string ToString() => $"{_kwota:F2} {_kod}";
}

// readonly struct — kompilator gwarantuje immutability
internal readonly struct Punkt3DStruct
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Punkt3DStruct(double x, double y, double z) { X = x; Y = y; Z = z; }

    public double Dlugosc => Math.Sqrt(X * X + Y * Y + Z * Z);

    // Zwraca NOWY struct — nie mutuje (immutable pattern)
    public Punkt3DStruct Normalizuj()
    {
        double d = Dlugosc;
        return new Punkt3DStruct(X / d, Y / d, Z / d);
    }

    public static Punkt3DStruct Zero => new(0, 0, 0);

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

// record struct (C# 10+) — value type + generated code
internal record struct WektorRS(double X, double Y, double Z)
{
    public double Dlugosc => Math.Sqrt(X * X + Y * Y + Z * Z);
}

// readonly record struct — immutable + value type + generated code
internal readonly record struct KolorRGB(byte R, byte G, byte B)
{
    public static KolorRGB Czerwony  => new(255, 0, 0);
    public static KolorRGB Zielony   => new(0, 255, 0);
    public static KolorRGB Niebieski => new(0, 0, 255);

    public KolorRGB Przyciemnij(double wsp) =>
        new((byte)(R * wsp), (byte)(G * wsp), (byte)(B * wsp));

    public string HexKod => $"#{R:X2}{G:X2}{B:X2}";
}
