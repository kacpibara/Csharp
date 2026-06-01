# 02_OOP — Programowanie Obiektowe w C#

Projekt demonstruje wszystkie aspekty OOP w C#: hermetyzację, dziedziczenie, interfejsy, polimorfizm, enumy, struktury, zasady SOLID i obsługę wyjątków.

---

## Spis treści
1. [Hermetyzacja i enkapsulacja](#1-hermetyzacja-i-enkapsulacja)
2. [Dziedziczenie](#2-dziedziczenie)
3. [Interfejsy i klasy abstrakcyjne](#3-interfejsy-i-klasy-abstrakcyjne)
4. [Polimorfizm](#4-polimorfizm)
5. [Typy wyliczeniowe (enum)](#5-typy-wyliczeniowe-enum)
6. [Struktury (struct)](#6-struktury-struct)
7. [SOLID Principles](#7-solid-principles)
8. [Wyjątki](#8-wyjatki)

---

## 1. Hermetyzacja i enkapsulacja

### Filozofia
Hermetyzacja to ukrywanie szczegółów implementacji i udostępnianie tylko tego co niezbędne. Obiekt sam zarządza swoim stanem — zewnętrzny kod nie może go zepsuć.

```csharp
// ŹLE — brak hermetyzacji
public class ZleKonto {
    public decimal Saldo;       // każdy może wpisać co chce
}
konto.Saldo = -999999;          // nikt tego nie pilnuje!

// DOBRZE — hermetyzacja chroni stan
public class DobreKonto {
    private decimal _saldo;
    public decimal Saldo => _saldo;    // tylko odczyt

    public void Wplac(decimal kwota) {
        if (kwota <= 0) throw new ArgumentException("Kwota > 0");
        _saldo += kwota;
    }
}
```

### Modyfikatory dostępu — pełna lista
| Modyfikator | Dostęp |
|---|---|
| `public` | wszędzie (wewnątrz + inne assembly) |
| `private` | TYLKO ta klasa (domyślny dla pól/metod) |
| `protected` | ta klasa + klasy pochodne |
| `internal` | to assembly (.dll/.exe) |
| `protected internal` | protected LUB internal |
| `private protected` | pochodne TYLKO w tym assembly (C# 7.2+) |

Zasada najmniejszych uprawnień: zawsze zacznij od `private`, rozszerzaj tylko gdy faktycznie potrzeba.

**Pułapka `protected`:** pochodna MA dostęp do `protected` SWOJEJ instancji, ale NIE do `protected` innej instancji klasy bazowej przez referencję bazowej.

### Właściwości (Properties)
```csharp
public class Temperatura {
    private double _celsjusz;

    // Klasyczna property z walidacją
    public double Celsjusz {
        get => _celsjusz;
        set {
            if (value < -273.15) throw new ArgumentOutOfRangeException(...);
            _celsjusz = value;
        }
    }

    // Computed property — brak własnego pola
    public double Fahrenheit {
        get => _celsjusz * 9 / 5 + 32;
        set => Celsjusz = (value - 32) * 5 / 9;
    }

    // Auto-property z private set
    public DateTime OstatniaZmiana { get; private set; } = DateTime.Now;
}
```

**Rodzaje właściwości:**
- **Auto-property**: `public string Imie { get; set; }` — kompilator generuje pole
- **Init-only (C# 9+)**: `public string Imie { get; init; }` — można ustawiać tylko w konstruktorze lub object initializer
- **Required (C# 11+)**: `public required string Imie { get; init; }` — musi być ustawione przy inicjalizacji
- **Computed**: wyliczana na bieżąco, brak pola
- **Lazy (`??=`)**: `_cache ??= WyliczKosztownie();`

### INotifyPropertyChanged — hermetyzacja z powiadomieniami
Fundamentalny wzorzec w WPF, MAUI, MVVM.
```csharp
public class ObserwowanaOsoba : INotifyPropertyChanged {
    private string _imie = "";
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? nazwa = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nazwa));

    public string Imie {
        get => _imie;
        set {
            if (_imie == value) return;   // bez zmiany — nie powiadamiaj
            _imie = value;
            OnPropertyChanged();           // automatycznie "Imie"
        }
    }

    public string KategoriaWiekowa => _wiek switch { < 18 => "Niepełnoletni", ... };
    // KategoriaWiekowa zależy od Wiek — powiadamiamy:
    // OnPropertyChanged(nameof(KategoriaWiekowa));
}
```

### Hermetyzacja kolekcji
```csharp
// ŹLE
public List<string> Czlonkowie = new();  // każdy może Clear(), Add(), podstawić null

// DOBRZE — IReadOnlyList
private readonly List<string> _czlonkowie = new();
public IReadOnlyList<string> Czlonkowie => _czlonkowie;

// Kontrolowane metody modyfikacji
public void DodajCzlonka(string imie) {
    if (string.IsNullOrWhiteSpace(imie)) throw new ArgumentException(...);
    if (_czlonkowie.Contains(imie)) throw new InvalidOperationException(...);
    _czlonkowie.Add(imie);
}
```
- `IReadOnlyList<T>` — brak `Add`, `Clear`, `Remove`
- `.AsReadOnly()` — wrapper
- kopia `new List<T>(_czlonkowie)` — pełna izolacja (kosztowne)

### Pola — const vs static readonly
```csharp
public const int MAX = 100;                          // wkompilowana w IL
public static readonly DateTime Uruchomienie = DateTime.Now;  // inicjowana raz w runtime
```
- `const` — znana w czasie kompilacji, `static readonly` — znana w runtime
- `const` jest niejawnie `static`
- Pola instancji: konwencja `_camelCase`
- `Interlocked.Increment(ref _licznik)` — thread-safe inkrementacja

### Konstruktory
```csharp
// Delegowanie (:this) — unikaj duplikacji
public Produkt(string nazwa) : this(nazwa, 0m, "PLN") { }
public Produkt(string nazwa, decimal cena) : this(nazwa, cena, "PLN") { }
public Produkt(string nazwa, decimal cena, string waluta) { ... }

// Static factory method — lepsza czytelność i walidacja
public static Produkt UtworzWEuro(string nazwa, decimal cenaEuro)
    => new(nazwa, cenaEuro, "EUR");

// Object initializer — tylko dla właściwości z set/init
var zamowienie = new Zamowienie { Id = "ZAM-001", Kwota = 299.99m };

// Primary constructor (C# 12) — parametry widoczne w całej klasie
internal class Punkt(double x, double y) {
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Odleglosc => Math.Sqrt(X * X + Y * Y);
}
```

### Singleton z Lazy\<T\>
```csharp
public sealed class Konfiguracja {
    private static readonly Lazy<Konfiguracja> _instancja =
        new(() => new Konfiguracja());
    private Konfiguracja() { }       // prywatny konstruktor
    public static Konfiguracja Instancja => _instancja.Value;
}
// Lazy<T>: thread-safe, inicjalizuje tylko raz przy pierwszym użyciu
```

### Builder Pattern
```csharp
public class Email {
    private Email() { }    // prywatny — tylko przez Builder

    public class Builder {
        private string _nadawca = "";
        // ...
        public Builder OdNadawcy(string email) { _nadawca = email; return this; }  // fluent
        public Email Build() {
            if (string.IsNullOrEmpty(_nadawca)) throw new InvalidOperationException("...");
            return new Email { Nadawca = _nadawca, ... };
        }
    }
}
var email = new Email.Builder()
    .OdNadawcy("a@b.pl")
    .DoOdbiorcy("x@y.pl")
    .Build();
```

### IDisposable — zarządzanie zasobami
Pełny wzorzec (Dispose pattern):
```csharp
public class MojZasob : IDisposable {
    private bool _disposed;

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);  // nie uruchamiaj finalizera — już posprzątano
    }

    protected virtual void Dispose(bool disposing) {
        if (_disposed) return;
        if (disposing) { /* zwolnij managed resources */ }
        /* zwolnij unmanaged (IntPtr, Win32 handles) */
        _disposed = true;
    }

    ~MojZasob() => Dispose(false);  // backup jeśli zapomnisz using
}
```

**Rekrutacyjne:** `true` = managed (inne IDisposable), `false` = unmanaged (IntPtr, uchwyty Win32). Finalizer jako backup gdy zapomnisz `using`.

### Lazy initialization
```csharp
private readonly Lazy<List<string>> _dane =
    new(() => WczytajDaneZPliku());  // thread-safe, inicjalizuje tylko raz
public bool DaneSaZaladowane => _dane.IsValueCreated;
```

---

## 2. Dziedziczenie

### Podstawy
```csharp
public class Zwierze {
    public string Imie { get; }
    public Zwierze(string imie) => Imie = imie;
    public virtual void WydajDzwiek() => Console.WriteLine($"{Imie} wydaje dźwięk");
    public override string ToString() => $"{GetType().Name}: {Imie}";
}

public class Pies : Zwierze {
    public string Rasa { get; }
    public Pies(string imie, string rasa) : base(imie) => Rasa = rasa;
    public override void WydajDzwiek() => Console.WriteLine($"{Imie} szczeka!");
}
```

- Klasa pochodna dziedziczy wszystko co `public` i `protected`
- `base(...)` wywołuje konstruktor klasy bazowej
- `override` nadpisuje metodę wirtualną
- `base.Metoda()` wywołuje bazową wersję z klasy pochodnej

### virtual, override, new — kluczowa różnica
```csharp
Figura f = new Kolo(5);
Console.WriteLine(f.PolePowierzchni()); // Kolo.PolePowierzchni — virtual+override = polimorfizm
Console.WriteLine(f.Obwod());           // Figura.Obwod!        — new NIE jest polimorficzne

Kolo k = new Kolo(5);
Console.WriteLine(k.Obwod());           // Kolo.Obwod — zmienna jest typu Kolo
```

**`new` = ukrywa metodę bazową bez polimorfizmu. Wersja wywołana zależy od TYPU ZMIENNEJ, nie obiektu.**

`virtual+override` = polimorfizm — typ OBIEKTU decyduje o wywołaniu.

### Klasy i metody abstrakcyjne
```csharp
public abstract class Ksztalt {
    public abstract double Pole();    // brak implementacji — MUSI być nadpisane
    public abstract double Obwod();

    public virtual void Rysuj() =>   // virtual — można ale nie trzeba nadpisywać
        Console.WriteLine($"Rysuję {GetType().Name}");

    public void WyswietlInfo() =>    // konkretna — dziedziczona bez zmian
        Console.WriteLine($"pole={Pole():F2}");
}

// var k = new Ksztalt();  // BŁĄD! Nie można tworzyć abstract
```

### sealed — zapieczętowanie
```csharp
// sealed class — brak dziedziczenia
public sealed class Singleton { private Singleton() { } ... }

// sealed override — blokuje dalsze nadpisywanie w łańcuchu
public class Posrednia : Bazowa {
    public sealed override void Metoda() => Console.WriteLine("koniec nadpisywania");
}
// public class Pochodna : Posrednia { override void Metoda() { } } // BŁĄD!
```

**Wydajność:** JIT może de-virtualizować wywołania `sealed` metod (~3ns → ~1ns).

### Kolejność konstruktorów — inicjalizacja hierarchii
Kolejność tworzenia obiektu:
1. Inicjalizatory pól klasy **pochodnej**
2. Inicjalizatory pól klasy **bazowej**
3. Konstruktor klasy **bazowej**
4. Konstruktor klasy **pochodnej**

**Pułapka: wywołanie virtual w konstruktorze!**
```csharp
public class Baza {
    public Baza() { Metoda(); }      // wywołuje override z pochodnej...
    public virtual void Metoda() { }
}
public class Pochodna : Baza {
    private string _pole = "wartość";
    public override void Metoda() => Console.WriteLine(_pole); // PROBLEM: może być null!
}
// Konstruktor Baza() wywołuje Pochodna.Metoda() ZANIM pole się zainicjuje!
```

### Upcasting i Downcasting
```csharp
// Upcasting — niejawny, zawsze bezpieczny
Zwierze z = new Pies("Rex", "Lab");   // Pies IS-A Zwierze

// Downcasting — jawny, może rzucić InvalidCastException
Pies p = (Pies)z;                      // rzuca gdy z nie jest Pies
Pies? p2 = z as Pies;                  // null gdy z nie jest Pies
if (z is Pies pies) { pies.Aportuj(); }  // pattern matching (C# 7+) — preferred!

// Property pattern
if (z is Kot { CzyDomowy: true } kot) { ... }
```

### GetType() vs typeof() vs is
```csharp
Zwierze z = new Pies("Rex");

z.GetType() == typeof(Pies)     // True  — dokładne dopasowanie (runtime)
z.GetType() == typeof(Zwierze)  // False — typ dokładny to Pies!
z is Pies                       // True  — hierarchia (IS-A, włącznie z podklasami)
z is Zwierze                    // True  — Pies jest Zwierzę
typeof(Zwierze).IsAbstract      // false — wartość w compile-time
```

| | `GetType() == typeof(X)` | `is X` |
|---|---|---|
| Mechanizm | Runtime, dokładny typ | Hierarchia (IS-A) |
| Podklasy | NIE pasuje | Pasuje |
| Kiedy | Gdy chcesz dokładny typ | Gdy sprawdzasz przynależność |

### Kompozycja vs dziedziczenie
```csharp
// ŹLE — Stack JEST List? Stack MA List!
public class ZlyStack : List<int> { ... }  // eksponuje Add, Remove, Insert!

// DOBRZE — kompozycja HAS-A
public class DobryStack {
    private readonly List<int> _lista = new();  // HAS-A
    public void Push(int item) => _lista.Add(item);
    public int Pop() { ... }
    public int Peek() { ... }
    public int Count => _lista.Count;
    // Eksponujesz TYLKO to co Stack powinien mieć
}
```

**Zasada:** preferuj kompozycję nad dziedziczenie. Dziedzicz gdy IS-A + polimorfizm. Komponuj gdy HAS-A + kontrola API.

### Template Method Pattern
```csharp
public abstract class ProcesatorDanych {
    // Szkielet algorytmu — niezmienny (non-virtual)
    public void Przetworz(string[] dane) {
        var wczytane    = WczytajDane(dane);     // abstract step
        var zwal        = WalidujDane(wczytane); // virtual z domyślną
        var wynik       = TransformujDane(zwal); // abstract step
        ZapiszWynik(wynik);                      // abstract step
    }
    protected abstract List<string> WczytajDane(string[] dane);
    protected abstract List<string> TransformujDane(List<string> dane);
    protected abstract void ZapiszWynik(List<string> wynik);
    protected virtual List<string> WalidujDane(List<string> dane)
        => dane.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
}
```

Klasy pochodne implementują kroki, baza kontroluje przepływ.

---

## 3. Interfejsy i klasy abstrakcyjne

### Podstawy interfejsu
```csharp
public interface IZwierze {
    string Imie { get; }
    void WydajDzwiek();             // publiczne, abstrakcyjne — domyślnie
    bool CzyLubiLudzi { get; }
}

// Klasa może implementować wiele interfejsów
public class Pies : IZwierze, IDomowy, ITresowalny { ... }
```

Interfejsy definiują **kontrakt (co)**, nie **implementację (jak)**.

### Explicit interface implementation
```csharp
// Konflikt nazw — dwa interfejsy z metodą Serializuj()
public class Dokument : ISerializowalny, IPersistable {
    string ISerializowalny.Serializuj() => "{ \"json\": true }";  // JSON
    string IPersistable.Serializuj()    => "<root/>";              // XML
    // Dostęp: tylko przez cast do odpowiedniego interfejsu!
}

var doc = new Dokument();
// doc.Serializuj()  — BŁĄD! Niejednoznaczność
((ISerializowalny)doc).Serializuj()  // OK — JSON
```

### Kluczowe interfejsy .NET
```csharp
// IComparable<T> — naturalna kolejność
public class Student : IComparable<Student> {
    public int CompareTo(Student? other) => Srednia.CompareTo(other?.Srednia);
}
studenci.Sort();  // używa CompareTo

// IComparer<T> — zewnętrzna logika sortowania
public class PoImieniu : IComparer<Student> {
    public int Compare(Student? x, Student? y)
        => string.Compare(x?.Imie, y?.Imie, StringComparison.Ordinal);
}

// IEquatable<T> — wartościowa równość
public class Punkt : IEquatable<Punkt> {
    public bool Equals(Punkt? other) => other != null && X == other.X && Y == other.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    // Ważne: override GetHashCode gdy override Equals!
}

// IEnumerable<T> — umożliwia foreach i LINQ
public class ZakresLiczb : IEnumerable<int> {
    public IEnumerator<int> GetEnumerator() {
        for (int i = Od; i <= Do; i += Krok)
            yield return i;  // generator — lazy evaluation
    }
}
```

### Default interface methods (C# 8+)
```csharp
public interface ILogger {
    void Log(string msg);   // abstract — musi być implementowane

    // Default implementation — nie wymaga implementacji w klasie
    void LogInfo(string msg) => Log($"[INFO] {msg}");
    void LogError(string msg) => Log($"[ERROR] {msg}");

    // Static member w interfejsie (C# 8+)
    static string FormatujPoziom(string level) => $"[{level,-7}]";
}
```

**Pułapka:** domyślna metoda dostępna TYLKO przez referencję interfejsu, nie przez referencję klasy:
```csharp
var logger = new MojLogger();
// logger.LogInfo("test");    // BŁĄD — nie widoczne przez klasę!
((ILogger)logger).LogInfo("test");  // OK — przez interfejs
```

### Interfejs vs klasa abstrakcyjna
| Cecha | Interfejs | Klasa abstrakcyjna |
|---|---|---|
| Pola instancyjne | ❌ Nie | ✅ Tak |
| Konstruktory | ❌ Nie | ✅ Tak |
| Dziedziczenie | Wiele interfejsów | Tylko jedna klasa |
| Domyślna impl | ✅ C# 8+ | ✅ Zawsze |
| Modyfikatory | public tylko | Dowolne |
| Semantyka | CAN-DO (zdolność) | IS-A (typ) |

**Złoty wzorzec:** interfejs (kontrakt dla DI) + abstract class (wspólna impl):
```csharp
public interface ILogger { void Log(string msg); }
public abstract class LoggerBase : ILogger {
    public abstract void Log(string msg);    // kontrakt
    public void LogInfo(string msg) => Log($"[INFO] {msg}");  // wspólna implementacja
}
```

### Static abstract members w interfejsach (C# 11+)
```csharp
public interface IKontener<TSelf> where TSelf : IKontener<TSelf> {
    static abstract TSelf Stworz();
}
// Umożliwia Factory Method przez interfejs z constraint
```

### Strategy Pattern przez interfejsy
```csharp
public interface IStrategiaWysylki {
    decimal ObliczKoszt(decimal waga, string kraj);
    string NazwaFirmy { get; }
    string CzasDostawy { get; }
}

public class InPost : IStrategiaWysylki { ... }
public class DHL : IStrategiaWysylki { ... }

// Zmiana strategii w RUNTIME bez modyfikacji kodu klienta
public class Zamowienie {
    private IStrategiaWysylki _wysylka = new InPost();
    public void UstawWysylke(IStrategiaWysylki s) => _wysylka = s;
}
```

---

## 4. Polimorfizm

### Vtable — jak CLR implementuje polimorfizm
```
vtable A: [slot0]=A.M1, [slot1]=A.M2
vtable B (override M1, new M2): [slot0]=B.M1, [slot1]=A.M2, [slot2]=B.M2(new), [slot3]=B.M4
vtable C (override M1, M4):     [slot0]=C.M1, [slot1]=A.M2, [slot2]=B.M2, [slot3]=C.M4
```

- `override` zastępuje istniejący slot w vtable
- `new` tworzy NOWY slot — stary wskazuje na bazową gdy wywołujesz przez starszy typ referencji

```csharp
VtableA refA = new VtableC();
refA.M1();  // C.M1 — slot 0 → C.M1 (override propaguje)
refA.M2();  // A.M2 — slot 1 → A.M2 (new w B nie nadpisał tego slotu!)

VtableB refB = new VtableC();
refB.M2();  // B.M2 — slot 2 → B.M2 (przez B ref mamy dostęp do nowego slotu)
```

**Kluczowe:** typ OBIEKTU (nie referencji) decyduje o wywołaniu virtual!

### Strategy Pattern przez polimorfizm
```csharp
// OCP — nowa strategia = nowa klasa, zero zmian w Koszyk
koszyk.UstawStrategieRabatu(new RabatProcentowy(15));    // -15%
koszyk.UstawStrategieRabatu(new RabatProgowy(500, 10));  // -10% przy ≥500zł
koszyk.UstawStrategieRabatu(new StrategiePolaczone(      // kompozycja
    new RabatProcentowy(10),
    new RabatStaly(50)));
```

Strategię można zmieniać w runtime — kluczowa cecha wzorca.

### Pułapki polimorfizmu

**Naruszenie symetrii Equals:**
```csharp
// Punkt2D nie sprawdza Z, Punkt3D sprawdza Z
p2d.Equals(p3d) // True
p3d.Equals(p2d) // False — NARUSZENIE a.Equals(b) == b.Equals(a)!
// Psuje Dictionary i HashSet!
// Rozwiązanie: sealed klasy LUB sprawdzaj GetType() == other.GetType()
```

**Naruszenie LSP (Liskov):**
```csharp
// PochodzacaZapis rzuca dla pewnych argumentów — czego bazowa nie robi
public override void Zapisz(string dane) {
    if (dane.Contains("błąd")) throw new NotSupportedException(...);
}
// Kod działający z BazaZapis może się posypać z PochodzacaZapis!
```

**Kod zapachowy — rzutowanie w metodzie:**
```csharp
// Każdy nowy typ = modyfikacja tej metody = naruszenie OCP!
if (zwierze is Pies p) p.Aportuj();
else if (zwierze is Kot k) k.Mruczenie();
// Rozwiązanie: dodaj WykonajTypoweZachowanie() do interfejsu/klasy bazowej
```

### Ad-hoc polimorfizm — przeciążanie
```csharp
static int    Dodaj(int a, int b)       => a + b;
static double Dodaj(double a, double b) => a + b;
static string Dodaj(string a, string b) => a + b;
// Rozwiązywane w CZASIE KOMPILACJI — to nie jest polimorfizm runtime!
```

---

## 5. Typy wyliczeniowe (enum)

### Podstawy
```csharp
public enum DzienTygodnia { Poniedzialek, Wtorek, Sroda, Czwartek, Piatek, Sobota, Niedziela }

// Niestandardowe wartości i typ bazowy
public enum KodHTTP : int { OK = 200, NotFound = 404, InternalServerError = 500 }

DzienTygodnia d = DzienTygodnia.Sroda;
int i = (int)d;                      // 2
DzienTygodnia d2 = (DzienTygodnia)2; // Sroda
```

### [Flags] — bitowe kombinacje
```csharp
[Flags]
public enum Uprawnienia {
    Brak   = 0,
    Odczyt  = 1,   // 1 << 0
    Zapis   = 2,   // 1 << 1
    Usuwanie = 4,  // 1 << 2
    Admin  = Odczyt | Zapis | Usuwanie
}

var uprawnienia = Uprawnienia.Odczyt | Uprawnienia.Zapis;
uprawnienia.HasFlag(Uprawnienia.Zapis)    // True
uprawnienia |= Uprawnienia.Usuwanie      // dodaj
uprawnienia &= ~Uprawnienia.Odczyt       // usuń
uprawnienia ^= Uprawnienia.Usuwanie      // toggle
```

**Wartości MUSZĄ być potęgami 2:** 1, 2, 4, 8, 16... — każda zajmuje jeden bit.

### Enum API
```csharp
Enum.GetValues<DzienTygodnia>()       // wszystkie wartości
Enum.GetNames<DzienTygodnia>()        // wszystkie nazwy jako string[]
Enum.Parse<DzienTygodnia>("Sroda")    // string → enum (rzuca gdy nie znany)
Enum.TryParse<DzienTygodnia>("Sroda", out var d) // bezpieczna wersja
Enum.IsDefined<DzienTygodnia>(999)    // false — nieznana wartość
Enum.GetName(typeof(DzienTygodnia), 2) // "Sroda"
```

### Extension methods dla enum
```csharp
public static class StatusZamowieniaExtensions {
    public static string Opis(this StatusZamowienia status) => status switch {
        StatusZamowienia.Nowe       => "Nowe zamówienie",
        StatusZamowienia.WTrakcie   => "W trakcie realizacji",
        _ => status.ToString()
    };
    public static bool CzyAktywne(this StatusZamowienia s)
        => s is StatusZamowienia.WTrakcie or StatusZamowienia.Wyslane;
    public static IEnumerable<StatusZamowienia> NastepneStatusy(this StatusZamowienia s)
        => _przejscia.TryGetValue(s, out var next) ? next : Enumerable.Empty<StatusZamowienia>();
}
```

### State machine z enum
```csharp
public class SygnalizacjaSwietlna {
    private StanSwiatla _stan = StanSwiatla.Czerwone;
    private readonly Dictionary<StanSwiatla, StanSwiatla> _przejscia = new() {
        [StanSwiatla.Czerwone] = StanSwiatla.Zielone,
        [StanSwiatla.Zielone]  = StanSwiatla.Zolte,
        [StanSwiatla.Zolte]    = StanSwiatla.Czerwone
    };
    public void Zmien() => _stan = _przejscia[_stan];
}
```

---

## 6. Struktury (struct)

### Struct vs class — kluczowe różnice
| Cecha | struct | class |
|---|---|---|
| Pamięć | stos (lub inline w heap) | sterta (heap) |
| Semantyka | wartościowa — kopia | referencji — ten sam obiekt |
| Domyślny = | wszystkie pola = 0/null | null |
| Dziedziczenie | ❌ (tylko interfejsy) | ✅ |
| Finalizer | ❌ | ✅ |
| Wydajność | brak GC pressure | GC przy short-lived |

```csharp
WalutaStruct a = new WalutaStruct(100m, "PLN");
WalutaStruct b = a;     // KOPIA — a i b niezależne
b.Kwota = 200;           // zmiana b nie wpływa na a

KlasaRef r1 = new KlasaRef();
KlasaRef r2 = r1;        // ta sama referencja — r1 == r2!
```

**Kiedy używać struct:**
- ✅ Mały rozmiar (≤ 16-24 bajtów)
- ✅ Immutable — `readonly struct`
- ✅ Value semantics — kopia = niezależna wartość
- ✅ Krótki czas życia, dużo instancji w tablicy
- ❌ > 24 bajtów, mutowalny, polimorfizm, dziedziczenie

### readonly struct — zero kopiowania
```csharp
public readonly struct Punkt3D {
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    // readonly struct: kompilator zapewnia immutability
    // Wszystkie pola muszą być readonly!

    public double Dlugosc => Math.Sqrt(X*X + Y*Y + Z*Z);

    // operator in — przesyłanie przez referencję bez kopiowania!
    public static double Dot(in Punkt3D a, in Punkt3D b)
        => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
}

// in parameter — zero-copy, ale readonly (nie można modyfikować)
static void PrintPunkt(in Punkt3D p) => Console.WriteLine($"({p.X}, {p.Y}, {p.Z})");
```

### Span\<T\> i ReadOnlySpan\<T\> — zero-copy slices
```csharp
int[] tablica = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

// Span — widok na fragment, bez kopiowania!
Span<int> fragment = tablica.AsSpan(2, 5);  // indeksy 2-6
fragment[0] = 99;    // modyfikuje oryginalną tablicę!

// String operations — brak alokacji
ReadOnlySpan<char> span = "Hello, World!".AsSpan(7, 5);  // "World"

// stackalloc — alokacja na stosie (brak GC pressure)
Span<int> bufor = stackalloc int[8];
for (int i = 0; i < bufor.Length; i++) bufor[i] = i * i;
```

### Boxing i unboxing — koszty
```csharp
// Boxing — kopiowanie value type do heap
object o = 42;          // boxing int → alokacja na heap, kopia wartości

// Unboxing — wyciągnięcie z heap z weryfikacją typu
int i = (int)o;         // unboxing — kosztowne!

// Kiedy boxing następuje (często nieświadomie):
object o2 = 42;                   // przypisanie do object
IComparable c = 42;               // interfejs bez generic constraint
ArrayList lista = new();
lista.Add(42);                    // pre-generic kolekcja

// ArrayList (boxing) vs List<int> (brak boxing) — ~7x wolniej
```

**Specjalna reguła Nullable<T> boxing:**
```csharp
int? hasValue = 42;
object boxed = hasValue;          // boxing int? → boxed INT (nie Nullable!)
int? noValue = null;
object? boxedNull = noValue;      // boxing int? null → null (nie Nullable<int>!)
```

### Nullable\<T\> — logika trójwartościowa
```csharp
int? a = 5, b = null;
int? suma = a + b;     // null — "lifted operators" — jeden null = cały wynik null
bool? r = a > 3;       // true — bool? trójwartościowa: true, false, null
bool? r2 = b > 3;      // null — nie wiemy
bool? r3 = b == null;  // true — null == null

// ?. i ?? — bezpieczne operacje
int wynik = a ?? 0;        // 5
int? len = b?.ToString().Length;  // null — bez NullReferenceException
```

### record struct (C# 10+)
```csharp
public record struct WektorRS(double X, double Y, double Z) {
    public double Dlugosc => Math.Sqrt(X*X + Y*Y + Z*Z);
}

var w1 = new WektorRS(1, 2, 3);
var w2 = new WektorRS(1, 2, 3);
w1 == w2             // True — value equality z record
var w4 = w1 with { Z = 10 };  // with expression — niedestruktywna mutacja

// readonly record struct — immutable value type
public readonly record struct KolorRGB(byte R, byte G, byte B) {
    public string HexKod => $"#{R:X2}{G:X2}{B:X2}";
}
```

---

## 7. SOLID Principles

### S — Single Responsibility Principle
**"Klasa powinna mieć tylko jeden powód do zmiany."**

```csharp
// ŹLE — jeden powód: zmiana walidacji, hashowania, bazy, emaila, logów = 5 powodów
public class ZlyZarzadzaczUzytkownikow {
    public void Zarejestruj(string email, string haslo) {
        if (!email.Contains("@")) throw new Exception("Zły email");
        string hash = Convert.ToBase64String(SHA256.HashData(...));
        Console.WriteLine($"INSERT INTO Users VALUES ('{email}', '{hash}')");
        Console.WriteLine($"SMTP: Wysyłam powitanie do {email}");
        Console.WriteLine($"[LOG] Zarejestrowano: {email}");
    }
}

// DOBRZE — każda klasa ma jeden powód do zmiany
public class WalidatorUzytkownika { public void Waliduj(string email, string haslo) { ... } }
public class HashowanieSerwis { public string Hashuj(string haslo) => ...; }
public interface IRepozytoriumUzytkownikow { void Dodaj(string email, string hash); }
public interface ISerwisEmail { Task WyslijPowitanieAsync(string email); }
public class RegistrationService {  // orchestrator
    public async Task ZarejestrujAsync(string email, string haslo) {
        _walidator.Waliduj(email, haslo);
        string hash = _hashing.Hashuj(haslo);
        _repo.Dodaj(email, hash);
        await _email.WyslijPowitanieAsync(email);
    }
}
```

**Test SRP:** ile powodów do zmiany ma klasa? > 1 → naruszasz SRP.
**Symptomy:** metody z setkami linii, dziesiątki metod w klasie, trudność testowania (trzeba mockować 5 rzeczy).

### O — Open/Closed Principle
**"Klasa powinna być otwarta na rozszerzenia, zamknięta na modyfikacje."**

```csharp
// ŹLE — każdy nowy format = modyfikacja istniejącej klasy
public string Generuj(object dane, string format) {
    if (format == "PDF") return ...;
    else if (format == "CSV") return ...;
    else if (format == "JSON") return ...;  // modyfikacja klasy!
}

// DOBRZE — nowy format = nowa klasa
public interface IFormatterRaportow { string Format { get; }; string Formatuj(object dane); }
public class PdfFormatter : IFormatterRaportow { ... }
public class CsvFormatter : IFormatterRaportow { ... }
public class XmlFormatter : IFormatterRaportow { ... }  // nowy — zero zmian!

public class GeneratorRaportow {
    private readonly Dictionary<string, IFormatterRaportow> _formattery;
    public GeneratorRaportow(IEnumerable<IFormatterRaportow> formattery)
        => _formattery = formattery.ToDictionary(f => f.Format);
}
```

**OCP nie zabrania naprawy bugów** — zabrania modyfikacji działającego kodu żeby dodać nową funkcjonalność. Bug fix = OK.

### L — Liskov Substitution Principle
**"Obiekty klas pochodnych muszą być wymienne z obiektami klas bazowych bez zepsucia programu."**

```csharp
// ŹLE — klasyczny błąd: Prostokąt vs Kwadrat
public class ZlyKwadrat : ZlyProstokat {
    // Ustawia OBE wymiary — łamie kontrakt Prostokąta!
    public override int Szerokosc { set { base.Szerokosc = value; base.Wysokosc = value; } }
}
// p.Szerokosc = 4; p.Wysokosc = 5; → p.Pole == 20  FAŁSZ dla Kwadratu!

// DOBRZE — osobne klasy
public abstract class Ksztalt2D { public abstract int Pole { get; } }
public class Prostokat : Ksztalt2D { public override int Pole => Szerokosc * Wysokosc; }
public class Kwadrat : Ksztalt2D { public override int Pole => Bok * Bok; }
```

**Zasada LSP — klasa pochodna:**
- ✓ Nie wzmacnia warunków wstępnych (preconditions)
- ✓ Nie osłabia warunków końcowych (postconditions)
- ✓ Nie rzuca nowych wyjątków których bazowa nie rzuca
- ✓ Zachowuje niezmienniki (invariants) klasy bazowej

**Test:** jeśli test przechodzi dla bazowej ale nie dla pochodnej → naruszasz LSP.

### I — Interface Segregation Principle
**"Klasy nie powinny być zmuszane do implementowania interfejsów których nie używają."**

```csharp
// ŹLE — gruby interfejs
public interface IZlyPracownik {
    void Pracuj(); void Jedz(); void Spi();
    void Zarzadzaj();       // nie każdy zarządza!
    void Programuje();      // nie każdy programuje!
    void SprzedajeKlientom(); // nie każdy ma kontakt z klientem!
}
public class ZlyProgramista : IZlyPracownik {
    public void Zarzadzaj() => throw new NotImplementedException();  // wymuszony!
}

// DOBRZE — małe, spójne interfejsy
public interface IPracownik    { string Imie { get; }; void Pracuj(); }
public interface IZarzadzajacy { void ProwadzSpotkanie(); void OceniPracownika(...); }
public interface IProgramista  { void Programuje(string jezyk); void RobisCodeReview(string kod); }

public class Programista : IPracownik, IProgramista { ... }       // tylko to czego potrzebuje
public class TeamLead : IPracownik, IProgramista, IZarzadzajacy { ... }
public class Handlowiec : IPracownik, IKontaktZKlientem { ... }
```

### D — Dependency Inversion Principle
**"Moduły wysokiego poziomu nie powinny zależeć od modułów niskiego poziomu. Oba powinny zależeć od abstrakcji."**

```csharp
// ŹLE — tight coupling
public class ZlySerwisZamowien {
    private readonly SqlServerBaza _baza = new SqlServerBaza();      // konkretna implementacja!
    private readonly SmtpEmailSerwis _email = new SmtpEmailSerwis();
    // Nie można przetestować bez prawdziwej bazy/emaila!
}

// DOBRZE — zależności od interfejsów, wstrzykiwane z zewnątrz
public class SerwisZamowien {
    private readonly IRepozytoriumZamowien _repo;
    private readonly ISerwisEmailowy _email;
    private readonly ILogger _logger;

    // Dependency Injection przez konstruktor — "odwrócenie kontroli"
    public SerwisZamowien(IRepozytoriumZamowien repo, ISerwisEmailowy email, ILogger logger)
        => (_repo, _email, _logger) = (repo, email, logger);
}

// Composition Root — jedyne miejsce które zna konkretne implementacje
var serwis = new SerwisZamowien(
    repo:   new SqlRepozytoriumZamowien("Server=..."),
    email:  new SendGridSerwis("sg-key"),
    logger: new ConsoleLogger());

// Na potrzeby testów — faki bez frameworku
var testSerwis = new SerwisZamowien(
    repo:   new InMemoryRepo(),     // prosty fake
    email:  new FakeEmail(),        // sprawdzamy wywołania
    logger: new ConsoleLogger());
```

**DIP vs DI vs IoC:**
- **DIP** = zasada (abstrakcje zamiast konkretów)
- **DI** = wzorzec implementujący DIP (wstrzykujesz z zewnątrz przez konstruktor)
- **IoC Container** (np. wbudowany w ASP.NET Core) = framework automatyzujący DI

### Podsumowanie SOLID
```
S — Single Responsibility  → jedna klasa, jeden powód do zmiany
O — Open/Closed            → rozszerzaj przez nowe klasy, nie modyfikuj starych
L — Liskov Substitution    → klasa pochodna zastępuje bazową bez niespodzianek
I — Interface Segregation  → małe interfejsy zamiast jednego grubego
D — Dependency Inversion   → zależności od interfejsów, wstrzykiwane z zewnątrz
```

---

## 8. Wyjątki

### try/catch/finally — podstawy
```csharp
try { /* kod */ }
catch (ArgumentNullException ex)   // najpierw najbardziej szczegółowy!
{
    Console.WriteLine(ex.ParamName);
}
catch (ArgumentException ex)       // potem bardziej ogólny
{ ... }
catch (Exception ex)               // na końcu najbardziej ogólny
{
    Console.WriteLine(ex.Message);
    throw;  // re-throw — zachowuje oryginalny stack trace!
}
finally
{
    // Wykonuje się ZAWSZE — po try, po catch, gdy żaden catch nie pasuje
    // NIE wykonuje się: Environment.FailFast(), StackOverflow, zabicie procesu
}
```

**finally a return:** `finally` wykonuje się PRZED faktycznym powrotem z metody.

**Pułapka:** wyjątek rzucony w `finally` zastępuje wyjątek z `catch` — oryginał jest UTRACONY!

**Zła kolejność** → kompilator: `CS0160` (martwy kod):
```csharp
catch (Exception ex) { }        // za ogólny na początku
catch (ArgumentNullException ex) { }  // MARTWY KOD — nigdy nie osiągnięty!
```

### throw vs throw ex
```csharp
// ŹLE — throw ex: resetuje stack trace od tej linii
catch (Exception ex) { LogujBlad(ex); throw ex; }

// DOBRZE — throw: zachowuje oryginalny stack trace
catch (Exception ex) { LogujBlad(ex); throw; }

// Owijanie z zachowaniem kontekstu — ZAWSZE przekaż InnerException!
catch (FileNotFoundException ex) {
    throw new KonfiguracjaException($"Brak pliku: {plik}", innerException: ex);
}
```

### ExceptionDispatchInfo — re-throw z innego miejsca
```csharp
// Gdy łapiesz wyjątek w jednym miejscu a chcesz rzucić w innym (np. inny wątek)
ExceptionDispatchInfo? przechwycony = null;
try { PobierzDane(1); }
catch (Exception ex) { przechwycony = ExceptionDispatchInfo.Capture(ex); }

// ... gdzieś indziej, nawet w innym wątku ...
przechwycony?.Throw();  // rzuca z oryginalnym stack trace + adnotacją gdzie ponownie
```

### Exception filters (C# 6+) — when
```csharp
// Filtr ewaluowany w FAZIE 1 (search) — stack trace NIEROZWINIĘTY → lepsze diagnostyki!
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    { /* 404 — graceful handling */ }
catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError)
    { throw; }  // 5xx — retry wyżej

// Logowanie BEZ łapania — stos nierozwinięty w momencie sprawdzania filtra!
static bool ZalogujIZwrocFalse(Exception ex) {
    Console.WriteLine($"[DIAGNOZA] {ex.GetType().Name}: {ex.Message}");
    return false;  // nie obsługujemy — wyjątek idzie dalej
}
catch (Exception ex) when (ZalogujIZwrocFalse(ex)) { /* NIGDY nie wykona się */ }

// Retry z exponential backoff
int proba = 0;
catch (HttpRequestException ex) when (proba++ < maxProb && CzyMoznaRetry(ex)) {
    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, proba)));
}
```

**Filtr vs if w catch:**
- `catch ... when (warunek)` → faza 1 — stos zachowany
- `catch ... { if (warunek) }` → faza 2 — stos już rozwinięty

### Własne wyjątki — trzy konstruktory to STANDARD
```csharp
[Serializable]
public class ZamowienieException : Exception {
    // 1. wiadomość
    public ZamowienieException(string message) : base(message) { }
    // 2. wiadomość + przyczyna (ZAWSZE przekaż InnerException!)
    public ZamowienieException(string message, Exception inner) : base(message, inner) { }
    // 3. serializacja (legacy, ale nadal konwencja .NET)
    protected ZamowienieException(SerializationInfo info, StreamingContext ctx) : base(info, ctx) { }
}

// Wyjątek z kontekstem domenowym — nie tylko string message!
public class NiewystarczajacySaldoException : Exception {
    public string NumerKonta { get; }
    public decimal Saldo { get; }
    public decimal Kwota { get; }
    public decimal Brakujace => Kwota - Saldo;
}
```

### Hierarchia własnych wyjątków
```csharp
public abstract class AplikacjaException : Exception {
    public string KodBledu { get; }
    public bool CzyKrytyczny { get; }
}
public class BazaDanychException : AplikacjaException { public string? Zapytanie { get; } }
public class WalidacjaException : AplikacjaException { public IReadOnlyList<string> Bledy { get; } }

// Polimorfizm wyjątków — łapanie klasy bazowej obsługuje wszystkie:
catch (WalidacjaException ex) { foreach (var b in ex.Bledy) ... }
catch (BazaDanychException ex) { if (ex.CzyKrytyczny) WyslijAlert(); }
catch (AplikacjaException ex) { /* łapie WSZYSTKIE AplikacjaException */ }
```

**Kiedy NIE tworzyć własnych:** gdy standardowy wyjątek wystarczy (`ArgumentException`, `InvalidOperationException` itp.).

### Result\<T\> — alternatywa dla wyjątków
```csharp
// Oczekiwane błędy NIE powinny być wyjątkami
public readonly struct Result<T> {
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string err) => new(err);

    // Monadic bind — komponowanie bez throw/catch
    public Result<TNext> Then<TNext>(Func<T, Result<TNext>> nastepna)
        => IsSuccess ? nastepna(Value) : Result<TNext>.Fail(Error);

    // Dekonstrukcja
    public void Deconstruct(out bool sukces, out T? wartosc, out string? blad) { ... }
}

// Użycie
var result = PodzielBezpieczne(10, 3)
    .Then(w => FormatujWynik(w));        // komponowanie

var (sukces, wartosc, blad) = PodzielBezpieczne(10, 0);  // dekonstrukcja

// Result<T> vs wyjątki:
// Wyjątki → NIEOCZEKIWANE błędy (bug, OutOfMemory, sieć)
// Result<T> → OCZEKIWANE scenariusze (brak rekordu, walidacja)
```

### Circuit Breaker
```csharp
public class CircuitBreaker {
    // CLOSED → OPEN (po N błędach) → HALF-OPEN (po czasie restartu) → CLOSED
    private bool _otwarty = false;
    private int _bledyZRzedu = 0;

    public void Wykonaj(Action operacja) {
        if (_otwarty && DateTime.UtcNow - _czasOtwarcia < _czasRestartu)
            throw new InvalidOperationException("Circuit breaker OTWARTY");

        try { operacja(); _bledyZRzedu = 0; }
        catch {
            if (++_bledyZRzedu >= _prog) { _otwarty = true; _czasOtwarcia = DateTime.UtcNow; }
            throw;
        }
    }
}
// W produkcji: biblioteka Polly
```

### using i IDisposable
```csharp
// 'using' = cukier składniowy dla try/finally z Dispose()
using (var plik = new StreamReader("dane.txt"))     // statement — jawny zakres
using (var writer = new StreamWriter("wynik.txt"))  // łączenie bez {}
{ ... }  // Dispose() wywołany na writer, potem plik — ODWROTNA kolejność!

using var plik2 = new StreamReader("dane.txt");  // declaration (C# 8+) — zakres do końca bloku

// IAsyncDisposable (C# 8+)
await using var polaczenie = new AsyncPolaczenie();  // await DisposeAsync() przy końcu
```

**ObjectDisposedException:** zawsze sprawdzaj `_disposed` przed użyciem zasobu:
```csharp
ObjectDisposedException.ThrowIf(_disposed, nameof(MojZasob));
```

### Global exception handlers
```csharp
AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
    var ex = (Exception)args.ExceptionObject;
    Console.Error.WriteLine($"[KRYTYCZNY] {ex}");
    // Zapisz log, wyślij alert — aplikacja i tak się zakończy
};

TaskScheduler.UnobservedTaskException += (sender, args) => {
    Console.Error.WriteLine($"[TASK] {args.Exception}");
    args.SetObserved();  // zapobiega crash aplikacji
};
```

### Nullable Reference Types (NRT)
```csharp
// <Nullable>enable</Nullable> w .csproj — domyślnie w .NET 6+

string  nienullowalna = "hello";   // kompilator ZAKŁADA że nigdy null
string? nullowalna    = null;      // kompilator WIE że może być null

// Null state analysis — kompilator śledzi stan:
if (s != null) Console.WriteLine(s.Length);  // OK — null-state: not-null
int? len = s?.Length;                         // OK — null-conditional

// Null-forgiving ! — używaj RZADKO
string gwarancja = mozeNull!;    // 'zaufaj mi, nie jest null'

// Adnotacje dla własnych metod:
bool TryGetName(int id, [NotNullWhen(true)] out string? name) { ... }
// Kompilator wie że name != null gdy return true

[MemberNotNull(nameof(_polaczenie))]
void Inicjalizuj() { _polaczenie = "Server=localhost"; }
// Kompilator wie że _polaczenie != null po wywołaniu

// Generics:
T? MozeNull<T>(bool zwroc) where T : class => zwroc ? default(T) : null;
```

---

## Pliki w projekcie

| Plik | Klasa demo | Tematy |
|---|---|---|
| `Hermetyzacja.cs` | `Hermetyzacja` | Modyfikatory, pola, właściwości, konstruktory, Singleton, IDisposable, Builder |
| `Dziedziczenie.cs` | `Dziedziczenie` | Podstawy, abstract, virtual/new, sealed, kolejność ctor, casting, GetType/is, kompozycja, Template Method |
| `Interfejsy.cs` | `Interfejsy` | Implementacja, explicit impl, IComparable/IComparer/IEquatable/IEnumerable, domyślne metody, Interfejs vs Abstract, Strategy |
| `Polimorfizm.cs` | `Polimorfizm` | Vtable, Strategy Pattern, pułapki (Equals asymetria, LSP, code smell), ad-hoc |
| `EnumyIStruktury.cs` | `EnumyIStruktury` | Enum basics, [Flags], API, extensions, state machine, struct vs class, readonly struct, Span<T>, boxing, record struct |
| `SOLID.cs` | `SOLIDPrinciples` | SRP, OCP, LSP, ISP, DIP z pełnymi przykładami |
| `WyjatkiOOP.cs` | `WyjatkiOOP` | TryCatch, throw vs throw ex, filtry, własne wyjątki, Result\<T\>, Circuit Breaker, using, NRT |
| `Program.cs` | — | Wywołanie wszystkich metod demo |
