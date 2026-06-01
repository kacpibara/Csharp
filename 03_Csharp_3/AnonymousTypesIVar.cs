namespace _03_Csharp_3;

// ── Dane do demonstracji ──────────────────────────────────────────────────────

public class ProduktatAT
{
    public int Id { get; set; }
    public string Nazwa { get; set; } = "";
    public decimal Cena { get; set; }
    public string Kategoria { get; set; } = "";
    public int StanMagazynu { get; set; }
}

public static class AnonymousTypesIVar
{
    static readonly List<ProduktatAT> Produkty = new()
    {
        new() { Id=1, Nazwa="Laptop",    Cena=3500, Kategoria="Elektronika", StanMagazynu=10 },
        new() { Id=2, Nazwa="Mysz",      Cena=80,   Kategoria="Elektronika", StanMagazynu=50 },
        new() { Id=3, Nazwa="Monitor",   Cena=1200, Kategoria="Elektronika", StanMagazynu=5  },
        new() { Id=4, Nazwa="Koszulka",  Cena=45,   Kategoria="Odzież",      StanMagazynu=100},
        new() { Id=5, Nazwa="Spodnie",   Cena=120,  Kategoria="Odzież",      StanMagazynu=30 },
        new() { Id=6, Nazwa="Słuchawki", Cena=300,  Kategoria="Elektronika", StanMagazynu=20 },
    };

    // ── 1. var — wnioskowanie typów ───────────────────────────────────────────

    public static void VarWnioskowanie()
    {
        Console.WriteLine("\n── VarWnioskowanie ──");

        // var — kompilator dedukuje typ z prawej strony przypisania
        // Typ jest znany w COMPILE TIME — to NIE jest dynamic!
        var liczba   = 42;               // int
        var tekst    = "hello";          // string
        var lista    = new List<int>();  // List<int>
        var pi       = 3.14159;          // double

        Console.WriteLine($"liczba: {liczba.GetType().Name}");   // Int32
        Console.WriteLine($"tekst:  {tekst.GetType().Name}");    // String
        Console.WriteLine($"lista:  {lista.GetType().Name}");    // List`1
        Console.WriteLine($"pi:     {pi.GetType().Name}");      // Double

        // KIEDY używać var:
        // ✓ Konstruktory — typ widoczny po prawej
        var dict = new Dictionary<string, List<int>>(); // czytelniej niż pełny typ po lewej
        // ✓ Długie typy generyczne — oszczędność powtórzenia
        // ✓ LINQ — typ anonimowy lub złożony
        var wyniki = Produkty.Where(p => p.Cena > 100).ToList();
        Console.WriteLine($"var LINQ: {wyniki.Count} produktów powyżej 100 zł");

        // KIEDY NIE używać var:
        // ✗ Proste typy gdy typ nie jest jasny z prawej strony
        // int x = GetValue(); — jasne
        // var x = GetValue(); — co zwraca GetValue? nie wiadomo bez zajrzenia
        var jasne = new System.Text.StringBuilder(); // OK — widać typ
        // var nieJasne = PobierzCos();  // złe — wymaga sprawdzenia

        // var NIE może być użyte bez inicjalizacji
        // var x; // BŁĄD kompilacji

        // var jest statyczny — kompilator nie pozwoli zmienić typu
        // var licznik = 0;
        // licznik = "tekst"; // BŁĄD — licznik jest int (CS0029)

        // var vs dynamic
        Console.WriteLine("\nvar vs dynamic:");
        Console.WriteLine("  var:     typ znany w compile-time, full IntelliSense, bezpieczny");
        Console.WriteLine("  dynamic: typ sprawdzany w runtime, brak IntelliSense, może rzucić RuntimeBinderException");
    }

    // ── 2. Anonymous types ─────────────────────────────────────────────────────

    public static void AnonymousTypes()
    {
        Console.WriteLine("\n── AnonymousTypes ──");

        // Typ anonimowy — kompilator generuje klasę bez nazwy
        // Właściwości: auto-properties read-only, Equals/GetHashCode/ToString wygenerowane
        var osoba = new { Imie = "Anna", Wiek = 30, Aktywna = true };
        Console.WriteLine($"osoba: {osoba}"); // { Imie = Anna, Wiek = 30, Aktywna = True }
        Console.WriteLine($"osoba.Imie = {osoba.Imie}");
        // osoba.Imie = "Jan"; // BŁĄD — właściwości są readonly!

        // Projekcja z istniejącej zmiennej — można pominąć nazwę
        string imie = "Jan";
        int wiek = 25;
        var auto = new { imie, wiek, Extra = "dane" }; // auto.imie, auto.wiek
        Console.WriteLine($"auto projekcja: {auto}");

        // Zagnieżdżone typy anonimowe
        var firma = new
        {
            Nazwa = "TechCorp",
            Adres = new { Ulica = "Marszałkowska 1", Miasto = "Warszawa" },
            Pracownicy = 150
        };
        Console.WriteLine($"firma.Adres.Miasto = {firma.Adres.Miasto}");

        // Equals — porównanie VALUE (nie referencji) dla typów anonimowych
        var a1 = new { X = 1, Y = 2 };
        var a2 = new { X = 1, Y = 2 };
        var a3 = new { X = 9, Y = 2 };
        Console.WriteLine($"a1.Equals(a2) = {a1.Equals(a2)}"); // True — te same wartości
        Console.WriteLine($"a1.Equals(a3) = {a1.Equals(a3)}"); // False
        Console.WriteLine($"ReferenceEquals(a1, a2) = {ReferenceEquals(a1, a2)}"); // False — różne obiekty

        // GetHashCode — spójny z Equals (ten sam hash gdy same wartości)
        Console.WriteLine($"a1.GetHashCode() == a2.GetHashCode(): {a1.GetHashCode() == a2.GetHashCode()}");

        // Ograniczenia typów anonimowych:
        Console.WriteLine("\nOgraniczenia:");
        Console.WriteLine("  - Nie można zwrócić z metody (poza dynamic/object)");
        Console.WriteLine("  - Właściwości są readonly (brak set)");
        Console.WriteLine("  - Nie implementują interfejsów");
        Console.WriteLine("  - Nie można użyć z new() z zewnątrz zakresu");
    }

    // ── 3. Typy anonimowe w LINQ ──────────────────────────────────────────────

    public static void AnonymousTypesWLinq()
    {
        Console.WriteLine("\n── AnonymousTypesWLinq ──");

        // Select z typem anonimowym — projekcja tylko potrzebnych pól
        var projekcja = Produkty
            .Where(p => p.Cena > 100)
            .Select(p => new { p.Nazwa, p.Cena, CenaZVat = p.Cena * 1.23m })
            .OrderByDescending(p => p.Cena);

        Console.WriteLine("Produkty >100 zł z VAT:");
        foreach (var p in projekcja)
            Console.WriteLine($"  {p.Nazwa}: {p.Cena:C} → z VAT: {p.CenaZVat:C}");

        // GroupBy z typem anonimowym
        var grupy = Produkty
            .GroupBy(p => p.Kategoria)
            .Select(g => new
            {
                Kategoria = g.Key,
                Ilosc = g.Count(),
                SredniaCena = g.Average(p => p.Cena),
                LacznyMagazyn = g.Sum(p => p.StanMagazynu)
            });

        Console.WriteLine("\nGrupy wg kategorii:");
        foreach (var g in grupy)
            Console.WriteLine($"  {g.Kategoria}: {g.Ilosc} szt., śr.cena={g.SredniaCena:C}, mag={g.LacznyMagazyn}");

        // Join z typem anonimowym
        var kategorie = new[] { new { Nazwa = "Elektronika", Rabat = 0.1m }, new { Nazwa = "Odzież", Rabat = 0.2m } };

        var z_rabatem = Produkty
            .Join(kategorie,
                p => p.Kategoria,
                k => k.Nazwa,
                (p, k) => new { p.Nazwa, p.Cena, CenaPoRabacie = p.Cena * (1 - k.Rabat), k.Rabat })
            .OrderBy(x => x.CenaPoRabacie);

        Console.WriteLine("\nCeny po rabacie kategoryjnym:");
        foreach (var x in z_rabatem)
            Console.WriteLine($"  {x.Nazwa}: {x.Cena:C} - {x.Rabat:P0} = {x.CenaPoRabacie:C}");
    }

    // ── 4. Porównanie: Anonymous Type vs Tuple vs Record ─────────────────────

    public static void PorownanieTuplesRecords()
    {
        Console.WriteLine("\n── PorownanieTuplesRecords ──");

        // 1. Anonymous type — LINQ projekcje, tylko wewnątrz metody
        var anon = new { Imie = "Anna", Wiek = 30 };
        Console.WriteLine($"Anonymous: {anon}, Equals działa: {anon.Equals(new { Imie = "Anna", Wiek = 30 })}");

        // 2. ValueTuple (C# 7+) — lekki, można zwrócić z metody, destrturyzacja
        var tuple = (Imie: "Anna", Wiek: 30);
        var (imie, wiek) = tuple; // destrukturyzacja
        Console.WriteLine($"ValueTuple: {tuple}, {imie} lat {wiek}");
        Console.WriteLine($"Tuple Equals: {tuple == (Imie: "Anna", Wiek: 30)}");

        // 3. Record (C# 9+) — ma nazwę, można zwrócić, immutable, with expression
        var record = new OsobaRecord("Anna", 30);
        var zmieniona = record with { Wiek = 31 };
        Console.WriteLine($"Record: {record}, zmieniona: {zmieniona}");
        Console.WriteLine($"Record Equals: {record.Equals(new OsobaRecord("Anna", 30))}");

        // 4. Zwykła klasa — mutowalny stan, dziedziczenie
        var obj = new OsobaKlasa { Imie = "Anna", Wiek = 30 };

        Console.WriteLine("\nKiedy co wybrać:");
        Console.WriteLine("  Anonymous type: LINQ projekcja, tylko lokalnie, brak nazwy");
        Console.WriteLine("  ValueTuple:     zwracanie kilku wartości z metody, szybkie rozwiązanie");
        Console.WriteLine("  Record:         DTO, immutable data, public API, trzeba nazwy");
        Console.WriteLine("  Klasa:          mutowalny stan, dziedziczenie, złożona logika");
    }

    // ── 5. Zaawansowane użycie var ─────────────────────────────────────────────

    public static void ZaawansowaneVar()
    {
        Console.WriteLine("\n── ZaawansowaneVar ──");

        // var z LINQ — często jedyny sposób (typ anonimowy nie ma nazwy)
        var raport = Produkty
            .GroupBy(p => p.Kategoria)
            .Select(g => new { g.Key, Suma = g.Sum(p => p.Cena * p.StanMagazynu) })
            .OrderByDescending(x => x.Suma);

        Console.WriteLine("Raport wartości magazynu:");
        foreach (var r in raport)
            Console.WriteLine($"  {r.Key}: {r.Suma:C}");

        // var w using — typ zasobu nie jest ważny, ważne jest Dispose
        using var reader = new System.IO.StringReader("linia1\nlinia2");
        string? linia;
        Console.Write("StringReader: ");
        while ((linia = reader.ReadLine()) != null)
            Console.Write($"{linia} ");
        Console.WriteLine();

        // var z switch expression — typ może być skomplikowany
        object value = "hello";
        var wynik = value switch
        {
            int i => $"liczba: {i}",
            string s => $"tekst: {s}",
            _ => "inne"
        };
        Console.WriteLine($"switch var: {wynik}");

        // Czytelność: var NIE oznacza dynamic, NIE oznacza object
        // Kompilator zna typ i IntelliSense działa tak samo
        Console.WriteLine("\nvar = syntactic sugar dla jawnego podania typu");
        Console.WriteLine("Kompilator zna dokładny typ — full type safety i IntelliSense");
    }
}

// Pomocnicze typy
public record OsobaRecord(string Imie, int Wiek);
public class OsobaKlasa { public string Imie { get; set; } = ""; public int Wiek { get; set; } }
