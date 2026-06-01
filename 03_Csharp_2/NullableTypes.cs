using System.Diagnostics.CodeAnalysis;

namespace _03_Csharp_2;

// ── Klasy pomocnicze dla NullableTypes ───────────────────────────────────────

public class KonfiguracjaNT
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Haslo { get; set; }
    public bool? SslWlaczone { get; set; }
    public LimitNT? Limity { get; set; }
}

public class LimitNT
{
    public int? MaxPolaczen { get; set; }
    public int? Timeout { get; set; }
}

public class SerwisKonfiguracjiNT
{
    private readonly KonfiguracjaNT? _konfig;

    public SerwisKonfiguracjiNT(KonfiguracjaNT? konfig) => _konfig = konfig;

    public string PobierzHost() => _konfig?.Host ?? "localhost";

    public int PobierzPort() => _konfig?.Port ?? 5432;

    public bool CzySslWlaczone() => _konfig?.SslWlaczone ?? false;

    public int PobierzMaxPolaczen()
        => _konfig?.Limity?.MaxPolaczen ?? 10;

    public string PobierzUrl()
        => $"{(CzySslWlaczone() ? "https" : "http")}://{PobierzHost()}:{PobierzPort()}";
}

public class UzytkownikNT
{
    public int Id { get; set; }
    public string? Imie { get; set; }
    public string? Email { get; set; }
    private string? _imie;

    [MemberNotNull(nameof(_imie))]
    public void UstawImie(string imie) => _imie = imie;
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class NullableTypes
{
    public static void NullableValueTypes()
    {
        Console.WriteLine("\n── NullableValueTypes ──");

        // Nullable<T> / T? — wrapper dla value types
        // Wewnętrznie: struct Nullable<T> { bool HasValue; T Value; }
        int? a = 42;
        int? b = null;

        Console.WriteLine($"a.HasValue={a.HasValue}, a.Value={a.Value}");
        Console.WriteLine($"b.HasValue={b.HasValue}");
        // b.Value — InvalidOperationException gdy HasValue=false!

        // GetValueOrDefault — bezpieczny odczyt
        Console.WriteLine($"b.GetValueOrDefault()={b.GetValueOrDefault()}");
        Console.WriteLine($"b.GetValueOrDefault(99)={b.GetValueOrDefault(99)}");

        // Boxing Nullable<T>: gdy null — box to null (NIE do Nullable<T>)
        int? nullInt = null;
        object? boxed = nullInt; // box — wynik to null, nie Nullable<int>!
        Console.WriteLine($"boxed is null: {boxed is null}"); // True

        int? noValue = 5;
        object? boxed5 = noValue; // box — wynik to int (5), nie Nullable<int>!
        Console.WriteLine($"boxed5 is int: {boxed5 is int}"); // True

        // Konwersje
        int? x = 5;
        int y = (int)x!;          // jawne rzutowanie (throw jeśli null)
        int z = x ?? 0;           // bezpieczne przez ??
        Console.WriteLine($"int? 5 → int: jawne={y}, ??(0)={z}");

        // Nullable arithmetic — "lifted operators"
        int? p = 3, q = null;
        Console.WriteLine($"3 + null = {p + q}");  // null
        Console.WriteLine($"3 * 4 = {p * 4}");     // 12
        Console.WriteLine($"null > 3 = {q > p}");  // false (nie null!)
    }

    public static void OperatoryNull()
    {
        Console.WriteLine("\n── OperatoryNull ──");

        // ?? — coalescing: lewa wartość jeśli nie-null, inaczej prawa
        string? name = null;
        string wyswietlana = name ?? "Anonim";
        Console.WriteLine($"null ?? \"Anonim\" = {wyswietlana}");

        int? liczba = null;
        int domyslna = liczba ?? -1;
        Console.WriteLine($"null ?? -1 = {domyslna}");

        // Łańcuchowanie ??
        string? a = null, b = null, c = "znaleziono";
        string wynik = a ?? b ?? c ?? "ostatni";
        Console.WriteLine($"null ?? null ?? \"znaleziono\" = {wynik}");

        // ??= — przypisz jeśli null (leniwa prawa strona)
        string? zmienna = null;
        zmienna ??= "inicjalizacja";
        Console.WriteLine($"??= po inicjalizacji: {zmienna}");
        zmienna ??= "nie zostanie przypisane"; // już nie null
        Console.WriteLine($"??= gdy nie null: {zmienna}");

        // ?. — null-conditional: wywołaj metodę/właściwość jeśli nie null
        string? tekst = null;
        int? dlugosc = tekst?.Length;
        Console.WriteLine($"null?.Length = {dlugosc}");  // null (nie NullReferenceException)

        string? tekst2 = "Hello";
        int? dlugosc2 = tekst2?.Length;
        Console.WriteLine($"\"Hello\"?.Length = {dlugosc2}");  // 5

        // Łańcuchowanie ?.
        KonfiguracjaNT? konfig = null;
        int? maxPol = konfig?.Limity?.MaxPolaczen;
        Console.WriteLine($"null?.Limity?.MaxPolaczen = {maxPol}");

        var konfig2 = new KonfiguracjaNT
        {
            Limity = new LimitNT { MaxPolaczen = 50 }
        };
        int? maxPol2 = konfig2?.Limity?.MaxPolaczen;
        Console.WriteLine($"konfig?.Limity?.MaxPolaczen = {maxPol2}");

        // ?[] — null-conditional na indekserze
        int[]? tablica = null;
        int? el = tablica?[0];
        Console.WriteLine($"null?[0] = {el}");

        int[]? tablica2 = { 10, 20, 30 };
        int? el2 = tablica2?[1];
        Console.WriteLine($"tablica?[1] = {el2}");

        // Kombinacja: ?. + ?? razem
        string? login = null;
        string wyswietlanyLogin = login?.ToUpper() ?? "GOŚĆ";
        Console.WriteLine($"null?.ToUpper() ?? \"GOŚĆ\" = {wyswietlanyLogin}");
    }

    public static void LiftedOperatorsIBoolTrojwartosciowy()
    {
        Console.WriteLine("\n── LiftedOperatorsIBoolTrojwartosciowy ──");

        // Lifted operators — operatory arytmetyczne przeniesione na Nullable<T>
        // Wynik: null gdy JEDEN z operandów jest null
        int? a = 5, b = null;
        Console.WriteLine($"5 + null = {a + b}");     // null
        Console.WriteLine($"5 * null = {a * b}");     // null
        Console.WriteLine($"5 - null = {a - b}");     // null

        // Porównania — NIE zwracają null! Zwracają bool
        // null > 5 → false (nie null)
        // null == null → true
        // null == 5 → false
        int? x = null;
        Console.WriteLine($"null > 5 = {x > 5}");     // false
        Console.WriteLine($"null < 5 = {x < 5}");     // false
        Console.WriteLine($"null == null = {x == null}");  // true
        Console.WriteLine($"null != null = {x != null}");  // false

        // bool? — logika trójwartościowa (SQL BOOLEAN)
        // true, false, null (nieznany)
        bool? t = true, f = false, n = null;

        // AND: null & true = null, null & false = false, null & null = null
        Console.WriteLine($"null & true  = {n & t}");   // null
        Console.WriteLine($"null & false = {n & f}");   // False (wiadomo że false)
        Console.WriteLine($"null & null  = {n & n}");   // null
        Console.WriteLine($"true & true  = {t & t}");   // True

        // OR: null | true = true, null | false = null, null | null = null
        Console.WriteLine($"null | true  = {n | t}");   // True (wiadomo że true)
        Console.WriteLine($"null | false = {n | f}");   // null
        Console.WriteLine($"null | null  = {n | n}");   // null

        // Zastosowanie: warunki WHERE w SQL — null propaguje się przez operacje
        bool? warunek1 = null, warunek2 = true;
        bool? obaSpelnione = warunek1 & warunek2;
        Console.WriteLine($"null & true (oba warunki) = {obaSpelnione}");
    }

    public static void PatternMatchingZNull()
    {
        Console.WriteLine("\n── PatternMatchingZNull ──");

        // is null vs == null
        string? s = null;
        Console.WriteLine($"s is null: {s is null}");         // True (pattern — polimorfizm?)
        Console.WriteLine($"s == null: {s == null}");         // True (może być override ==)
        Console.WriteLine($"s is not null: {s is not null}"); // False

        // W praktyce 'is null' jest zalecane — nie może być zoverrideowane
        object? obj = null;
        if (obj is null)
            Console.WriteLine("obj to null — sprawdzono przez 'is null'");

        // Switch expression z null arm
        string? status = null;
        string opis = status switch
        {
            null => "brak statusu",
            "aktywny" => "użytkownik aktywny",
            "zablokowany" => "użytkownik zablokowany",
            _ => $"nieznany: {status}"
        };
        Console.WriteLine($"switch null → '{opis}'");

        string? status2 = "aktywny";
        string opis2 = status2 switch
        {
            null => "brak statusu",
            "aktywny" => "użytkownik aktywny",
            _ => $"nieznany: {status2}"
        };
        Console.WriteLine($"switch \"aktywny\" → '{opis2}'");

        // Wzorzec is T zmienna — automatyczne sprawdzenie null + rzutowanie
        object? dane = "Hello, World!";
        if (dane is string tekst)
            Console.WriteLine($"is string: długość={tekst.Length}");

        if (dane is not int)
            Console.WriteLine("dane NIE jest int");

        // Tuple pattern z null
        string? imie = "Anna";
        int? wiek = null;
        string info = (imie, wiek) switch
        {
            (null, null) => "brak danych",
            (string i, null) => $"imię: {i}, wiek nieznany",
            (null, int w) => $"wiek: {w}, imię nieznane",
            (string i, int w) => $"{i}, lat {w}"
        };
        Console.WriteLine($"Tuple pattern: {info}");

        // Extended property pattern (C# 10+)
        var konfig = new KonfiguracjaNT { Port = 443, SslWlaczone = true };
        if (konfig is { Port: > 400, SslWlaczone: true })
            Console.WriteLine("Extended property pattern: port>400 i SSL=true");
    }

    public static void NullableReferenceTypes()
    {
        Console.WriteLine("\n── NullableReferenceTypes (C# 8+) ──");

        // NRT: kompilator analizuje przepływ i ostrzega przed potencjalnym null
        // #nullable enable — w tym projekcie jest włączone globalnie (Nullable=enable)

        // string (non-nullable) vs string? (nullable)
        // string nonNullable = null; // CS8600 — przypisanie null do non-nullable!
        string? maybeNull = null;              // kompilator: może być null

        // Flow analysis — kompilator śledzi stan
        if (maybeNull != null)
        {
            // Tu kompilator wie: maybeNull nie jest null
            Console.WriteLine($"Po null-check: {maybeNull.Length}"); // bez ostrzeżenia
        }

        string? wynik = ObliczWynik(true);
        if (wynik is not null)
            Console.WriteLine($"Wynik nie-null: {wynik}");

        // null-forgiving operator ! — "wiem co robię, zaufaj mi"
        string? mozeNull = PobierzDane();
        string pewnie = mozeNull!; // CS8601 silence — uważaj!
        Console.WriteLine($"! operator: {pewnie.Length}");

        // Ostrzeżenia NRT:
        // CS8600 — przypisanie możliwego null do non-nullable
        // CS8602 — dereferencja możliwego null
        // CS8603 — zwracanie możliwego null gdzie wymagany non-null
        Console.WriteLine("NRT: kompilator ostrzega w compile-time, chroni przed NullReferenceException");
    }

    static string? ObliczWynik(bool sukces) => sukces ? "wynik" : null;
    static string? PobierzDane() => "dane"; // w tym przypadku zawsze nie-null

    public static void AtrybutyNullowosci()
    {
        Console.WriteLine("\n── AtrybutyNullowosci ──");

        // [NotNullWhen(true)] — "gdy metoda zwraca true, parametr out nie jest null"
        if (TryParseKwota("123.45", out decimal? kwota))
            Console.WriteLine($"[NotNullWhen] TryParse sukces: {kwota:C}");

        if (!TryParseKwota("nie-liczba", out decimal? blad))
            Console.WriteLine($"[NotNullWhen] TryParse fail: kwota={blad}");

        // [NotNull] — "mimo nullable input, wynik nigdy nie jest null"
        string? wejscie = null;
        string wynik = ZapewnijNieNull(wejscie);
        Console.WriteLine($"[NotNull] ZapewnijNieNull(null) = '{wynik}'");

        // [MemberNotNull] — "po wywołaniu tej metody, pole nie jest null"
        var uz = new UzytkownikNT { Id = 1 };
        uz.UstawImie("Anna"); // [MemberNotNull(nameof(_imie))]
        Console.WriteLine("[MemberNotNull] pole _imie zainicjalizowane przez metodę");

        // [DoesNotReturn] — "ta metoda nigdy nie wraca normalnie"
        Console.WriteLine("Flow: po rzuceniu wyjątku kompilator wie że kod jest nieosiągalny");

        // Praktyczne zastosowanie
        string? login = PobierzLogin();
        if (TryGetUser(login, out string? user))
            Console.WriteLine($"Użytkownik znaleziony: {user.ToUpper()}"); // bez CS8602
    }

    static bool TryParseKwota(string input, [NotNullWhen(true)] out decimal? kwota)
    {
        if (decimal.TryParse(input, out decimal val))
        {
            kwota = val;
            return true;
        }
        kwota = null;
        return false;
    }

    [return: NotNull]
    static string ZapewnijNieNull(string? wejscie) => wejscie ?? "domyślna";

    static string? PobierzLogin() => "jan.kowalski";

    static bool TryGetUser(string? login, [NotNullWhen(true)] out string? user)
    {
        if (login is null) { user = null; return false; }
        user = $"User:{login}";
        return true;
    }

    public static void PraktycznyKonfigurator()
    {
        Console.WriteLine("\n── PraktycznyKonfigurator ──");

        // Serwis z pełną konfiguracją
        var pelnaKonfig = new KonfiguracjaNT
        {
            Host = "db.example.com",
            Port = 5432,
            SslWlaczone = true,
            Limity = new LimitNT { MaxPolaczen = 100, Timeout = 30 }
        };

        var serwis = new SerwisKonfiguracjiNT(pelnaKonfig);
        Console.WriteLine($"Host: {serwis.PobierzHost()}");
        Console.WriteLine($"Port: {serwis.PobierzPort()}");
        Console.WriteLine($"SSL: {serwis.CzySslWlaczone()}");
        Console.WriteLine($"MaxPolaczen: {serwis.PobierzMaxPolaczen()}");
        Console.WriteLine($"URL: {serwis.PobierzUrl()}");

        // Serwis z pustą konfiguracją — wszystkie defaulty
        var pustaKonfig = new SerwisKonfiguracjiNT(null);
        Console.WriteLine($"\nPusta konfiguracja:");
        Console.WriteLine($"Host: {pustaKonfig.PobierzHost()} (domyślny)");
        Console.WriteLine($"Port: {pustaKonfig.PobierzPort()} (domyślny)");
        Console.WriteLine($"URL: {pustaKonfig.PobierzUrl()}");

        // Częściowa konfiguracja
        var czesciowaKonfig = new KonfiguracjaNT { Host = "prod.db.com" };
        var serwis3 = new SerwisKonfiguracjiNT(czesciowaKonfig);
        Console.WriteLine($"\nCzęściowa konfiguracja:");
        Console.WriteLine($"Host: {serwis3.PobierzHost()}");
        Console.WriteLine($"MaxPolaczen: {serwis3.PobierzMaxPolaczen()} (domyślny, bo Limity=null)");

        // ??= do lazy initialization
        KonfiguracjaNT? lazyCfg = null;
        lazyCfg ??= new KonfiguracjaNT { Host = "lazy.db.com" };
        lazyCfg ??= new KonfiguracjaNT { Host = "nie zostanie użyte" };
        Console.WriteLine($"\n??= lazy init: Host={lazyCfg.Host}");
    }
}
