# 03_Csharp_3 — C# 3.0: LINQ, Lambdy, Expression Trees, Inicjalizatory

## 1. Wyrażenia Lambda

Lambda to **anonimowa funkcja** którą możesz zapisać w zmiennej, przekazać jako argument lub zwrócić z metody. Fundament LINQ, eventów i całego nowoczesnego C#.

### Składnia

```csharp
// Lambda wyrażeniowa — jeden parametr, bez nawiasów
Func<int, int> kwadrat = x => x * x;

// Wiele parametrów — nawiasy obowiązkowe
Func<int, int, int> suma = (a, b) => a + b;

// Bez parametrów
Func<DateTime> teraz = () => DateTime.Now;

// Lambda blokowa — blok kodu z return
Func<int, string> opis = n =>
{
    if (n < 0) return "ujemna";
    if (n == 0) return "zero";
    return "dodatnia";
};

// Async lambda
Func<int, Task<int>> asyncKwadrat = async x =>
{
    await Task.Delay(1);
    return x * x;
};

// Method group — istniejąca metoda jako delegate
Func<double, double> sqrt = Math.Sqrt;

// Natural type (C# 10+) — kompilator wnioskuje typ lambdy
var dodaj = (int a, int b) => a + b;  // Func<int, int, int>
```

### Func i Action

```csharp
// Func<TIn, TOut> — zwraca wartość; ostatni typ = typ zwracany
Func<int, bool>     czyParzyste = n => n % 2 == 0;
Func<string, int>   dlugosc     = s => s.Length;
Func<int, int, int> max         = (a, b) => Math.Max(a, b);

// Action — void, nie zwraca wartości
Action<string> log   = msg => Console.WriteLine($"[LOG] {msg}");
Action<string, int>  = (msg, n) => Console.WriteLine($"{msg}: {n}");

// Przechowywanie w kolekcji (pipeline)
var pipeline = new List<Func<string, string>>
{
    s => s.Trim(),
    s => s.ToLower(),
    s => s.Replace(" ", "-"),
    s => $"[{s}]"
};
string wynik = pipeline.Aggregate("  Hello World  ", (curr, f) => f(curr));
// [hello-world]
```

### Closures — domknięcia

Closure przechwytuje **REFERENCJĘ** do zmiennej, nie jej wartość w momencie tworzenia:

```csharp
int mnoznik = 3;
Func<int, int> mnoz = x => x * mnoznik;  // przechwytuje mnoznik

Console.WriteLine(mnoz(5));   // 15
mnoznik = 10;                 // zmiana zmiennej zewnętrznej!
Console.WriteLine(mnoz(5));   // 50 — lambda widzi AKTUALNĄ wartość!

// Kompilator generuje klasę domknięcia:
// class <>c__DisplayClass { public int mnoznik; }
```

**Pułapka pętli** — przechwytuje zmienną (referencję), nie wartość iteracji:

```csharp
// ŹLE — wszystkie lambdy przechwytują TĘ SAMĄ zmienną i
var akcjeBled = new List<Action>();
for (int i = 0; i < 5; i++)
    akcjeBled.Add(() => Console.Write($"{i} "));  // i = referencja do JEDNEJ zmiennej
akcjeBled.ForEach(a => a());   // 5 5 5 5 5 — po pętli i == 5!

// DOBRZE — kopia w każdej iteracji (osobna closure)
var akcjeOk = new List<Action>();
for (int i = 0; i < 5; i++)
{
    int kopia = i;
    akcjeOk.Add(() => Console.Write($"{kopia} "));
}
akcjeOk.ForEach(a => a());  // 0 1 2 3 4

// foreach NIE ma tej pułapki (C# 5+) — każda iteracja ma własną zmienną
```

### Static Lambda (C# 9+)

```csharp
// static lambda — nie może przechwytywać żadnych zmiennych z zewnątrz
// Zero alokacji obiektu domknięcia
Func<int, int> razy2 = static x => x * 2;  // OK
// Func<int, int> zla = static x => x * mnoznik;  // CS8820 — błąd!

// Użycie w LINQ — optymalizacja
var parzyste = lista.Where(static n => n % 2 == 0).ToList();
```

Alokacje: static lambda = 0, lambda bez domknięcia = 0, lambda z domknięciem = 1 per wywołanie otaczającej metody.

### Wzorce funkcyjne

**Memoizacja** — cache wyników dla tych samych argumentów:
```csharp
Func<TIn, TOut> Memoize<TIn, TOut>(Func<TIn, TOut> f) where TIn : notnull
{
    var cache = new Dictionary<TIn, TOut>();
    return x => cache.TryGetValue(x, out var v) ? v : (cache[x] = f(x));
}
```

**Currying** — `f(a, b)` → `f(a)(b)`:
```csharp
Func<int, Func<int, int>> dodaj = a => b => a + b;
var dodaj5 = dodaj(5);  // Func<int,int> — częściowo zastosowana
Console.WriteLine(dodaj5(3));  // 8
```

**Kompozycja** — `g(f(x))`:
```csharp
Func<T, TResult> Compose<T, TMid, TResult>(Func<T, TMid> f, Func<TMid, TResult> g)
    => x => g(f(x));
```

**Retry** — ponowne próby przez lambdę:
```csharp
static T Retry<T>(Func<T> operacja, int maxProb)
{
    for (int i = 1; i <= maxProb; i++)
        try { return operacja(); }
        catch when (i < maxProb) { }
    return operacja();
}
```

**Lazy\<T\>** — obliczenie przy pierwszym dostępie:
```csharp
var lazy = new Lazy<int>(() => KosztownaOperacja());
Console.WriteLine(lazy.Value);  // teraz oblicza — jeden raz, potem cache
```

---

## 2. Extension Methods — Metody Rozszerzające

Extension methods pozwalają **dodawać metody do istniejących typów bez modyfikowania ich kodu** — bez dziedziczenia, bez wrappera. To mechanizm który sprawia że LINQ wygląda jak część języka.

### Wymagania syntaktyczne

```csharp
// 1. Musi być w STATIC CLASS
// 2. Musi być STATIC METHOD
// 3. Pierwszy parametr z THIS — to typ który rozszerzasz
public static class StringExtensions
{
    public static bool CzyEmail(this string s) =>
        !string.IsNullOrEmpty(s) && s.Contains('@') && s.Contains('.');

    public static string Skroc(this string s, int max, string suffix = "...") =>
        s.Length <= max ? s : s[..(max - suffix.Length)] + suffix;

    // Można wywoływać na null!
    public static bool CzyPuste(this string? s) => string.IsNullOrWhiteSpace(s);
}

// Kompilator tłumaczy:
"jan@test.com".CzyEmail()
// na:
StringExtensions.CzyEmail("jan@test.com")
```

### Rozszerzanie interfejsów — klucz do LINQ

```csharp
// Rozszerzenie IEnumerable<T> — działa dla List<T>, array, HashSet<T>, wszystkiego
public static class IEnumerableExtensions
{
    public static void ForEach<T>(this IEnumerable<T> src, Action<T> akcja)
    {
        foreach (T e in src) akcja(e);
    }

    public static IEnumerable<IEnumerable<T>> Porcje<T>(this IEnumerable<T> src, int rozmiar)
    {
        var paczka = new List<T>();
        foreach (T e in src)
        {
            paczka.Add(e);
            if (paczka.Count == rozmiar) { yield return paczka; paczka = new(); }
        }
        if (paczka.Count > 0) yield return paczka;
    }
}
```

### Zasady priorytetu

1. **Metoda instancji zawsze wygrywa** z extension method o tej samej nazwie
2. Rozwiązywanie statyczne w **compile-time** na podstawie **typu zmiennej** (nie obiektu) — brak polimorfizmu
3. Można wywoływać na `null` jeśli metoda obsługuje null receiver

### Fluent API przez extension methods

```csharp
// Każda metoda zwraca this — łańcuchowanie
var (ok, bledy) = ""
    .ZaczniWalidowac()
    .NieMozeBycPusty()
    .MusiZawierac("@")
    .MinimalnieDlugi(5)
    .Waliduj();
```

### LINQ = extension methods na IEnumerable\<T\>

```csharp
// Query syntax i method syntax to to samo — kompilator tłumaczy query na metody
var z_metodami = liczby.Where(n => n > 4).OrderBy(n => n).Select(n => n * n);
var z_query    = from n in liczby where n > 4 orderby n select n * n;
// Identyczny IL!

// Własna reimplementacja Where:
static IEnumerable<T> MojeWhere<T>(IEnumerable<T> src, Func<T, bool> pred)
{
    foreach (var e in src) if (pred(e)) yield return e;
}
```

### Pułapki

1. **Brak polimorfizmu** — rozwiązywanie statyczne w compile-time
2. **Konflikty nazw** — dwa using z tą samą ext → CS0121 niejednoznaczność
3. **Brak dostępu do private** — jest zewnętrzną metodą statyczną
4. **Null receiver** — dozwolony, ale musisz jawnie obsłużyć null
5. **Własna klasa** — jeśli możesz edytować jej kod, nie używaj extension method

---

## 3. Typy Anonimowe i `var`

### `var` — wnioskowanie typów

`var` to wnioskowanie typów w **czasie kompilacji** — typ jest ustalany raz i jest stały:

```csharp
var liczba = 42;               // int   — COMPILE TIME!
var tekst  = "hello";          // string
var lista  = new List<int>();  // List<int>
var pi     = 3.14159;          // double

// var NIE jest dynamic!
var x = 5;
// x = "tekst";  // CS0029 — x jest int na zawsze

// var bez inicjalizacji — BŁĄD
// var y;  // CS0818
```

**Kiedy używać var:**
- Konstruktory — typ widoczny po prawej: `var dict = new Dictionary<string, int>()`
- Długie typy generyczne
- LINQ — typ anonimowy lub złożony (jedyna opcja!)
- `using var reader = new StreamReader(...)` — Dispose() na końcu scope

**Kiedy NIE używać:**
- Gdy typ nie wynika z kontekstu: `var wynik = ObliczCos()` — co zwraca?

### Typy anonimowe

Kompilator generuje klasę bez nazwy z auto-properties read-only, wygenerowanym `Equals`/`GetHashCode`/`ToString`:

```csharp
var osoba = new { Imie = "Anna", Wiek = 30, Aktywna = true };
Console.WriteLine(osoba);  // { Imie = Anna, Wiek = 30, Aktywna = True }
// osoba.Imie = "Jan";  // CS0200 — readonly!

// Projekcja z istniejącej zmiennej — można pominąć nazwę
string imie = "Jan";
var auto = new { imie, Wiek = 25 };  // auto.imie, auto.Wiek

// EQUALS porównuje VALUES (nie referencje)
var a1 = new { X = 1, Y = 2 };
var a2 = new { X = 1, Y = 2 };
Console.WriteLine(a1.Equals(a2));         // True
Console.WriteLine(ReferenceEquals(a1, a2)); // False
Console.WriteLine(a1.GetHashCode() == a2.GetHashCode()); // True
```

**Ograniczenia:**
- Nie można zwrócić z metody (poza `object`/`dynamic`)
- Właściwości są readonly
- Nie implementują interfejsów
- Kolejność właściwości ma znaczenie — `{X=1,Y=2}` i `{Y=2,X=1}` to różne typy

### Typy anonimowe w LINQ

```csharp
// Select z projekcją — tylko potrzebne pola
var projekcja = Produkty
    .Where(p => p.Cena > 100)
    .Select(p => new { p.Nazwa, p.Cena, CenaZVat = p.Cena * 1.23m });

// GroupBy z agregacją
var grupy = Produkty
    .GroupBy(p => p.Kategoria)
    .Select(g => new
    {
        Kategoria = g.Key,
        Ilosc = g.Count(),
        SredniaCena = g.Average(p => p.Cena)
    });
```

### Anonymous Type vs ValueTuple vs Record vs Klasa

| Typ | Kiedy | Zalety | Ograniczenia |
|-----|-------|--------|--------------|
| Anonymous | LINQ projekcja lokalna | Zwięzły, Equals z wartości | Nie można zwrócić z metody |
| ValueTuple | Zwracanie 2-3 wartości | Lekki, destrukturyzacja | Brak Equals/ToString |
| Record | DTO, public API | Pełny typ, Equals z wartości, with | Trzeba zdefiniować |
| Klasa | Mutowalny stan, logika | Dziedziczenie, full control | Dużo kodu |

---

## 4. LINQ — Podstawy

LINQ (Language Integrated Query) to **system zapytań wbudowany w C#**. Pozwala odpytywać kolekcje, bazy danych i XML używając tej samej składni.

### Query vs Method Syntax

```csharp
// Query syntax — SQL-like, kompilator tłumaczy na method syntax
var queryResult =
    from p in Pracownicy
    where p.Dzial == "IT"
    orderby p.Pensja descending
    select new { p.Imie, p.Pensja };

// Method syntax — extension methods, identyczny IL
var methodResult = Pracownicy
    .Where(p => p.Dzial == "IT")
    .OrderByDescending(p => p.Pensja)
    .Select(p => new { p.Imie, p.Pensja });

// Reguła: query gdy join+groupby czytelniejszy, method w pozostałych przypadkach
```

### Filtrowanie — Where

```csharp
// Proste filtrowanie
var senior = Pracownicy.Where(p => p.Wiek >= 40);

// Łańcuchowanie Where — każde Where to kolejny filtr (AND)
var itSenior = Pracownicy.Where(p => p.Dzial == "IT").Where(p => p.Pensja > 8000);

// Where z indeksem
var coPiaty = Pracownicy.Where((p, idx) => idx % 2 == 0);

// OfType<T> — filtrowanie po typie
object[] mieszane = { 1, "dwa", 3.0, "cztery" };
var sameLiczby = mieszane.OfType<int>();  // [1]
```

### Projekcje — Select i SelectMany

```csharp
// Select — mapowanie 1:1
var imiona = Pracownicy.Select(p => p.Imie);

// Select z indeksem
var ponumerowane = Pracownicy.Select((p, i) => $"{i + 1}. {p.Imie}");

// SelectMany — "flatten" kolekcji zagnieżdżonej (1:N)
var dzialy = new[] {
    new { Dzial="IT", Osoby=new[]{"Anna","Marek"} },
    new { Dzial="HR", Osoby=new[]{"Jan"} }
};

var wszyscy = dzialy.SelectMany(d => d.Osoby);
// ["Anna", "Marek", "Jan"]

// SelectMany z zachowaniem kontekstu rodzica
var pary = dzialy.SelectMany(d => d.Osoby, (d, osoba) => new { d.Dzial, Osoba = osoba });

// Iloczyn kartezjański
var kombinacje = kolory.SelectMany(k => rozmiary, (k, r) => $"{k}-{r}");
```

### Sortowanie

```csharp
var wgPensji = Pracownicy.OrderBy(p => p.Pensja);           // rosnąco
var wgWieku  = Pracownicy.OrderByDescending(p => p.Wiek);   // malejąco

// Wielopoziomowe sortowanie
var wielopoziom = Pracownicy
    .OrderBy(p => p.Dzial)
    .ThenByDescending(p => p.Pensja)
    .ThenBy(p => p.Imie);
```

### Grupowanie

```csharp
// GroupBy — IEnumerable<IGrouping<TKey, TElement>>
var wgDzialu = Pracownicy.GroupBy(p => p.Dzial);
foreach (var g in wgDzialu)
    Console.WriteLine($"{g.Key}: {g.Count()} osób, avg={g.Average(p => p.Pensja):C}");

// GroupBy z klucz złożony
var wgKatIDostepnosci = Produkty.GroupBy(p => new { p.Kategoria, Dostepny = p.StanMagazynu > 0 });

// ToLookup — zmaterializowany GroupBy (wielokrotny dostęp)
var lookup = Pracownicy.ToLookup(p => p.Dzial);
var itTeam = lookup["IT"];       // IEnumerable<Pracownik>
var brak   = lookup["Zarząd"];   // pusta kolekcja — nie rzuca!
```

### Agregacje

```csharp
int liczba    = Pracownicy.Count();
int aktywne   = Pracownicy.Count(p => p.Dzial == "IT");
decimal suma  = Pracownicy.Sum(p => p.Pensja);
decimal avg   = Pracownicy.Average(p => p.Pensja);
decimal max   = Pracownicy.Max(p => p.Pensja);
decimal min   = Pracownicy.Min(p => p.Pensja);

// MinBy / MaxBy (.NET 6+) — zwraca ELEMENT, nie wartość
Pracownik? najlepszy = Pracownicy.MaxBy(p => p.Pensja);

// Aggregate — fold/reduce ogólny
decimal sumaReczna = Pracownicy.Aggregate(0m, (acc, p) => acc + p.Pensja);
string imiona = Pracownicy.Select(p => p.Imie).Aggregate((a, b) => $"{a}, {b}");
```

### Wyszukiwanie elementów

```csharp
bool jestIT = Pracownicy.Any(p => p.Dzial == "IT");    // zatrzymuje przy pierwszym!
bool wszystkieMail = Pracownicy.All(p => p.Email != null);

var pierwszy = Pracownicy.First(p => p.Dzial == "IT");           // rzuca gdy brak
var pierwszyLub = Pracownicy.FirstOrDefault(p => p.Dzial == "Zarząd"); // null gdy brak

// Single — dokładnie jeden element; rzuca gdy 0 lub >1
var jedyny = Pracownicy.Single(p => p.Id == 5);

// Take / Skip — paginacja
var strona = Pracownicy.Skip(pageSize * (page - 1)).Take(pageSize);

// TakeWhile / SkipWhile — warunek ciągły (nie filtr!)
var mlodzi = Pracownicy.OrderBy(p => p.Wiek).TakeWhile(p => p.Wiek < 35);
```

### Join i GroupJoin

```csharp
// Join — INNER JOIN
var pracZZam = Pracownicy.Join(Zamowienia,
    p => p.Id,
    z => z.PracownikId,
    (p, z) => new { p.Imie, z.Kwota });

// GroupJoin — LEFT OUTER JOIN (wszyscy z lewej, z kolekcją z prawej)
var wszystkiePrac = Pracownicy.GroupJoin(Zamowienia,
    p => p.Id,
    z => z.PracownikId,
    (p, zamGrupa) => new { p.Imie, LiczbaZam = zamGrupa.Count(), Suma = zamGrupa.Sum(z => z.Kwota) });

// Operacje na zbiorach
var union     = a.Union(b);      // suma (bez duplikatów)
var intersect = a.Intersect(b);  // część wspólna
var except    = a.Except(b);     // różnica A-B
var zip       = a.Zip(b, (x, y) => $"{x}={y}");  // łączenie parami
var chunk     = lista.Chunk(3);  // podział na porcje (.NET 6+)
```

### Odłożone wykonanie (Deferred Execution)

```csharp
// LINQ nie wykonuje się przy deklaracji — tylko przy iteracji!
var zapytanie = lista.Where(n => n % 2 == 0);  // nic się nie dzieje

// Wykonanie następuje przy:
foreach (var n in zapytanie) { }   // tutaj!
int count = zapytanie.Count();      // tutaj! (ponownie!)

// MATERIALIZACJA (eager) — wykonuje jeden raz i cache'uje
var zmaterializowane = lista.Where(n => n % 2 == 0).ToList(); // TERAZ
foreach (var n in zmaterializowane) { }   // z listy — brak ponownego wykonania

// Metody materializujące: ToList, ToArray, ToDictionary, ToHashSet, ToLookup
// Count, Sum, Min, Max, Average, Aggregate
// First, Last, Single, Any, All, ElementAt
```

---

## 5. LINQ — Zaawansowane

### SelectMany zaawansowane

```csharp
// Spłaszczanie tagów
var unikalneTagi = Produkty.SelectMany(p => p.Tagi).Distinct().OrderBy(t => t);

// Iloczyn kartezjański (cross join)
var warianty = rozmiary.SelectMany(r => kolory, (r, k) => $"{r}/{k}");

// Query syntax SelectMany — wiele from
var query = from p in Produkty
            from tag in p.Tagi
            where tag.StartsWith("g")
            select new { p.Nazwa, Tag = tag };
```

### Aggregate zaawansowane

```csharp
// Running total
var cumSum = liczby.Aggregate(new List<int>(),
    (acc, n) => { acc.Add((acc.Count > 0 ? acc[^1] : 0) + n); return acc; });

// Budowanie słownika
var dict = Produkty.Aggregate(new Dictionary<string, int>(),
    (acc, p) => { acc[p.Kategoria] = acc.GetValueOrDefault(p.Kategoria) + 1; return acc; });

// Scan (running aggregate)
static IEnumerable<TAccum> Scan<T, TAccum>(IEnumerable<T> src, TAccum seed, Func<TAccum, T, TAccum> f)
{
    TAccum acc = seed;
    foreach (T e in src) { acc = f(acc, e); yield return acc; }
}
```

### IQueryable vs IEnumerable

```csharp
// IEnumerable — wykonanie po stronie KLIENTA (in-process)
// Predykat: Func<T, bool> — skompilowany delegate
IEnumerable<Produkt> asEnum = Produkty;
var filtered = asEnum.Where(p => p.Cena > 300);  // filtr w C#

// IQueryable — wykonanie po stronie DOSTAWCY (SQL, etc.)
// Predykat: Expression<Func<T, bool>> — drzewo wyrażeń → SQL
IQueryable<Produkt> asQuery = Produkty.AsQueryable();
var filteredQ = asQuery.Where(p => p.Cena > 300);  // potencjalnie → WHERE Cena > 300

// W EF Core ZAWSZE używaj IQueryable do filtrowania!
// AsEnumerable() — od tego miejsca wykonuj w C# (dla operacji niemapowanych na SQL)
// AsQueryable() — owinięcie IEnumerable w IQueryable (dynamiczne zapytania)
```

### Specification Pattern

```csharp
// Hermetyzacja reguł biznesowych jako obiekty
public abstract class Specyfikacja<T>
{
    public abstract bool IsSatisfiedBy(T element);
    public Specyfikacja<T> And(Specyfikacja<T> other) => new AndSpec<T>(this, other);
    public Specyfikacja<T> Or(Specyfikacja<T> other)  => new OrSpec<T>(this, other);
    public Specyfikacja<T> Negate()                    => new NotSpec<T>(this);
}

// Użycie — kompozycja reguł
var elektronika = new KategoriaSpec("Elektronika");
var drogie      = new CenaMinSpec(400);
var bestseller  = new TagSpec("bestseller");

var wynik = Produkty.Where(elektronika.And(drogie).IsSatisfiedBy);
```

### LINQ Anti-patterns

1. `Count() > 0` → użyj `Any()` (zatrzymuje przy pierwszym)
2. `ToList()` przed filtrowaniem — filtruj PRZED materializacją
3. Wielokrotna re-enumeracja `IEnumerable` — zmaterializuj `ToList()`
4. `First()` bez sprawdzenia → `FirstOrDefault()` z null check
5. `OrderBy().Where()` — sortuj PO filtrowaniu (mniej elementów)
6. LINQ w pętli O(n²) → `ToDictionary()` + lookup O(1)

### Operacje na zbiorach

```csharp
setA.Union(setB)           // suma (unikalne)
setA.Intersect(setB)       // część wspólna
setA.Except(setB)          // różnica A-B
lista.Distinct()           // usunięcie duplikatów
Produkty.DistinctBy(p => p.Kategoria)  // distinct wg klucza (.NET 6+)
a.Zip(b, (x, y) => ...)   // łączenie parami
liczby.Chunk(3)            // podział na porcje (.NET 6+)
s1.SequenceEqual(s2)       // porównanie sekwencji element po elemencie
a.Concat(b)                // łączenie (z duplikatami)
lista.Prepend(x)           // dodaj na początku
lista.Append(x)            // dodaj na końcu
```

---

## 6. Expression Trees — Drzewa Wyrażeń

Expression Tree to **reprezentacja kodu jako danych** — zamiast kompilować lambdę do IL, kompilator buduje drzewo obiektów opisujących strukturę wyrażenia.

### Func vs Expression\<Func\>

```csharp
// Func — skompilowany kod (delegate), czarna skrzynka
Func<OsobaET, bool> funcPred = o => o.Wiek > 30;
bool wynik = funcPred(osoba);  // można wywołać

// Expression<Func<>> — DRZEWO WYRAŻEŃ: dane opisujące kod
Expression<Func<OsobaET, bool>> exprPred = o => o.Wiek > 30;

// Można zbadać strukturę:
Console.WriteLine(exprPred.Body);          // (o.Wiek > 30)
Console.WriteLine(exprPred.Body.NodeType); // GreaterThan

// Kompilacja do Func:
Func<OsobaET, bool> skompilowany = exprPred.Compile();
bool wynik2 = skompilowany(osoba);

// ANALOGIA:
// Func      = gotowe danie — możesz zjeść, nie wiesz jak ugotowane
// Expression = przepis — możesz czytać, modyfikować, tłumaczyć na SQL
```

### Anatomia drzewa wyrażeń

```csharp
// o => o.Imie == "Anna" && o.Wiek > 25
//              Lambda
//                │
//           AndAlso (&&)
//          ╱           ╲
//      Equal           GreaterThan
//     ╱    ╲            ╱        ╲
// Member  Const     Member     Const
// o.Imie  "Anna"    o.Wiek      25

// Główne węzły:
// ParameterExpression  — parametr lambdy (o)
// MemberExpression     — dostęp do właściwości/pola (o.Imie)
// ConstantExpression   — stała ("Anna", 25)
// BinaryExpression     — operator binarny (==, >, &&, +)
// UnaryExpression      — operator unarny (!, -)
// MethodCallExpression — wywołanie metody (.Contains(), .StartsWith())
// LambdaExpression     — cała lambda (o => ...)
```

### Ręczne budowanie drzewa

```csharp
// Odpowiednik: o => o.Wiek > 30
ParameterExpression param = Expression.Parameter(typeof(OsobaET), "o");
MemberExpression wiek     = Expression.Property(param, nameof(OsobaET.Wiek));
ConstantExpression stala  = Expression.Constant(30);
BinaryExpression cialo    = Expression.GreaterThan(wiek, stala);
Expression<Func<OsobaET, bool>> lambda = Expression.Lambda<Func<OsobaET, bool>>(cialo, param);

Console.WriteLine(lambda);  // o => (o.Wiek > 30)
var wynik = Osoby.Where(lambda.Compile()).ToList();
```

### ExpressionVisitor — podmiana węzłów

```csharp
// Visitor do podmiany ParameterExpression (potrzebne przy łączeniu wyrażeń)
public class ParameterReplacer(ParameterExpression toReplace, ParameterExpression replacement)
    : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
        => node == toReplace ? replacement : base.VisitParameter(node);
}

// Łączenie AND dwóch Expression<Func<T,bool>>
static Expression<Func<T, bool>> CombineAnd<T>(
    Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
{
    var visitor = new ParameterReplacer(right.Parameters[0], left.Parameters[0]);
    var rightBody = visitor.Visit(right.Body);
    return Expression.Lambda<Func<T, bool>>(
        Expression.AndAlso(left.Body, rightBody), left.Parameters[0]);
}
```

### Dynamiczny filtr i sortowanie

```csharp
// Budowanie filtra w runtime na podstawie nazwy właściwości + operatora + wartości
IQueryable<OsobaET> ZastosujFiltr(IQueryable<OsobaET> query, string prop, string op, object value)
{
    var param  = Expression.Parameter(typeof(OsobaET), "o");
    var member = Expression.Property(param, prop);
    var stala  = Expression.Constant(Convert.ChangeType(value, member.Type));
    Expression cialo = op switch
    {
        "==" => Expression.Equal(member, stala),
        ">"  => Expression.GreaterThan(member, stala),
        "<"  => Expression.LessThan(member, stala),
        _    => throw new ArgumentException($"Nieznany operator: {op}")
    };
    return query.Where(Expression.Lambda<Func<OsobaET, bool>>(cialo, param));
}
```

### Expression Trees w EF Core

```csharp
// EF Core: dbContext.Users.Where(u => u.Wiek > 18)
// → Expression<Func<User, bool>> analizowane przez EF
// → generuje: SELECT * FROM Users WHERE Wiek > 18
// → filtrowanie W BAZIE, nie w pamięci!

// IEnumerable.Where dostaje Func<T, bool>  — delegate, wykonywane w C#
// IQueryable.Where dostaje Expression<...> — drzewo, tłumaczone przez provider

// To jest KLUCZOWA różnica — IQueryable pozwala generować SQL
```

---

## 7. Object Initializers — Inicjalizatory Obiektów

### Ewolucja auto-properties

```csharp
// C# 2: pełna właściwość z polem
private string _imie = "";
public string Imie { get { return _imie; } set { _imie = value; } }

// C# 3: auto-property get;set;
public int Wiek { get; set; }

// C# 6: auto-property tylko do odczytu (ustawialna tylko przez konstruktor)
public Guid Id { get; } = Guid.NewGuid();

// C# 6: wartość domyślna
public string Rola { get; set; } = "user";
public bool Aktywny { get; set; } = true;

// C# 9: init-only — można ustawić w inicjalizatorze lub konstruktorze, potem readonly
public string Miasto { get; init; } = "";

// C# 11: required — kompilator wymaga podania wartości w inicjalizatorze
public required string Nazwa { get; set; }
```

### Object Initializer (C# 3)

```csharp
// Przypisanie właściwości PO wywołaniu konstruktora
var pracownik = new PracownikOI
{
    Imie = "Jan",
    Nazwisko = "Kowalski",
    Wiek = 30,
    Email = "jan@example.com"
};

// init-only: po zakończeniu {} już nie można zmienić
// pracownik.Imie = "Piotr";  // CS8852 — Imie jest init-only!

// required: kompilator wymaga podania
var ew = new EvolucjaWlasciwosci
{
    Nazwa = "Test",  // required — obowiązkowe
    Miasto = "Gdańsk"
};

// record with expression — kopiowanie z modyfikacją (shallow copy)
var starsza = osoba with { Wiek = 29 };
```

### Inicjalizatory kolekcji (C# 3)

```csharp
// List
var lista = new List<int> { 1, 2, 3, 4, 5 };

// Dictionary — dwa style
var dict1 = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
var dict2 = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };  // C# 6

// Inicjalizatory kolekcji = kompilator wywołuje .Add() dla każdego elementu
// Własna klasa z Add<T>(T) może korzystać z {} collection initializer
```

### C# 12 — Wyrażenia Kolekcji

```csharp
// Ujednolicona składnia [] dla wielu typów
int[] tablica = [1, 2, 3, 4, 5];
List<string> lista = ["raz", "dwa", "trzy"];
Span<int> span = [10, 20, 30];
int[] pusta = [];

// Spread element (..) — "rozwinięcie" kolekcji
int[] a = [1, 2, 3];
int[] b = [4, 5, 6];
int[] polaczone = [..a, ..b];         // [1, 2, 3, 4, 5, 6]
int[] z_obu    = [0, ..a, ..b, 7];   // [0, 1, 2, 3, 4, 5, 6, 7]

// Ewolucja:
// C# 1:  new int[] { 1, 2, 3 }
// C# 3:  new[] { 1, 2, 3 }       (type inference)
// C# 3:  new List<int> { 1, 2, 3 }
// C# 12: [1, 2, 3]               (działa dla wielu typów)
// C# 12: [..a, ..b]              (spread)
```

### Wzorce inicjalizacji

**Builder z Action\<T\> configure:**
```csharp
static KonfigOI Skonfiguruj(Action<KonfigOI> configure)
{
    var cfg = new KonfigOI();
    configure(cfg);
    return cfg;
}

var config = Skonfiguruj(cfg =>
{
    cfg.Host = "api.example.com";
    cfg.Port = 443;
    cfg.UseTls = true;
    cfg.Headers["Authorization"] = "Bearer token123";
});
```

**Zagnieżdżony inicjalizator:**
```csharp
var pracownik = new PracownikOI
{
    Imie = "Zofia",
    Adres = new AdresOI { Ulica = "Marszałkowska 1", Miasto = "Warszawa" },
    Umiejetnosci = { "C#", "Azure", "SQL" }
};
```

### Pułapki inicjalizatorów

1. **Shallow copy przy record `with`** — `List<T>` w rekordzie to ta sama referencja → użyj `ImmutableList<T>`
2. **`init`** wymuszane przez kompilator (nie runtime) — reflection może ominąć
3. **`required`** wymaga C# 11 / .NET 7+ — starsze projekty nie skompilują; `[SetsRequiredMembers]` na konstruktorze pozwala pominąć required
4. **Collection initializer** = kompilator wywołuje `.Add()` — klasa musi mieć publiczną metodę `Add`

### Kiedy co wybrać

| Rodzaj | Kiedy |
|--------|-------|
| `get;set;` | Mutowalny obiekt, configure action pattern |
| `get;init;` | Semi-immutable, można ustawić w inicjalizatorze |
| `get;` (getter-only) | Pełne immutable — tylko przez konstruktor |
| `required` | Wymuszenie przez kompilator że pole musi być podane |
| `record` | Immutable DTO + Equals/GetHashCode/ToString za darmo |

---

## Typowe pytania rekrutacyjne

**"Czym różni się `Func<T,bool>` od `Expression<Func<T,bool>>`?"**
`Func<T,bool>` to skompilowany delegate — można wywołać, ale nie można analizować co robi. `Expression<Func<T,bool>>` to drzewo danych opisujące wyrażenie — można zbadać strukturę, zmodyfikować, skompilować przez `.Compile()`. EF Core używa `Expression` żeby tłumaczyć lambdy na SQL. Jeśli przekażesz `Func` do EF — dane pobrane do pamięci i filtrowane w C# (N+1 problem).

**"Co to closure i dlaczego przechwytuje referencję?"**
Closure to mechanizm który pozwala lambdzie "pamiętać" zmienne z otaczającego scope'u nawet gdy ten scope już nie istnieje. Kompilator generuje ukrytą klasę trzymającą przechwycone zmienne jako pola — lambdy są metodami tej klasy. Przechwytuje referencję (pole w obiekcie), nie wartość — to pozwala wielu lambdom dzielić stan i modyfikować go wzajemnie.

**"Jaka różnica między Query Syntax a Method Syntax?"**
Query syntax jest tłumaczona przez kompilator na method syntax — w IL są identyczne. Query syntax czytelniejsza dla złożonych joinów i grupowań. Method syntax bardziej elastyczna — obsługuje metody bez odpowiednika query syntax (`Take`, `Skip`, `FirstOrDefault`, `Zip`, `Chunk`, `Aggregate`).

**"Co to deferred execution?"**
LINQ zapytania są lazy — wykonują się dopiero przy iteracji (`foreach`, `ToList`, `Count()`). Pułapki: wielokrotna iteracja = wielokrotne wykonanie; modyfikacja źródłowej kolekcji jest widoczna; closure z pętlą przechwytuje referencję. Materializuj `ToList()` gdy iterujesz wielokrotnie.

**"Jaka różnica między `First()` a `Single()`?"**
`First()` — zwraca pierwszy pasujący, nie obchodzi go ile jest. `Single()` — rzuca `InvalidOperationException` gdy jest zero lub więcej niż jeden. Używaj `Single` gdy logika biznesowa gwarantuje unikalność (Id).

**"Kiedy `var` a kiedy jawny typ?"**
`var` gdy typ jest oczywisty z prawej strony lub nieważny: konstruktory, LINQ (jedyna opcja dla anonymous type), długie generyki. Jawny typ gdy typ jest ważny dla czytelności: metody które zwracają nieoczywisty wynik.

**"Jak działają extension methods pod maską?"**
Kompilator zamienia `obiekt.Metoda(arg)` na `KlasaStatyczna.Metoda(obiekt, arg)`. Zero narzutu w runtime — w IL brak różnicy. Dlatego można je wywoływać na `null`. Metoda instancji zawsze wygrywa z extension method. Brak polimorfizmu — wywoływana wersja zależy od typu zmiennej, nie faktycznego obiektu.

**"Czym różni się `init` od `required`?"**
`init` — właściwość można ustawić w inicjalizatorze `{ }` lub konstruktorze; po tym jest readonly. `required` (C# 11) — kompilator wymaga podania wartości w inicjalizatorze, ale nie ogranicza modyfikacji po tym (jeśli `set`). Można kombinować: `public required string Imie { get; init; }` — wymagane i niezmienne.

---

## Pliki projektu

| Plik | Zawartość |
|------|-----------|
| `WyrazzeniaLambda.cs` | Składnia, closures, static lambda, memoizacja, currying, wzorce funkcyjne |
| `MetodyRozszerzajace.cs` | string/int/DateTime/IEnumerable/Dictionary extensions, fluent API, pułapki |
| `AnonymousTypesIVar.cs` | var, anonymous types, LINQ projekcje, porównanie z Tuple/Record |
| `LinqPodstawy.cs` | Query/method syntax, Where, Select, SelectMany, OrderBy, GroupBy, agregacje, Join, deferred execution |
| `LinqZaawansowane.cs` | SelectMany zaawansowane, Aggregate, IQueryable vs IEnumerable, Specification Pattern, anti-patterns, operacje zbiorowe, paginacja |
| `ExpressionTrees.cs` | Func vs Expression, anatomia drzewa, ręczne budowanie, ExpressionVisitor, dynamiczny filtr, mini SQL translator |
| `ObjectInitializers.cs` | Ewolucja auto-properties, object/collection initializers, C# 12 collection expressions, wzorce, pułapki |
| `Program.cs` | Top-level statements wywołujące wszystkie metody demo |
