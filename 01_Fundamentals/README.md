# 01 — Fundamenty C# i .NET

## Spis treści
1. [Środowisko i Setup](#1-środowisko-i-setup)
2. [Typy Danych i Zmienne](#2-typy-danych-i-zmienne)
3. [Operatory](#3-operatory)
4. [Stringi](#4-stringi)
5. [Instrukcje Warunkowe i Pętle](#5-instrukcje-warunkowe-i-pętle)
6. [Metody i Parametry](#6-metody-i-parametry)
7. [Tablice i Kolekcje](#7-tablice-i-kolekcje)
8. [Klasy i Obiekty](#8-klasy-i-obiekty)
9. [Typowe pytania rekrutacyjne](#9-typowe-pytania-rekrutacyjne)

---

## 1. Środowisko i Setup

### Co instalować
- **.NET SDK** — kompilator, runtime, CLI. Pobierz z dotnet.microsoft.com (wybierz LTS, obecnie .NET 8).
- **IDE** — Visual Studio 2022 Community (Windows, zalecane), JetBrains Rider (polecane APBD), VS Code.

### Sprawdzenie SDK
```bash
dotnet --version          # np. 8.0.100
dotnet --list-sdks        # wszystkie zainstalowane wersje
```

### Tworzenie projektu
```bash
dotnet new console -n MojProjekt   # nowy projekt konsolowy
dotnet build                        # kompilacja bez uruchamiania
dotnet run                          # kompilacja + uruchomienie
dotnet add package Newtonsoft.Json  # instalacja NuGet package
dotnet publish -c Release           # build produkcyjny
```

### Struktura projektu
```
HelloCSharp/
├── HelloCSharp.csproj    ← plik projektu (jak package.json)
├── Program.cs            ← punkt wejścia
├── bin/                  ← skompilowane pliki (nie commituj!)
└── obj/                  ← pliki pośrednie (nie commituj!)
```

### Plik .csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### Program.cs — top-level statements (.NET 6+)
```csharp
// Nie ma tu klasy ani metody Main — kompilator generuje je za Ciebie
Console.WriteLine("Cześć, C#!");
Greet("Kacper");

static void Greet(string imie) => Console.WriteLine($"Welcome, {imie}!");
```

### Jak działa kompilacja
```
Kod .cs → Roslyn (kompilator) → IL (Intermediate Language) → JIT → Kod maszynowy
```
IL jest niezależny od systemu — ten sam program działa na Windows, Linux, macOS (o ile mają runtime .NET).

### Solution vs Project
```bash
dotnet new sln -n MojaAplikacja
dotnet sln add MojaAplikacja.ConsoleApp
dotnet add reference ../MojaAplikacja.Core
```

---

## 2. Typy Danych i Zmienne

### Typy wartościowe vs referencyjne

| | Wartościowe | Referencyjne |
|---|---|---|
| Przykłady | `int`, `bool`, `char`, `struct`, `enum` | `string`, `object`, klasy, tablice |
| Kopiowanie | Kopia wartości | Kopia referencji (adresu) |
| Żyją | Lokalne → stos; pola klasy → sterta | Zawsze sterta |

```csharp
// VALUE TYPE — kopia
int a = 5; int b = a; b = 99;
Console.WriteLine(a); // 5 — niezmienione

// REFERENCE TYPE — kopia referencji
int[] tab1 = { 1, 2, 3 }; int[] tab2 = tab1;
tab2[0] = 99;
Console.WriteLine(tab1[0]); // 99 — oba wskazują na ten sam obiekt!
```

### Wbudowane typy

| Typ | Rozmiar | Zakres/Precyzja | Użycie |
|---|---|---|---|
| `bool` | 1B | true/false | flagi |
| `byte` | 1B | 0–255 | protokoły, pliki |
| `int` | 4B | ±2.1 mld | domyślna liczba całkowita |
| `long` | 8B | ±9.2 trln, sufiks `L` | duże ID, timestamp |
| `float` | 4B | ~7 cyfr, sufiks `f` | grafika 3D, gry |
| `double` | 8B | ~15 cyfr | obliczenia naukowe |
| `decimal` | 16B | ~28 cyfr, sufiks `M` | **pieniądze, finanse** |
| `char` | 2B | jeden znak Unicode | apostrofy: `'A'` |
| `string` | ref | dowolny | niemutowalny, klasa |

```csharp
// Separator cyfr — czytelność
int milion = 1_000_000;
long duzy  = 9_223_372_036_854_775_807L;

// PUŁAPKA float/double — błąd zaokrąglenia IEEE 754
double d = 0.1 + 0.2;      // 0.30000000000000004 — NIE 0.3!
decimal m = 0.1M + 0.2M;   // 0.3 — dokładnie (użyj do pieniędzy)
```

### var — wnioskowanie typów
```csharp
var wiek  = 25;                          // int — kompilator wywnioskuje
var lista = new List<int>();             // List<int>
var sb    = new Dictionary<string, List<int>>(); // bez var = masakra

// var NIE jest dynamic — typ znany w CZASIE KOMPILACJI
// var x; ← BŁĄD — niezainicjalizowane
// var x = 5; x = "tekst"; ← BŁĄD — typ int na zawsze
```

### const vs readonly
```csharp
const double PI = 3.14159;    // znana w kompilacji, wkompilowana w IL, tylko typy proste
readonly string ConnStr;      // ustalana w konstruktorze, może być dowolnym typem
```

### Konwersje
```csharp
// Implicit (bezpieczna, bez utraty danych)
int i = 100; long l = i;   // int → long ✓

// Explicit cast (może stracić dane — obcina, nie zaokrągla!)
double d = 9.99; int n = (int)d;   // 9 !

// TryParse — ZALECANE, nie rzuca wyjątku
if (int.TryParse("123", out int wynik)) { /* wynik = 123 */ }
bool ok = int.TryParse("abc", out _);  // false — discard _
```

### Nullable Reference Types (C# 8+)
```csharp
string  nienull = "Ania";   // NIE może być null
string? nullable = null;     // może być null

int? dlug = nullable?.Length;       // null-conditional — zero wyjątków
string w  = nullable ?? "domyślna"; // null coalescing
nullable ??= "Anonimowy";           // null coalescing assignment
```

---

## 3. Operatory

```csharp
// Arytmetyczne
int a = 10, b = 3;
a / b      // 3   — UWAGA: dzielenie całkowitoliczbowe!
(double)a / b // 3.333... — wymaga rzutowania
a % b      // 1   — reszta z dzielenia

// Inkrementacja
int x = 5;
int j = x++;   // j=5, x=6 (post: najpierw użyj, potem dodaj)
int k = ++x;   // k=7, x=7 (pre: najpierw dodaj, potem użyj)

// Logiczne — short-circuit evaluation
string? s = null;
if (s != null && s.Length > 0) { } // s.Length nie wykona się gdy s==null!

// Null-related
string? imie = null;
string w    = imie ?? "Anonim";    // "Anonim"
imie       ??= "Kacper";           // przypisz tylko gdy null
int? dl     = imie?.Length;        // null gdy imie==null
```

---

## 4. Stringi

### Tworzenie
```csharp
string s1 = "Witaj, " + imie;          // konkatenacja — tworzy nowy obiekt
string s2 = $"Witaj, {imie}!";         // interpolacja — ZALECANA
string s3 = @"C:\Users\Kacper";        // verbatim — ignoruje escape
string s4 = """                         // raw string (C# 11+)
    { "klucz": "wartość" }
    """;
```

### Ważne metody
```csharp
tekst.Trim()                              // usuwa spacje z obu stron
tekst.ToUpper() / ToLower()
tekst.Contains("C#")                      // case sensitive!
tekst.Contains("c#", StringComparison.OrdinalIgnoreCase) // ignoreCase
tekst.Replace("stare", "nowe")
tekst.StartsWith("Pre") / EndsWith("suf")
tekst.Substring(7, 5)  // lub: tekst[7..12]  (C# 8+)
tekst.Split(',')
string.Join(", ", array)
string.IsNullOrEmpty(s) / IsNullOrWhiteSpace(s)  // ← preferuj to drugie
```

### Porównywanie
```csharp
// Dla porównania case-insensitive NIE używaj ToLower().== — użyj:
s1.Equals(s2, StringComparison.OrdinalIgnoreCase) // True
// String interning: literały → ten sam obiekt w pamięci
object.ReferenceEquals("abc", "abc") // True (intern pool)
```

### Immutability → StringBuilder
```csharp
// ZŁE — O(n²) alokacji:
string zly = "";
for (int i = 0; i < 10000; i++) zly += i;  // tysiące nowych obiektów!

// DOBRE — StringBuilder: jeden bufor, O(n)
var sb = new System.Text.StringBuilder(capacity: 1000);
sb.Append("Cześć").AppendLine().AppendFormat("{0} lat", 25);
string wynik = sb.ToString();  // jeden string na końcu
```

### Formatowanie
```csharp
$"{liczba:F2}"   // 1234567,89 (fixed, 2 miejsca)
$"{liczba:N2}"   // 1 234 567,89 (separatory tysięcy)
$"{kwota:C}"     // 9 876,54 zł (waluta)
$"{proc:P1}"     // 75,3% (procent)
$"{n:X}"         // FF (hex), {:B} (binary), {:D5} → 00042 (padding zerami)
$"{data:yyyy-MM-dd HH:mm:ss}"
$"{"Imię",-15} {25,5}"  // wyrównanie: - lewa, bez minus prawa
```

---

## 5. Instrukcje Warunkowe i Pętle

### if / else
```csharp
if (wiek < 18) { ... }
else if (wiek < 65) { ... }
else { ... }

// Ternary
string status = wiek >= 18 ? "dorosły" : "niepełnoletni";
```

### Pattern matching — is (C# 7+)
```csharp
if (obj is string tekst)       // sprawdza typ I rzutuje
    Console.WriteLine(tekst.Length);

if (obj is string t && t.Length > 5) { ... }  // z warunkiem
if (obj is not null) { ... }                   // negacja (C# 9+)
```

### Switch expression (C# 8+) — preferowany w nowym kodzie
```csharp
string dzien = numer switch
{
    1 => "Poniedziałek",
    6 or 7 => "Weekend",        // operator 'or'
    _ => "Inny"                 // wildcard (default)
};

// Property pattern (C# 8+)
string kat = osoba switch
{
    { Wiek: < 18 }                    => "Niepełnoletni",
    { Wiek: >= 18, Imie: "Admin" }    => "Admin",
    { Wiek: >= 18 }                   => "Dorosły",
    null                              => "Brak",
    _                                 => "?"
};
```

### Pętle — kiedy co używać

| Pętla | Kiedy | Przykład |
|---|---|---|
| `for` | Znasz liczbę iteracji, potrzebujesz indeksu | `for (int i=0; i<n; i++)` |
| `foreach` | Iterujesz po kolekcji, nie potrzebujesz indeksu | `foreach (var x in lista)` |
| `while` | Nie wiesz ile iteracji, może się nie wykonać | `while (!gotowe)` |
| `do-while` | Musisz wykonać przynajmniej raz | `do { ... } while (!ok)` |

```csharp
// PUŁAPKA: nie modyfikuj kolekcji podczas foreach!
// lista.Remove(item) w foreach → InvalidOperationException

// Prawidłowo:
for (int i = lista.Count - 1; i >= 0; i--)
    if (lista[i] % 2 == 0) lista.RemoveAt(i);
// Lub:
lista.RemoveAll(x => x % 2 == 0);
```

### break / continue
```csharp
break;    // wychodzi z CAŁEJ pętli (tylko z najbliższej przy zagnieżdżeniu!)
continue; // pomija resztę iteracji, idzie do następnej

// Wyjście z zagnieżdżonych pętli — flaga lub return
bool znaleziono = false;
for (int i = 0; i < 5 && !znaleziono; i++)
    for (int j = 0; j < 5; j++)
        if (warunek) { znaleziono = true; break; }
```

### Zakresy (C# 8+)
```csharp
int[] tab = { 10, 20, 30, 40, 50 };
tab[^1]      // 50 — ostatni (od końca)
tab[^2]      // 40 — przedostatni
tab[1..4]    // { 20, 30, 40 } — 4 jest EXCLUDED, tworzy nową tablicę
tab[2..]     // { 30, 40, 50 }
tab[..3]     // { 10, 20, 30 }
tab[^2..]    // { 40, 50 }
```

---

## 6. Metody i Parametry

### Anatomia
```csharp
// [modyfikator] [static?] [typ zwracany] [nazwa]([parametry])
public static int Dodaj(int a, int b) => a + b;   // expression-bodied
```

### Typy zwracane
```csharp
// Tuple (C# 7+) — zwracanie wielu wartości
(int Min, int Max, double Srednia) Statystyki(int[] l) =>
    (l.Min(), l.Max(), l.Average());

var (min, max, sr) = Statystyki(dane);  // dekonstrukcja

// Nullable return — zamiast magicznego -1
int? Szukaj(int[] tab, int cel) { ... return null; }
```

### Parametry — modyfikatory

| Modyfikator | Znaczenie |
|---|---|
| (brak) | Kopia wartości (lub kopia referencji dla klas) |
| `ref` | Przez referencję, czyta i modyfikuje oryginał, musi być zainicjowana |
| `out` | Przez referencję, metoda MUSI przypisać, nie musi być zainicjowana |
| `in` | Przez referencję, tylko odczyt (optymalizacja dla dużych struct) |
| `params` | Dowolna liczba argumentów, musi być ostatni |

```csharp
// ref — swap klasyczny
void Swap(ref int a, ref int b) { int t = a; a = b; b = t; }
Swap(ref x, ref y);

// out — wzorzec TryXxx
bool TryParsuj(string s, out int wynik) { ... }
if (TryParsuj("42", out int v)) { ... }   // inline deklaracja (C# 7+)
TryParsuj("42", out _);                   // discard — nie potrzebujesz wartości

// params
int Suma(params int[] l) { int s = 0; foreach (int n in l) s += n; return s; }
Suma(1, 2, 3, 4, 5);    // lub Suma(new[] { 1, 2, 3 })
```

### Parametry domyślne i nazwane
```csharp
void Log(string msg, string lvl = "INFO", bool plik = false)
{ ... }

Log("Błąd", "ERROR");          // pozycyjne
Log("Audyt", plik: true);      // pomiń środkowe dzięki nazwanym

// PUŁAPKA: wartości domyślne wkompilowane w IL wywołującego!
// Zmiana wartości w DLL nie wpłynie na stary kod dopóki się nie przekompiluje.
```

### Przeciążanie — overload resolution
```csharp
// Kompilator wybiera w CZASIE KOMPILACJI na podstawie sygnatury:
// 1. Dokładne dopasowanie → 2. Implicit conversion → 3. Boxing → błąd
int    Dodaj(int a, int b)    => a + b;
double Dodaj(double a, double b) => a + b;
// NIE można przeciążyć tylko typem zwracanym!
```

### Local functions (C# 7+)
```csharp
long Silnia(int n)
{
    return Oblicz(n);
    long Oblicz(int x) => x <= 1 ? 1 : x * Oblicz(x - 1);  // widoczna tylko tu
}

// static local function (C# 8+) — zakaz domknięć (closures)
static int Transformuj(int x) => x * 2;
```

### Rekurencja — memoizacja
```csharp
// Naiwny Fibonacci — O(2^n) — dla n>40 praktycznie niemożliwy
// Z memoizacją — O(n):
long FibMemo(int n, Dictionary<int, long>? cache = null)
{
    cache ??= new();
    if (n <= 1) return n;
    if (cache.TryGetValue(n, out long c)) return c;
    cache[n] = FibMemo(n-1, cache) + FibMemo(n-2, cache);
    return cache[n];
}
// Ważne: C# NIE optymalizuje tail calls — głęboka rekurencja → StackOverflow
// W C# preferuj iterację dla głębokiej rekurencji!
```

---

## 7. Tablice i Kolekcje

### Tablice — stały rozmiar, O(1) dostęp
```csharp
int[] tab = { 1, 2, 3, 4, 5 };   // lub: new int[5], lub: [1,2,3] (C#12)
tab[^1]    // ostatni, tab[^2] przedostatni
tab[1..4]  // { 2, 3, 4 } — tworzy NOWĄ tablicę

Array.Sort(tab);           // IN PLACE!
Array.Reverse(tab);        // IN PLACE!
Array.BinarySearch(tab, 5); // O(log n) — tablica MUSI być posortowana

// Rectangular vs Jagged:
int[,] rect = { { 1, 2 }, { 3, 4 } };   // stałe wiersze, jeden blok pamięci
int[][] jagged = { new[] { 1 }, new[] { 2, 3, 4 } }; // różne długości

// Span<T> — widok na pamięć, ZERO kopiowania
Span<int> widok = dane.AsSpan(2, 5);  // nie alokuje!
widok[0] = 999;  // modyfikuje ORYGINAŁ
```

### Kolekcje — kiedy co

| Kolekcja | Dostęp | Add | Contains | Kiedy |
|---|---|---|---|---|
| `T[]` | O(1) | ❌ | O(n) | Stały rozmiar, wydajność |
| `List<T>` | O(1) | O(1)* | O(n) | Domyślny wybór |
| `Dictionary<K,V>` | O(1) | O(1) | O(1) | Klucz → wartość |
| `HashSet<T>` | — | O(1) | O(1) | Unikalne, operacje zbiorowe |
| `Queue<T>` | O(1) | O(1) | — | FIFO, kolejki |
| `Stack<T>` | O(1) | O(1) | — | LIFO, cofanie, DFS |

```csharp
// List<T> pod maską: tablica podwajana przy przepełnieniu
// Add = O(1) amortyzowane; gdy znasz rozmiar → new List<int>(1000)

// Dictionary — bezpieczny odczyt:
if (dict.TryGetValue(klucz, out var val)) { }      // ZALECANE
int v = dict.GetValueOrDefault("klucz", 0);         // C# 8+

// HashSet — operacje zbiorowe (modyfikują oryginał!):
a.IntersectWith(b);   // część wspólna
a.UnionWith(b);       // suma
a.ExceptWith(b);      // różnica

// Usuwanie duplikatów:
var bez = new HashSet<int>(lista);       // HashSet
var bez2 = lista.Distinct().ToList();    // LINQ
```

---

## 8. Klasy i Obiekty

### Anatomia klasy
```csharp
public class Pracownik
{
    private string _imie;               // pole — prywatne, szczegół impl.
    private static int _liczba = 0;     // static — wspólne dla WSZYSTKICH instancji

    public string Imie                  // właściwość — kontrolowany dostęp
    {
        get => _imie;
        set => _imie = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("...")
            : value;
    }

    public string Nazwisko { get; set; }           // auto-property
    public string PelneNazwisko => $"{_imie} {Nazwisko}"; // computed
    public decimal Pensja { get; private set; }    // private setter
    public int Id { get; init; }                   // init-only (C# 9+)
    public required string Email { get; set; }     // required (C# 11+)

    public static int LiczbaInstancji => _liczba;

    public Pracownik(string imie, string nazw, decimal pensja)
    {
        Imie = imie; Nazwisko = nazw; Pensja = pensja; _liczba++;
    }

    // Przeciążony konstruktor deleguje przez : this(...)
    public Pracownik(string imie, string nazw) : this(imie, nazw, 0) { }

    // Statyczny konstruktor — wywołany RAZ, przed pierwszym użyciem klasy
    static Pracownik() { }
}

// Object initializer
var p = new Pracownik("Anna", "Kowalska", 5000m);
var a = new Adres { Ulica = "Główna 1", Miasto = "Kraków" };
```

### Record (C# 9+) — immutable value objects
```csharp
// Kompilator generuje: konstruktor, właściwości, ToString, Equals,
// GetHashCode, operator ==, with expression
public record Osoba(string Imie, string Nazwisko, int Wiek);

var o1 = new Osoba("Anna", "K", 30);
var o2 = new Osoba("Anna", "K", 30);
o1 == o2     // True! (VALUE-BASED, nie referencyjne jak w klasie)
o1.ToString() // "Osoba { Imie = Anna, Nazwisko = K, Wiek = 30 }"

var o3 = o1 with { Wiek = 31 };  // kopia ze zmianą — oryginał niezmieniony
public record struct Punkt(double X, double Y);  // value type record
```

### Object — baza wszystkiego
```csharp
// Zawsze nadpisuj ToString, Equals, GetHashCode razem!
// ZASADA: jeśli a.Equals(b), to a.GetHashCode() == b.GetHashCode()
// Dictionary i HashSet używają GetHashCode() do wyznaczenia bucketu.

public override string  ToString()         => $"{Marka} {Model}";
public override bool    Equals(object? o)  => o is Auto a && a.Marka == Marka;
public override int     GetHashCode()      => HashCode.Combine(Marka, Model);
```

---

## 9. Typowe pytania rekrutacyjne

**Jaka jest różnica między `.NET Framework` a `.NET 5+`?**
`.NET Framework` to stary Windows-only stack (do 4.8), już nie rozwijany. `.NET 5+` (teraz po prostu `.NET`) to cross-platform, open-source następca. Aktualnie używamy `.NET 8` (LTS).

**Co to CLR?**
Common Language Runtime — silnik uruchomieniowy .NET. Zarządza pamięcią (Garbage Collector), kompiluje IL do kodu maszynowego (JIT), obsługuje wyjątki i bezpieczeństwo typów. Odpowiednik JVM w Javie.

**Dlaczego `0.1 + 0.2 != 0.3`?**
`float` i `double` używają IEEE 754 — binarnej reprezentacji ułamków. Nie wszystkie ułamki dziesiętne mają dokładną reprezentację binarną. `decimal` używa reprezentacji dziesiętnej — jest dokładny. Do pieniędzy zawsze `decimal`.

**Czym różni się `const` od `readonly`?**
`const` — wartość w czasie kompilacji, wkompilowana w IL, domyślnie `static`, tylko typy proste. `readonly` — wartość w konstruktorze, może być instancyjna, może być dowolnym typem.

**Czym różni się `var` od `dynamic`?**
`var` — wnioskowanie typów w czasie kompilacji (typ znany i stały). `dynamic` — typ rozwiązywany w runtime (brak sprawdzania, błędy w runtime). `var` to wygoda, `dynamic` to rezygnacja z bezpieczeństwa typów.

**Dlaczego `string +` w pętli jest nieefektywne?**
String jest niemutowalny — każde `+` tworzy nowy obiekt i kopiuje całą dotychczasową zawartość. Dla n iteracji to O(n²) alokacji. Rozwiązanie: `string.Join` (kolekcja z separatorem) lub `StringBuilder` (złożona logika).

**Jaka różnica między `ref`, `out` a `in`?**
`ref` — dwukierunkowy, zmienna musi być zainicjowana. `out` — jednokierunkowy wyjściowy, metoda MUSI przypisać, nie wymaga inicjalizacji (wzorzec `TryXxx`). `in` — tylko odczyt, optymalizacja dla dużych struct.

**Kiedy `HashSet` zamiast `List`?**
`Contains` na `HashSet` to O(1), na `List` to O(n). Używaj `HashSet` gdy sprawdzasz istnienie elementów, chcesz unikalnych wartości lub potrzebujesz operacji zbiorowych.

**Dlaczego nadpisując `Equals` musisz nadpisać `GetHashCode`?**
`Dictionary` i `HashSet` najpierw obliczają bucket przez `GetHashCode()`, potem potwierdzają przez `Equals()`. Jeśli dwa równe obiekty mają różne hashe, trafią do różnych bucketów i `Dictionary` ich nie znajdzie. Kontrakt: `a.Equals(b) → a.GetHashCode() == b.GetHashCode()`.

**Jaka różnica między klasą a rekordem?**
Klasa ma domyślne porównanie referencyjne. Record (C# 9+) ma porównanie po wartościach generowane przez kompilator, gotowe `ToString()`, `GetHashCode()` i wyrażenie `with`. Idealne dla immutable DTO i value objects.

**Rectangular vs Jagged array?**
`int[,]` — jeden ciągły blok pamięci, stałe rozmiary wierszy. `int[][]` — tablica referencji do tablic, elastyczne rozmiary wierszy, naturalnie współpracuje z LINQ.

**Co to `Span<T>`?**
Lekki widok (`ref struct`) na ciągły obszar pamięci — bez kopiowania. Działa na tablicach, stringach, `stackalloc`. Zero alokacji przy przekazywaniu fragmentów. Używany intensywnie w ASP.NET Core.
