namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// DZIEDZICZENIE W C#
// C# = tylko POJEDYNCZE dziedziczenie klas + wiele interfejsów
// Hierarchia: klasa pochodna IS-A klasa bazowa
// ─────────────────────────────────────────────────────────────────────────────

public static class Dziedziczenie
{
    // PODSTAWY — Zwierze/Pies/Kot, upcasting, kolekcja polimorficzna
    public static void PodstawyDziedziczenia()
    {
        Console.WriteLine("\n=== PODSTAWY DZIEDZICZENIA ===");

        var z1 = new ZwierzeDz("Generyk", 1);
        var pies = new PiesDz("Rex", 3, "Labrador");
        var kot = new KotDz("Mruczek", 5, "Europejski");

        z1.Dzwiek();    // ZwierzeDz.Dzwiek() — virtual, domyślna
        pies.Dzwiek();  // PiesDz.Dzwiek() — override
        kot.Dzwiek();   // KotDz.Dzwiek()  — override

        Console.WriteLine($"\npies.ToString(): {pies}");
        Console.WriteLine($"pies.Rasa: {pies.Rasa}");

        // Upcasting — niejawny, zawsze bezpieczny
        ZwierzeDz zwierze = pies;  // PiesDz → ZwierzeDz
        zwierze.Dzwiek();           // POLIMORFIZM: wywołuje PiesDz.Dzwiek()!
        // zwierze.Rasa;            // BŁĄD — ZwierzeDz nie zna Rasa

        // Kolekcja polimorficzna — serce OOP
        Console.WriteLine("\nKolekcja polimorficzna:");
        var zwierzeta = new List<ZwierzeDz>
        {
            new PiesDz("Burek", 2, "Owczarek"),
            new KotDz("Luna", 3, "Perski"),
            new ZwierzeDz("Generyk", 1)
        };
        foreach (var z in zwierzeta)
            z.Dzwiek(); // każde wywołuje SWOJĄ implementację

        // Energía — protected field dostępny w klasach pochodnych
        pies.Jedz(50);
        Console.WriteLine($"Energia Rexa po jedzeniu: {pies.PokazEnergie()}");
    }

    // KLASY ABSTRAKCYJNE — abstract method musi być nadpisana
    public static void KlasyAbstrakcyjne()
    {
        Console.WriteLine("\n=== KLASY ABSTRAKCYJNE ===");

        // new KsztaltDz() — BŁĄD: nie można instancjonować abstract class!

        var ksztalty = new KsztaltDz[]
        {
            new KoloDz(5),
            new ProstokatDz(4, 6),
            new SzeSciokatDz(3)
        };

        double lacznePole = 0;
        foreach (var k in ksztalty)
        {
            k.Wyswietl(); // template method — implementacja w KsztaltDz
            lacznePole += k.Pole();
        }
        Console.WriteLine($"Łączne pole: {lacznePole:F2}");

        // Kolejność malejąco po polu — polimorfizm z LINQ
        var posortowane = ksztalty.OrderByDescending(k => k.Pole());
        Console.WriteLine("Posortowane malejąco po polu:");
        foreach (var k in posortowane)
            Console.WriteLine($"  {k.NazwaKsztaltu}: {k.Pole():F2}");
    }

    // VIRTUAL vs NEW — kluczowa różnica (vtable vs ukrycie)
    public static void VirtualVsNew()
    {
        Console.WriteLine("\n=== VIRTUAL/OVERRIDE vs NEW ===");

        var bazOvr = new BazaVN();
        var pochOvr = new PochodnaVirtualna();
        var pochNew = new PochodnaNew();

        // override — POLIMORFIZM: typ obiektu decyduje (vtable)
        BazaVN refOverride = new PochodnaVirtualna();
        refOverride.MetodaVirtual();    // PochodnaVirtualna.MetodaVirtual!
        Console.WriteLine("override → typ OBIEKTU decyduje (vtable)");

        // new — BRAK POLIMORFIZMU: typ REFERENCJI decyduje
        BazaVN refNew = new PochodnaNew();
        refNew.MetodaVirtual();         // BazaVN.MetodaVirtual — bo referencja to BazaVN!
        PochodnaNew bezposrednio = new PochodnaNew();
        bezposrednio.MetodaVirtual();   // PochodnaNew.MetodaVirtual — bezpośrednia ref

        Console.WriteLine("new → typ REFERENCJI decyduje (brak polimorfizmu)");
        Console.WriteLine("WNIOSEK: new nie nadpisuje wirtualnej metody, tworzy nowy slot w vtable");

        // vtable model uproszczony:
        Console.WriteLine("\nvtable A: [slot0]=A.M1, [slot1]=A.M2");
        Console.WriteLine("vtable B (override M1): [slot0]=B.M1, [slot1]=A.M2");
        Console.WriteLine("vtable B (new M1):      [slot0]=A.M1, [slot1]=A.M2, [slot2]=B.M1(new)");
    }

    // SEALED — zakaz dziedziczenia lub nadpisywania
    public static void SealedDemo()
    {
        Console.WriteLine("\n=== SEALED ===");

        // sealed class — nie można dziedziczyć
        var konf = new KonfiguracjaSealed { Host = "localhost", Port = 5432 };
        // class Moja : KonfiguracjaSealed {} // BŁĄD kompilacji

        var labrador = new LabradorSeal();
        labrador.Dzwiek(); // PiesSeal.Dzwiek (sealed) — nie można nadpisać w Labrador

        Console.WriteLine("sealed class → zakaz dziedziczenia (np. string, int w .NET)");
        Console.WriteLine("sealed override → zatrzymaj łańcuch nadpisywania");
        Console.WriteLine("Wydajność: JIT może de-virtualizować (sealed) → ~3ns → ~1ns");
    }

    // KOLEJNOŚĆ INICJALIZACJI KONSTRUKTORÓW
    public static void KolejnoscKonstruktorow()
    {
        Console.WriteLine("\n=== KOLEJNOŚĆ KONSTRUKTORÓW ===");
        Console.WriteLine("Tworzenie SamochodElektryczny:");

        var tesla = new SamochodEl("Tesla", 2024, 4, 600);
        Console.WriteLine($"Wynik: {tesla}");

        // Kolejność inicjalizacji:
        // 1. Inicjalizatory PÓL klasy pochodnej
        // 2. Inicjalizatory PÓL klasy bazowej
        // 3. Konstruktor BAZOWY (base())
        // 4. Ciało konstruktora POCHODNEGO

        Console.WriteLine("\nPUŁAPKA — wywołanie virtual w konstruktorze:");
        Console.WriteLine("Konstruktor bazowy może wywołać override z klasy pochodnej");
        Console.WriteLine("ZANIM pola pochodnej zostaną zainicjowane → null/default!");
        var niebezp = new NiebezpiecznaPochodna();
    }

    // CASTING — upcasting, downcasting, is, as, pattern matching
    public static void UpcastingIDowncasting()
    {
        Console.WriteLine("\n=== UPCASTING I DOWNCASTING ===");

        PiesDz pies = new PiesDz("Burek", 2, "Husky");
        ZwierzeDz zwierze = pies;    // implicit upcasting — zawsze OK
        object obj = pies;           // każdy → object

        // Downcasting — jawne, może rzucić InvalidCastException
        PiesDz p1 = (PiesDz)zwierze;       // rzuca gdy błędny typ
        ZwierzeDz z2 = new ZwierzeDz("X", 1);
        // PiesDz p2 = (PiesDz)z2;           // InvalidCastException!

        // Operator 'as' — null zamiast wyjątku
        PiesDz? mozeByc = zwierze as PiesDz;   // OK → obiekt
        ZwierzeDz zInner = new ZwierzeDz("Y", 1);
        PiesDz? napewnoNie = zInner as PiesDz;  // null, nie wyjątek!
        Console.WriteLine($"as PiesDz: mozeByc={mozeByc?.Imie ?? "null"}, napewnoNie={(napewnoNie == null ? "null" : napewnoNie.Imie)}");

        // Pattern matching — is (C# 7+) — sprawdzenie + rzutowanie atomowo
        if (zwierze is PiesDz wykryty)
        {
            Console.WriteLine($"Pattern matching: {wykryty.Imie}, rasa={wykryty.Rasa}");
        }

        // switch expression z typami i property patterns
        object[] obiekty = { 42, "tekst", 3.14, new PiesDz("Azor", 1, "Husky"), null! };
        foreach (var o in obiekty)
        {
            string opis = o switch
            {
                int n when n > 0         => $"Dodatni int: {n}",
                string { Length: > 4 } s => $"Długi string: '{s}'",
                PiesDz { Rasa: "Husky" } p => $"Husky: {p.Imie}",
                PiesDz pDog              => $"Pies: {pDog.Imie}",
                double d                 => $"Double: {d}",
                null                     => "null",
                _                        => $"Inny: {o.GetType().Name}"
            };
            Console.WriteLine($"  {opis}");
        }

        Console.WriteLine("(T)obj — gdy pewny typu | as → null gdy błąd | is T t → preferred (C# 7+)");
    }

    // GetType vs typeof vs is — różnice
    public static void GetTypeVsTypeof()
    {
        Console.WriteLine("\n=== GetType vs typeof vs is ===");

        ZwierzeDz obj = new PiesDz("Rex", 3, "Lab");

        // GetType() — rzeczywisty typ w RUNTIME
        Console.WriteLine($"obj.GetType().Name:            {obj.GetType().Name}");         // PiesDz
        Console.WriteLine($"obj.GetType() == typeof(PiesDz): {obj.GetType() == typeof(PiesDz)}"); // True
        Console.WriteLine($"obj.GetType() == typeof(ZwierzeDz): {obj.GetType() == typeof(ZwierzeDz)}"); // False!

        // typeof() — typ w CZASIE KOMPILACJI (statyczny)
        Console.WriteLine($"typeof(ZwierzeDz).Name: {typeof(ZwierzeDz).Name}");
        Console.WriteLine($"typeof(ZwierzeDz).IsAbstract: {typeof(ZwierzeDz).IsAbstract}");

        // is — uwzględnia hierarchię dziedziczenia
        Console.WriteLine($"obj is PiesDz:   {obj is PiesDz}");    // True
        Console.WriteLine($"obj is ZwierzeDz: {obj is ZwierzeDz}"); // True — IS-A!
        Console.WriteLine($"obj is object:   {obj is object}");    // True — wszystko

        Console.WriteLine("\nKluczowa różnica:");
        Console.WriteLine("  GetType()==typeof(X): dokładne dopasowanie (bez podklas)");
        Console.WriteLine("  is X:                 hierarchia (IS-A, włącznie z podklasami)");

        // Reflection
        var typ = typeof(PiesDz);
        Console.WriteLine($"\nReflection PiesDz:");
        Console.WriteLine($"  BaseType: {typ.BaseType?.Name}");
        Console.WriteLine($"  IsSealed: {typ.IsSealed}");
    }

    // KOMPOZYCJA vs DZIEDZICZENIE
    public static void KompozycjaVsDziedziczenie()
    {
        Console.WriteLine("\n=== KOMPOZYCJA vs DZIEDZICZENIE ===");

        // ZLE: Stack przez dziedziczenie z List<T>
        // class ZlyStack<T> : List<T> — EKSPONUJE 400+ metod Listy!
        // Użytkownik może Add(item, 0) — wstawia w środek, nie na szczyt
        Console.WriteLine("ZlyStack przez dziedziczenie: eksponuje wszystkie metody List<T>!");

        // DOBRZE: Stack przez kompozycję — kontrolujesz API
        var stack = new DobryStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        Console.WriteLine($"Push 1,2,3. Peek={stack.Peek()}, Count={stack.Count}");
        Console.WriteLine($"Pop={stack.Pop()}, Count={stack.Count}");

        Console.WriteLine("\nZasada: preferuj kompozycję nad dziedziczenie");
        Console.WriteLine("Dziedzicz gdy masz IS-A i potrzebujesz polimorfizmu");
        Console.WriteLine("Kompozycja gdy chcesz HAS-A lub kontrolować API");
    }

    // TEMPLATE METHOD PATTERN — klasa bazowa definiuje szkielet
    public static void SzablonMetody()
    {
        Console.WriteLine("\n=== TEMPLATE METHOD PATTERN ===");

        string[] daneCsv = { "Kacper,25", "Ania,30", "", "Michał,28" };

        Console.WriteLine("--- ProcesatorCSV ---");
        ProcesatorDanychBase procesor = new ProcesatorCSVImpl();
        procesor.Przetworz(daneCsv);

        Console.WriteLine("\n--- ProcesatorJSON ---");
        procesor = new ProcesatorJSONImpl();
        procesor.Przetworz(daneCsv);

        Console.WriteLine("\nTemplate Method: sealed szkielet + abstract kroki + virtual z domyślną");
        Console.WriteLine("Użycie: sealed override Przetworz() → nie można zmienić kolejności kroków");
    }
}

// ─── KLASY POMOCNICZE ────────────────────────────────────────────────────────

internal class ZwierzeDz
{
    public string Imie { get; }
    public int Wiek { get; private set; }
    protected int _energia = 100;  // protected — dostępny w klasach pochodnych

    public ZwierzeDz(string imie, int wiek)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imie);
        ArgumentOutOfRangeException.ThrowIfNegative(wiek);
        Imie = imie; Wiek = wiek;
    }

    public virtual void Dzwiek() => Console.WriteLine($"{Imie} wydaje dźwięk.");
    public virtual void Jedz(int kalorii) { _energia += kalorii; Console.WriteLine($"{Imie} je. Energia: {_energia}"); }
    public int PokazEnergie() => _energia;  // publiczna metoda zamiast publicznego pola
    public override string ToString() => $"{GetType().Name} '{Imie}', wiek: {Wiek}";
}

internal class PiesDz : ZwierzeDz
{
    public string Rasa { get; }

    // Konstruktor pochodny musi wywołać bazowy przez : base(...)
    public PiesDz(string imie, int wiek, string rasa) : base(imie, wiek)
    {
        Rasa = rasa;
    }

    public override void Dzwiek() => Console.WriteLine($"{Imie} szczeka: Hau hau!");

    public override void Jedz(int kalorii)
    {
        Console.WriteLine($"{Imie} je łapczywie.");
        base.Jedz(kalorii);  // wywołaj implementację bazowej
    }

    public void Aportuj()
    {
        _energia -= 10;  // protected _energia dostępna
        Console.WriteLine($"{Imie} ({Rasa}) aportuje! Energia: {_energia}");
    }
}

internal class KotDz : ZwierzeDz
{
    public string Rasa { get; }
    public KotDz(string imie, int wiek, string rasa) : base(imie, wiek) { Rasa = rasa; }
    public override void Dzwiek() => Console.WriteLine($"{Imie} mruczy: Mrrr...");
}

// Abstract class — nie można instancjonować
internal abstract class KsztaltDz
{
    public string Kolor { get; set; } = "Czarny";

    // abstract — brak implementacji, MUSI być nadpisane w pochodnych
    public abstract double Pole();
    public abstract double Obwod();
    public abstract string NazwaKsztaltu { get; }

    // Metoda z implementacją — pochodne dziedziczą (Template Method)
    public void Wyswietl()
    {
        Console.WriteLine($"{NazwaKsztaltu} [{Kolor}]: pole={Pole():F2}, obwód={Obwod():F2}");
    }

    // virtual z domyślną — można, ale nie trzeba nadpisywać
    public virtual string Opis() => $"{NazwaKsztaltu}: pole={Pole():F2}";
}

internal class KoloDz : KsztaltDz
{
    public double Promien { get; }
    public KoloDz(double r) => Promien = r;

    public override double Pole() => Math.PI * Promien * Promien;
    public override double Obwod() => 2 * Math.PI * Promien;
    public override string NazwaKsztaltu => "Koło";
}

internal class ProstokatDz : KsztaltDz
{
    public double Szerokosc { get; }
    public double Wysokosc { get; }

    public ProstokatDz(double s, double w) { Szerokosc = s; Wysokosc = w; }

    public override double Pole() => Szerokosc * Wysokosc;
    public override double Obwod() => 2 * (Szerokosc + Wysokosc);
    public override string NazwaKsztaltu => "Prostokąt";

    public override string Opis() => base.Opis() + $", {Szerokosc}x{Wysokosc}";
}

// Abstrakcyjna klasa może dziedziczyć z abstrakcyjnej — nie musi implementować wszystkiego
internal abstract class KsztaltRegularnyDz : KsztaltDz
{
    public abstract int LiczbaBokow { get; }
}

internal class SzeSciokatDz : KsztaltRegularnyDz
{
    public double Bok { get; }
    public SzeSciokatDz(double b) => Bok = b;

    public override double Pole() => 3 * Math.Sqrt(3) / 2 * Bok * Bok;
    public override double Obwod() => 6 * Bok;
    public override string NazwaKsztaltu => "Sześciokąt";
    public override int LiczbaBokow => 6;
}

// Demo virtual vs new
internal class BazaVN
{
    public virtual void MetodaVirtual() => Console.WriteLine("BazaVN.MetodaVirtual");
    public void MetodaNieWirtual() => Console.WriteLine("BazaVN.MetodaNieWirtual");
}

internal class PochodnaVirtualna : BazaVN
{
    public override void MetodaVirtual() => Console.WriteLine("PochodnaVirtualna.MetodaVirtual (override)");
}

internal class PochodnaNew : BazaVN
{
    // new — UKRYWA metodę bazową, tworzy NOWY slot w vtable
    // Bez 'new' kompilator da ostrzeżenie CS0108
    public new void MetodaVirtual() => Console.WriteLine("PochodnaNew.MetodaVirtual (new, brak polimorfizmu)");
}

// Sealed
internal sealed class KonfiguracjaSealed
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
}

internal class ZwierjeSealed2
{
    public virtual void Dzwiek() => Console.WriteLine("...");
}

internal class PiesSeal : ZwierjeSealed2
{
    public sealed override void Dzwiek() => Console.WriteLine("Hau! (sealed — nie można nadpisać)");
}

internal class LabradorSeal : PiesSeal
{
    // public override void Dzwiek() {} // BŁĄD — PiesSeal.Dzwiek jest sealed!
}

// Hierarchia konstruktorów
internal class PojazDz
{
    public string Marka { get; }
    public int Rok { get; }
    public PojazDz(string marka, int rok) { Marka = marka; Rok = rok; Console.WriteLine($"  Pojazd({marka})"); }
}

internal class SamochodDz : PojazDz
{
    public int LiczbaDrzwi { get; }
    public SamochodDz(string marka, int rok, int drzwi) : base(marka, rok)
    {
        LiczbaDrzwi = drzwi; Console.WriteLine($"  Samochod({marka}, {drzwi} drzwi)");
    }
}

internal class SamochodEl : SamochodDz
{
    public int ZasiegKm { get; }
    public SamochodEl(string marka, int rok, int drzwi, int zasieg)
        : base(marka, rok, drzwi)  // → Samochod → Pojazd → object
    {
        ZasiegKm = zasieg; Console.WriteLine($"  SamochodEl(zasięg {zasieg}km)");
    }
    public override string ToString() => $"{Marka} ({Rok}), {LiczbaDrzwi} drzwi, zasięg: {ZasiegKm}km";
}

// Pułapka: virtual w konstruktorze
internal class NiebezpiecznaBaza
{
    public NiebezpiecznaBaza()
    {
        Console.WriteLine("  NiebezpiecznaBaza konstruktor — wywołuje virtual:");
        WywolajMetode(); // może wywołać override zanim pola pochodnej są zainicjowane!
    }
    public virtual void WywolajMetode() => Console.WriteLine("  Baza.WywolajMetode");
}

internal class NiebezpiecznaPochodna : NiebezpiecznaBaza
{
    private readonly string _pole = "wartość";

    public NiebezpiecznaPochodna() { Console.WriteLine($"  Pochodna konstruktor: _pole={_pole}"); }

    // To się wywoła z konstruktora Bazowej — PRZED inicjalizacją _pole (inicjalizator)!
    public override void WywolajMetode()
        => Console.WriteLine($"  Pochodna.WywolajMetode: _pole='{_pole}' ← może być null!");
}

// Kompozycja — dobry Stack
internal class DobryStack<T>
{
    private readonly List<T> _dane = new(); // HAS-A, nie IS-A

    public void Push(T item) => _dane.Add(item);
    public T Pop() { var item = _dane[^1]; _dane.RemoveAt(_dane.Count - 1); return item; }
    public T Peek() => _dane[^1];
    public int Count => _dane.Count;
    public bool IsEmpty => _dane.Count == 0;
    // Tylko operacje sensowne dla Stack — brak Add(item, index) i podobnych
}

// Template Method Pattern
internal abstract class ProcesatorDanychBase
{
    // non-virtual — szkielet algorytmu jest niezmienny (nie można nadpisać)
    public void Przetworz(string[] dane)
    {
        var wczytane = WczytajDane(dane);
        var zwalidowane = WalidujDane(wczytane);     // virtual — można nadpisać
        var wynik = TransformujDane(zwalidowane);
        ZapiszWynik(wynik);
        Console.WriteLine($"  Przetworzono {wynik.Count} rekordów.");
    }

    protected abstract List<string> WczytajDane(string[] dane);
    protected abstract List<string> TransformujDane(List<string> dane);
    protected abstract void ZapiszWynik(List<string> wynik);

    // virtual z domyślną — pochodne mogą nadpisać lub nie
    protected virtual List<string> WalidujDane(List<string> dane)
        => dane.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
}

internal class ProcesatorCSVImpl : ProcesatorDanychBase
{
    protected override List<string> WczytajDane(string[] dane)
    {
        Console.WriteLine("  Wczytuję CSV...");
        return dane.Select(l => l.Trim()).ToList();
    }

    protected override List<string> WalidujDane(List<string> dane)
    {
        var podstawowa = base.WalidujDane(dane);  // wywołaj bazową
        return podstawowa.Where(l => l.Contains(',')).ToList(); // tylko linie CSV
    }

    protected override List<string> TransformujDane(List<string> dane)
    {
        Console.WriteLine("  Transformuję CSV → JSON...");
        return dane.Select(l =>
        {
            var cols = l.Split(',');
            return $"{{\"imie\":\"{cols[0]}\",\"wiek\":\"{(cols.Length > 1 ? cols[1] : "")}\"}}";
        }).ToList();
    }

    protected override void ZapiszWynik(List<string> wynik)
        => Console.WriteLine($"  Zapisuję {wynik.Count} rekordów JSON do pliku...");
}

internal class ProcesatorJSONImpl : ProcesatorDanychBase
{
    protected override List<string> WczytajDane(string[] dane) { Console.WriteLine("  Wczytuję JSON..."); return dane.ToList(); }
    protected override List<string> TransformujDane(List<string> dane) { Console.WriteLine("  Normalizuję JSON..."); return dane.Select(d => d.ToLowerInvariant()).ToList(); }
    protected override void ZapiszWynik(List<string> wynik) => Console.WriteLine("  Wysyłam do API...");
}
