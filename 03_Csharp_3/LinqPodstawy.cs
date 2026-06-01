namespace _03_Csharp_3;

// ── Dane do demonstracji ──────────────────────────────────────────────────────

public class PracownikLP
{
    public int Id { get; set; }
    public string Imie { get; set; } = "";
    public string Nazwisko { get; set; } = "";
    public string Dzial { get; set; } = "";
    public decimal Pensja { get; set; }
    public int Wiek { get; set; }
    public DateTime DataZatrudnienia { get; set; }
    public string? Email { get; set; }
}

public class ZamowienieLP2
{
    public int Id { get; set; }
    public int PracownikId { get; set; }
    public decimal Kwota { get; set; }
    public DateTime Data { get; set; }
    public string Status { get; set; } = "";
}

public static class LinqPodstawy
{
    static readonly List<PracownikLP> Pracownicy = new()
    {
        new() { Id=1, Imie="Anna",    Nazwisko="Kowalska", Dzial="IT",      Pensja=8500,  Wiek=28, DataZatrudnienia=new DateTime(2020,3,1),  Email="anna@firma.pl" },
        new() { Id=2, Imie="Jan",     Nazwisko="Nowak",    Dzial="HR",      Pensja=6000,  Wiek=35, DataZatrudnienia=new DateTime(2018,6,15), Email="jan@firma.pl"  },
        new() { Id=3, Imie="Marek",   Nazwisko="Wiśniak",  Dzial="IT",      Pensja=9200,  Wiek=42, DataZatrudnienia=new DateTime(2015,1,10), Email=null            },
        new() { Id=4, Imie="Kasia",   Nazwisko="Lewandowska", Dzial="HR",   Pensja=5800,  Wiek=27, DataZatrudnienia=new DateTime(2022,9,1),  Email="kasia@firma.pl"},
        new() { Id=5, Imie="Piotr",   Nazwisko="Zając",    Dzial="IT",      Pensja=11000, Wiek=38, DataZatrudnienia=new DateTime(2012,4,20), Email="piotr@firma.pl"},
        new() { Id=6, Imie="Zofia",   Nazwisko="Adamczyk", Dzial="Finanse", Pensja=7500,  Wiek=33, DataZatrudnienia=new DateTime(2019,11,5), Email="zofia@firma.pl"},
        new() { Id=7, Imie="Tomasz",  Nazwisko="Krawczyk", Dzial="Finanse", Pensja=8000,  Wiek=45, DataZatrudnienia=new DateTime(2010,8,22), Email="tomasz@firma.pl"},
        new() { Id=8, Imie="Ewa",     Nazwisko="Mazurek",  Dzial="IT",      Pensja=7800,  Wiek=31, DataZatrudnienia=new DateTime(2021,2,14), Email="ewa@firma.pl"  },
    };

    static readonly List<ZamowienieLP2> Zamowienia = new()
    {
        new() { Id=1, PracownikId=1, Kwota=1200, Data=new DateTime(2024,1,5),  Status="Zrealizowane" },
        new() { Id=2, PracownikId=1, Kwota=450,  Data=new DateTime(2024,2,10), Status="Zrealizowane" },
        new() { Id=3, PracownikId=3, Kwota=3000, Data=new DateTime(2024,1,20), Status="Zrealizowane" },
        new() { Id=4, PracownikId=5, Kwota=800,  Data=new DateTime(2024,3,1),  Status="W toku"       },
        new() { Id=5, PracownikId=2, Kwota=250,  Data=new DateTime(2024,3,5),  Status="Zrealizowane" },
        new() { Id=6, PracownikId=5, Kwota=2200, Data=new DateTime(2024,3,12), Status="Zrealizowane" },
        new() { Id=7, PracownikId=7, Kwota=600,  Data=new DateTime(2024,4,1),  Status="Anulowane"    },
        new() { Id=8, PracownikId=8, Kwota=950,  Data=new DateTime(2024,4,8),  Status="W toku"       },
    };

    // ── 1. Query syntax vs Method syntax ─────────────────────────────────────

    public static void QueryVsMethodSyntax()
    {
        Console.WriteLine("\n── QueryVsMethodSyntax ──");

        // Query syntax — SQL-like, kompilator tłumaczy na method syntax
        var queryResult =
            from p in Pracownicy
            where p.Dzial == "IT"
            orderby p.Pensja descending
            select new { p.Imie, p.Pensja };

        // Method syntax — bezpośrednie wywołania extension methods
        var methodResult = Pracownicy
            .Where(p => p.Dzial == "IT")
            .OrderByDescending(p => p.Pensja)
            .Select(p => new { p.Imie, p.Pensja });

        // Identyczne wyniki — query syntax to syntactic sugar
        Console.WriteLine("IT dept (query syntax):");
        foreach (var p in queryResult)
            Console.WriteLine($"  {p.Imie}: {p.Pensja:C}");

        Console.WriteLine("IT dept (method syntax — identyczny wynik):");
        foreach (var p in methodResult)
            Console.WriteLine($"  {p.Imie}: {p.Pensja:C}");

        // Query syntax wymaga: from, select — reszta opcjonalna
        // Method syntax: pełna kontrola, obsługuje metody bez odpowiednika query (Zip, Aggregate, Take...)
        // Reguła: query gdy join+groupby czytelniejszy, method w pozostałych przypadkach
        Console.WriteLine("\nQuery syntax: from...where...orderby...select");
        Console.WriteLine("Method syntax: extension methods na IEnumerable<T>");
        Console.WriteLine("Kompilator: query → method (identyczny IL)");
    }

    // ── 2. Filtrowanie — Where ────────────────────────────────────────────────

    public static void FilterowanieWhere()
    {
        Console.WriteLine("\n── FilterowanieWhere ──");

        // Podstawowe filtrowanie
        var senior = Pracownicy.Where(p => p.Wiek >= 40);
        Console.WriteLine("Pracownicy >=40 lat:");
        foreach (var p in senior)
            Console.WriteLine($"  {p.Imie} {p.Nazwisko}, {p.Wiek} lat");

        // Łańcuchowanie Where — każde Where to kolejny filtr (AND)
        var itSenior = Pracownicy
            .Where(p => p.Dzial == "IT")
            .Where(p => p.Pensja > 8000);
        Console.WriteLine("\nIT z pensją >8000:");
        foreach (var p in itSenior)
            Console.WriteLine($"  {p.Imie}: {p.Pensja:C}");

        // Złożone predykaty
        var specjalni = Pracownicy
            .Where(p => (p.Dzial == "IT" && p.Pensja > 9000) || p.Wiek > 40);
        Console.WriteLine("\nSpecjalni (IT>9000 lub wiek>40):");
        foreach (var p in specjalni)
            Console.WriteLine($"  {p.Imie}, {p.Dzial}, {p.Pensja:C}");

        // Where z indeksem (wersja z indeksem)
        var coPiaty = Pracownicy
            .Where((p, idx) => idx % 2 == 0)
            .Select(p => p.Imie);
        Console.WriteLine($"\nCo drugi (indeks parzysty): {string.Join(", ", coPiaty)}");

        // OfType<T> — filtrowanie po typie (LINQ na IEnumerable)
        object[] mieszane = { 1, "dwa", 3.0, "cztery", 5, true };
        var sameLiczbyCal = mieszane.OfType<int>();
        Console.WriteLine($"OfType<int>: {string.Join(", ", sameLiczbyCal)}");
    }

    // ── 3. Projekcje — Select i SelectMany ───────────────────────────────────

    public static void ProjekacjeSelect()
    {
        Console.WriteLine("\n── ProjekacjeSelect ──");

        // Select — mapowanie 1:1
        var imiona = Pracownicy.Select(p => p.Imie);
        Console.WriteLine($"Imiona: {string.Join(", ", imiona)}");

        // Select z typem anonimowym — projekcja wielu pól
        var podsumowania = Pracownicy.Select(p => new
        {
            FullName = $"{p.Imie} {p.Nazwisko}",
            p.Dzial,
            Brutto = p.Pensja,
            Netto = Math.Round(p.Pensja * 0.77m, 2)
        });
        Console.WriteLine("\nPodsumowania (pierwsze 3):");
        foreach (var x in podsumowania.Take(3))
            Console.WriteLine($"  {x.FullName} ({x.Dzial}): brutto={x.Brutto:C}, netto={x.Netto:C}");

        // Select z indeksem
        var ponumerowane = Pracownicy
            .Select((p, i) => $"{i + 1}. {p.Imie} {p.Nazwisko}");
        Console.WriteLine("\nPonumerowani:");
        foreach (var s in ponumerowane)
            Console.WriteLine($"  {s}");

        // SelectMany — "flatten" kolekcji zagnieżdżonej (1:N)
        var dzialy = new[]
        {
            new { Dzial = "IT",      Osoby = new[] { "Anna", "Marek", "Piotr", "Ewa" } },
            new { Dzial = "HR",      Osoby = new[] { "Jan", "Kasia" } },
            new { Dzial = "Finanse", Osoby = new[] { "Zofia", "Tomasz" } },
        };

        // SelectMany spłaszcza: każda kolekcja Osoby staje się jednym ciągiem
        var wszyscyRazem = dzialy.SelectMany(d => d.Osoby);
        Console.WriteLine($"\nSelectMany (wszystkie osoby): {string.Join(", ", wszyscyRazem)}");

        // SelectMany z wynikowym elementem — para (Dzial, Osoba)
        var pary = dzialy.SelectMany(
            d => d.Osoby,
            (d, osoba) => new { d.Dzial, Osoba = osoba });
        Console.WriteLine("Pary Dzial-Osoba:");
        foreach (var p in pary)
            Console.WriteLine($"  {p.Dzial}: {p.Osoba}");

        // SelectMany na tablicy tablic
        int[][] macierz = { new[] { 1, 2, 3 }, new[] { 4, 5 }, new[] { 6, 7, 8, 9 } };
        var splaszczona = macierz.SelectMany(row => row);
        Console.WriteLine($"Spłaszczona macierz: {string.Join(", ", splaszczona)}");
    }

    // ── 4. Sortowanie ─────────────────────────────────────────────────────────

    public static void Sortowanie()
    {
        Console.WriteLine("\n── Sortowanie ──");

        // OrderBy — rosnąco (domyślne)
        var wgPensji = Pracownicy
            .OrderBy(p => p.Pensja)
            .Select(p => $"{p.Imie}:{p.Pensja:C}");
        Console.WriteLine($"OrderBy Pensja: {string.Join(", ", wgPensji)}");

        // OrderByDescending — malejąco
        var wgWieku = Pracownicy
            .OrderByDescending(p => p.Wiek)
            .Select(p => $"{p.Imie}:{p.Wiek}");
        Console.WriteLine($"OrderByDescending Wiek: {string.Join(", ", wgWieku)}");

        // Wielopoziomowe: OrderBy + ThenBy
        var wielopoziom = Pracownicy
            .OrderBy(p => p.Dzial)
            .ThenByDescending(p => p.Pensja)
            .Select(p => $"{p.Dzial}/{p.Imie}:{p.Pensja:C}");
        Console.WriteLine("\nOrderBy Dzial, ThenByDesc Pensja:");
        foreach (var s in wielopoziom) Console.WriteLine($"  {s}");

        // Reverse — odwraca kolejność bez sortowania (działa po zmaterializowaniu)
        var ostatnie3 = Pracownicy
            .OrderBy(p => p.DataZatrudnienia)
            .TakeLast(3)
            .Reverse()
            .Select(p => $"{p.Imie} ({p.DataZatrudnienia:yyyy})");
        Console.WriteLine($"\nOstatnie 3 (odwrócone): {string.Join(", ", ostatnie3)}");

        // IComparer<T> — własny komparator
        var wgDlugosciNazwiska = Pracownicy
            .OrderBy(p => p.Nazwisko, Comparer<string>.Create((a, b) => a.Length.CompareTo(b.Length)))
            .ThenBy(p => p.Nazwisko)
            .Select(p => p.Nazwisko);
        Console.WriteLine($"Wg długości nazwiska: {string.Join(", ", wgDlugosciNazwiska)}");
    }

    // ── 5. Grupowanie ─────────────────────────────────────────────────────────

    public static void Grupowanie()
    {
        Console.WriteLine("\n── Grupowanie ──");

        // GroupBy — zwraca IEnumerable<IGrouping<TKey, TSource>>
        // IGrouping<TKey,T>: Key + IEnumerable<T>
        var wgDzialu = Pracownicy.GroupBy(p => p.Dzial);

        foreach (var grupa in wgDzialu)
        {
            Console.WriteLine($"Dział: {grupa.Key} ({grupa.Count()} osób)");
            foreach (var p in grupa.OrderBy(x => x.Pensja))
                Console.WriteLine($"    {p.Imie}: {p.Pensja:C}");
        }

        // GroupBy z element selector + result selector
        var podsumowania = Pracownicy
            .GroupBy(
                p => p.Dzial,
                p => p.Pensja, // element selector
                (klucz, pensje) => new { Dzial = klucz, SredniaPensja = pensje.Average(), MaxPensja = pensje.Max() }
            );
        Console.WriteLine("\nPodsumowania działów:");
        foreach (var g in podsumowania.OrderByDescending(g => g.SredniaPensja))
            Console.WriteLine($"  {g.Dzial}: avg={g.SredniaPensja:C}, max={g.MaxPensja:C}");

        // GroupBy wg zakresu (bucketing)
        var wgPrzedzialu = Pracownicy.GroupBy(p =>
            p.Pensja < 7000 ? "Junior (<7k)" :
            p.Pensja < 9000 ? "Mid (7k-9k)" : "Senior (>9k)");
        Console.WriteLine("\nPrzedziały płac:");
        foreach (var g in wgPrzedzialu)
            Console.WriteLine($"  {g.Key}: {string.Join(", ", g.Select(p => p.Imie))}");

        // ToLookup — zmaterializowany GroupBy (wielokrotny dostęp)
        var lookup = Pracownicy.ToLookup(p => p.Dzial);
        Console.WriteLine($"\nLookup[\"IT\"]: {string.Join(", ", lookup["IT"].Select(p => p.Imie))}");
        Console.WriteLine($"Lookup[\"Brak\"] (nieistniejący klucz): {lookup["Brak"].Count()} elementów");
    }

    // ── 6. Agregacje ──────────────────────────────────────────────────────────

    public static void Agregacje()
    {
        Console.WriteLine("\n── Agregacje ──");

        // Podstawowe agregacje
        int liczba     = Pracownicy.Count();
        decimal suma   = Pracownicy.Sum(p => p.Pensja);
        decimal avg    = Pracownicy.Average(p => p.Pensja);
        decimal max    = Pracownicy.Max(p => p.Pensja);
        decimal min    = Pracownicy.Min(p => p.Pensja);

        Console.WriteLine($"Count: {liczba}");
        Console.WriteLine($"Sum pensji: {suma:C}");
        Console.WriteLine($"Average pensja: {avg:C}");
        Console.WriteLine($"Max pensja: {max:C}");
        Console.WriteLine($"Min pensja: {min:C}");

        // MinBy / MaxBy (C# 6 / .NET 6+) — zwraca ELEMENT, nie wartość
        var najlepszyPlacowo = Pracownicy.MaxBy(p => p.Pensja)!;
        var najgorzejPlacowo = Pracownicy.MinBy(p => p.Pensja)!;
        Console.WriteLine($"\nMaxBy(Pensja): {najlepszyPlacowo.Imie} ({najlepszyPlacowo.Pensja:C})");
        Console.WriteLine($"MinBy(Pensja): {najgorzejPlacowo.Imie} ({najgorzejPlacowo.Pensja:C})");

        // Count z predykatem
        int itCount = Pracownicy.Count(p => p.Dzial == "IT");
        Console.WriteLine($"\nCount(IT): {itCount}");

        // LongCount — dla kolekcji >int.MaxValue
        long longCount = Pracownicy.LongCount();
        Console.WriteLine($"LongCount: {longCount}");

        // Aggregate — fold/reduce ogólny
        // Aggregate(seed, accumulator[, resultSelector])
        decimal sumaReczna = Pracownicy.Aggregate(0m, (acc, p) => acc + p.Pensja);
        Console.WriteLine($"\nAggregate suma pensji: {sumaReczna:C}");

        string wszyscieImiona = Pracownicy
            .Select(p => p.Imie)
            .Aggregate((a, b) => $"{a}, {b}");
        Console.WriteLine($"Aggregate imiona: {wszyscieImiona}");

        // Aggregate z result selector (finalna transformacja)
        string raport = Pracownicy
            .GroupBy(p => p.Dzial)
            .Aggregate("Działy: ",
                (acc, g) => acc + $"{g.Key}({g.Count()}) ",
                result => result.TrimEnd());
        Console.WriteLine($"{raport}");
    }

    // ── 7. Wyszukiwanie i elementy ────────────────────────────────────────────

    public static void WyszukiwanieIElementy()
    {
        Console.WriteLine("\n── WyszukiwanieIElementy ──");

        // Any — czy istnieje element spełniający warunek
        bool jestIT   = Pracownicy.Any(p => p.Dzial == "IT");
        bool jestCEO  = Pracownicy.Any(p => p.Pensja > 100_000);
        Console.WriteLine($"Any IT: {jestIT}, Any pensja>100k: {jestCEO}");

        // All — czy WSZYSTKIE spełniają warunek
        bool wszyscyMajaMail = Pracownicy.All(p => p.Email != null);
        bool wszyscyIT       = Pracownicy.All(p => p.Dzial == "IT");
        Console.WriteLine($"All z emailem: {wszyscyMajaMail}, All IT: {wszyscyIT}");

        // Contains (na sekwencji lub z IEqualityComparer)
        var id3 = Pracownicy.FirstOrDefault(p => p.Id == 3);
        Console.WriteLine($"Contains Id=3 (przez FirstOrDefault): {id3?.Imie}");

        // First / FirstOrDefault — pierwszy element
        var pierwszyIT = Pracownicy.First(p => p.Dzial == "IT");
        var pierwszyZarzad = Pracownicy.FirstOrDefault(p => p.Dzial == "Zarząd");
        Console.WriteLine($"\nFirst IT: {pierwszyIT.Imie}");
        Console.WriteLine($"FirstOrDefault Zarząd: {pierwszyZarzad?.Imie ?? "null"}");

        // Last / LastOrDefault — ostatni element
        var ostatniFinanse = Pracownicy.Last(p => p.Dzial == "Finanse");
        Console.WriteLine($"Last Finanse: {ostatniFinanse.Imie}");

        // Single / SingleOrDefault — dokładnie jeden element
        try
        {
            var jedyny = Pracownicy.Single(p => p.Id == 5); // OK — jeden
            Console.WriteLine($"Single(Id=5): {jedyny.Imie}");
            var wieleDuplikatow = Pracownicy.Single(p => p.Dzial == "IT"); // rzuca!
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("Single na IT: InvalidOperationException (wiele elementów)");
        }

        // ElementAt / ElementAtOrDefault — dostęp po indeksie
        var trzeci = Pracownicy.ElementAt(2);
        var setny  = Pracownicy.ElementAtOrDefault(99);
        Console.WriteLine($"\nElementAt(2): {trzeci.Imie}");
        Console.WriteLine($"ElementAtOrDefault(99): {setny?.Imie ?? "null"}");

        // Take / Skip / TakeLast / SkipLast
        var pierwszeTrzy = Pracownicy.Take(3).Select(p => p.Imie);
        var pominDwoch   = Pracownicy.Skip(6).Select(p => p.Imie);
        Console.WriteLine($"\nTake(3): {string.Join(", ", pierwszeTrzy)}");
        Console.WriteLine($"Skip(6): {string.Join(", ", pominDwoch)}");

        // TakeWhile / SkipWhile — warunek ciągły (nie filtr!)
        var dopokiMlodzi = Pracownicy
            .OrderBy(p => p.Wiek)
            .TakeWhile(p => p.Wiek < 35)
            .Select(p => $"{p.Imie}:{p.Wiek}");
        Console.WriteLine($"TakeWhile(wiek<35): {string.Join(", ", dopokiMlodzi)}");
    }

    // ── 8. Łączenie — Join i GroupJoin ───────────────────────────────────────

    public static void Lacznie()
    {
        Console.WriteLine("\n── Lacznie ──");

        // Join — INNER JOIN: tylko pasujące elementy z obu stron
        var pracZZam = Pracownicy
            .Join(Zamowienia,
                p => p.Id,
                z => z.PracownikId,
                (p, z) => new { p.Imie, z.Id, z.Kwota, z.Status })
            .OrderBy(x => x.Imie);

        Console.WriteLine("Join Pracownicy-Zamowienia:");
        foreach (var x in pracZZam)
            Console.WriteLine($"  {x.Imie}: Zam#{x.Id} {x.Kwota:C} [{x.Status}]");

        // GroupJoin — LEFT OUTER JOIN: wszyscy z lewej strony, z pasującą kolekcją z prawej
        var pracZWszystkimiZam = Pracownicy
            .GroupJoin(Zamowienia,
                p => p.Id,
                z => z.PracownikId,
                (p, zamowieniaGrupy) => new
                {
                    p.Imie,
                    LiczbaZam = zamowieniaGrupy.Count(),
                    SumaZam   = zamowieniaGrupy.Sum(z => z.Kwota)
                });

        Console.WriteLine("\nGroupJoin (wszyscy pracownicy z ich zamówieniami):");
        foreach (var x in pracZWszystkimiZam.OrderByDescending(x => x.SumaZam))
            Console.WriteLine($"  {x.Imie}: {x.LiczbaZam} zam., suma={x.SumaZam:C}");

        // SelectMany jako JOIN (alternatywa dla Join)
        var joinAlt = Pracownicy
            .SelectMany(
                p => Zamowienia.Where(z => z.PracownikId == p.Id),
                (p, z) => new { p.Imie, z.Kwota });
        Console.WriteLine($"\nSelectMany jako join — {joinAlt.Count()} rekordów");

        // Operacje na zbiorach
        var ita    = new[] { "Anna", "Marek", "Piotr" };
        var itb    = new[] { "Piotr", "Ewa", "Anna" };
        Console.WriteLine($"\nUnion:     {string.Join(", ", ita.Union(itb))}");
        Console.WriteLine($"Intersect: {string.Join(", ", ita.Intersect(itb))}");
        Console.WriteLine($"Except:    {string.Join(", ", ita.Except(itb))}");

        // Zip — łączenie parami
        var liczby = new[] { 1, 2, 3 };
        var slowa  = new[] { "jeden", "dwa", "trzy" };
        var zip = liczby.Zip(slowa, (n, s) => $"{n}={s}");
        Console.WriteLine($"Zip: {string.Join(", ", zip)}");

        // Chunk (C# 6 / .NET 6+) — podział na porcje
        var porcje = Pracownicy.Chunk(3);
        Console.WriteLine($"\nChunk(3): {porcje.Count()} porcji");
        foreach (var porcja in porcje)
            Console.WriteLine($"  [{string.Join(", ", porcja.Select(p => p.Imie))}]");
    }

    // ── 9. Odłożone wykonanie ─────────────────────────────────────────────────

    public static void OdlozoneWykonanie()
    {
        Console.WriteLine("\n── OdlozoneWykonanie ──");

        // Odłożone (lazy) — zapytanie NIE jest wykonywane przy deklaracji
        // Wykonuje się przy: foreach, ToList/ToArray, Count, First, Any...
        var lista = new List<int> { 1, 2, 3, 4, 5 };

        Console.Write("Tworzę zapytanie (brak obliczeń)...");
        var zapytanie = lista.Where(n =>
        {
            Console.Write($"[filtr:{n}] ");
            return n % 2 == 0;
        });
        Console.WriteLine(" gotowe (nic nie wypisano!)");

        // Teraz dopiero wykonuje się filtr
        Console.Write("foreach (teraz wykonanie): ");
        foreach (var n in zapytanie) Console.Write($"{n} ");
        Console.WriteLine();

        // Ponowna iteracja — wykonuje się PONOWNIE (nie ma cache)
        Console.Write("Ponowna iteracja: ");
        foreach (var n in zapytanie) Console.Write($"{n} ");
        Console.WriteLine();

        // ToList/ToArray — materializacja (natychmiastowe wykonanie i cache)
        Console.Write("Zmaterializowane (ToList — raz): ");
        var zmaterializowane = lista
            .Where(n => { Console.Write($"[f:{n}] "); return n % 2 == 0; })
            .ToList();
        Console.WriteLine();
        Console.Write("Iteracja po List<T> (zero [f:]): ");
        foreach (var n in zmaterializowane) Console.Write($"{n} ");
        Console.WriteLine();

        // Modyfikacja kolekcji między deklaracją a wykonaniem
        var dynamiczna = lista.Where(n => n > 3); // jeszcze nie wykonane
        lista.Add(10);
        lista.Add(11);
        var wynikDynamic = dynamiczna.ToList(); // teraz — widzi 10 i 11!
        Console.WriteLine($"\nPo dodaniu 10 i 11 do źródła: {string.Join(", ", wynikDynamic)}");

        // Materialization methods (wykonują natychmiast):
        // ToList, ToArray, ToDictionary, ToHashSet, ToLookup
        // Count, Sum, Min, Max, Average, Aggregate
        // First, Last, Single, Any, All, Contains
        // ElementAt
        Console.WriteLine("\nMetody materializujące: ToList, ToArray, ToDictionary,");
        Console.WriteLine("  ToHashSet, Count/Sum/Min/Max/Avg, First/Last/Single, Any/All");
    }
}
