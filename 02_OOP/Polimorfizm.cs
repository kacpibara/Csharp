namespace _02_OOP;

// ─────────────────────────────────────────────────────────────────────────────
// POLIMORFIZM
// "Wiele form" — ten sam interfejs, różne zachowania
// W C#: polimorfizm przez dziedziczenie (virtual/override) i interfejsy
// ─────────────────────────────────────────────────────────────────────────────

public static class Polimorfizm
{
    // VTABLE — jak CLR implementuje polimorfizm
    public static void VtableModel()
    {
        Console.WriteLine("\n=== VTABLE MODEL ===");

        var a = new VtableA();
        var b = new VtableB();
        var c = new VtableC();

        Console.WriteLine("vtable A: [slot0]=A.M1, [slot1]=A.M2");
        Console.WriteLine("vtable B (override M1): [slot0]=B.M1, [slot1]=A.M2, [slot2]=B.M2(new), [slot3]=B.M4");
        Console.WriteLine("vtable C (override M1, M4): [slot0]=C.M1, [slot1]=A.M2, [slot2]=B.M2, [slot3]=C.M4");
        Console.WriteLine();

        VtableA refA = new VtableC();
        refA.M1(); // C.M1 — vtable C slot 0 → C.M1
        refA.M2(); // A.M2 — vtable C slot 1 → A.M2 (new w B nie nadpisuje slot 1!)

        VtableB refB = new VtableC();
        refB.M1(); // C.M1 — slot 0 → C.M1
        refB.M2(); // B.M2 — slot 2 → B.M2 (przez B mamy dostęp do nowego slotu)
        refB.M4(); // C.M4 — slot 3 → C.M4

        Console.WriteLine("\nKluczowe: typ OBIEKTU (nie referencji) decyduje o wywołaniu virtual!");
        Console.WriteLine("new tworzy NOWY slot — stary wskazuje na bazową gdy wywołujesz przez starszy typ.");
    }

    // STRATEGY PATTERN — wzorzec przez polimorfizm
    public static void StrategyRabatu()
    {
        Console.WriteLine("\n=== STRATEGY PATTERN (rabaty) ===");

        // Bez Strategy — anty-wzorzec: switch/if na typach, naruszenie OCP
        Console.WriteLine("Bez Strategy: if (typRabatu == \"student\")... — naruszenie OCP!");
        Console.WriteLine("Każdy nowy rabat = modyfikacja istniejącej klasy.\n");

        var koszyk = new KoszykPoly();
        koszyk.DodajProdukt("Laptop",    3499.99m);
        koszyk.DodajProdukt("Mysz",        89.99m);
        koszyk.DodajProdukt("Klawiatura", 199.99m);

        // Strategia 1: rabat procentowy
        koszyk.UstawStrategieRabatu(new RabatProcentowy(15));
        koszyk.WyswietlPodsumowanie();

        // Strategia 2: rabat progowy — zmiana BEZ modyfikacji Koszyk!
        koszyk.UstawStrategieRabatu(new RabatProgowy(500, 10));
        koszyk.WyswietlPodsumowanie();

        // Strategia 3: złożona (kompozyt)
        koszyk.UstawStrategieRabatu(new StrategiePolaczonePoly(
            new RabatProcentowy(10),
            new RabatStaly(50)));
        koszyk.WyswietlPodsumowanie();

        Console.WriteLine("\nStrategy = OCP: nowa strategia = nowa klasa, zero zmian w Koszyk");
    }

    // PUŁAPKI POLIMORFIZMU
    public static void PulapePolimorfizmu()
    {
        Console.WriteLine("\n=== PUŁAPKI POLIMORFIZMU ===");

        // Pułapka 1: Naruszenie symetrii Equals w hierarchii
        var p2d = new PunktPoly(1, 2);
        var p3d = new Punkt3DPoly(1, 2, 0);

        Console.WriteLine("Naruszenie symetrii Equals:");
        Console.WriteLine($"  p2d.Equals(p3d) = {p2d.Equals(p3d)}");  // True — Punkt nie sprawdza Z
        Console.WriteLine($"  p3d.Equals(p2d) = {p3d.Equals(p2d)}");  // False — Punkt3D sprawdza Z
        Console.WriteLine("  a.Equals(b) != b.Equals(a) — psuje Dictionary i HashSet!");
        Console.WriteLine("  Rozwiązanie: sealed klasy LUB sprawdzaj GetType() == other.GetType()");

        // Pułapka 2: Naruszenie LSP
        Console.WriteLine("\nNaruszenie LSP (Liskov Substitution Principle):");
        void TestujZapis(BazaZapis zapisywacz) => zapisywacz.Zapisz("dane");

        TestujZapis(new BazaZapis());         // OK
        Console.WriteLine("  BazaZapis.Zapisz(\"dane\") — OK");
        try
        {
            var zly = new PochodzacaZapis();
            // PochodzacaZapis zawęża kontrakt — rzuca dla pewnych argumentów
            zly.Zapisz("specjalny_błąd");
        }
        catch (NotSupportedException ex) { Console.WriteLine($"  PochodzacaZapis rzuca: {ex.Message}"); }
        Console.WriteLine("  Kod działający z BazaZapis może się posypać z PochodzacaZapis!");

        // Pułapka 3: Rzutowanie w metodzie — kod zapachowy
        Console.WriteLine("\nKod zapachowy — rzutowanie w metodzie:");
        Console.WriteLine("  if (zwierze is Pies p) p.Aportuj();");
        Console.WriteLine("  else if (zwierze is Kot k) k.Mruczenie();");
        Console.WriteLine("  → Każdy nowy typ = modyfikacja tej metody = naruszenie OCP!");
        Console.WriteLine("  Rozwiązanie: dodaj metodę WykonajTypoweZachowanie() do interfejsu/klasy bazowej");
    }

    // AD-HOC POLIMORFIZM — przeciążanie metod
    public static void AdhocPolimorfizm()
    {
        Console.WriteLine("\n=== AD-HOC POLIMORFIZM (przeciążanie) ===");

        Console.WriteLine($"Dodaj(int, int): {Kalkulator.Dodaj(1, 2)}");
        Console.WriteLine($"Dodaj(double, double): {Kalkulator.Dodaj(1.5, 2.5)}");
        Console.WriteLine($"Dodaj(string, string): {Kalkulator.Dodaj("Hello", " World")}");
        Console.WriteLine($"Dodaj(int, int, int): {Kalkulator.Dodaj(1, 2, 3)}");

        Console.WriteLine("\nPrzeciążanie nie jest polimorfizmem runtime!");
        Console.WriteLine("Rozwiązywane w CZASIE KOMPILACJI na podstawie typów argumentów.");
        Console.WriteLine("Runtime: virtual/override. Compile-time: przeciążanie.");
    }
}

// ─── VTABLE DEMO ─────────────────────────────────────────────────────────────

internal class VtableA
{
    public virtual void M1() => Console.WriteLine("  A.M1  (slot 0)");
    public virtual void M2() => Console.WriteLine("  A.M2  (slot 1)");
    public void M3() => Console.WriteLine("  A.M3  (nie w vtable — bezpośredni adres)");
}

internal class VtableB : VtableA
{
    public override void M1() => Console.WriteLine("  B.M1  (slot 0 → B.M1)");

    // new M2 — tworzy NOWY slot 2, slot 1 nadal wskazuje A.M2!
    public new void M2() => Console.WriteLine("  B.M2  (nowy slot 2 — przez A ref nadal A.M2)");

    public virtual void M4() => Console.WriteLine("  B.M4  (slot 3)");
}

internal class VtableC : VtableB
{
    public override void M1() => Console.WriteLine("  C.M1  (slot 0 → C.M1)");
    public override void M4() => Console.WriteLine("  C.M4  (slot 3 → C.M4)");
}

// ─── STRATEGY PATTERN ────────────────────────────────────────────────────────

internal interface IStrategiaRabatu
{
    decimal ZastosujRabat(decimal cenaBase);
    string Opis { get; }
    bool MoznaLaczyc => true;
}

internal class BrakRabatu : IStrategiaRabatu
{
    public decimal ZastosujRabat(decimal cena) => cena;
    public string Opis => "Brak rabatu";
}

internal class RabatProcentowy : IStrategiaRabatu
{
    private readonly decimal _procent;
    public RabatProcentowy(decimal procent)
    {
        if (procent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(procent));
        _procent = procent;
    }
    public decimal ZastosujRabat(decimal cena) => cena * (1 - _procent / 100);
    public string Opis => $"Rabat {_procent}%";
}

internal class RabatStaly : IStrategiaRabatu
{
    private readonly decimal _kwota;
    public RabatStaly(decimal kwota) => _kwota = kwota;
    public decimal ZastosujRabat(decimal cena) => Math.Max(0, cena - _kwota);
    public string Opis => $"Rabat -{_kwota:C}";
}

internal class RabatProgowy : IStrategiaRabatu
{
    private readonly decimal _prog, _procent;
    public RabatProgowy(decimal prog, decimal procent) { _prog = prog; _procent = procent; }
    public decimal ZastosujRabat(decimal cena) => cena >= _prog ? cena * (1 - _procent / 100) : cena;
    public string Opis => $"Rabat {_procent}% przy zakupie ≥{_prog:C}";
}

internal class StrategiePolaczonePoly : IStrategiaRabatu
{
    private readonly IReadOnlyList<IStrategiaRabatu> _strategie;

    public StrategiePolaczonePoly(params IStrategiaRabatu[] strategie)
        => _strategie = strategie.Where(s => s.MoznaLaczyc).ToList();

    // Kompozyt: każda strategia stosowana po poprzedniej
    public decimal ZastosujRabat(decimal cena)
        => _strategie.Aggregate(cena, (aktualna, s) => s.ZastosujRabat(aktualna));

    public string Opis => string.Join(" + ", _strategie.Select(s => s.Opis));
}

internal class KoszykPoly
{
    private readonly List<(string Nazwa, decimal Cena)> _pozycje = new();
    private IStrategiaRabatu _strategia = new BrakRabatu();

    public void DodajProdukt(string nazwa, decimal cena) => _pozycje.Add((nazwa, cena));

    // Strategię można zmieniać w RUNTIME — kluczowa cecha wzorca
    public void UstawStrategieRabatu(IStrategiaRabatu s) => _strategia = s;

    public decimal CenaBase => _pozycje.Sum(p => p.Cena);
    public decimal CenaFinalna => _strategia.ZastosujRabat(CenaBase);

    public void WyswietlPodsumowanie()
    {
        Console.WriteLine("\n  === KOSZYK ===");
        foreach (var (nazwa, cena) in _pozycje)
            Console.WriteLine($"  {nazwa,-18} {cena,9:C}");
        Console.WriteLine($"  {"─────────────────────────────":─<30}");
        Console.WriteLine($"  {"Suma:",-18} {CenaBase,9:C}");
        decimal rabat = CenaFinalna - CenaBase;
        Console.WriteLine($"  {_strategia.Opis + ":",-18} {rabat,9:C}");
        Console.WriteLine($"  {"DO ZAPŁATY:",-18} {CenaFinalna,9:C}");
    }
}

// ─── PUŁAPKI ─────────────────────────────────────────────────────────────────

internal class PunktPoly
{
    public int X { get; }
    public int Y { get; }
    public PunktPoly(int x, int y) => (X, Y) = (x, y);

    public override bool Equals(object? obj)
        => obj is PunktPoly p && X == p.X && Y == p.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

internal class Punkt3DPoly : PunktPoly
{
    public int Z { get; }
    public Punkt3DPoly(int x, int y, int z) : base(x, y) => Z = z;

    // PUŁAPKA: naruszenie symetrii!
    // p2d.Equals(p3d) = True (Punkt nie sprawdza Z)
    // p3d.Equals(p2d) = False (Punkt3D sprawdza Z)
    public override bool Equals(object? obj)
        => obj is Punkt3DPoly p && base.Equals(p) && Z == p.Z;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Z);
}

internal class BazaZapis
{
    public virtual void Zapisz(string dane)
        => Console.WriteLine($"  BazaZapis: zapisuję '{dane}'");
}

internal class PochodzacaZapis : BazaZapis
{
    public override void Zapisz(string dane)
    {
        // NARUSZENIE LSP — zawęża kontrakt (rzuca gdy dane zawiera "błąd")
        if (dane.Contains("błąd")) throw new NotSupportedException("PochodzacaZapis nie obsługuje błędów");
        Console.WriteLine($"  PochodzacaZapis: zapisuję '{dane}'");
    }
}

// Ad-hoc polimorfizm
internal static class Kalkulator
{
    public static int Dodaj(int a, int b) => a + b;
    public static double Dodaj(double a, double b) => a + b;
    public static string Dodaj(string a, string b) => a + b;
    public static int Dodaj(int a, int b, int c) => a + b + c;
}
