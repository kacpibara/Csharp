# 03_Csharp_2 — C# 2.0+: Generyki, Nullable, Delegaty, Iteratory, Partial/Static, Kowariancja

## Spis treści
1. [Generyki](#generyki)
2. [Nullable Types](#nullable-types)
3. [Delegaty i Zdarzenia](#delegaty-i-zdarzenia)
4. [Iteratory i yield](#iteratory-i-yield)
5. [Partial Classes i Static Classes](#partial-classes-i-static-classes)
6. [Kowariancja i Kontrawariancja](#kowariancja-i-kontrawariancja)

---

## Generyki

### Problem bez generyk

Przed .NET 2.0 (C# 1.0): każda kolekcja przechowywała `object` → boxing dla value types, brak type safety, casting przy odczycie, błędy w runtime.

```csharp
ArrayList lista = new ArrayList();
lista.Add(1);           // boxing int → object
lista.Add("błąd");      // kompilator nie zatrzymuje!
int n = (int)lista[0];  // unboxing + ryzyko InvalidCastException
```

**Rozwiązanie — generyki:** jeden kod, wiele typów, zero boxing dla value types.

### Klasy generyczne

```csharp
// T = type parameter — placeholder zastępowany konkretnym typem przy kompilacji
public class Stos<T>
{
    private T[] _dane;
    private int _szczyt = 0;
    public Stos(int capacity = 4) => _dane = new T[capacity];
    public void Push(T element) { /* ... */ }
    public T Pop() { /* ... */ }
    public T Peek() { /* ... */ }
    public IEnumerable<T> OdNajnowszego() { for (int i = _szczyt-1; i >= 0; i--) yield return _dane[i]; }
}

// Para<T1, T2> — dwa parametry typów
public class Para<TFirst, TSecond>
{
    public TFirst Pierwszy { get; }
    public TSecond Drugi { get; }
    public Para<TSecond, TFirst> Odwroc() => new(Drugi, Pierwszy);
}

// Użycie
var stosInt = new Stos<int>();      // brak boxing — T[] to int[]
var stosStr = new Stos<string>();   // T[] to string[] (referencje)
var para = new Para<string, int>("Anna", 30); // (Anna, 30)
```

**Konwencje nazewnicze:** `T` = jeden parametr, `TKey`/`TValue` = z semantycznym znaczeniem, `TResult` = wynik.

### Pułapka: statyczne pola

```csharp
class Licznik<T>
{
    private static int _instancje = 0;
    public Licznik() => _instancje++;
    public static int IleInstancji => _instancje;
}

new Licznik<int>(); new Licznik<int>(); new Licznik<string>();
Licznik<int>.IleInstancji;    // 2
Licznik<string>.IleInstancji; // 1 — Licznik<int> i Licznik<string> to RÓŻNE typy w runtime!
```

### Dziedziczenie generyczne

```csharp
class BazaGeneryczna<T> { }
class PochodzacaGeneryczna<T>    : BazaGeneryczna<T> { }   // przekazuje T dalej (otwarta)
class PochodzacaKonkretna        : BazaGeneryczna<int> { } // konkretyzuje T (zamknięta)
class PochodzacaRozszerzona<T,U> : BazaGeneryczna<T> { }   // dodaje parametr
```

### Metody generyczne

```csharp
// Parametr typu przy METODZIE, nie klasie
public static void Zamien<T>(ref T a, ref T b) { T tmp = a; a = b; b = tmp; }

// Wnioskowanie typów (type inference) — kompilator dedukuje T z argumentów
int x = 5, y = 10;
Zamien(ref x, ref y);  // kompilator: T = int — explicit <int> zbędne

// Wnioskowanie NIE działa dla typów zwracanych
static T Domyslny<T>() => default!;
var d = Domyslny<int>(); // T musi być jawne — skąd kompilator wie?

// Generyczny filtr — uproszczony LINQ Where
public static IEnumerable<T> Filtruj<T>(IEnumerable<T> src, Func<T, bool> pred)
{
    foreach (var e in src) if (pred(e)) yield return e;
}
```

### Constrainty (where)

Constrainty określają co T musi spełniać — kompilator może wtedy zezwolić na operacje na T.

```csharp
// Bez constraintu: T to "czarna skrzynka" — tylko przypisanie i null-check
// Z constraintem: kompilator zna możliwości T

// where T : struct          — value type (int, double, struct, nie Nullable<T>)
// where T : class           — reference type
// where T : notnull         — non-nullable value LUB non-nullable reference (C# 8+)
// where T : unmanaged       — unmanaged type — dla Span/unsafe/sizeof(T)
// where T : new()           — bezparametrowy publiczny konstruktor (musi być ostatni!)
// where T : KlasaBazowa     — T dziedziczy z KlasaBazowa
// where T : IInterfejs      — T implementuje IInterfejs
// where T : IComparable<T>  — T umie się porównywać
// where T : U               — naked type constraint: T musi być tym samym/pochodnym od U

// Przykłady:
public static T Min<T>(T a, T b) where T : IComparable<T>
    => a.CompareTo(b) <= 0 ? a : b;

public class Fabryka<T> where T : new()
{
    public T Utworz() => new T(); // bez new() — błąd kompilacji
}

// Wielokrotne constrainty
public class Repozytorium<T> where T : class, IEntity
{
    // T musi być klasą implementującą IEntity
}

// Kilka parametrów — niezależne constrainty
public static TTarget Konwertuj<TSource, TTarget>(TSource src, Func<TSource, TTarget> f)
    where TSource : notnull
    where TTarget : notnull
    => f(src);
```

### Constraint unmanaged i static abstract (C# 11)

```csharp
// unmanaged — struct bez żadnych referencji: int, double, Guid, własne proste struct
// Pozwala używać sizeof(T), Span<T>, unsafe
public static string NazwaUnmanaged<T>() where T : unmanaged => typeof(T).Name;
// NazwaUnmanaged<int>() // OK
// NazwaUnmanaged<string>() // BŁĄD — string zawiera referencję

// static abstract (C# 11) — metody generyczne wywołują statyczne metody na T
// Podstawa Generic Math (.NET 7+)
public interface IDodawalne<T> where T : IDodawalne<T>
{
    static abstract T operator +(T a, T b);
    static abstract T Zero { get; }
}

public static T Suma<T>(IEnumerable<T> elementy) where T : IDodawalne<T>
{
    T wynik = T.Zero;           // statyczna właściwość na T — możliwa przez constraint
    foreach (var e in elementy) wynik = wynik + e;
    return wynik;
}

// .NET 7+: System.Numerics.INumber<T>, IAdditionOperators<T,T,T>
// Jeden algorytm dla int, double, decimal, float, własnych typów liczbowych
```

### Generic Repository (wzorzec)

```csharp
public interface IEntity { int Id { get; } }

public abstract class RepozytoiumBazowe<T> where T : class, IEntity
{
    protected readonly List<T> _magazyn = new();
    public T? ZnajdzPoId(int id) => _magazyn.FirstOrDefault(e => e.Id == id);
    public void Dodaj(T encja) { /* sprawdź duplikat Id */ _magazyn.Add(encja); }
    public IReadOnlyList<T> PobierzWszystkie() => _magazyn.AsReadOnly();
}

public record Uzytkownik(int Id, string Imie, string Email) : IEntity;
public class UzytkownicyRepo : RepozytoiumBazowe<Uzytkownik> { }
```

### Generic Pipeline i LazyCache

```csharp
// Pipeline<T> — łańcuch transformacji
public class Pipeline<T>
{
    private readonly List<Func<T, T>> _kroki = new();
    public Pipeline<T> DodajKrok(Func<T, T> krok) { _kroki.Add(krok); return this; }
    public T Wykonaj(T wejscie) => _kroki.Aggregate(wejscie, (curr, krok) => krok(curr));
}

var pipeline = new Pipeline<string>()
    .DodajKrok(s => s.Trim())
    .DodajKrok(s => s.ToUpper())
    .DodajKrok(s => $"[{s}]");
pipeline.Wykonaj("  hello  "); // [HELLO]

// LazyCache<TKey, TValue> — oblicza wartość raz, potem serwuje z pamięci
public class LazyCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _cache = new();
    private readonly Func<TKey, TValue> _fabryka;
    public LazyCache(Func<TKey, TValue> fabryka) => _fabryka = fabryka;
    public TValue PobierzLubOblicz(TKey klucz)
    {
        if (!_cache.TryGetValue(klucz, out TValue? v))
        {
            v = _fabryka(klucz);
            _cache[klucz] = v;
        }
        return v;
    }
}
```

### IEnumerable`<T>` vs IEnumerable

| Cecha | `IEnumerable` (.NET 1.0) | `IEnumerable<T>` (.NET 2.0) |
|---|---|---|
| Typ Current | `object` | `T` |
| Boxing value types | Tak | Nie |
| Type safety | Nie | Tak |
| LINQ | Nie | Tak |

**Hierarchia interfejsów kolekcji:**
```
IEnumerable<T>          → tylko foreach (GetEnumerator)
    └── ICollection<T>  → Count, Add, Remove, Contains
            └── IList<T>   → indekser [i], IndexOf, Insert, RemoveAt
            └── ISet<T>    → operacje zbiorów (UnionWith, IntersectWith)
```

**Zasada:** przyjmuj NAJSZERSZY interfejs który rzeczywiście potrzebujesz.

---

## Nullable Types

### Nullable`<T>` / T?

Nullable<T> to struct owijający value type, dodający stan "brak wartości".

```csharp
// Anatomia: struct Nullable<T> { bool HasValue; T Value; }
int? a = 42;    // a.HasValue=true,  a.Value=42
int? b = null;  // b.HasValue=false

b.Value;                   // InvalidOperationException gdy HasValue=false!
b.GetValueOrDefault();     // 0 — bezpieczny odczyt
b.GetValueOrDefault(99);   // 99 — z wartością domyślną

// Boxing Nullable<T>:
int? nullInt = null;
object? boxed = nullInt;   // box → null (NIE do Nullable<int>!)
int? piec = 5;
object? b5 = piec;         // box → int (5), NIE Nullable<int>!
```

### Operatory ??, ??=, ?., ?[]

```csharp
// ?? — coalescing: lewa jeśli nie-null, inaczej prawa
string? name = null;
string wyswietlana = name ?? "Anonim";      // "Anonim"
string wynik = a ?? b ?? c ?? "ostatni";    // łańcuchowanie

// ??= — przypisz jeśli null (leniwa prawa strona)
string? s = null;
s ??= "inicjalizacja";   // s = "inicjalizacja"
s ??= "nie zostanie";    // s już nie jest null — brak przypisania

// ?. — null-conditional: wywołaj jeśli nie-null, inaczej null
string? tekst = null;
int? dlugosc = tekst?.Length;        // null (nie NullReferenceException!)
int? maxPol = konfig?.Limity?.MaxPolaczen;  // łańcuchowanie

// ?[] — null-conditional na indekserze
int[]? tab = null;
int? el = tab?[0];   // null

// Kombinacja
string wyswietlanyLogin = login?.ToUpper() ?? "GOŚĆ";
```

### Lifted operators i bool? (logika trójwartościowa)

```csharp
// Lifted operators — arithmetic z nullable
int? a = 5, b = null;
a + b;  // null — JEDEN null → wynik null
a * 4;  // 20

// Porównania NIE zwracają null — zwracają bool
null > 5;     // false (nie null!)
null == null; // true

// bool? — SQL BOOLEAN: true, false, null (nieznany)
bool? t = true, f = false, n = null;

// AND: null & false = false (wiadomo!), null & true = null
n & t;  // null
n & f;  // False — skoro jeden fałszywy, wynik fałszywy niezależnie od null
t & t;  // True

// OR: null | true = true (wiadomo!), null | false = null
n | t;  // True
n | f;  // null
```

### Pattern matching z null

```csharp
// is null — zalecane (nie może być nadpisane przez operator ==)
if (obj is null) { /* ... */ }
if (obj is not null) { /* ... */ }

// Switch expression z null arm
string? status = null;
string opis = status switch
{
    null => "brak statusu",
    "aktywny" => "użytkownik aktywny",
    _ => $"nieznany: {status}"
};

// is T zmienna — sprawdzenie null + rzutowanie naraz
if (dane is string tekst) Console.WriteLine(tekst.Length); // brak CS8602

// Tuple pattern z null
string info = (imie, wiek) switch
{
    (null, null)     => "brak danych",
    (string i, null) => $"imię: {i}, wiek nieznany",
    (null, int w)    => $"wiek: {w}",
    (string i, int w) => $"{i}, lat {w}"
};

// Extended property pattern (C# 10+)
if (konfig is { Port: > 400, SslWlaczone: true })
    Console.WriteLine("port>400 i SSL=true");
```

### Nullable Reference Types (C# 8+)

```csharp
// Włączone przez <Nullable>enable</Nullable> w .csproj
// lub #nullable enable w pliku

string  nonNullable = "zawsze wartość";   // kompilator: nigdy null
string? maybeNull   = null;               // kompilator: może być null

// Flow analysis — kompilator śledzi stan
if (maybeNull != null)
    Console.WriteLine(maybeNull.Length);  // bez ostrzeżenia — kompilator wie!

// null-forgiving operator ! — "wiem co robię, zaufaj mi"
string pewnie = mozeNull!;  // ucisza CS8601 — uważaj!

// Ostrzeżenia:
// CS8600 — przypisanie możliwego null do non-nullable
// CS8602 — dereferencja możliwego null
// CS8603 — zwracanie możliwego null gdzie wymagany non-null
```

### Atrybuty nullowości

```csharp
// [NotNullWhen(true)] — gdy metoda zwraca true, parametr out nie jest null
static bool TryGetUser(string? login, [NotNullWhen(true)] out string? user)
{
    if (login is null) { user = null; return false; }
    user = $"User:{login}";
    return true;
}

if (TryGetUser(login, out string? user))
    Console.WriteLine(user.ToUpper()); // bez CS8602 — kompilator wie że user != null

// [return: NotNull] — wynik metody nigdy nie jest null mimo nullable parametru
[return: NotNull]
static string ZapewnijNieNull(string? s) => s ?? "domyślna";

// [MemberNotNull(nameof(_pole))] — po wywołaniu metody pole nie jest null
[MemberNotNull(nameof(_imie))]
public void UstawImie(string imie) => _imie = imie;
```

---

## Delegaty i Zdarzenia

### Podstawy delegatów

Delegat to typ wskaźnika na metodę — bezpieczny typowo (w odróżnieniu od wskaźników C).

```csharp
// Deklaracja własnego delegata
public delegate double OperacjaMatematyczna(double a, double b);

// Tworzenie instancji
OperacjaMatematyczna dodaj = (a, b) => a + b;
OperacjaMatematyczna potega = Math.Pow;  // group conversion — metoda pasuje do sygnatury

// Wywołanie — jak metoda
dodaj(10, 3);   // 13

// Delegat jako parametr — higher-order function
double[] Transformuj(double[] src, Func<double, double> f) => src.Select(f).ToArray();
Transformuj(liczby, Math.Sqrt);
```

### Func, Action, Predicate`<T>`

Wbudowane delegaty — nie trzeba deklarować własnych.

```csharp
// Func<T..., TResult> — ostatni typ = TResult (funkcja z wynikiem)
Func<int, int, int> suma = (a, b) => a + b;
Func<string, int>   len  = s => s.Length;
Func<double>        pi   = () => Math.PI;      // zero argumentów

// Action<T...> — brak wyniku (void)
Action<string>     wypisz  = s => Console.WriteLine(s);
Action             loguj   = () => Console.WriteLine("log");

// Predicate<T> — specjalny Func<T, bool>
Predicate<int>    CzyDodatnia = n => n > 0;

// Currying — f(a,b) → f(a)(b)
Func<int, Func<int, int>> mnoz = x => y => x * y;
var podwoj = mnoz(2);   // Func<int, int>
podwoj(7);              // 14

// Kompozycja
Func<int, int> razy2 = x => x * 2;
Func<int, int> plus10 = x => x + 10;
Func<int, int> razem = x => plus10(razy2(x));
razem(5);               // 20
```

### Multicast delegaty

```csharp
// += dodaje metodę do invocation list, -= usuwa
Action<string> handler = null!;
handler += s => Console.WriteLine($"[H1] {s}");
handler += s => Console.WriteLine($"[H2] {s}");
handler("test");  // wywołuje H1, potem H2

// GetInvocationList — iteracja przez handlery
foreach (Func<int> del in fn.GetInvocationList())
    Console.WriteLine(del()); // zbiera wyniki wszystkich

// UWAGA: Func multicast zwraca tylko wynik OSTATNIEGO delegata!
Func<int> fn = () => 1;
fn += () => 2;
fn += () => 3;
fn();  // 3 — tylko ostatni!
```

### event — hermetyzacja delegata

```csharp
public class Minutnik
{
    // event — zewnętrzny kod: tylko += i -=
    // Właściciel klasy: może wywołać i sprawdzić null
    public event Action<int>? Tyk;
    public event Action? Zatrzymany;

    public void Uruchom(int sek)
    {
        for (int i = 1; i <= sek; i++) Tyk?.Invoke(i);
        Zatrzymany?.Invoke();
    }
}

var m = new Minutnik();
m.Tyk += sek => Console.WriteLine($"Tyk {sek}");
// m.Tyk = null;     // BŁĄD — event nie pozwala na = z zewnątrz
// m.Tyk("hack");    // BŁĄD — event nie można wywołać z zewnątrz
```

### EventHandler`<TEventArgs>` — wzorzec .NET

```csharp
// EventArgs — dane zdarzenia
public class ZmianaTemperaturyArgs : EventArgs
{
    public double Stara { get; }
    public double Nowa { get; }
    public double Roznica => Nowa - Stara;
    public ZmianaTemperaturyArgs(double stara, double nowa) { Stara = stara; Nowa = nowa; }
}

// Publisher
public class Termometr
{
    private double _temperatura;
    public event EventHandler<ZmianaTemperaturyArgs>? ZmianaTemperatury;

    public double Temperatura
    {
        get => _temperatura;
        set
        {
            var args = new ZmianaTemperaturyArgs(_temperatura, value);
            _temperatura = value;
            OnZmianaTemperatury(args);  // wywołaj przez protected virtual
        }
    }

    // protected virtual OnXxx — klasy pochodne mogą przechwycić zdarzenie
    protected virtual void OnZmianaTemperatury(ZmianaTemperaturyArgs args)
        => ZmianaTemperatury?.Invoke(this, args);
}

// Subscriber z IDisposable — wyrejestrowanie zapobiega wyciekowi pamięci
public class LoggerTemperatury : IDisposable
{
    private readonly Termometr _t;
    public LoggerTemperatury(Termometr t) { _t = t; _t.ZmianaTemperatury += OnZmiana; }
    private void OnZmiana(object? s, ZmianaTemperaturyArgs e) => Console.WriteLine(e.Nowa);
    public void Dispose() => _t.ZmianaTemperatury -= OnZmiana; // wyrejestrowanie!
}
```

### Domknięcia (closures) i pułapka pętli

```csharp
// Domknięcie przechwytuje REFERENCJĘ do zmiennej, nie wartość!
int mnoznik = 3;
Func<int, int> mnoz = x => x * mnoznik;
mnoz(5);     // 15
mnoznik = 10;
mnoz(5);     // 50 — zmienna mnoznik się zmieniła!

// Pułapka pętli — i jest współdzielone przez wszystkie lambdy
var akcje = new List<Action>();
for (int i = 0; i < 5; i++)
    akcje.Add(() => Console.Write(i));  // przechwytuje referencję do i!
akcje.ForEach(a => a());  // 5 5 5 5 5 — i=5 po pętli!

// Naprawka: kopia zmiennej w każdej iteracji
for (int i = 0; i < 5; i++)
{
    int kopia = i;  // nowa zmienna dla każdej iteracji
    akcje.Add(() => Console.Write(kopia));
}
// 0 1 2 3 4 ✓

// Static lambda (C# 9+) — nie może przechwytywać zmiennych zewnętrznych
// Bezpieczna i nie alokuje obiektu domknięcia
Func<int, int> staticLambda = static x => x * 2;
// Func<int, int> zla = static x => x * mnoznik; // BŁĄD kompilacji
```

### Wyciek pamięci przez zdarzenia

Wydawca (event source) trzyma referencję do subskrybenta → subskrybent nie może być zebrany przez GC.

```csharp
// Wyciek — lambda zarejestrowana bez możliwości wyrejestrowania
termometr.ZmianaTemperatury += (_, e) => Console.WriteLine(e.Nowa); // nie można -= lambda

// Rozwiązanie 1: IDisposable
using var logger = new LoggerTemperatury(termometr); // -= w Dispose()

// Rozwiązanie 2: WeakReference<T> — słaba referencja nie blokuje GC
var weakRef = new WeakReference<T>(handler);
```

---

## Iteratory i yield

### foreach desugaring

`foreach` jest lukrem składniowym — kompilator zamienia go na:

```csharp
// Oryginał:
foreach (int n in kolekcja) Console.WriteLine(n);

// Desugaring:
using var enumerator = kolekcja.GetEnumerator();
while (enumerator.MoveNext())
    Console.WriteLine(enumerator.Current);
```

### Ręczna implementacja IEnumerable`<T>`

```csharp
public class ZakresLiczb : IEnumerable<int>
{
    private readonly int _start, _koniec, _krok;

    public IEnumerator<int> GetEnumerator()
        => new ZakresEnumerator(_start, _koniec, _krok);

    // Non-generic dla kompatybilności — deleguje do generycznej
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    private class ZakresEnumerator : IEnumerator<int>
    {
        private int _aktualny;
        public int Current => _aktualny;
        object System.Collections.IEnumerator.Current => _aktualny;
        public bool MoveNext() { _aktualny += _krok; return _aktualny <= _koniec; }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}

// Każde GetEnumerator() daje NOWY enumerator — niezależne iteracje!
```

### yield return i yield break

`yield return` — kompilator generuje state machine, metoda staje się iteratorem. Wykonanie jest LENIWE.

```csharp
// Podstawowy iterator — kompilator tworzy klasę state machine
public static IEnumerable<int> PierwszeN(int n)
{
    for (int i = 1; i <= n; i++)
        yield return i;  // każde yield = "pauza + oddaj element"
}

// Nieskończona sekwencja — lazy evaluation umożliwia
public static IEnumerable<int> Fibonacci()
{
    int a = 0, b = 1;
    while (true) { yield return a; (a, b) = (b, a + b); }
}

Fibonacci().Take(10); // bierze tylko 10 — reszta nie generowana

// yield break — natychmiastowe zakończenie iteratora
public static IEnumerable<int> TakeWhileParzysty(int[] dane)
{
    foreach (int n in dane)
    {
        if (n % 2 != 0) yield break;  // koniec — nie przetwarza dalej
        yield return n;
    }
}

// finally zawsze wykonane — nawet gdy caller przerwie iterację (break/Dispose)
public static IEnumerable<int> IteratorZZasobem()
{
    Console.Write("[open] ");
    try { yield return 1; yield return 2; }
    finally { Console.Write("[close] "); }  // zawsze wykonane!
}
```

### Lazy vs Eager

```csharp
// LAZY — elementy generowane na żądanie
// Zalety: brak alokacji pełnej kolekcji, możliwe nieskończone sekwencje
var lazyChain = Enumerable.Range(1, 1_000_000)
    .Where(n => n % 7 == 0)   // nie wykonuje
    .Select(n => n * n)        // nie wykonuje
    .Take(3);                  // nie wykonuje!
foreach (var v in lazyChain) { } // TERAZ wykonuje — przetwarza ~21 elementów, nie milion!

// EAGER — ToList, ToArray, Count, Sum, Max — materializują sekwencję
var lista = Fibonacci().Take(10).ToList(); // lista w pamięci
lista.Count; lista.Max(); // operacje na gotowej liście
```

### Praktyczne wzorce z yield

```csharp
// Paginacja
static IEnumerable<List<T>> Paginuj<T>(IEnumerable<T> src, int rozmiar)
{
    var paczka = new List<T>();
    foreach (T e in src)
    {
        paczka.Add(e);
        if (paczka.Count == rozmiar) { yield return paczka; paczka = new(); }
    }
    if (paczka.Count > 0) yield return paczka;
}

// Spłaszczanie zagnieżdżonych kolekcji
static IEnumerable<T> Splaszcz<T>(IEnumerable<IEnumerable<T>> zagniezdzone)
{
    foreach (var lista in zagniezdzone)
        foreach (T el in lista) yield return el;
}

// DFS drzewa — rekurencja z yield
static IEnumerable<Wezel<T>> DFS<T>(Wezel<T> wezel)
{
    yield return wezel;
    foreach (var dziecko in wezel.Dzieci)
        foreach (var pod in DFS(dziecko))  // rekurencja
            yield return pod;
}

// ExponentialBackoff — generator opóźnień retry
static IEnumerable<TimeSpan> ExponentialBackoff(int max)
{
    var delay = TimeSpan.FromMilliseconds(100);
    for (int i = 0; i < max; i++) { yield return delay; delay *= 2; }
}
```

### IAsyncEnumerable`<T>` (C# 8+)

```csharp
// Asynchroniczny odpowiednik IEnumerable<T> — streaming bez buforowania całości
// Idealne: streaming z bazy danych, API, kolejki

async IAsyncEnumerable<int> GeneratorAsync(int ile)
{
    for (int i = 1; i <= ile; i++)
    {
        await Task.Delay(100);  // nie blokuje wątku
        yield return i;
    }
}

// Konsumpcja
await foreach (var item in GeneratorAsync(5))
    Console.WriteLine(item);

// Z CancellationToken
async IAsyncEnumerable<int> GeneratorZAnulowaniem(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    for (int i = 1; ; i++)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(100, ct);
        yield return i;
    }
}
```

### Pułapki iteratorów

```csharp
// Pułapka 1: Wielokrotna enumeracja
IEnumerable<int> gen = Generator();
int sum = gen.Sum(); // pierwsze przejście
int max = gen.Max(); // DRUGIE przejście — generator uruchamia się ponownie!
// Dla zapytania DB: dwa razy kwerenda!
// Naprawka: List<int> lista = gen.ToList(); → wielokrotny dostęp do listy

// Pułapka 2: yield return NIE może być w catch
// try { yield return 1; }
// catch { yield return -1; } // BŁĄD KOMPILACJI
// Można: yield w try (bez catch), NIE w finally

// Pułapka 3: Walidacja argumentów opóźniona
IEnumerable<int> GenerujZWalidacja(int n)
{
    if (n < 0) throw new ArgumentException("..."); // NIE tu — przy wywołaniu
    for (int i = 0; i < n; i++) yield return i;
}
var gen = GenerujZWalidacja(-1); // BRAK wyjątku!
gen.First();                     // wyjątek TUTAJ (przy pierwszym MoveNext)
// Naprawka: wrapper nieiteratorowy sprawdza argument, wywołuje właściwy iterator
```

---

## Partial Classes i Static Classes

### Partial class

`partial` pozwala rozdzielić definicję klasy na wiele plików. Kompilator scala je w JEDNĄ klasę.

```csharp
// Plik: Uzytkownik.cs
public partial class Uzytkownik
{
    public int Id { get; set; }
    public string Imie { get; set; } = "";
    public Uzytkownik(int id, string imie) { Id = id; Imie = imie; }
}

// Plik: Uzytkownik.Walidacja.cs
public partial class Uzytkownik
{
    public bool CzyImiePoprawne() => Imie.Length >= 2;
    public IReadOnlyList<string> Waliduj() { /* ... */ }
}

// Plik: Uzytkownik.Formatowanie.cs
public partial class Uzytkownik
{
    public override string ToString() => $"Uzytkownik({Id}: {Imie})";
}

// Dla kodu zewnętrznego — JEDNA klasa
var u = new Uzytkownik(1, "Anna");
u.CzyImiePoprawne();  // z Walidacja.cs
u.ToString();         // z Formatowanie.cs
```

**Główne zastosowania:**
- **Kod generowany + ręczny** — generator (EF Core, Swagger) nadpisuje swój plik, ręczny kod bezpieczny
- **Duże klasy** — podział tematyczny na pliki dla czytelności
- **Współpraca zespołu** — mniej konfliktów Git

### Partial methods — hooki dla generowanego kodu

```csharp
// Część generowana (generator wstawia hooki)
public partial class Formularz
{
    public string Tytul { set { OnTytulZmieniaSie(_tytul, value); _tytul = value; } }

    partial void OnTytulZmieniaSie(string stary, string nowy); // deklaracja bez ciała
    partial void OnPrzedRenderowaniem();                        // deklaracja bez ciała
}

// Część ręczna (opcjonalna implementacja)
public partial class Formularz
{
    partial void OnTytulZmieniaSie(string stary, string nowy)
        => Console.WriteLine($"Zmiana: '{stary}' → '{nowy}'");

    // OnPrzedRenderowaniem — brak implementacji
    // Kompilator USUWA wywołania → zero narzutu w runtime!
}
```

Od C# 9: partial methods mogą mieć modyfikator dostępu → implementacja obowiązkowa.

### Static class

`static class` — nie można tworzyć instancji, wszystkie składowe muszą być `static`, nie dziedziczy.

```csharp
public static class MathHelper
{
    public const double PI = Math.PI;

    public static double Silnia(int n)
    {
        if (n < 0) throw new ArgumentException("n musi być >= 0");
        return n <= 1 ? 1 : n * Silnia(n - 1);
    }

    public static bool CzyPierwsza(int n) { /* ... */ }
    public static int NWD(int a, int b) => b == 0 ? a : NWD(b, a % b);
}

MathHelper.Silnia(5);    // 120
// new MathHelper();     // BŁĄD kompilacji — static class!
```

### Extension methods — najważniejsze zastosowanie static class

Extension methods MUSZĄ być w `static class`. Kompilator zamienia `"tekst".Skroc(10)` na `StringExtensions.Skroc("tekst", 10)`.

```csharp
public static class StringExtensions
{
    // this string — rozszerzasz typ string
    public static bool CzyEmail(this string s) => s.Contains('@') && s.Contains('.') && s.Length > 5;
    public static string Skroc(this string s, int max, string suffix = "...")
    {
        if (s.Length <= max) return s;
        return s[..(max - suffix.Length)] + suffix;
    }
    public static string Powtorz(this string s, int razy) => string.Concat(Enumerable.Repeat(s, razy));
}

public static class KolekcjaExtensions
{
    public static IEnumerable<T> KazdeN<T>(this IEnumerable<T> src, int n) { /* ... */ }
    public static IEnumerable<List<T>> Podziel<T>(this IEnumerable<T> src, int rozmiar) { /* ... */ }
    public static void ForEach<T>(this IEnumerable<T> src, Action<T> akcja) { /* ... */ }
}

// Użycie — wyglądają jak metody instancji!
"jan@test.com".CzyEmail();         // True
"długi tekst".Skroc(10);           // "długi tek..."
"ab".Powtorz(4);                   // "abababab"
liczby.KazdeN(3).ForEach(...);     // co trzeci element
```

**LINQ (`Where`, `Select`, `OrderBy`) to extension methods na `IEnumerable<T>`!**

### Static class ze stanem globalnym

```csharp
public static class Logger
{
    private static readonly object _blokada = new();

    // Static constructor — wywołany raz przed pierwszym użyciem
    static Logger() => Console.WriteLine("Logger inicjalizowany");

    private static void Zapisz(string msg)
    {
        lock (_blokada)  // thread-safe zapis
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }
}
```

**Static class z mutowalnym stanem — używaj ostrożnie:**
- Zalety: prosty dostęp wszędzie
- Wady: trudny do testowania, thread safety, ukryte zależności

### Partial static class

```csharp
// Można łączyć — partial static class
public static partial class Konwersje
{
    public static double CelsjuszNaFahrenheit(double c) => c * 9.0 / 5 + 32;
}

public static partial class Konwersje  // w innym pliku
{
    public static double KilogramyNaFunty(double kg) => kg * 2.20462;
    public static double KilometryNaMile(double km) => km * 0.621371;
}
```

| Cecha | `partial class` | `static class` |
|---|---|---|
| Kiedy używać | Kod generowany + ręczny, duże klasy | Utility, extension methods, stałe |
| Instancje | Tak (chyba że też static) | Nie |
| Dziedziczenie | Tak | Nie |
| Extension methods | Nie wymagane | Wymagane |

---

## Kowariancja i Kontrawariancja

### Problem — dlaczego to istnieje

```csharp
class Zwierze { public string Imie { get; set; } = ""; }
class Pies : Zwierze { public string Rasa { get; set; } = ""; }
class Pudel : Pies { }

// Upcasting — zawsze działa
Pies pies = new Pies();
Zwierze zwierze = pies;  // OK — Pies IS-A Zwierze

// Ale z kolekcjami:
List<Pies> listaPsow = new List<Pies>();
// List<Zwierze> listaZwierzat = listaPsow;  // BŁĄD! Mimo że Pies : Zwierze

// Dlaczego błąd? Gdyby działało:
// listaZwierzat.Add(new Kot());  // Kot jest Zwierzęciem — OK przez List<Zwierze>
// Ale listaPsow zawierałaby Kota! psy[1].Rasa → ClassCastException

// List<T> jest INWARIANTNA — brak wariancji w żadną stronę
// Ale IEnumerable<T> jest kowariantna — tylko odczyt!
IEnumerable<Zwierze> zwierzeta = listaPsow;  // OK!
```

### Inwariantność — brak wariancji (domyślne dla klas)

```csharp
// Klasy generyczne są INWARIANTNE
// List<Pies> → List<Zwierze>: BŁĄD (bo jest Add, modyfikacje możliwe)
// IList<Pies> → IList<Zwierze>: BŁĄD (bo jest indekser do zapisu)

// Tablice — KOWARIANTNE (historyczny błąd C#, niebezpieczne!)
Zwierze[] tablicaZwierzat = new Pies[3];   // kompiluje się!
tablicaZwierzat[0] = new Kot();            // ArrayTypeMismatchException w runtime!
// Sprawdzanie typów przeniesione z compile-time do runtime — narusza bezpieczeństwo
// Lepsza alternatywa: IReadOnlyList<T>
```

### Kowariancja — `out T` — producent

```csharp
// Intuicja: jeśli coś PRODUKUJE Psy, to produkuje też Zwierzęta
// out T — T pojawia się TYLKO na wyjściu (return type)
// Kierunek: Pochodna → Bazowa (zgodny z kierunkiem dziedziczenia)

public interface IProducent<out T>   // out = kowariantny
{
    T Produkuj();
    IEnumerable<T> ProdukujWiele();
    // void Przyjmij(T e); // BŁĄD — out T nie może być parametrem!
}

// Kowariancja w akcji
IProducent<Pudel> hodowlaPudli = new HodowlaPudli();
IProducent<Pies> producent = hodowlaPudli;  // OK! Pudel : Pies
Pies p = producent.Produkuj();  // dostajemy Pudla, ale jako Psa — bezpieczne!

// I wyżej
IProducent<Zwierze> producentZ = hodowlaPudli;  // OK! Pudel : Zwierze

// Lista producentów różnych typów
var hodowle = new List<IProducent<Zwierze>>
{
    hodowlaPsow,   // IProducent<Pies> → IProducent<Zwierze> ✓
    hodowlaPudli   // IProducent<Pudel> → IProducent<Zwierze> ✓
};
```

### Kontrawariancja — `in T` — konsument

```csharp
// Intuicja: jeśli coś AKCEPTUJE Zwierzęta, to akceptuje też Psy
// in T — T pojawia się TYLKO na wejściu (parametry)
// Kierunek: Bazowa → Pochodna (ODWRÓCONY względem dziedziczenia!)

public interface IKonsument<in T>    // in = kontrawariantny
{
    void Przyjmij(T element);
    // T Zwroc(); // BŁĄD — in T nie może być typem zwracanym!
}

// Kontrawariancja w akcji
IKonsument<Zwierze> lecznica = new LecznicaZwierzat();
IKonsument<Pies> lecznicaJakoKonsPsa = lecznica;  // OK! kontrawariancja

// Bezpieczne: wywołamy Przyjmij(pies). Lecznica obsługuje Zwierze. Pies : Zwierze — działa!
lecznicaJakoKonsPsa.Przyjmij(new Pies { Imie = "Rex" }); // OK

// IKonsument<Zwierze> → IKonsument<Pies> → IKonsument<Pudel> (coraz bardziej specyficzne)
```

### Wariancja na delegatach

```csharp
// Func<out TResult> — TResult kowariantny
Func<Pies> produkujPsa = () => new Pies { Imie = "Rex" };
Func<Zwierze> produkujZwierze = produkujPsa;  // OK! Func<Pies> → Func<Zwierze>

// Action<in T> — T kontrawariantny
Action<Zwierze> lecz = z => Console.WriteLine(z.Imie);
Action<Pies> leczPsa = lecz;    // OK! Action<Zwierze> → Action<Pies>

// Func<in T, out TResult> — kombinacja obu
Func<Zwierze, Pies> transformuj = z => new Pies { Imie = z.Imie };
Func<Pies, Zwierze> transformuj2 = transformuj;  // OK!
// in T: Pies→Zwierze ✓ (kontrawariancja)
// out TResult: Pies→Zwierze ✓ (kowariancja)
```

### Wbudowane wariantne interfejsy .NET

**Kowariantne (`out T`):**
- `IEnumerable<out T>` — najczęściej używany
- `IEnumerator<out T>`
- `IReadOnlyList<out T>` — bezpieczna alternatywa dla kowariantnych tablic
- `IReadOnlyCollection<out T>`
- `IReadOnlyDictionary<TKey, out TValue>`
- `IQueryable<out T>`

**Kontrawariantne (`in T`):**
- `IComparer<in T>` — komparator dla ogólniejszego typu pasuje do bardziej szczegółowego
- `IComparable<in T>`
- `IEqualityComparer<in T>`
- `Action<in T>`
- `IObserver<in T>`

```csharp
// IComparer<Zwierze> → IComparer<Pies> (kontrawariancja)
IComparer<Zwierze> porownywacz = Comparer<Zwierze>.Create((a, b) => string.Compare(a.Imie, b.Imie));
IComparer<Pies> porownywaczPsow = porownywacz;  // OK!
listaPsow.Sort(porownywaczPsow);  // używa komparatora Zwierząt dla Psów
```

### Własny interfejs z oboma rodzajami wariancji

```csharp
public interface ITransformator<in TZrodlo, out TDocel>
{
    TDocel Transformuj(TZrodlo zrodlo);
    IEnumerable<TDocel> TransformujWiele(IEnumerable<TZrodlo> zrodla);
}

// Przykład wariancji:
// PudelNaPies : ITransformator<Pudel, Pies> → ITransformator<Pudel, Zwierze>
// TZrodlo: Pudel→Pudel ✓, TDocel: Pies→Zwierze (kowariancja out ✓)

// PiesNaZwierze : ITransformator<Pies, Zwierze> → ITransformator<Pudel, Zwierze>
// TZrodlo: Pies→Pudel (kontrawariancja in — Pies ogólniejszy ✓), TDocel: Zwierze→Zwierze ✓
```

### Mnemonic i reguły

| Cecha | Kowariancja (`out T`) | Kontrawariancja (`in T`) | Inwariancja |
|---|---|---|---|
| T na | Wyjściu (return) | Wejściu (parametry) | Obu |
| Kierunek przypisania | Pochodna → Bazowa | Bazowa → Pochodna | Brak |
| Intuicja | Producent | Konsument | Producent + konsument |
| Przykłady | `IEnumerable<out T>` | `Action<in T>`, `IComparer<in T>` | `List<T>`, `Dictionary<K,V>` |

**Reguła:** jeśli T pojawia się tylko na wyjściu → możesz `out`. Jeśli tylko na wejściu → możesz `in`. Jeśli na obu → inwariantność (brak wariancji).

---

## Pytania rekrutacyjne

**"Do czego służy `partial class`?"** Głównie do separacji kodu generowanego automatycznie od ręcznego — generator nadpisuje swój plik, developer jest bezpieczny. Drugie zastosowanie: duże klasy podzielone tematycznie.

**"Czym jest `static class`?"** Kompilator wymusza że WSZYSTKIE składowe są statyczne i nie można tworzyć instancji. Extension methods wymagają `static class` — bez tego błąd kompilacji. Jest silniejszym kontraktem niż klasa z metodami statycznymi.

**"Czym są extension methods?"** Metody statyczne w statycznej klasie, których pierwszy parametr ma `this`. Kompilator zamienia `"tekst".Skroc(10)` na `StringExtensions.Skroc("tekst", 10)`. LINQ to extension methods na `IEnumerable<T>`.

**"Kiedy `partial method` jest usuwana przez kompilator?"** Gdy nie ma implementacji. Kompilator usuwa deklarację i wszystkie wywołania — zero narzutu.

**"Wyjaśnij kowariancję i kontrawariancję."** Kowariancja (out T): użyj bardziej szczegółowego tam gdzie ogólniejszy — `IEnumerable<Pies>` pasuje gdzie `IEnumerable<Zwierze>` bo tylko czytasz. Kontrawariancja (in T): użyj ogólniejszego tam gdzie szczegółowszy — `Action<Zwierze>` pasuje gdzie `Action<Pies>` bo obsługuje każde Zwierze, też Psa.

**"Dlaczego `List<T>` jest inwariantna a `IEnumerable<T>` kowariantna?"** List ma `Add(T)` — kowariantność pozwoliłaby wstawić Kota do `List<Pies>`. `IEnumerable<T>` ma tylko `GetEnumerator()` zwracający T — wyłącznie odczyt, żadne modyfikacje.

**"Dlaczego tablice w C# są kowariantne i dlaczego to błąd?"** Historyczna decyzja kompatybilności z Javą. `string[] → object[]` kompiluje się, ale `obj[0] = 42` rzuca `ArrayTypeMismatchException` w runtime — sprawdzanie przeniesione z compile-time do runtime. Bezpieczna alternatywa: `IReadOnlyList<T>`.
