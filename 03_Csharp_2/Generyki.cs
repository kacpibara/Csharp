namespace _03_Csharp_2;

// ── Klasy pomocnicze dla Generyki ─────────────────────────────────────────────

public class StosTGen<T>
{
    private T[] _dane;
    private int _szczyt = 0;

    public StosTGen(int capacity = 4) => _dane = new T[capacity];

    public void Push(T element)
    {
        if (_szczyt == _dane.Length)
            Array.Resize(ref _dane, _dane.Length * 2);
        _dane[_szczyt++] = element;
    }

    public T Pop()
    {
        if (_szczyt == 0) throw new InvalidOperationException("Stos jest pusty.");
        return _dane[--_szczyt];
    }

    public T Peek()
    {
        if (_szczyt == 0) throw new InvalidOperationException("Stos jest pusty.");
        return _dane[_szczyt - 1];
    }

    public bool CzyPusty => _szczyt == 0;
    public int Rozmiar => _szczyt;

    public IEnumerable<T> OdNajnowszego()
    {
        for (int i = _szczyt - 1; i >= 0; i--)
            yield return _dane[i];
    }
}

public class ParaTGen<TFirst, TSecond>
{
    public TFirst Pierwszy { get; }
    public TSecond Drugi { get; }

    public ParaTGen(TFirst pierwszy, TSecond drugi)
    {
        Pierwszy = pierwszy;
        Drugi = drugi;
    }

    public ParaTGen<TSecond, TFirst> Odwroc() => new(Drugi, Pierwszy);
    public override string ToString() => $"({Pierwszy}, {Drugi})";
}

public class WynikGen<T>
{
    public bool Sukces { get; }
    public T? Wartosc { get; }
    public string Blad { get; }

    private WynikGen(bool sukces, T? wartosc, string blad)
    {
        Sukces = sukces; Wartosc = wartosc; Blad = blad;
    }

    public static WynikGen<T> Ok(T wartosc) => new(true, wartosc, "");
    public static WynikGen<T> Fail(string blad) => new(false, default, blad);

    public WynikGen<TNext> Then<TNext>(Func<T, WynikGen<TNext>> f)
        => Sukces ? f(Wartosc!) : WynikGen<TNext>.Fail(Blad);

    public override string ToString() => Sukces ? $"Ok({Wartosc})" : $"Fail({Blad})";
}

public class LicznikGen<T>
{
    private static int _instancje = 0;
    public LicznikGen() => _instancje++;
    public static int IleInstancji => _instancje;
}

public interface IEntityGen { int Id { get; } }

public record UzytkownikGen(int Id, string Imie, string Email) : IEntityGen;
public record ProduktGen(int Id, string Nazwa, decimal Cena) : IEntityGen;

public abstract class RepoBaseGen<T> where T : class, IEntityGen
{
    protected readonly List<T> _magazyn = new();

    public T? ZnajdzPoId(int id) => _magazyn.FirstOrDefault(e => e.Id == id);

    public void Dodaj(T encja)
    {
        if (_magazyn.Any(e => e.Id == encja.Id))
            throw new InvalidOperationException($"Id={encja.Id} już istnieje.");
        _magazyn.Add(encja);
    }

    public IReadOnlyList<T> PobierzWszystkie() => _magazyn.AsReadOnly();
}

public class UzytkownicyRepGen : RepoBaseGen<UzytkownikGen> { }

public class PipelineGen<T>
{
    private readonly List<Func<T, T>> _kroki = new();

    public PipelineGen<T> DodajKrok(Func<T, T> krok) { _kroki.Add(krok); return this; }

    public T Wykonaj(T wejscie) => _kroki.Aggregate(wejscie, (curr, krok) => krok(curr));
}

public class LazyCacheGen<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _cache = new();
    private readonly Func<TKey, TValue> _fabryka;

    public LazyCacheGen(Func<TKey, TValue> fabryka) => _fabryka = fabryka;

    public TValue PobierzLubOblicz(TKey klucz)
    {
        if (!_cache.TryGetValue(klucz, out TValue? wartosc))
        {
            wartosc = _fabryka(klucz);
            _cache[klucz] = wartosc;
        }
        return wartosc;
    }

    public int IleWCache => _cache.Count;
}

public interface IDodawalneGen<T> where T : IDodawalneGen<T>
{
    static abstract T operator +(T a, T b);
    static abstract T Zero { get; }
}

public readonly struct ZlotyGen : IDodawalneGen<ZlotyGen>
{
    public decimal Wartosc { get; }
    public ZlotyGen(decimal v) => Wartosc = v;
    public static ZlotyGen operator +(ZlotyGen a, ZlotyGen b) => new(a.Wartosc + b.Wartosc);
    public static ZlotyGen Zero => new(0);
    public override string ToString() => $"{Wartosc:C}";
}

public class FabrykaGen<T> where T : new()
{
    public T Utworz() => new T();
}

// ── Klasa demonstracyjna ──────────────────────────────────────────────────────

public static class Generyki
{
    public static void ProblemBezGenerykow()
    {
        Console.WriteLine("\n── ProblemBezGenerykow ──");

        // Przed .NET 2.0: ArrayList — brak type safety, boxing dla value types
        var lista = new System.Collections.ArrayList();
        lista.Add(1);
        lista.Add(2);
        lista.Add("błąd"); // kompilator nie zatrzymuje!
        // int n = (int)lista[2]; // InvalidCastException w runtime
        Console.WriteLine("ArrayList: brak type safety, boxing dla int, casting przy odczycie");

        // Rozwiązanie: generyki — jeden kod, wiele typów, zero boxing dla value types
        var stosInt = new StosTGen<int>();
        stosInt.Push(10); stosInt.Push(20); stosInt.Push(30);
        Console.WriteLine($"Stos<int>: Pop={stosInt.Pop()}, Peek={stosInt.Peek()}, Rozmiar={stosInt.Rozmiar}");

        var stosStr = new StosTGen<string>();
        stosStr.Push("A"); stosStr.Push("B"); stosStr.Push("C");
        Console.Write("Stos<string> od najnowszego: ");
        foreach (var s in stosStr.OdNajnowszego()) Console.Write($"{s} ");
        Console.WriteLine();

        // Para<T1,T2> — dwa parametry typów
        var para = new ParaTGen<string, int>("Anna", 30);
        Console.WriteLine($"Para: {para} → Odwrócona: {para.Odwroc()}");

        // WynikGen<T> — generyczny Result type
        var ok = WynikGen<int>.Ok(42);
        var fail = WynikGen<int>.Fail("Nie znaleziono");
        var chained = ok.Then(v => WynikGen<string>.Ok($"Wartość to {v}"));
        Console.WriteLine($"WynikGen: {ok}, {fail}");
        Console.WriteLine($"Then: {chained}");
    }

    public static void KlasyGeneryczne()
    {
        Console.WriteLine("\n── KlasyGeneryczne ──");

        // Statyczne pola — każde zamknięte T ma WŁASNE pole statyczne
        new LicznikGen<int>();
        new LicznikGen<int>();
        new LicznikGen<string>();
        Console.WriteLine($"Licznik<int>.IleInstancji = {LicznikGen<int>.IleInstancji}");    // 2
        Console.WriteLine($"Licznik<string>.IleInstancji = {LicznikGen<string>.IleInstancji}"); // 1
        Console.WriteLine("LicznikGen<int> i LicznikGen<string> to RÓŻNE typy w runtime — osobne pola statyczne!");

        // Kombinacje dziedziczenia
        // class Otwarta<T> : Baza<T> { }           — przekazuje T dalej
        // class Zamknieta : Baza<int> { }           — konkretyzuje T
        // class Rozszerzona<T,U> : Baza<T> { }      — dodaje parametr
        // class CzesciowaNaprawiona<T> : Para<string, T> {} — konkretyzuje jeden parametr
        Console.WriteLine("Dziedziczenie: otwarte (T→T), zamknięte (→int), rozszerzone (T,U→T)");
    }

    public static void MetodyGeneryczne()
    {
        Console.WriteLine("\n── MetodyGeneryczne ──");

        // Type inference — kompilator wydedukuje T z argumentów
        int x = 5, y = 10;
        ZamienGen(ref x, ref y);
        Console.WriteLine($"Zamien<int>: x={x}, y={y}");

        string s1 = "hello", s2 = "world";
        ZamienGen(ref s1, ref s2);
        Console.WriteLine($"Zamien<string>: s1={s1}, s2={s2}");

        // Constraint where T : IComparable<T>
        Console.WriteLine($"Min(3,7)={MinGen(3, 7)}, Max(3,7)={MaxGen(3, 7)}");
        Console.WriteLine($"Min(\"apple\",\"fig\")={MinGen("apple", "fig")}");
        Console.WriteLine($"Min(3.14,2.71)={MinGen(3.14, 2.71):F2}");

        // Filtrowanie generyczne — implementacja LINQ Where
        var liczby = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var parzyste = FiltrujGen(liczby, n => n % 2 == 0);
        Console.WriteLine($"FiltrujGen (parzyste): [{string.Join(", ", parzyste)}]");

        // Wnioskowanie NIE działa dla typów zwracanych
        // var d = DomyslnyGen();  // BŁĄD — kompilator nie wie co to T
        var d = DomyslnyGen<int>(); // jawne podanie T wymagane
        Console.WriteLine($"DomyslnyGen<int>={d}");

        // new() constraint — fabryka generyczna
        var fab = new FabrykaGen<List<int>>();
        var nowa = fab.Utworz();
        nowa.Add(42); nowa.Add(99);
        Console.WriteLine($"FabrykaGen<List<int>>: [{string.Join(", ", nowa)}]");
    }

    static void ZamienGen<T>(ref T a, ref T b) { T tmp = a; a = b; b = tmp; }
    static T MinGen<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) <= 0 ? a : b;
    static T MaxGen<T>(T a, T b) where T : IComparable<T> => a.CompareTo(b) >= 0 ? a : b;
    static IEnumerable<T> FiltrujGen<T>(IEnumerable<T> src, Func<T, bool> pred)
    {
        foreach (var e in src) if (pred(e)) yield return e;
    }
    static T DomyslnyGen<T>() => default!;

    public static void OgraniczeniaTypow()
    {
        Console.WriteLine("\n── OgraniczeniaTypow ──");

        // struct constraint
        Console.WriteLine($"DomyslnyStruct<int>={DomyslnyStruct<int>()}");
        Console.WriteLine($"DomyslnyStruct<double>={DomyslnyStruct<double>()}");

        // class constraint + notnull
        string? nullStr = null;
        Console.WriteLine($"LubDomyslna(null)={LubDomyslna(nullStr, "domyślna")}");

        // IComparable<T> — sortowanie
        int[] tab = { 3, 1, 4, 1, 5, 9, 2, 6 };
        Array.Sort(tab);
        Console.WriteLine($"Posortowane: [{string.Join(", ", tab)}]");

        // Repository pattern z constraints where T : class, IEntityGen
        var repo = new UzytkownicyRepGen();
        repo.Dodaj(new UzytkownikGen(1, "Anna", "anna@test.com"));
        repo.Dodaj(new UzytkownikGen(2, "Jan", "jan@test.com"));
        var znaleziony = repo.ZnajdzPoId(1);
        Console.WriteLine($"Repozytorium: znaleziony={znaleziony}");
        Console.WriteLine($"Wszystkich w repo: {repo.PobierzWszystkie().Count}");

        // notnull — non-nullable value LUB non-nullable reference (C# 8+)
        Console.WriteLine("notnull: blokuje przypisanie null w miejscach które wymagają wartości");

        // unmanaged — struct bez referencji, dla Span<T>/unsafe/sizeof(T)
        Console.WriteLine($"unmanaged<int>: {NazwaUnmanaged<int>()}");
        Console.WriteLine($"unmanaged<double>: {NazwaUnmanaged<double>()}");
        Console.WriteLine("unmanaged<string>: niedozwolone — string zawiera referencję");

        // Kilka constraintów naraz
        Console.WriteLine("Kilka constraintów: where T : class, IComparable<T>, new()");
        Console.WriteLine("new() MUSI być ostatni na liście constraintów");
    }

    static T DomyslnyStruct<T>() where T : struct => default;
    static T LubDomyslna<T>(T? wartosc, T domyslna) where T : class => wartosc ?? domyslna;
    static string NazwaUnmanaged<T>() where T : unmanaged => typeof(T).Name;

    public static void PipelineICache()
    {
        Console.WriteLine("\n── PipelineICache ──");

        // Generic Pipeline — łańcuch transformacji
        var strPipeline = new PipelineGen<string>()
            .DodajKrok(s => s.Trim())
            .DodajKrok(s => s.ToUpper())
            .DodajKrok(s => $"[{s}]");

        Console.WriteLine($"StrPipeline: '{strPipeline.Wykonaj("  witaj świecie  ")}'");

        var numPipeline = new PipelineGen<int>()
            .DodajKrok(x => x * 2)
            .DodajKrok(x => x + 10)
            .DodajKrok(x => x * x);
        Console.WriteLine($"NumPipeline(5): {numPipeline.Wykonaj(5)}"); // ((5*2)+10)^2 = 400

        // LazyCache — oblicza wartość raz, potem serwuje z pamięci
        int obliczen = 0;
        var cache = new LazyCacheGen<int, double>(klucz =>
        {
            obliczen++;
            return Math.Sqrt(klucz);
        });

        Console.WriteLine($"sqrt(16)={cache.PobierzLubOblicz(16):F4}");
        Console.WriteLine($"sqrt(16)={cache.PobierzLubOblicz(16):F4} (z cache — bez obliczenia)");
        Console.WriteLine($"sqrt(25)={cache.PobierzLubOblicz(25):F4}");
        Console.WriteLine($"Faktycznych obliczeń: {obliczen}, elementów w cache: {cache.IleWCache}");
    }

    public static void StaticAbstractCSharp11()
    {
        Console.WriteLine("\n── StaticAbstractCSharp11 (C# 11) ──");

        // static abstract members — metoda generyczna wywołuje statyczne metody na T
        // Bez tego: nie można użyć operatora + ani T.Zero w metodzie generycznej
        var platnosci = new[] { new ZlotyGen(100), new ZlotyGen(250), new ZlotyGen(75) };
        ZlotyGen suma = SumaIDodawalnych(platnosci);
        Console.WriteLine($"Suma płatności: {suma}"); // 425 zł

        // .NET 7+ definiuje System.Numerics.INumber<T>, IAdditionOperators<T,T,T>
        // Ten sam wzorzec — możesz pisać algorytmy math dla int, double, decimal
        var intSuma = new int[] { 1, 2, 3, 4, 5 }.Aggregate(0, (acc, n) => acc + n);
        Console.WriteLine($"Suma int (Aggregate): {intSuma}");
        Console.WriteLine("Wzorzec static abstract = Generic Math — jeden algorytm dla wszystkich liczb");
    }

    static T SumaIDodawalnych<T>(IEnumerable<T> elementy) where T : IDodawalneGen<T>
    {
        T wynik = T.Zero;
        foreach (var e in elementy) wynik = wynik + e;
        return wynik;
    }

    public static void IEnumerableTvsIEnumerable()
    {
        Console.WriteLine("\n── IEnumerableTvsIEnumerable ──");

        // IEnumerable (non-generic, .NET 1.0) — boxing dla value types
        int[] liczby = { 1, 2, 3, 4, 5 };
        int sumaBoxing = 0;
        foreach (object o in (System.Collections.IEnumerable)liczby)
            sumaBoxing += (int)o; // boxing przy każdym elemencie + unboxing
        Console.WriteLine($"Non-generic (boxing): suma={sumaBoxing}");

        // IEnumerable<T> (generic, .NET 2.0) — zero boxing dla value types
        int sumaNoBox = 0;
        foreach (int n in (IEnumerable<int>)liczby)
            sumaNoBox += n;
        Console.WriteLine($"Generic<T> (bez boxing): suma={sumaNoBox}");

        // Lazy evaluation — elementy generowane dopiero gdy potrzebne
        Console.Write("Lazy: ");
        var gen = LazyGeneratorLiczb();
        foreach (int n in gen)
        {
            Console.Write($"{n} ");
            if (n == 2) break; // przerywa — elementy 3,4 NIE zostaną wygenerowane
        }
        Console.WriteLine("(break po 2 — reszta nie wygenerowana)");

        // Wielokrotna enumeracja — PUŁAPKA
        var sekwencja = LicznikPrzebiegu();
        int s1 = sekwencja.Sum(); // pierwsze przejście
        int s2 = sekwencja.Max(); // DRUGIE przejście — generator uruchamia się ponownie!
        Console.WriteLine($"Wielokrotna enumeracja: Sum={s1}, Max={s2} (2x generator)");
        Console.WriteLine("Rozwiązanie: zmaterializuj → .ToList() przed wielokrotnym użyciem");

        // Hierarchia interfejsów
        Console.WriteLine("Hierarchia: IEnumerable<T> → ICollection<T> → IList<T> / ISet<T>");
        Console.WriteLine("Zasada: przyjmuj NAJSZERSZY interfejs który rzeczywiście potrzebujesz");

        // Deferred LINQ execution
        var zapytanie = Enumerable.Range(1, 1_000_000)
            .Where(n => n % 2 == 0)
            .Select(n => n * n)
            .Take(5);
        Console.WriteLine($"LINQ lazy (5 parzystych kwadratów): [{string.Join(", ", zapytanie)}]");
    }

    static IEnumerable<int> LazyGeneratorLiczb()
    {
        for (int i = 0; i < 5; i++) yield return i;
    }

    static IEnumerable<int> LicznikPrzebiegu()
    {
        Console.Write("(przebieg) ");
        yield return 1; yield return 2; yield return 3;
    }
}
